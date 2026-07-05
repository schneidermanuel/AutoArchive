using AutoArchive.Core.Classification;

namespace AutoArchive.Core.Tests.Classification;

public class ClassificationResponseParserTests
{
    [Fact]
    public void Parse_ValidJson_ReturnsExpectedResult()
    {
        const string json = """
            {
              "matchedFolderPath": "Finance/Invoices",
              "confidence": 0.85,
              "reasoning": "Looks like an invoice.",
              "archiveBodyAsDocument": false,
              "suggestedNewFolderName": null,
              "suggestedNewFolderInformationMd": null
            }
            """;

        var result = ClassificationResponseParser.Parse(json);

        Assert.Equal("Finance/Invoices", result.MatchedFolderRelativePath);
        Assert.Equal(0.85, result.Confidence);
        Assert.Equal("Looks like an invoice.", result.Reasoning);
        Assert.False(result.ArchiveBodyAsDocument);
        Assert.Null(result.SuggestedNewFolderName);
    }

    [Fact]
    public void Parse_StripsMarkdownCodeFences()
    {
        const string fenced = """
            ```json
            { "matchedFolderPath": "NONE", "confidence": 0.2, "reasoning": "no match", "archiveBodyAsDocument": true }
            ```
            """;

        var result = ClassificationResponseParser.Parse(fenced);

        Assert.Null(result.MatchedFolderRelativePath);
        Assert.True(result.ArchiveBodyAsDocument);
    }

    [Fact]
    public void Parse_TreatsLiteralNoneAsNoMatch()
    {
        const string json = """{ "matchedFolderPath": "NONE", "confidence": 0.9, "reasoning": "x" }""";

        var result = ClassificationResponseParser.Parse(json);

        Assert.Null(result.MatchedFolderRelativePath);
    }

    [Fact]
    public void Parse_MalformedJson_ReturnsNoMatchInsteadOfThrowing()
    {
        var result = ClassificationResponseParser.Parse("this is not json at all {{{");

        Assert.Null(result.MatchedFolderRelativePath);
        Assert.Equal(0, result.Confidence);
    }

    [Theory]
    [InlineData(1.5, 1.0)]
    [InlineData(-0.5, 0.0)]
    public void Parse_ClampsConfidenceToValidRange(double rawConfidence, double expectedConfidence)
    {
        var json = $$"""{ "matchedFolderPath": "A", "confidence": {{rawConfidence}}, "reasoning": "x" }""";

        var result = ClassificationResponseParser.Parse(json);

        Assert.Equal(expectedConfidence, result.Confidence);
    }

    [Fact]
    public void Parse_MissingOptionalFields_UsesSafeDefaults()
    {
        const string json = """{ "matchedFolderPath": "A" }""";

        var result = ClassificationResponseParser.Parse(json);

        Assert.Equal(string.Empty, result.Reasoning);
        Assert.False(result.ArchiveBodyAsDocument);
        Assert.Null(result.SuggestedNewFolderName);
        Assert.Null(result.SuggestedNewFolderInformationMd);
    }

    [Fact]
    public void Parse_BlankSuggestedNewFolderName_IsTreatedAsNull()
    {
        const string json = """{ "matchedFolderPath": "NONE", "confidence": 0, "suggestedNewFolderName": "   " }""";

        var result = ClassificationResponseParser.Parse(json);

        Assert.Null(result.SuggestedNewFolderName);
    }
}
