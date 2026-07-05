namespace AutoArchive.Core.Abstractions;

/// <summary>Extracts a truncated plain-text excerpt from an attachment file, for use as classification context.</summary>
public interface ITextExtractor
{
    bool CanExtract(string fileName, string contentType);

    Task<string?> ExtractTextAsync(string filePath, CancellationToken cancellationToken);
}
