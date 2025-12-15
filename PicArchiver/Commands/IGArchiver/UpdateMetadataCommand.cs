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
            description: "Move existing files found along graph metadata to this folder.");
        
        var setIncomingFlagOption =  new Option<bool>(
            name: "--set-incoming-flag",
            description: "Sets the incoming flag in the DB.");
        
        var curatedArchiveFolderOption =  new Option<string?>(
            name: "--curated-folder",
            description: "Folder with existing curated pictures. Used to detect duplicates.");
        
        var metadataArchiveFolderOption =  new Option<string?>(
            name: "--metadata-archive-folder",
            description: "Move loaded metadata files to this folder.");
        
        var connectionStringOption =  new Option<string>(
            name: "--connection-string",
            description: "The connection string to the metadata database.");
 
        this.AddOption(sourceFolderOption);
        this.AddOption(connectionStringOption);
        this.AddOption(destinationFolderOption);
        this.AddOption(metadataArchiveFolderOption);
        this.AddOption(curatedArchiveFolderOption);
        this.AddOption(setIncomingFlagOption);
        
        this.SetHandler(
            ScanInternalAsync, 
            sourceFolderOption, 
            connectionStringOption, 
            destinationFolderOption,
            metadataArchiveFolderOption,
            curatedArchiveFolderOption,
            setIncomingFlagOption);
    }

    private async Task ScanInternalAsync(string directory, string connectionString, string? destinationFolder, 
        string? metadataArchiveFolder, string? curatedFolder, bool setIncomingFlag)
    {
        var options = new UpdateMetadataOptions
        {
            SourceFolder =  directory,
            ConnectionString =  connectionString,
            IncomingFolder = destinationFolder,
            CuratedArchiveFolder = curatedFolder,
            MetadataArchiveFolder = metadataArchiveFolder,
            SetIncomingFlag = setIncomingFlag
        };
        
        Console.WriteLine($"Scanning {options.SourceFolder}...");
        await ScanInternalAsync(options);
    }
    
    public static async Task ScanInternalAsync(UpdateMetadataOptions options)
    {
        options.DbConnection ??= new MySqlConnection(options.ConnectionString);
        var loadedMetadata = new HashSet<string>();
        var loadedCount = 0;
        var movedCount = 0;
        var igFiles = Directory.EnumerateFiles(options.SourceFolder, "*.*", SearchOption.AllDirectories).Select(IgFile.Parse);
        foreach (var igFile in igFiles)
        {
            var loaded = false;
            if (igFile.IsValid)
            {
                if (igFile.IsMetadata)
                {
                    loaded = await LoadMetadata(igFile.FullPath, options.DbConnection, loadedMetadata, 
                        igFile.UserName, igFile.UserId, options.MetadataArchiveFolder, options.SetIncomingFlag);
                }
                else
                {
                    var metadataFileName = igFile.FullPath + IgFile.MetadataExtension;
                    if (File.Exists(metadataFileName))
                    {
                        loaded = await LoadMetadata(metadataFileName, options.DbConnection, loadedMetadata,
                            igFile.UserName, igFile.UserId, options.MetadataArchiveFolder, options.SetIncomingFlag, igFile.FullPath);
                    }
                }
            }
            else if (igFile.FileName.EndsWith(IgGraphNodeRoot.Extension))
            {
                var graphData = await IgGraphNodeRoot.LoadAsync(igFile.FullPath);
                var node = graphData.Node;
                if (node != null && loadedMetadata.Add(igFile.FullPath))
                {
                    var index = 1;
                    var incomingFolder = options.IncomingFolder;
                    foreach (var (originalFileName, newIgFile) in graphData.ArchiveFileNames)
                    {
                        node.CurrentCarrouselIndex = index++;
                        loaded = await LoadMetadata(options.DbConnection, originalFileName, newIgFile, node, 
                            options.CuratedArchiveFolder, options.SetIncomingFlag);
                        if (loaded && Directory.Exists(incomingFolder) && File.Exists(originalFileName))
                        {
                            var userIdStr = $"{newIgFile.UserId}";
                            if (!incomingFolder.EndsWith(userIdStr))
                            {
                                incomingFolder = Path.Join(incomingFolder, userIdStr);
                                Directory.CreateDirectory(incomingFolder);
                            }
                            
                            var newFileName = Path.Join(incomingFolder, newIgFile.FileName);
                            File.Move(originalFileName, newFileName, overwrite: true);
                            Console.WriteLine($"MOVED: {originalFileName} => {newFileName}");
                            movedCount++;
                        }
                    }
                    
                    if (loaded && options.MetadataArchiveFolder is not null)
                    {
                        var metadataArchiveFolder = Path.Join(options.MetadataArchiveFolder, $"{node.IgOwner.Id}");
                        Directory.CreateDirectory(metadataArchiveFolder);
                        File.Move(igFile.FullPath, Path.Join(metadataArchiveFolder, 
                            Path.GetFileName(igFile.FullPath.AsSpan())));
                    }
                }
            }
            
            if (loaded) loadedCount++;
        }
        
        Console.WriteLine($"LOADED: {loadedCount} of {loadedMetadata.Count} files.");
        if (movedCount > 0)
        {
            Console.WriteLine($"MOVED: {movedCount} files to {options.IncomingFolder}.");
        }
    }

    private static async Task<bool> LoadMetadata(
        IDbConnection dbConnection, 
        string originalFileName, 
        IgFile newIgFile, 
        IgGraphNode node,
        string? curatedFolder,
        bool setIncomingFlag)
    {
        try
        {
            var existingPath = Directory.Exists(curatedFolder) && newIgFile.DestinationPathIfExists(curatedFolder)  is { } 
                path ? path : await dbConnection.GetPicturePath(newIgFile.PictureDbId) ;
            
            if (existingPath != null)
            {
                Console.WriteLine($"EXISTS: {originalFileName} already exists as {existingPath}");
                File.Delete(originalFileName);
            }
            
            await dbConnection.AddOrUpdatePictureIgGraphMetadata(newIgFile.PictureDbId, newIgFile, node, setIncomingFlag);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"ERROR: Loading metadata for {newIgFile.FullPath}: {e.Message}");
        }
        
        return false;
    }

    private static async Task<bool> LoadMetadata(
        string metadataFileName, 
        IDbConnection dbConnection, 
        HashSet<string> loadedMetadata, 
        string igUserName, 
        long igUserId, 
        string? metadataArchiveFolder,
        bool setIncomingFlag,
        string? originalFilName = null)
    {
        var picFileName = string.Empty;
        try
        {
            if (loadedMetadata.Add(metadataFileName))
            {
                picFileName = originalFilName ?? metadataFileName.Replace(IgFile.MetadataExtension, string.Empty);
                var metadata = await IgMetadataRoot.LoadAsync(metadataFileName);
                DateTime? dateAdded = File.Exists(picFileName) ? File.GetCreationTimeUtc(picFileName) : null;
                if (!dateAdded.HasValue || setIncomingFlag)
                {
                    dateAdded = DateTime.Now;
                }
                
                var pictureId = picFileName.ComputeFileNameHash();
                await dbConnection.AddOrUpdatePictureMetadata(pictureId, picFileName, metadata.Data,
                    igUserName, igUserId, dateAdded, setIncomingFlag);

                if (metadataArchiveFolder is not null)
                {
                    metadataArchiveFolder = Path.Join(metadataArchiveFolder, $"{igUserId}");
                    Directory.CreateDirectory(metadataArchiveFolder);
                    File.Move(metadataFileName, Path.Join(metadataArchiveFolder, 
                        Path.GetFileName(metadataFileName.AsSpan())));
                }
                
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
    
    public class UpdateMetadataOptions
    {
        public required string SourceFolder { get; init; }
        public required string ConnectionString { get; init; }
        public string? IncomingFolder { get; init; }
        public string? MetadataArchiveFolder { get; init; }
        public IDbConnection? DbConnection { get; set; }
        public string? CuratedArchiveFolder { get; init; }
        public bool SetIncomingFlag { get; init; }
    }
}