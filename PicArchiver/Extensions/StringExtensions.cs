using System.Text.RegularExpressions;
using PicArchiver.Core.Metadata;

namespace PicArchiver.Extensions;

public static partial class StringExtensions
{
    private static readonly IList<string>  ByteSizes = [ "B", "KB", "MB", "GB", "TB" ];
    
    // FNV-1a 128-bit Offset Basis
    // Decimal: 144066263297769815596495629667062367629
    // Hex: 6C62272E07BB0142 62B821756295C58D
    private static readonly UInt128 OffsetBasis = new UInt128(0x6C62272E07BB0142, 0x62B821756295C58D);

    // FNV-1a 128-bit Prime
    // Decimal: 309485009821345068724781371
    // Hex: 0000000001000000 000000000000013B
    private static readonly UInt128 Prime = new UInt128(0x01000000, 0x000000000000013B);

    
    public static string ResolveTokens(this string template, FileMetadata? replacements)
    {
        if (replacements != null && template.Contains('{') && template.Contains('}'))
        {
            return ResolveCurlyTokensRegex().Replace(template, 
                match => replacements.Get<object>(match.Groups[1].Value)?.ToString() ?? match.Value);
        }

        return template;
    }
    
    public static ulong ComputeHash(this string input) => 
        input.Aggregate(14695981039346656037ul, (hash, c) => (hash ^ c) * 1099511628211ul);
    
    public static UInt128 ComputeHash128(this string input)
    {
        // Standard FNV-1a usually processes Bytes (UTF8), not Chars.
        // Converting to UTF8 bytes is "safer" for cross-platform compatibility.
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);

        return bytes.Aggregate(OffsetBasis, (hash, b) => (hash ^ b) * Prime);
    }
    
    public static string ToHumanReadableString(this TimeSpan t, bool shortDesc = false)
    {
        var secondsStr = shortDesc ? "s" : " seconds";
        var minsStr = shortDesc ? "m" : " minutes";
        var hoursStr = shortDesc ? "m" : " hours";
        var daysStr = shortDesc ? "m" : " days";
        
        if (t.TotalSeconds <= 1) {
            return $@"{t:s\.ff}{secondsStr}";
        }
        if (t.TotalMinutes <= 1) {
            return $@"{t:%s}{secondsStr}";
        }
        if (t.TotalHours <= 1) {
            return $@"{t:%m}{minsStr}";
        }
        if (t.TotalDays <= 1) {
            return $@"{t:%h}{hoursStr}";
        }

        return $@"{t:%d}{daysStr}";
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
            lenght /= 1024;
        }
        
        return $"{lenght:0.0}{ByteSizes[index]}";
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