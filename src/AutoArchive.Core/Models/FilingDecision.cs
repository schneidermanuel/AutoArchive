namespace AutoArchive.Core.Models;

/// <summary>The final, validated decision of where to file a message, after applying the confidence threshold and
/// cross-checking any matched path against the current folder index.</summary>
public sealed record FilingDecision(
    string TargetFolderRelativePath,
    bool IsNewFolder,
    string? NewFolderInformationMd,
    bool ArchiveBodyAsDocument,
    double Confidence,
    string Reasoning);
