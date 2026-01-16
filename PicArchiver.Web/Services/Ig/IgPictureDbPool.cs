using System.Threading.Channels;
using Microsoft.Extensions.Options;
using PicArchiver.Commands.IGArchiver;
using PicArchiver.Core.DataAccess;
using PicArchiver.Core.Metadata;
using PicArchiver.Core.Metadata.Loaders;
using PicArchiver.Extensions;
using PicArchiver.Web.Services.MySqlServices;

namespace PicArchiver.Web.Services.Ig;

public class IgPictureDbPool : IPictureProvider, IDisposable
{
    private readonly PictureProvidersConfig _config;
    private readonly ILogger<IgPictureDbPool> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDbConnectionAccessor _connectionAccessor;
    private readonly IConfiguration _configuration;
    private readonly Channel<string> _pool;
    private readonly int _minThreshold;
    private readonly int _maxCapacity;

    // The single dedicated thread
    private readonly Thread _workerThread;

    // Token to handle graceful shutdown
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();
    private bool _disposed;

    public IgPictureDbPool(
        IOptions<PictureProvidersConfig> config,
        ILogger<IgPictureDbPool> logger,
        IServiceScopeFactory scopeFactory,
        IDbConnectionAccessor connectionAccessor,
        IConfiguration configuration)
    {
        _config = config.Value;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _connectionAccessor = connectionAccessor;
        _configuration = configuration;

        _minThreshold = 7500;
        _maxCapacity = 10000;

        // Configure Channel
        var options = new BoundedChannelOptions(_maxCapacity)
        {
            SingleWriter = true, // Optimization: We know only ONE thread writes (the worker)
            SingleReader = false, // Multiple threads can read
            FullMode = BoundedChannelFullMode.Wait // If full, writer waits (though our logic prevents this)
        };
        
        _pool = Channel.CreateBounded<string>(options);
        _ = StartRefillPoolLoop();
        logger.LogInformation("IG Provider started. Pic Path: '{PicturesBasePath}'", _config.PicturesBasePath);
    }

    public string PicturesBasePath => _config.PicturesBasePath;

    /// <summary>
    /// Asynchronously gets a value. Thread-safe.
    /// </summary>
    public async ValueTask<string> GetNextRandomValueAsync(CancellationToken ct = default)
    {
        var value = await _pool.Reader.ReadAsync(ct);
        return value;
    }

    public async Task<IEnumerable<string>> GetPictureSetIds(ulong setId)
    {
        var ids = await _connectionAccessor.DbConnection.GetPictureIdsForUser(setId);
        return ids.Select(id => id.ToString());
    }

    public async Task<IEnumerable<string>> GetPictureSetIds(string setId)
    {
        var ids = setId.StartsWith('+') || setId.StartsWith('-') 
                                 ? await _connectionAccessor.DbConnection.SearchPicturesInBoolMode(setId) 
                                 : await QueryPictureIds(setId);
        
        return ids.Select(id => id.ToString()); 
    }

    private async Task<IEnumerable<ulong>> QueryPictureIds(string query)
    {
        var result = await _connectionAccessor.DbConnection.GetPictureIdsForQuery(query) ?? 
                                      await _connectionAccessor.DbConnection.GetPictureIdsForUser(query);
        return result;
    }

    public ulong GetPictureIdFromPath(string fullPicturePath) => fullPicturePath.ComputeFileNameHash();

    public PictureStats? CreatePictureStats(string? path, ulong pictureId)
    {
        if (path is null)
            return null;

        var igFile = IgFile.Parse(path);
        if (!igFile.IsValid)
            return null;
        
        var userIdStr = $"{igFile.UserId}";
        var isFullPath = path.StartsWith(_config.PicturesBasePath) && path.EndsWith(igFile.FileName);
        var fullFilePath = isFullPath ? path : Path.Combine(_config.PicturesBasePath, userIdStr, igFile.FileName);
        if (!File.Exists(fullFilePath))
        {
            fullFilePath = Path.Combine(_config.PicturesIncomingBasePath, userIdStr, igFile.FileName);
        }

        if (!File.Exists(fullFilePath))
            return null;
        
        return new PictureStats(fullFilePath, pictureId)
        {
            ContextData = igFile
        };
    }


    /// <summary>
    /// The logic running on the dedicated thread.
    /// </summary>
    private async Task StartRefillPoolLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                if (_cts.IsCancellationRequested) 
                    break;

                // Fill logic
                var currentCount = _pool.Reader.Count;
                var needed = _maxCapacity - currentCount;

                if (needed <= 0)
                {
                    await Task.Delay(1000, _cts.Token); 
                    continue;
                }

                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("Refilling {needed} items...", needed);

                using var serviceScope = _scopeFactory.CreateScope();
                var connectionAccessor = new BasicMySqlConnectionAccessor(serviceScope.ServiceProvider, _configuration);
                using var dbConnection = connectionAccessor.DbConnection;
                for (var i = 0; i < needed; i++)
                {
                    // Stop if disposed mid-loop
                    if (_cts.IsCancellationRequested) 
                        break;

                    var fileName = await dbConnection.GetRandomPictureFileName();
                    if (fileName != null)
                    {
                        var picturePath = Path.Join(_config.PicturesBasePath, fileName);
                        if (IgMetadataProvider.IsValidFilePath(picturePath))
                        {
                            if (!_pool.Writer.TryWrite(picturePath))
                            {
                                break;
                            }
                        }
                    }
                }
                
                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("Refill complete. Going back to sleep.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refill.");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) 
            return;
        
        _disposed = true;

        _cts.Cancel(); // Tell loop to stop 
        _cts.Dispose();
    }
}