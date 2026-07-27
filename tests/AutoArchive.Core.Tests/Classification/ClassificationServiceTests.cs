using AutoArchive.Core.Abstractions;
using AutoArchive.Core.Classification;
using AutoArchive.Core.Models;
using AutoArchive.Core.Options;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace AutoArchive.Core.Tests.Classification;

public class ClassificationServiceTests
{
    private static readonly MailMessageContent SampleMessage =
        new("id1", "Q1 Invoice", "billing@vendor.com", DateTimeOffset.UtcNow, "Please find attached.", []);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static ClassificationService CreateService(IOllamaClient client, double confidenceThreshold = 0.6) =>
        new(client, Microsoft.Extensions.Options.Options.Create(new OllamaOptions { ConfidenceThreshold = confidenceThreshold }),
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 5, 12, 0, 0, TimeSpan.Zero)));

    [Fact]
    public async Task ClassifyAsync_ConfidentMatchInSnapshot_ReturnsExistingFolderDecision()
    {
        var snapshot = new FolderIndexSnapshot([new FolderInfo("Finance/Invoices", "/archive/Finance/Invoices", "Invoices.")]);
        var client = Substitute.For<IOllamaClient>();
        client.ChatJsonAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("""{ "matchedFolderPath": "Finance/Invoices", "confidence": 0.9, "reasoning": "match" }""");

        var decision = await CreateService(client).ClassifyAsync(SampleMessage, snapshot, CancellationToken.None);

        Assert.Equal("Finance/Invoices", decision.TargetFolderRelativePath);
        Assert.False(decision.IsNewFolder);
        Assert.Null(decision.NewFolderInformationMd);
    }

    [Fact]
    public async Task ClassifyAsync_ConfidenceBelowThreshold_CreatesNewFolderInstead()
    {
        var snapshot = new FolderIndexSnapshot([new FolderInfo("Finance/Invoices", "/archive/Finance/Invoices", "Invoices.")]);
        var client = Substitute.For<IOllamaClient>();
        client.ChatJsonAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("""{ "matchedFolderPath": "Finance/Invoices", "confidence": 0.3, "reasoning": "weak match", "suggestedNewFolderName": "Receipts", "suggestedNewFolderInformationMd": "Receipts live here." }""");

        var decision = await CreateService(client).ClassifyAsync(SampleMessage, snapshot, CancellationToken.None);

        Assert.True(decision.IsNewFolder);
        Assert.Equal("Receipts", decision.TargetFolderRelativePath);
        Assert.Equal("Receipts live here.", decision.NewFolderInformationMd);
    }

    [Fact]
    public async Task ClassifyAsync_MatchedPathNotInSnapshot_IsTreatedAsHallucinationAndCreatesNewFolder()
    {
        var client = Substitute.For<IOllamaClient>();
        client.ChatJsonAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("""{ "matchedFolderPath": "Nonexistent/Folder", "confidence": 0.95, "reasoning": "hallucinated" }""");

        var decision = await CreateService(client).ClassifyAsync(SampleMessage, FolderIndexSnapshot.Empty, CancellationToken.None);

        Assert.True(decision.IsNewFolder);
    }

    [Fact]
    public async Task ClassifyAsync_NoSuggestedName_FallsBackToTimestampedUnsortedName()
    {
        var client = Substitute.For<IOllamaClient>();
        client.ChatJsonAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("""{ "matchedFolderPath": "NONE", "confidence": 0, "reasoning": "no match" }""");

        var decision = await CreateService(client).ClassifyAsync(SampleMessage, FolderIndexSnapshot.Empty, CancellationToken.None);

        Assert.Equal("Unsorted-20260705-120000", decision.TargetFolderRelativePath);
    }

    [Fact]
    public async Task ClassifyAsync_NewFolderNameCollidesWithExisting_GetsNumericSuffix()
    {
        var snapshot = new FolderIndexSnapshot([new FolderInfo("Receipts", "/archive/Receipts", "Existing receipts.")]);
        var client = Substitute.For<IOllamaClient>();
        client.ChatJsonAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("""{ "matchedFolderPath": "NONE", "confidence": 0, "reasoning": "no match", "suggestedNewFolderName": "Receipts" }""");

        var decision = await CreateService(client).ClassifyAsync(SampleMessage, snapshot, CancellationToken.None);

        Assert.Equal("Receipts_2", decision.TargetFolderRelativePath);
    }
}
