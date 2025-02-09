namespace PicArchiver.Core.Metadata;

public interface IMetadataLoader
{
    bool Initialize(FileArchiveContext context);
    void Finalize(FileArchiveContext? context);
    bool LoadMetadata(FileArchiveContext context);
    bool LoadMetadata(string path, FileMetadata metadata);
}