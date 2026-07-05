using AutoArchive.Core.Abstractions;

namespace AutoArchive.Infrastructure.TextExtraction;

public sealed class PlainTextAttachmentExtractor : ITextExtractor
{
    private static readonly string[] PlainTextExtensions = [".txt", ".md", ".csv", ".log"];

    public bool CanExtract(string fileName, string contentType) =>
        contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
        PlainTextExtensions.Any(ext => fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

    public async Task<string?> ExtractTextAsync(string filePath, CancellationToken cancellationToken) =>
        await File.ReadAllTextAsync(filePath, cancellationToken);
}
