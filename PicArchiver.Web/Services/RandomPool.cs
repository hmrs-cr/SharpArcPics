using System.Threading.Channels;

namespace PicArchiver.Web.Services;

public interface IRandomProvider
{
    ValueTask<string> GetNextRandomValueAsync(CancellationToken ct = default);
}

public class RandomPool : IRandomProvider, IDisposable
{
    private readonly IMetadataProvider _metadataProvider;
    private readonly ILogger<RandomPool> _logger;
    private readonly Channel<string> _pool;
    private readonly int _minThreshold;
    private readonly int _maxCapacity;

    // The signal to wake up the background thread.
    // false = initial state (not signaled)
    private readonly AutoResetEvent _refillSignal = new AutoResetEvent(false);
    
    // The single dedicated thread
    private readonly Thread _workerThread;
    
    // Token to handle graceful shutdown
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();
    private bool _disposed;

    public RandomPool(IMetadataProvider metadataProvider, ILogger<RandomPool> logger)
    {
        _metadataProvider = metadataProvider;
        _logger = logger;

        _minThreshold = 1500;
        _maxCapacity = 2000;

        // Configure Channel
        var options = new BoundedChannelOptions(_maxCapacity)
        {
            SingleWriter = true, // Optimization: We know only ONE thread writes (the worker)
            SingleReader = false, // Multiple threads can read
            FullMode = BoundedChannelFullMode.Wait // If full, writer waits (though our logic prevents this)
        };
        _pool = Channel.CreateBounded<string>(options);

        // Initialize and start the dedicated background thread
        _workerThread = new Thread(RefillLoop)
        {
            IsBackground = true, // Ensures thread dies if app closes
            Name = "PoolRefillWorker",
            Priority = ThreadPriority.BelowNormal // Let consumers have higher CPU priority
        };
        _workerThread.Start();

        // Signal immediately to perform the initial fill
        _refillSignal.Set();
    }

    /// <summary>
    /// Asynchronously gets a value. Thread-safe.
    /// </summary>
    public async ValueTask<string> GetNextRandomValueAsync(CancellationToken ct = default)
    {
        // 1. Try to read asynchronously
        var value = await _pool.Reader.ReadAsync(ct);

        // 2. Check thresholds
        // We only signal if the count dropped below min.
        // ChannelReader.Count is efficient enough for this check.
        var currentCount = _pool.Reader.Count;
        if (currentCount < _minThreshold)
        {
            // Wake up the background thread!
            // Set() is non-blocking and very fast. If already signaled, it does nothing.
            _refillSignal.Set();
        }

        return value;
    }

    /// <summary>
    /// The logic running on the dedicated thread.
    /// </summary>
    private void RefillLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                // 1. WAIT here until a consumer signals that the pool is low.
                // This blocks the thread with zero CPU usage until _refillSignal.Set() is called.
                _refillSignal.WaitOne();

                // 2. Validation check after waking up
                if (_cts.IsCancellationRequested) 
                    break;

                // 3. Fill logic
                var currentCount = _pool.Reader.Count;
                var needed = _maxCapacity - currentCount;

                if (needed <= 0) 
                    continue;
                
                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("Refilling {needed} items...", needed);
                    
                for (var i = 0; i < needed; i++)
                {
                    // Stop if disposed mid-loop
                    if (_cts.IsCancellationRequested) 
                        break;

                    var val = _metadataProvider.GetRandomPicturePath();
                    // Write to channel (TryWrite is efficient for Bounded channels)
                    // If false (full), we just stop trying.
                    if (val != null && IsValidFilePath(val) && !_pool.Writer.TryWrite(val))
                    {
                        break; 
                    }
                }
                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("Refill complete. Going back to sleep.");
            }
            catch (Exception ex)
            {
                // Log exception (don't crash the background thread)
                _logger.LogError(ex, "Failed to refill.");
            }
        }
    }

    private bool IsValidFilePath(string val)
    {
        return !val.EndsWith(".mp4", StringComparison.InvariantCultureIgnoreCase);
    }

    public void Dispose()
    {
        if (_disposed) 
            return;
        
        _disposed = true;

        _cts.Cancel(); // Tell loop to stop
        _refillSignal.Set(); // Wake up thread so it can check the token and exit
        
        _workerThread.Join(1000); 
        
        _cts.Dispose();
        _refillSignal.Dispose();
    }
}