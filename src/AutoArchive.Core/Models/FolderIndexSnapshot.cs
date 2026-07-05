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

    public static FolderIndexSnapshot Empty { get; } = new([]);
}
