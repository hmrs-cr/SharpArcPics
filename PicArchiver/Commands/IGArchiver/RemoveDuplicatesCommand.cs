using System.CommandLine;

namespace PicArchiver.Commands.IGArchiver;

public class RemoveDuplicatesCommand : IGBaseCommand
{
    internal RemoveDuplicatesCommand() : base("remove-duplicates", "Scans the specified folder and deletes duplicate files.")
    {
        var destinationOption =  new Option<string>(
            name: "--folder",
            description: "The folder to scan.");
        
        this.AddOption(destinationOption);
        this.SetHandler(RemoveDuplicatesInternal, destinationOption);
    }

    private static void RemoveDuplicatesInternal(string folder)
    {
        var count = RemoveDuplicates(folder);
        if (count > 0)
        {
            Console.WriteLine($"Removed {count} files.");
        }
    }
        

    public static int RemoveDuplicates(string folder)
    {
        var scanResult = ScanCommand.Scan(folder);
        var removedCount = 0;
        foreach (var pics in scanResult.PicturesByPictureId.Where(p => p.Value.Count > 1))
        {
            var fileInfo = pics.Value
                .Select(f => new FileInfo(f))
                .OrderBy(f => f.FullName.Contains(").") ? long.MinValue : long.MaxValue)
                .ThenByDescending(f => f.Length)
                .ThenByDescending(f => f.CreationTime)
                .ToList();

            while (fileInfo.Count > 1)
            {
                var file = fileInfo[0];
                try
                {
                    File.Delete(file.FullName);
                    Console.WriteLine($"Deleted file: {file.FullName}");
                    fileInfo.Remove(file);
                    removedCount++;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error deleting file: {e.Message}");
                }
            }
        }

        return removedCount;
    }
}