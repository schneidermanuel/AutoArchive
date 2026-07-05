using AutoArchive.Core.Naming;

namespace AutoArchive.Core.Tests.Naming;

public class FilenameSanitizerTests
{
    [Fact]
    public void Sanitize_RemovesInvalidPathCharacters()
    {
        var result = FilenameSanitizer.Sanitize("Invoices/2026\\Q1", "fallback");

        Assert.Equal("Invoices2026Q1", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sanitize_ReturnsFallback_WhenCandidateIsBlank(string? candidate)
    {
        var result = FilenameSanitizer.Sanitize(candidate, "fallback");

        Assert.Equal("fallback", result);
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("con")]
    [InlineData("LPT1")]
    public void Sanitize_ReturnsFallback_ForReservedWindowsNames(string reservedName)
    {
        var result = FilenameSanitizer.Sanitize(reservedName, "fallback");

        Assert.Equal("fallback", result);
    }

    [Fact]
    public void Sanitize_ReturnsFallback_ForPathTraversal()
    {
        var result = FilenameSanitizer.Sanitize("..", "fallback");

        Assert.Equal("fallback", result);
    }

    [Fact]
    public void Sanitize_TrimsToMaxLength()
    {
        var longName = new string('a', 200);

        var result = FilenameSanitizer.Sanitize(longName, "fallback", maxLength: 10);

        Assert.Equal(10, result.Length);
    }

    [Fact]
    public void Slugify_LowercasesAndReplacesNonAlphanumericWithDashes()
    {
        var result = FilenameSanitizer.Slugify("Q1 2026 Invoice: Amazon!!");

        Assert.Equal("q1-2026-invoice-amazon", result);
    }

    [Fact]
    public void Slugify_ReturnsUntitled_WhenNothingUsableRemains()
    {
        var result = FilenameSanitizer.Slugify("!!!???");

        Assert.Equal("untitled", result);
    }
}
