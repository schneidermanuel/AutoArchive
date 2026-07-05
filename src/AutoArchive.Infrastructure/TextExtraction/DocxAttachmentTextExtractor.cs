using AutoArchive.Core.Abstractions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AutoArchive.Infrastructure.TextExtraction;

public sealed class DocxAttachmentTextExtractor : ITextExtractor
{
    public bool CanExtract(string fileName, string contentType) =>
        contentType.Equals("application/vnd.openxmlformats-officedocument.wordprocessingml.document", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase);

    public Task<string?> ExtractTextAsync(string filePath, CancellationToken cancellationToken)
    {
        using var document = WordprocessingDocument.Open(filePath, isEditable: false);
        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null)
        {
            return Task.FromResult<string?>(null);
        }

        var paragraphs = body.Descendants<Paragraph>().Select(p => p.InnerText);
        return Task.FromResult<string?>(string.Join(Environment.NewLine, paragraphs));
    }
}
