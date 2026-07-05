namespace AutoArchive.Core.Naming;

public static class CollisionSafeNamer
{
    public static string BuildAttachmentFileName(DateTimeOffset timestamp, string subject, string originalFileName)
    {
        var slug = FilenameSanitizer.Slugify(subject);
        var safeOriginal = FilenameSanitizer.Sanitize(originalFileName, "attachment");
        return $"{timestamp:yyyyMMdd-HHmmss}_{slug}_{safeOriginal}";
    }

    public static string BuildBodyDocumentFileName(DateTimeOffset timestamp, string subject)
    {
        var slug = FilenameSanitizer.Slugify(subject);
        return $"{timestamp:yyyyMMdd-HHmmss}_{slug}_body.md";
    }

    /// <summary>Appends a numeric suffix before the extension until <paramref name="exists"/> returns false.
    /// Works for extension-less names (e.g. folders) too, since the "extension" is then just empty.</summary>
    public static string ResolveCollision(string candidateName, Func<string, bool> exists)
    {
        if (!exists(candidateName))
        {
            return candidateName;
        }

        var extension = Path.GetExtension(candidateName);
        var stem = Path.GetFileNameWithoutExtension(candidateName);

        for (var i = 2; ; i++)
        {
            var candidate = $"{stem}_{i}{extension}";
            if (!exists(candidate))
            {
                return candidate;
            }
        }
    }
}
