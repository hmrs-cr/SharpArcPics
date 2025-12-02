namespace PicArchiver.Web.Data;

using System;

public class User
{
    public Guid UserId { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
}

public class Picture
{
    public long PictureId { get; set; }
    public string FileName { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsIncoming { get; set; }
}

public class UserFavorite
{
    public Guid UserId { get; set; }
    public long PictureId { get; set; }
    public DateTime DateTime { get; set; }
    public bool IsActive { get; set; }
}

public enum VoteDirection
{
    Up,
    Down
}

public class PictureVote
{
    public Guid UserId { get; set; }
    public long PictureId { get; set; }
    // Dapper handles string-to-enum mapping, but we often map this manually 
    // to ensure the string "up"/"down" matches MySQL format exactly.
    public VoteDirection VoteDirection { get; set; } 
    public DateTime DateTime { get; set; }
}
