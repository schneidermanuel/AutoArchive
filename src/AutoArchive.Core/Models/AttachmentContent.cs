namespace AutoArchive.Core.Models;

/// <summary>A single attachment extracted from an email, already streamed to a local temp file.</summary>
public sealed record AttachmentContent(
    string FileName,
    string ContentType,
    long SizeBytes,
    string TempFilePath,
    string? ExtractedText);
