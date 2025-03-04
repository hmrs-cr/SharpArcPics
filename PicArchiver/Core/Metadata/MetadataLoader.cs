using PicArchiver.Core.Metadata.Loaders;

namespace PicArchiver.Core.Metadata;

public abstract class MetadataLoader : IMetadataLoader
{
    public virtual bool Initialize(FileArchiveContext context) => true;

    public virtual void Finalize(FileArchiveContext? context)
    {
    }

    public virtual bool LoadMetadata(FileArchiveContext context) => 
        LoadMetadata(context.SourceFileFullPath, context.Metadata);

    public abstract bool LoadMetadata(string path, FileMetadata metadata);
    
    public static void LoadMetadata(string sourceFileName, string loaders, FileMetadata metadata)
    {
        var span = loaders.AsSpan();
        foreach (var range in span.Split(','))
        {
            var metadataLoader = GetMetadataLoader(span[range].Trim());
            metadataLoader?.LoadMetadata(sourceFileName, metadata);
        }
    }
    
    public static IEnumerable<string> GetLoaderNames() => MetadataLoaderFactories.Keys;
    
    public static IMetadataLoader? GetMetadataLoader(ReadOnlySpan<char> name)
    {
        var metadataLoadersInstancesSpanLookup = MetadataLoadersInstances.GetAlternateLookup<ReadOnlySpan<char>>();
        if (metadataLoadersInstancesSpanLookup.TryGetValue(name, out var result))
        {
            return result;
        }

        var metadataLoadersTypeSpanLookup = MetadataLoaderFactories.GetAlternateLookup<ReadOnlySpan<char>>();
        if (metadataLoadersTypeSpanLookup.TryGetValue(name, out var factory))
        {
            return metadataLoadersInstancesSpanLookup[name] = factory();
        }

        return null;
    }

    private static readonly Dictionary<string, Func<IMetadataLoader>> MetadataLoaderFactories  =
        new()
        {
            {"Default", CreateMetadataLoader<DefaultMetadataLoader>},
            {"IG", CreateMetadataLoader<IgMetadataLoader>},
            {"Exif", CreateMetadataLoader<ExifMetadataLoader>},
            {"ChkSum", CreateMetadataLoader<ChecksumMetadataLoader>},
            {"ChkSumLite", CreateMetadataLoader<ChecksumLiteMetadataLoader>}
        };

    private static readonly Dictionary<string, IMetadataLoader> MetadataLoadersInstances = new();

    private static T CreateMetadataLoader<T>() where T : IMetadataLoader, new() => new();

}