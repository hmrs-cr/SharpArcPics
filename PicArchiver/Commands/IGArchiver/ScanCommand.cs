using System.Collections.Immutable;
using System.CommandLine;
using PicArchiver.Core.Metadata;
using PicArchiver.Core.Metadata.Loaders;

namespace PicArchiver.Commands.IGArchiver;

public class ScanCommand : IGBaseCommand
{
    internal ScanCommand() : base("scan", "Scans the specified directory and return stats about the picture collection in it.")
    {
        var destinationOption =  new Option<string>(
            name: "--folder",
            description: "The folder to scan.");
        
        var duplicatesOption =  new Option<bool>(
            name: "--duplicates",
            description: "Prints the list of duplicate files by picture id.");
        
        var multiUsersOption =  new Option<bool>(
            name: "--print-multi-usernames",
            description: "Prints the list of users with more than one user name.");
        
        var allUsersOption =  new Option<bool>(
            name: "--print-all-usernames",
            description: "Prints all the list of users names.");
 
        this.AddOption(destinationOption);
        this.AddOption(duplicatesOption);
        this.AddOption(multiUsersOption);
        this.AddOption(allUsersOption);
        
        this.SetHandler(ScanInternal, destinationOption, duplicatesOption, multiUsersOption, allUsersOption);
    }

    private static void ScanInternal(string directory, bool duplicates, bool multiUsers, bool printAllUsers)
    {
        var result = Scan(directory);
        
        Console.WriteLine($"Total files: {result.AllIGFiles.Count}");
        Console.WriteLine($"Valid files: {result.ValidCount}");
        Console.WriteLine($"Total user names: {result.UserNames.Count}");
        Console.WriteLine($"Total users: {result.UserNamesByUserId.Count}");
        Console.WriteLine($"Duplicate files: {result.PicturesByPictureId.Count(p => p.Value.Count > 1)}");

        if (duplicates)
        {
            Console.WriteLine("Duplicate files:");
            foreach (var pics in result.PicturesByPictureId.Where(p => p.Value.Count > 1))
            {
                foreach (var filename in pics.Value)
                {
                    Console.WriteLine(filename);
                }

                Console.WriteLine();
            }
        }

        if (multiUsers || printAllUsers)
        {
            Console.WriteLine("User Names:");
            foreach (var user in result.UserNamesByUserId.Where(u => printAllUsers || u.Value.Count > 1))
            {
                Console.WriteLine($"{user.Key}: {string.Join(", ", user.Value)}");
            }
        }
    }

    public static HashSet<string> ScanUserNames(string? directory, string? except = null)
    {
        var result = new HashSet<string>();
        if (directory == null)
            return result;
        
        foreach (var igFile in Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
                     .Select(IgFile.Parse).Where(igf => igf.IsValid))
        {
            if (!igFile.UserName.Equals(except, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(igFile.UserName);
            }
        }
        return result;
    }

    public static ScanResult Scan(string directory)
    {
        if (string.IsNullOrEmpty(directory))
        {
            directory = Directory.GetCurrentDirectory();
        }
        
        var validCount = 0;
        var userNames = new HashSet<string>();
        var userNamesByUserId = new Dictionary<long, HashSet<string>>();
        var picturesByPictureId = new Dictionary<long, ICollection<string>>();
        
        var allFiles = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories).Select(IgFile.Parse).ToImmutableList();
        var allValidIgFiles = allFiles.Where(igf => igf.IsValid);

        foreach (var file in allValidIgFiles)
        {
            validCount++;
            userNames.Add(file.UserName);
            (userNamesByUserId[file.UserId] = userNamesByUserId.GetValueOrDefault(file.UserId, [])).Add(file.UserName);
            (picturesByPictureId[file.PictureId] = picturesByPictureId.GetValueOrDefault(file.PictureId, [])).Add(file.FullPath);
        }

        return new ScanResult(userNames, userNamesByUserId, picturesByPictureId, allFiles, validCount);
    }

    public sealed class ScanResult
    {
        public int ValidCount { get; }

        internal ScanResult(
            IEnumerable<string> userNames,
            IDictionary<long, HashSet<string>> userNamesById,
            IDictionary<long, ICollection<string>> picturesByPictureId,
            ICollection<IgFile> allFiles,
            int validCount)
        {
            this.ValidCount = validCount;
            this.UserNames = userNames.ToImmutableList();
            this.UserNamesByUserId = userNamesById.ToImmutableDictionary(kvp => kvp.Key, ICollection<string> (kvp) => kvp.Value.ToImmutableList());
            this.PicturesByPictureId = picturesByPictureId.ToImmutableDictionary(kvp => kvp.Key, ICollection<string> (kvp) => kvp.Value.ToImmutableList());
            this.AllIGFiles = allFiles.ToImmutableList();
            
        }

        public ICollection<IgFile> AllIGFiles { get; }
        public ICollection<string> UserNames { get; }
        public IDictionary<long, ICollection<string>> UserNamesByUserId { get; }
        public IDictionary<long, ICollection<string>> PicturesByPictureId { get; }
    }
}