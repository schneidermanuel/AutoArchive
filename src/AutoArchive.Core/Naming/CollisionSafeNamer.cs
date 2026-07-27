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
    /// Works for extension-less names (e.g. folders) too, since the "extension" is then just empty. Also works for
    /// nested relative paths (e.g. "Reisen/2026/Rinerhorn") - the suffix is applied only to the last segment, not
    /// the whole path, so the parent folder it's nested under is preserved.</summary>
    public static string ResolveCollision(string candidateName, Func<string, bool> exists)
    {
        if (!exists(candidateName))
        {
            return candidateName;
        }

        var normalized = candidateName.Replace('\\', '/');
        var lastSlash = normalized.LastIndexOf('/');
        var parentPrefix = lastSlash >= 0 ? normalized[..(lastSlash + 1)] : string.Empty;
        var leaf = lastSlash >= 0 ? normalized[(lastSlash + 1)..] : normalized;

        var extension = Path.GetExtension(leaf);
        var stem = Path.GetFileNameWithoutExtension(leaf);

        for (var i = 2; ; i++)
        {
            var candidate = $"{parentPrefix}{stem}_{i}{extension}";
            if (!exists(candidate))
            {
                return candidate;
            }
        }
    }
}
