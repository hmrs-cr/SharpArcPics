using System.Text.Json;
using System.Text.RegularExpressions;
using PicArchiver.Core.Metadata;
using PicArchiver.Core.Metadata.Loaders;

namespace PicArchiver.Core.Configs;

public sealed class ArchiveConfig
{
    private Regex? _sourceFileNameRegEx;
    private List<IMetadataLoader>? _metadataLoaderInstances = null;

    public bool? DryRun { get; init; }

    public long? MinDriveSize { get; init; }
    public bool? MoveFiles { get; init; }
    public bool? OverrideDestination { get; init; }
    public bool? IgnoreDuplicates { get; init; }
    public bool? DeleteSourceFileIfDestExists { get; init; }
    
    public string? SubfolderTemplate { get; init; }
    public string? FileNameTemplate { get; init; }
    
    public bool? Rotate { get; init; }
    public string? SourceFileNameRegExPattern { get; init; }
    
    public bool? Recursive { get; init; }
    
    public string? MetadaLoaders { get; init; }
    
    public Dictionary<string, object?>? TokenValues { get; init; }
    
    public Dictionary<string, ArchiveConfig>? MediaConfigs { get; init; }

    public ICollection<IMetadataLoader>? MetadataLoaderInstances
    {
        get
        {
            if (_metadataLoaderInstances != null)
                return _metadataLoaderInstances;
            
            if (MetadaLoaders == null)
            {
                return null;
            }
            
            var span = MetadaLoaders.AsSpan();
            foreach (var range in span.Split(','))
            {
                var metadataLoader = MetadataLoader.GetMetadataLoader(span[range].Trim());
                if (metadataLoader != null)
                {
                    _metadataLoaderInstances ??= [];
                    _metadataLoaderInstances.Add(metadataLoader) ;
                }
            }
            
            return _metadataLoaderInstances;
        }
    }

    internal Regex? SourceFileNameRegEx => 
        string.IsNullOrEmpty(SourceFileNameRegExPattern) ? null : _sourceFileNameRegEx ??= new Regex(SourceFileNameRegExPattern);

    internal ArchiveConfig Merge(ArchiveConfig config1, ArchiveConfig? config2 = null, bool onlyFirstLevelConfigValues = false) => new()
    {
        MinDriveSize = config1.MinDriveSize ?? MinDriveSize ?? config2?.MinDriveSize,
        MoveFiles = config1.MoveFiles ?? MoveFiles ?? config2?.MoveFiles,
        OverrideDestination = config1.OverrideDestination ?? OverrideDestination ?? config2?.OverrideDestination,
        IgnoreDuplicates = config1.IgnoreDuplicates ?? IgnoreDuplicates ?? config2?.IgnoreDuplicates,
        DeleteSourceFileIfDestExists = config1.DeleteSourceFileIfDestExists ?? DeleteSourceFileIfDestExists ?? config2?.DeleteSourceFileIfDestExists,
        SubfolderTemplate = config1.SubfolderTemplate ?? SubfolderTemplate ?? config2?.SubfolderTemplate,
        FileNameTemplate = config1.FileNameTemplate ?? FileNameTemplate ?? config2?.FileNameTemplate,
        Rotate = config1.Rotate ?? Rotate ?? config2?.Rotate,
        SourceFileNameRegExPattern = config1.SourceFileNameRegExPattern ?? SourceFileNameRegExPattern ?? config2?.SourceFileNameRegExPattern,
        TokenValues = config1.TokenValues ?? TokenValues ?? config2?.TokenValues,
        DryRun = config1.DryRun ?? DryRun ?? config2?.DryRun,
        Recursive = config1.Recursive ?? Recursive ?? config2?.Recursive,
        
        MetadaLoaders = onlyFirstLevelConfigValues ? null : config1.MetadaLoaders ?? MetadaLoaders,
        MediaConfigs = onlyFirstLevelConfigValues ? null :MergeMediaConfigs(config1),
    };

    private Dictionary<string, ArchiveConfig>? MergeMediaConfigs(ArchiveConfig loadedConfig)
    {
        if (MediaConfigs is null)
            return loadedConfig.MediaConfigs;

        if (loadedConfig.MediaConfigs?.Any(mc => mc.Value.MediaConfigs is not null || mc.Value.MetadaLoaders is not null) == true)
            throw new InvalidOperationException("Invalid config: Media specific configs cannot have other media specific configs or metadata loaders specified.");
        
        if (loadedConfig.MediaConfigs is null)
            return MediaConfigs;

        foreach (var loadedKvp in loadedConfig.MediaConfigs)
        {
            var thisConfig = MediaConfigs.GetValueOrDefault(loadedKvp.Key);
            MediaConfigs[loadedKvp.Key] = thisConfig is null ? loadedKvp.Value : thisConfig.Merge(loadedKvp.Value, loadedConfig, true);
        }
        
        return MediaConfigs;
    }

    internal static ArchiveConfig? Load(string path)
    {
        if (File.Exists(path))
        {
            try
            {
                using var stream = File.OpenRead(path);
                return JsonSerializer.Deserialize<ArchiveConfig>(stream,
                    ArchiveConfigSourceGenerationContext.Default.ArchiveConfig);
            }
            catch (Exception e)
            {
                throw new ApplicationException($"Cannot load dest config file '{path}': {e.Message}", e);
            }
        }

        return DefaultFileArchiveConfig.GetDefaultConfig(path);
    }
}

public static class MetadataLoadersExtensions
{
    public static bool LoadMetadata(this IEnumerable<IMetadataLoader>? metadataLoaderInstances, FileArchiveContext context)
    {
        if (metadataLoaderInstances is null)
        {
            return true;
        }

        foreach (var metadataLoader in metadataLoaderInstances!)
        {
            if (!metadataLoader.LoadMetadata(context))
            {
                return false;
            }
        }
        
        return true;
    }
    
    public static bool Initialize(this IEnumerable<IMetadataLoader>? metadataLoaderInstances, FileArchiveContext context)
    {
        if (metadataLoaderInstances is not null)
        {
            foreach (var metadataLoader in metadataLoaderInstances)
            {
                var result = metadataLoader.Initialize(context);
                if (!result)
                {
                    return false;
                }
            }
        }
        
        return true;
    }

    public static void Finalize(this IEnumerable<IMetadataLoader>? metadataLoaderInstances, FileArchiveContext? context)
    {
        if (metadataLoaderInstances is not null)
        {
            foreach (var metadataLoader in metadataLoaderInstances)
            {
                metadataLoader.Finalize(context);
            }
        }
    }
}