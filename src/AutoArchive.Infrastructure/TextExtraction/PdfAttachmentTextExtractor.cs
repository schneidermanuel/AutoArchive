using System.Text;
using AutoArchive.Core.Abstractions;
using iText.Kernel.Pdf;

namespace AutoArchive.Infrastructure.TextExtraction;

/// <summary>Extracts text from PDF attachments via iText7. Named "Attachment" to avoid colliding with
/// iText's own iText.Kernel.Pdf.Canvas.Parser.PdfTextExtractor type.</summary>
public sealed class PdfAttachmentTextExtractor : ITextExtractor
{
    public bool CanExtract(string fileName, string contentType) =>
        contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    public Task<string?> ExtractTextAsync(string filePath, CancellationToken cancellationToken)
    {
        using var reader = new PdfReader(filePath);
        using var document = new PdfDocument(reader);

        var text = new StringBuilder();
        var pageCount = document.GetNumberOfPages();
        for (var pageNumber = 1; pageNumber <= pageCount; pageNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = document.GetPage(pageNumber);
            text.AppendLine(iText.Kernel.Pdf.Canvas.Parser.PdfTextExtractor.GetTextFromPage(page));
        }

        return Task.FromResult<string?>(text.ToString());
    }
}
