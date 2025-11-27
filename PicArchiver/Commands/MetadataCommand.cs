using System.CommandLine;
using PicArchiver.Core.Metadata;
using PicArchiver.Core.Metadata.Loaders;

namespace PicArchiver.Commands;

public class MetadataCommand : BaseCommand
{
    public MetadataCommand() : base("metadata", "Reads metadata from the specified file using the specified metadata loaders.")
    {
        var loadersOption = new Option<string>(
            name: "--loaders",
            description: "A comma separated list of metadata loader to use. folder containing the pictures to archive. " +
                         $"Current loaders: {string.Join(',', MetadataLoader.GetLoaderNames())}");
        
        var fileOption = new Option<string>(
            name: "--file",
            description: "The file to read metadata from.");
        
        AddOption(loadersOption);
        AddOption(fileOption);

        this.SetHandler(LoadMetadata, loadersOption, fileOption);
    }

    private void LoadMetadata(string loaders, string filePath)
    {
        if (string.IsNullOrWhiteSpace(loaders))
        {
            WriteErrorLine("No metadata loaders specified.");
            return;
        }
        
        if (!File.Exists(filePath))
        {
            WriteErrorLine($"File '{filePath}' does not exist.");
            return;
        }
        
        try
        {
            var metadata = new FileMetadata();
            MetadataLoader.LoadMetadata(filePath, loaders, metadata);
            
            WriteLine("Metadata:");
            foreach (var kvp in metadata)
            {
                Write($"{kvp.Key}: ");
                WriteLine(ConsoleColor.DarkGreen, $"{kvp.Value}");
            }
        }
        catch (Exception e)
        {
            WriteErrorLine($"ERROR: {e.Message}");
            return;
        }
    }
}