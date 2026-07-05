using AutoArchive.Core.Naming;

namespace AutoArchive.Core.Tests.Naming;

public class CollisionSafeNamerTests
{
    [Fact]
    public void BuildAttachmentFileName_ProducesTimestampSlugOriginalFormat()
    {
        var timestamp = new DateTimeOffset(2026, 7, 5, 14, 30, 22, TimeSpan.Zero);

        var result = CollisionSafeNamer.BuildAttachmentFileName(timestamp, "Q1 Invoice", "scan0042.pdf");

        Assert.Equal("20260705-143022_q1-invoice_scan0042.pdf", result);
    }

    [Fact]
    public void BuildBodyDocumentFileName_EndsWithBodyMd()
    {
        var timestamp = new DateTimeOffset(2026, 7, 5, 14, 30, 22, TimeSpan.Zero);

        var result = CollisionSafeNamer.BuildBodyDocumentFileName(timestamp, "Forwarded note");

        Assert.Equal("20260705-143022_forwarded-note_body.md", result);
    }

    [Fact]
    public void ResolveCollision_ReturnsOriginalName_WhenNoCollisionExists()
    {
        var result = CollisionSafeNamer.ResolveCollision("invoice.pdf", _ => false);

        Assert.Equal("invoice.pdf", result);
    }

    [Fact]
    public void ResolveCollision_AppendsNumericSuffix_WhenCollisionExists()
    {
        var existing = new HashSet<string> { "invoice.pdf", "invoice_2.pdf" };

        var result = CollisionSafeNamer.ResolveCollision("invoice.pdf", existing.Contains);

        Assert.Equal("invoice_3.pdf", result);
    }

    [Fact]
    public void ResolveCollision_HandlesExtensionlessNamesLikeFolders()
    {
        var existing = new HashSet<string> { "Invoices" };

        var result = CollisionSafeNamer.ResolveCollision("Invoices", existing.Contains);

        Assert.Equal("Invoices_2", result);
    }
}
