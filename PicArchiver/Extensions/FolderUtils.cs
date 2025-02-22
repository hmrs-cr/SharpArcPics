namespace PicArchiver.Extensions;

public static class FolderUtils
{
    public static readonly ICollection<string> PictureFolders = ["DCIM"]; 
    public static readonly ICollection<string> VideoFolders = ["private/M4ROOT/CLIP"]; 
    public static readonly ICollection<string> AllFolders = [..PictureFolders, ..VideoFolders];

    public static IReadOnlyCollection<DirectoryInfo> GetAllConnectedCameraFolders() => GetConnectedCameraFolders(AllFolders).ToList();
    
    public static IEnumerable<DirectoryInfo> GetConnectedCameraFolders(params ICollection<string> subfolders)
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            foreach (var subfolder in subfolders)
            {
                var cameraFolder = Path.Join(drive.RootDirectory.FullName, subfolder.AsSpan());
                if (Directory.Exists(cameraFolder))
                {
                    yield return new DirectoryInfo(cameraFolder);
                }
            }
        }
    }
}