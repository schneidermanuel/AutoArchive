using System.Text;

namespace AutoArchive.Core.Naming;

public static class FilenameSanitizer
{
    private static readonly char[] InvalidChars = Path.GetInvalidFileNameChars();

    private static readonly HashSet<string> ReservedWindowsNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>Sanitizes a single-segment file or folder name: strips invalid/reserved characters, rejects path
    /// traversal, trims length, and falls back to the caller-supplied default if nothing usable remains.</summary>
    public static string Sanitize(string? candidate, string fallback, int maxLength = 100)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return fallback;
        }

        var builder = new StringBuilder(candidate.Length);
        foreach (var c in candidate)
        {
            if (c == '/' || c == '\\' || Array.IndexOf(InvalidChars, c) >= 0 || char.IsControl(c))
            {
                continue;
            }

            builder.Append(c);
        }

        var cleaned = builder.ToString().Trim().Trim('.');

        if (cleaned.Length == 0 || cleaned == ".." || ReservedWindowsNames.Contains(cleaned))
        {
            return fallback;
        }

        return cleaned.Length > maxLength ? cleaned[..maxLength].TrimEnd() : cleaned;
    }

    /// <summary>Sanitizes a possibly-nested relative folder path (e.g. "Reisen/2026/Rinerhorn"), so a new folder
    /// can be proposed under an existing category instead of only ever as a flat top-level name. Each segment is
    /// sanitized independently via <see cref="Sanitize"/>; the whole path falls back to <paramref name="fallback"/>
    /// if it's blank or any segment turns out empty/invalid (e.g. "..") after cleaning.</summary>
    public static string SanitizeRelativePath(string? candidate, string fallback, int maxSegmentLength = 100)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return fallback;
        }

        var segments = candidate.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return fallback;
        }

        var sanitizedSegments = new string[segments.Length];
        for (var i = 0; i < segments.Length; i++)
        {
            var sanitized = Sanitize(segments[i], fallback: string.Empty, maxSegmentLength);
            if (sanitized.Length == 0)
            {
                return fallback;
            }

            sanitizedSegments[i] = sanitized;
        }

        return string.Join('/', sanitizedSegments);
    }

    public static string Slugify(string text, int maxLength = 60)
    {
        var builder = new StringBuilder(text.Length);
        var lastWasDash = false;
        foreach (var c in text.Trim())
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
                lastWasDash = false;
            }
            else if (!lastWasDash && builder.Length > 0)
            {
                builder.Append('-');
                lastWasDash = true;
            }
        }

        var slug = builder.ToString().Trim('-');
        if (slug.Length > maxLength)
        {
            slug = slug[..maxLength].TrimEnd('-');
        }

        return slug.Length == 0 ? "untitled" : slug;
    }
}
