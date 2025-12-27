using System.CommandLine;
using System.Data;
using Dapper;
using MySql.Data.MySqlClient;
using PicArchiver.Core.DataAccess;
using PicArchiver.Core.Metadata.Loaders;
using PicArchiver.Extensions;

namespace PicArchiver.Commands.IGArchiver;

public class SyncIgIdsCommand: IGBaseCommand
{
    internal SyncIgIdsCommand() : base("sync-igids", "Sync IG Ids.")
    {
        var sourceFolderOption =  new Option<string>(
            name: "--folder",
            description: "The folder to scan for sync.");
        
        var connectionStringOption =  new Option<string>(
            name: "--connection-string",
            description: "The connection string to the metadata database.");
 
        this.AddOption(sourceFolderOption);
        this.AddOption(connectionStringOption);
        
        this.SetHandler(
            SyncIdsInternalAsync, 
            sourceFolderOption, 
            connectionStringOption);
    }

    private async Task SyncIdsInternalAsync(string directory, string connectionString)
    {
        await using var dbReadConnection = new MySqlConnection(connectionString);
        await using var dbWriteConnection = new MySqlConnection(connectionString);
        
        var invalidCount = 0;
        var totalCount = 0;
        var updateCount = 0;
        
        Console.WriteLine($"Scanning... Folder: {directory}");

        await foreach (var picData in dbReadConnection.ScanAllPictures())
        {
            totalCount++;
            var igFile = IgFile.Parse(picData.FileName);
            if (igFile.IsValid)
            {
                var localFile = Path.Combine(directory, $"{igFile.UserId}", Path.GetFileName(picData.FileName));
                var localFileExists = File.Exists(localFile);
                long? newIgPictureId = null;
                long? newIgUserId = null;
                bool? newIsDeleted = null;

                if (picData.IgPictureId != igFile.PictureId)
                {
                    newIgPictureId = picData.IgPictureId;
                }

                if (picData.IgUserId != igFile.UserId)
                {
                    newIgUserId = picData.IgUserId;
                }

                if (!localFileExists != picData.IsDeleted)
                {
                    newIsDeleted = !picData.IsDeleted;
                }
                
                if (newIgPictureId.HasValue || newIgUserId.HasValue || newIsDeleted.HasValue)
                {
                    await dbWriteConnection.UpdateIgIds(pictureId: picData.PictureId, igPictureId: newIgPictureId,
                        igUserId: newIgUserId, deleted: newIsDeleted);
                
                    var pidDiff = newIgPictureId.HasValue
                        ? $"{picData.IgPictureId?.ToString() ?? "NULL"} => {newIgPictureId}"
                        : "[NC]";
                
                    var uidDiff = newIgUserId.HasValue
                        ? $"{picData.IgUserId?.ToString() ?? "NULL"} => {newIgUserId}"
                        : "[NC]";
                
                    Console.WriteLine($"UPDATED: '{picData.FileName}' => PID: {pidDiff}, UID: {uidDiff}, Exists: {localFileExists}");
                    
                    updateCount++;
                }
            }
            else
            {
                invalidCount++;
                Console.WriteLine(
                    $"Invalid IG file: '{picData.FileName}': PID: {picData.IgPictureId?.ToString() ?? "NULL"}/{picData.IgUserId?.ToString() ?? "NULL"}, UID: {picData.IgUserId?.ToString() ?? "NULL"}/{picData.IgUserId?.ToString() ?? "NULL"}");
            }
        }
        
        Console.WriteLine($"TOTAL:   {totalCount}");
        Console.WriteLine($"UPDATED: {updateCount}");
        Console.WriteLine($"INVALID: {invalidCount}");
    }
}