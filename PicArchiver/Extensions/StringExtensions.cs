using System.Text.RegularExpressions;
using PicArchiver.Core.Metadata;

namespace PicArchiver.Extensions;

public static partial class StringExtensions
{
    private static readonly IList<string>  ByteSizes = [ "B", "KB", "MB", "GB", "TB" ];
    
    public static string ResolveTokens(this string template, FileMetadata? replacements)
    {
        if (replacements != null && template.Contains('{') && template.Contains('}'))
        {
            return ResolveCurlyTokensRegex().Replace(template, 
                match => replacements.Get<object>(match.Groups[1].Value)?.ToString() ?? match.Value);
        }

        return template;
    }
    
    public static string ToHumanReadableString (this TimeSpan t)
    {
        if (t.TotalSeconds <= 1) {
            return $@"{t:s\.ff} seconds";
        }
        if (t.TotalMinutes <= 1) {
            return $@"{t:%s} seconds";
        }
        if (t.TotalHours <= 1) {
            return $@"{t:%m} minutes";
        }
        if (t.TotalDays <= 1) {
            return $@"{t:%h} hours";
        }

        return $@"{t:%d} days";
    }

    public static string ToHumanReadableByteSize(this object size)
    {
        var lenght = size switch
        {
            long l => l,
            int i => i,
            float f => f,
            double d => d,
            string s => double.Parse(s),
            _ => throw new ArgumentException(message: "Invalid type", paramName: nameof(size))
        };

        var index = 0;
        while (lenght >= 1024 && index < ByteSizes.Count - 1) 
        {
            index++;
            lenght = lenght/1024;
        }
        
        return $"{lenght:0.#}{ByteSizes[index]}";
    }


    public static string ToSizeString(this long num, string suffix = "B")
    {
        string[] units = ["", "K", "M", "G", "T", "P", "E", "Z"];
        double size = num;
        foreach (var unit in units)
        {
            if (Math.Abs(size) < 1024.0)
            {
                return $"{size:0.0} {unit}{suffix}";
            }

            size /= 1024.0;
        }

        return $"{size:0.0} Y{suffix}";
    }

    [GeneratedRegex(@"{(.*?)}")]
    private static partial Regex ResolveCurlyTokensRegex();
}