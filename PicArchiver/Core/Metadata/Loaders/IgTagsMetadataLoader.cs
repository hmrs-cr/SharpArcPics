using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SharpCompress.Compressors.Xz;

namespace PicArchiver.Core.Metadata.Loaders;

public class IgTagsMetadataLoader : MetadataLoader
{
    public static readonly string GenTagsKey = nameof(IgMetadataRoot) + "Object";
    public static readonly string IgGraphDataKey = nameof(IgGraphNode) + "Object";

    public override bool LoadMetadata(string path, FileMetadata metadata)
    {
        var genTagsFileName = string.Empty;
        if (path.EndsWith(IgFile.MetadataExtension, StringComparison.OrdinalIgnoreCase) && File.Exists(path))
        {
            genTagsFileName = path;
        }
        else if (!File.Exists(genTagsFileName = path + IgFile.MetadataExtension))
        {
            genTagsFileName = string.Empty;
        }

        if (!string.IsNullOrEmpty(genTagsFileName))
        {
            var genTags = JsonSerializer.Deserialize<IgMetadataRoot>(genTagsFileName);
            metadata.GenTags = genTags;
        }

        return true;
    }
}

public readonly struct IgMetadataRoot
{
    public static readonly JsonSerializerOptions JssOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling =  JsonNumberHandling.AllowReadingFromString,
    };
    
    public bool Success { get; init; }
    public MetadataData Data { get; init; }

    public override string ToString() => !Success ? string.Empty : Data.ToString();
    
    public static Task<IgMetadataRoot> LoadAsync(Stream stream)
    {
        return IgTagsMetadataLoaderExtensions.LoadAsync<IgMetadataRoot>(stream);
    }
    
    public static Task<IgMetadataRoot> LoadAsync(string fileName)
    {
        return IgTagsMetadataLoaderExtensions.LoadAsync<IgMetadataRoot>(fileName);
    }

    public readonly struct MetadataData
    {
        public IReadOnlyCollection<string>? Paras { get; init; }
        public MetadataTable? Table { get; init; }
            
        public override string ToString() => Table?.ToString() ?? string.Empty;
            
        public class MetadataTable
        {
            public string Clothing  { get; init; }
            public string People { get; init; }
            public string Emotions { get; init; }
            public string Race { get; init; }
                
            public string Objects { get; init; }
                
            public string Insights { get; init; }

            public override string ToString() => $"{People}\t({Race}) in\t{Clothing}.\t{Emotions}. \t{Objects}";
        }
    }
}

public record IgCarouselMedia(
    [property: JsonPropertyName("id")] 
    string Id,

    [property: JsonPropertyName("taken_at")] 
    long TakenAt
);

public record IgIphoneStruct(
    [property: JsonPropertyName("carousel_media")] IReadOnlyList<IgCarouselMedia>? CarouselMedia
);

public record IgOwner(
    [property: JsonPropertyName("full_name")] string FullName,
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("profile_pic_url_hd")] string ProfilePicUrlHd
);

public record IgGraphNode(
    [property: JsonPropertyName("__typename")]
    string TypeName,
    [property: JsonPropertyName("date")] long Date,
    [property: JsonPropertyName("caption")]
    string Caption,
    [property: JsonPropertyName("owner")] IgOwner IgOwner,
    [property: JsonPropertyName("shortcode")]
    string Shortcode,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("iphone_struct")]
    IgIphoneStruct? IgIphoneStruct
)
{
    internal int CurrentCarrouselIndex { get; set; }
    internal int CarrouselCount { get; set; }
};

public enum Gender
{
    Unknown,
    Masculine,
    Feminine,
    Both
};

public class IgGraphNodeRoot
{
    private string? _fileName;
    public static readonly string Extension = ".json.xz";
    
    public IgGraphNode? Node { get; init; }

    public static async Task<IgGraphNodeRoot> LoadAsync(string fileName)
    {
        var result = await IgTagsMetadataLoaderExtensions.LoadAsync<IgGraphNodeRoot>(fileName);
        result._fileName = fileName;
        return result;
    }

    public IReadOnlyCollection<KeyValuePair<string, IgFile>> ArchiveFileNames
    {
        get
        {
            IReadOnlyCollection<KeyValuePair<string, IgFile>> result = [];
            if (Node?.IgIphoneStruct?.CarouselMedia?.Count > 0)
            {
                var index = 1;
                var r = new Dictionary<string, IgFile>(Node.IgIphoneStruct.CarouselMedia.Count);
                foreach (var media in Node.IgIphoneStruct.CarouselMedia)
                {
                    var igFile = IgFile.Parse($"{Node.IgOwner.Username}_{media.TakenAt}_{media.Id}.jpg");
                    if (igFile.IsValid)
                    {
                        var picFileName = _fileName?.Replace(Extension, $"_{index++}.jpg") ?? string.Empty;
                        r[picFileName] = igFile;
                    }
                }
                
                result = r;
            }
            else if (Node?.IgOwner != null)
            {
                var igFile = IgFile.Parse($"{Node.IgOwner.Username}_{Node.Date}_{Node.Id}_{Node.IgOwner.Id}.jpg");
                if (igFile.IsValid)
                {
                    result = [KeyValuePair.Create(_fileName?.Replace(Extension, ".jpg") ?? string.Empty, igFile)];
                }
            }
            
            Node?.CarrouselCount = result.Count;
            return result;
        }
    }
}

public static partial class IgTagsMetadataLoaderExtensions
{
    public enum Gender
    {
        Unknown,
        Masculine,
        Feminine,
        Both
    };
    
    private static readonly Dictionary<Gender, IEnumerable<string>> GenderMappings = new()
    {
        { Gender.Masculine, [ "man", "he", "his", "him", "male", "boy", "gentleman" ] },
        { Gender.Feminine, [ "woman", "she", "her", "hers", "female", "girl", "lady" ] },
    };

    public static Gender InferGender(this IEnumerable<string> lines)
    {
        var allWords = lines
            .SelectMany(line => WordSplitRegex().Split(line)
                .Where(word => !string.IsNullOrWhiteSpace(word))).ToList();

        var masculineCount = 0;
        var feminineCount = 0;

        foreach (var (gender, keywords) in GenderMappings)
        {
            foreach (var keyword in keywords)
            {
                var count = allWords.Count(word => word.Equals(keyword, StringComparison.OrdinalIgnoreCase));
                switch (gender)
                {
                    case Gender.Masculine:
                        masculineCount += count;
                        break;
                    case Gender.Feminine:
                        feminineCount += count;
                        break;
                }
            }
        }

        if (masculineCount > 0 && feminineCount > 0)
        {
            return Gender.Both;
        }

        if (feminineCount > 0)
        {
            return Gender.Feminine;
        }

        return masculineCount > 0 ? Gender.Masculine : Gender.Unknown;
    }
    
    public static async Task<T> LoadAsync<T>(Stream stream)
    {
        var result = await JsonSerializer.DeserializeAsync<T>(stream, IgMetadataRoot.JssOptions);
        return result ?? throw new NullReferenceException();
    }

    public static async Task<T> LoadAsync<T>(string fileName)
    {
        Stream? xzStream = null;

        await using var fileStream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (fileName.EndsWith(".xz", StringComparison.OrdinalIgnoreCase))
        {
            xzStream = new XZStream(fileStream);
        }

        try
        {
            return await LoadAsync<T>(xzStream ?? fileStream);
        }
        finally
        {
            if (xzStream != null)
            {
                await xzStream.DisposeAsync();
            }
        }
    }

    extension(FileMetadata metadata)
    {
        public IgGraphNode? IgGraphData
        {
            get => metadata[IgTagsMetadataLoader.IgGraphDataKey] as IgGraphNode;
            set => metadata[IgTagsMetadataLoader.IgGraphDataKey] = value;
        }
        
        public IgMetadataRoot? GenTags
        {
            get => metadata[IgTagsMetadataLoader.GenTagsKey] as IgMetadataRoot?;
            set => metadata[IgTagsMetadataLoader.GenTagsKey] = value;
        }
    }

    [GeneratedRegex(@"\W+")]
    private static partial Regex WordSplitRegex();
}

