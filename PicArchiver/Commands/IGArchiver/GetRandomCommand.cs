using System.Collections.Concurrent;
using System.CommandLine;
using PicArchiver.Core.Metadata.Loaders;

namespace PicArchiver.Commands.IGArchiver;

public class GetRandomCommand : IGBaseCommand
{       
    internal GetRandomCommand() : base("get-random", "Gets a random image path from the specified folder.")
    {
        var destinationOption =  new Option<string>(
            name: "--folder",
            description: "The folder to scan.");
        
        this.AddOption(destinationOption);
        this.SetHandler(GetRandomInternal, destinationOption);
    }

    private static void GetRandomInternal(string folder)
    {
        var randomImage = GetRandom(folder);
        if (randomImage != null)
        {
            Console.WriteLine(randomImage);
        }
    }
    
    public static string? GetRandom(string baseFolder)
    {
        var random = Random.Shared;

        var maxSufix = 100;
        string[] directories = [];
        while (maxSufix > 0)
        {
            var suffix = random.Next(maxSufix);
            var prefix = random.Next(1, suffix == 0 ? 10 : suffix);
            directories = Directory.GetDirectories(baseFolder, $"{prefix}*{suffix}", SearchOption.TopDirectoryOnly);
            if (directories.Length > 0)
            {
                break;
            }

            if (maxSufix > 10)
            {
                maxSufix /= 10;
            }
            else
            {
                maxSufix--;
            }
        }
        
        var pictureFolder = directories.Length switch
        {
            1 => directories[0],
            > 1 => directories[random.Next(directories.Length)],
            _ => string.Empty
        };

        if (pictureFolder == string.Empty)
        {
            return null;
        }
        
        var files = Directory.EnumerateFiles(pictureFolder).Where(f => !f.EndsWith(IgFile.MetadataExtension)).ToList();
        return files.Count switch
        {
            1 => files[0],
            > 1 => files[random.Next(files.Count)],
            _ => null
        };
    }
}