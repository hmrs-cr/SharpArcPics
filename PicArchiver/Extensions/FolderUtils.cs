namespace PicArchiver.Extensions;

public static class FolderUtils
{
    private static readonly char[] Separators = [' ', '\t'];

    public static readonly ICollection<string> PictureFolders = ["DCIM"]; 
    public static readonly ICollection<string> VideoFolders = ["private/M4ROOT/CLIP"]; 
    public static readonly ICollection<string> AllFolders = [..PictureFolders, ..VideoFolders];

    public static ICollection<string> GetAllConnectedCameraFolders() => GetConnectedCameraFolders(AllFolders);
    
    public static ICollection<string> GetConnectedCameraFolders(params ICollection<string> subfolders)
    {
        var process = ProcessExtensions.CreateProcessCommand("df", "-Ph");
        if (process.StartAndWaitUntilExit() != 0) 
            return Array.Empty<string>();
        
        List<string>? result = null;
        Span<Range> ranges = stackalloc Range[6];
        while (process.StandardOutput.ReadLine() is { } line)
        {
            if (!line.StartsWith("/dev/") && !line.StartsWith("//"))
                continue;
                
            var lineSpan = line.AsSpan();
            var parts = lineSpan.SplitAny(ranges, Separators.AsSpan(), StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts != 6)
                continue;

            var mountPointPath = lineSpan[ranges[5]];
            foreach (var subfolder in subfolders)
            {
                var cameraFolder = Path.Join(mountPointPath, subfolder.AsSpan());
                if (Directory.Exists(cameraFolder))
                {
                    (result ??= []).Add(cameraFolder);
                }
            }
        }

        return result != null ? result : Array.Empty<string>();
    }
}