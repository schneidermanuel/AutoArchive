namespace AutoArchive.Core.Models;

/// <summary>The raw, defensively-parsed decision returned by Ollama for one message, before threshold/existence checks.</summary>
public sealed record ClassificationResult(
    string? MatchedFolderRelativePath,
    double Confidence,
    string Reasoning,
    bool ArchiveBodyAsDocument,
    string? SuggestedNewFolderName,
    string? SuggestedNewFolderInformationMd)
{
    public static ClassificationResult NoMatch(string reasoning) =>
        new(null, 0, reasoning, false, null, null);
}
