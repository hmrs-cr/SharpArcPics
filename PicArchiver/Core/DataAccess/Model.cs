namespace PicArchiver.Core.DataAccess;

public class UserData
{
    public Guid Id { get; set; }
    public ICollection<string>? Favs { get; set; }
    
    public AppInfo? AppInfo { get; set; }
    
    public bool IsAdmin => HasRoles("Admin");

    public bool HasRoles(params IEnumerable<string> roles)
    {
        // No roles implement for now.
        // TODO: Implement roles.
        return true;
    }
}

public class AppInfo
{
    public required string AppName { get; init; }
    public required string AppVersion { get; init; }
    public required string AppDescription { get; init; }
    public required string Developer { get; init; }
    
    public string? FavsLabel { get; init; }
    public string? TopRatedLabel { get; init; }
    public string? LowRatedLabel { get; init; }
}

public class PictureData
{
    public ulong PictureId { get; init; }
    public string FileName { get; init; }
    public long? IgPictureId { get; init; }
    public long? IgUserId { get; init; }
    public bool IsDeleted { get; init; }
}

public class PictureMetaData
{
    public required string FileName { get; init; }
    public string? Description { get; init; } 
    public string? Clothing {get; init; } 
    public string? Emotions {get; init; } 
    public string? Objects  {get; init; } 
    public string? People  {get; init; } 
    public string? Race  {get; init; } 
    public string? AuthorNames  {get; init; } 
    public DateTime? DateAdded { get; init; }
}