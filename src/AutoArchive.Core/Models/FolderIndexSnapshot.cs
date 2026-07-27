namespace AutoArchive.Core.Models;

/// <summary>An immutable point-in-time view of the archive folder tree, rebuilt on each rescan.</summary>
public sealed class FolderIndexSnapshot
{
    private readonly Dictionary<string, FolderInfo> _byRelativePath;

    public FolderIndexSnapshot(IReadOnlyList<FolderInfo> folders)
    {
        Folders = folders;
        _byRelativePath = folders.ToDictionary(f => f.RelativePath, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<FolderInfo> Folders { get; }

    public bool Contains(string relativePath) => _byRelativePath.ContainsKey(relativePath);

    /// <summary>Resolves a path the LLM returned to the canonical stored relative path. Models sometimes echo a
    /// path as written inside a folder's own information.md (e.g. an absolute "/Dossier/X" documentation line)
    /// rather than the bare relative path we actually gave them, so an exact match is tried first and a
    /// suffix match (tolerating any such extra prefix) is tried as a fallback.</summary>
    public bool TryResolve(string candidatePath, out string resolvedRelativePath)
    {
        if (_byRelativePath.TryGetValue(candidatePath, out var exact))
        {
            resolvedRelativePath = exact.RelativePath;
            return true;
        }

        var normalized = candidatePath.Trim().TrimStart('/', '\\').Replace('\\', '/');
        foreach (var folder in Folders)
        {
            if (normalized.Equals(folder.RelativePath, StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith("/" + folder.RelativePath, StringComparison.OrdinalIgnoreCase))
            {
                resolvedRelativePath = folder.RelativePath;
                return true;
            }
        }

        resolvedRelativePath = string.Empty;
        return false;
    }

    public static FolderIndexSnapshot Empty { get; } = new([]);
}
