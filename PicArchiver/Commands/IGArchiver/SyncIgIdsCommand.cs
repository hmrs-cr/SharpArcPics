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
        var updateList = new List<UpdateRecord>();
        var invalidCount = 0;
        var totalCount = 0;
        var total = 0;

        await using (var dbReadConnection = new MySqlConnection(connectionString))
        {
            Console.WriteLine($"Scanning... Folder: '{directory}'");
            total = await dbReadConnection.CountAllPictures();
            Console.WriteLine($"Found {total} records in DB. Scanning changes...");
            
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
                        newIgPictureId = igFile.PictureId;
                    }

                    if (picData.IgUserId != igFile.UserId)
                    {
                        newIgUserId = igFile.UserId;
                    }

                    if (!localFileExists != picData.IsDeleted)
                    {
                        newIsDeleted = !picData.IsDeleted;
                    }

                    if (newIgPictureId.HasValue || newIgUserId.HasValue || newIsDeleted.HasValue)
                    {
                        updateList.Add(new UpdateRecord(picData, newIgPictureId, newIgUserId, newIsDeleted,
                            localFileExists));
                    }
                }
                else
                {
                    invalidCount++;
                    Console.WriteLine(
                        $"Invalid IG file: '{picData.FileName}': PID: {picData.IgPictureId?.ToString() ?? "NULL"}/{picData.IgUserId?.ToString() ?? "NULL"}, UID: {picData.IgUserId?.ToString() ?? "NULL"}/{picData.IgUserId?.ToString() ?? "NULL"}");
                }
            }
        }
        
        Console.WriteLine($"TOTAL SCANNED:   {totalCount}");
        Console.WriteLine($"CHANGES FOUND: {updateList.Count}");
        Console.WriteLine($"INVALID: {invalidCount}");

        if (updateList.Count == 0)
        {
            Console.WriteLine("Nothing to update");
        }
        else
        {
            await using var dbWriteConnection = new MySqlConnection(connectionString);
            var updateCount = 0;
            foreach (var updateRecord in updateList)
            {
                await dbWriteConnection.UpdateIgIds(pictureId: updateRecord.PicData.PictureId,
                    igPictureId: updateRecord.NewIgPictureId,
                    igUserId: updateRecord.NewIgUserId, deleted: updateRecord.NewIsDeleted);

                var pidDiff = updateRecord.NewIgPictureId.HasValue
                    ? $"{updateRecord.PicData.IgPictureId?.ToString() ?? "NULL"} => {updateRecord.NewIgPictureId}"
                    : "[NC]";

                var uidDiff = updateRecord.NewIgUserId.HasValue
                    ? $"{updateRecord.PicData.IgUserId?.ToString() ?? "NULL"} => {updateRecord.NewIgUserId}"
                    : "[NC]";

                Console.WriteLine(
                    $"UPDATED [{++updateCount}/{updateList.Count}]: '{updateRecord.PicData.FileName}' \t \t => PID: {pidDiff}, UID: {uidDiff}, Exists: {updateRecord.LocalFileExists}");
            }

            Console.WriteLine($"SCANNED/TOTAL: {totalCount}/{total}");
            Console.WriteLine($"UPDATED: {updateList.Count}");
            Console.WriteLine($"INVALID: {invalidCount}");
        }
    }

    private record UpdateRecord(
        PictureData PicData,
        long? NewIgPictureId,
        long? NewIgUserId,
        bool? NewIsDeleted,
        bool LocalFileExists);
}