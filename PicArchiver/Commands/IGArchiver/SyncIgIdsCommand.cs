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
        await using var dbConnection = new MySqlConnection(connectionString);
        
        var invalidCount = 0;
        var totalCount = 0;
        var updateCount = 0;

        await foreach (var picData in dbConnection.ScanAllPictures())
        {
            totalCount++;
            var igFile = IgFile.Parse(picData.FileName);
            if (igFile.IsValid)
            {
                var localFile = Path.Combine(directory, $"{igFile.UserId}", Path.GetFileName(picData.FileName));
                var localFileExists = File.Exists(localFile);
                long? newIgPictureId = null;
                long? newIgUserId = null;
                if (picData.IgPictureId != igFile.PictureId)
                {
                    newIgPictureId =  picData.IgPictureId;
                }

                if (picData.IgUserId != igFile.UserId)
                {
                    newIgUserId  = picData.IgUserId;
                }

                if (newIgPictureId.HasValue || newIgUserId.HasValue)
                {
                    await dbConnection.UpdateIgIds(pictureId: picData.PictureId, igPictureId: newIgPictureId, igUserId: newIgUserId, deleted: !localFileExists);
                    Console.WriteLine($"UPDATED: '{picData.FileName}' => PID: {picData.IgPictureId?.ToString() ?? "NULL"} => {picData.IgUserId}, UID: {picData.IgUserId?.ToString() ?? "NULL"} => {picData.IgUserId}");
                    updateCount++;
                }
                
            }
            else
            {
                invalidCount++;
                Console.WriteLine(
                    $"Invalid IG file: '{picData.FileName}': PID: {picData.IgPictureId?.ToString() ?? "NULL"}/{picData.IgUserId?.ToString() ?? "NULL"}, UID: {picData.IgUserId?.ToString() ?? "NULL"}/{picData.IgUserId?.ToString() ?? "NULL"}");
            }
            
            
            Console.WriteLine($"TOTAL:   {totalCount}");
            Console.WriteLine($"UPDATED: {updateCount}");
            Console.WriteLine($"INVALID: {invalidCount}");
        }
    }
}