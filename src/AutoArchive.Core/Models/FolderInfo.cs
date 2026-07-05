namespace AutoArchive.Core.Models;

/// <summary>A folder in the archive tree that contains an information.md and is a valid filing target.</summary>
public sealed record FolderInfo(string RelativePath, string AbsolutePath, string InformationMdContent);
