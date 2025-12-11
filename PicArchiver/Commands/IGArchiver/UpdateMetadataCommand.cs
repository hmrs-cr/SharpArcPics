using System.CommandLine;
using System.Data;
using MySql.Data.MySqlClient;
using PicArchiver.Core.DataAccess;
using PicArchiver.Core.Metadata.Loaders;
using PicArchiver.Extensions;

namespace PicArchiver.Commands.IGArchiver;

public class UpdateMetadataCommand: IGBaseCommand
{
    internal UpdateMetadataCommand() : base("update-metadata", "Scans the specified directory and updates the metadata database.")
    {
        var sourceFolderOption =  new Option<string>(
            name: "--folder",
            description: "The folder to scan.");
        
        var destinationFolderOption =  new Option<string?>(
            name: "--incoming-folder",
            description: "Copy existing files found along graph metadata to this folder.");
        
        var connectionStringOption =  new Option<string>(
            name: "--connection-string",
            description: "The connection string to the metadata database.");
 
        this.AddOption(sourceFolderOption);
        this.AddOption(connectionStringOption);
        this.AddOption(destinationFolderOption);
        
        this.SetHandler(ScanInternalAsync, sourceFolderOption, connectionStringOption, destinationFolderOption);
    }

    private async Task ScanInternalAsync(string directory, string connectionString, string? destinationFolder)
    {
        var dbConnection = new MySqlConnection(connectionString);
        await ScanInternalAsync(directory, dbConnection, destinationFolder);
    }
    
    public static async Task ScanInternalAsync(string directory, IDbConnection dbConnection, string? destinationFolder)
    {
        var loadedMetadata = new HashSet<string>();
        var loadedCount = 0;
        var movedCount = 0;
        var igFiles = Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories).Select(IgFile.Parse);
        foreach (var igFile in igFiles)
        {
            var loaded = false;
            if (igFile.IsValid)
            {
                if (igFile.IsMetadata)
                {
                    loaded = await LoadMetadata(igFile.FullPath, dbConnection, loadedMetadata, igFile.UserName, igFile.UserId);
                }
                else
                {
                    var metadataFileName = igFile.FullPath + IgFile.MetadataExtension;
                    if (File.Exists(metadataFileName))
                    {
                        loaded = await LoadMetadata(metadataFileName, dbConnection, loadedMetadata,
                            igFile.UserName, igFile.UserId, igFile.FullPath);
                    }
                }
            }
            else if (igFile.FileName.EndsWith(IgGraphNodeRoot.Extension))
            {
                var graphData = await IgGraphNodeRoot.LoadAsync(igFile.FullPath);
                var node = graphData.Node;
                if (node != null && loadedMetadata.Add(igFile.FullPath))
                {
                    foreach (var (originalFileName, newIgFile) in graphData.ArchiveFileNames)
                    {
                        loaded = await LoadMetadata(dbConnection, originalFileName, newIgFile, node);
                        if (loaded && Directory.Exists(destinationFolder) && File.Exists(originalFileName))
                        {
                            if (!destinationFolder.EndsWith(newIgFile.UserName))
                            {
                                destinationFolder = Path.Combine(destinationFolder, newIgFile.UserName);
                                Directory.CreateDirectory(destinationFolder);
                            }
                            
                            var newFileName = Path.Join(destinationFolder, newIgFile.FileName);
                            File.Move(originalFileName, newFileName);
                            Console.WriteLine($"MOVED: {originalFileName} => {newFileName}");
                            movedCount++;
                        }
                    }
                }
            }
            
            if (loaded) loadedCount++;
        }
        
        Console.WriteLine($"LOADED: {loadedCount} of {loadedMetadata.Count} files.");
        if (movedCount > 0)
        {
            Console.WriteLine($"MOVED: {movedCount} files to {destinationFolder}.");
        }
    }

    private static async Task<bool> LoadMetadata(IDbConnection dbConnection, string originalFileName, IgFile newIgFile, IgGraphNode node)
    {
        try
        {
            var pictureId = newIgFile.FileName.ComputeFileNameHash();
            if (await dbConnection.GetPicturePath(pictureId) is { } picturePath)
            {
                Console.WriteLine($"EXISTS: {originalFileName} already exists as {picturePath}");
                File.Delete(originalFileName);
            }
            
            await dbConnection.AddOrUpdatePictureIgGraphMetadata(pictureId, newIgFile, node);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"ERROR: Loading metadata for {newIgFile.FullPath}: {e.Message}");
        }
        
        return false;
    }

    private static async Task<bool> LoadMetadata(string metadataFileName, IDbConnection dbConnection, 
        HashSet<string> loadedMetadata, string igUserName, long igUserId, string? originalFilName = null)
    {
        var picFileName = string.Empty;
        try
        {
            if (loadedMetadata.Add(metadataFileName))
            {
                picFileName = originalFilName ?? metadataFileName.Replace(IgFile.MetadataExtension, string.Empty);
                var metadata = await IgMetadataRoot.LoadAsync(metadataFileName);
                DateTime? dateAdded = File.Exists(picFileName) ? File.GetCreationTimeUtc(picFileName) : null;
                var pictureId = picFileName.ComputeFileNameHash();
                await dbConnection.AddOrUpdatePictureMetadata(pictureId, picFileName, metadata.Data,
                    igUserName, igUserId, dateAdded);
                Console.WriteLine($"LOADED: Metadatada for {picFileName}");
                return true;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"ERROR: Loading metadata for {picFileName}: {e.Message}");
        }
        
        return false;
    }
}