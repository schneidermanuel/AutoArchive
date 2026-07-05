using AutoArchive.Core.Classification;
using AutoArchive.Core.Models;
using AutoArchive.Core.Options;

namespace AutoArchive.Core.Tests.Classification;

public class ClassificationPromptBuilderTests
{
    private static readonly OllamaOptions DefaultOptions = new()
    {
        MaxInformationMdCharsPerFolder = 2000,
        MaxBodyChars = 4000,
        MaxAttachmentExcerptChars = 2000,
    };

    [Fact]
    public void BuildUserPrompt_IncludesEachCandidateFolderPathAndDescription()
    {
        var snapshot = new FolderIndexSnapshot([
            new FolderInfo("Finance/Invoices", "/archive/Finance/Invoices", "Invoices go here."),
            new FolderInfo("Personal/Health", "/archive/Personal/Health", "Medical records."),
        ]);
        var message = new MailMessageContent("id1", "Subject", "a@b.com", DateTimeOffset.UtcNow, "Body text", []);

        var prompt = ClassificationPromptBuilder.BuildUserPrompt(snapshot, message, DefaultOptions);

        Assert.Contains("Finance/Invoices: Invoices go here.", prompt);
        Assert.Contains("Personal/Health: Medical records.", prompt);
    }

    [Fact]
    public void BuildUserPrompt_WithNoFolders_StatesNoneExistYet()
    {
        var message = new MailMessageContent("id1", "Subject", "a@b.com", DateTimeOffset.UtcNow, "Body", []);

        var prompt = ClassificationPromptBuilder.BuildUserPrompt(FolderIndexSnapshot.Empty, message, DefaultOptions);

        Assert.Contains("none exist yet", prompt);
    }

    [Fact]
    public void BuildUserPrompt_TruncatesBodyBeyondMaxChars()
    {
        var longBody = new string('x', 5000);
        var message = new MailMessageContent("id1", "Subject", "a@b.com", DateTimeOffset.UtcNow, longBody, []);

        var prompt = ClassificationPromptBuilder.BuildUserPrompt(FolderIndexSnapshot.Empty, message, DefaultOptions);

        Assert.Contains("...(truncated)", prompt);
        Assert.DoesNotContain(new string('x', 5000), prompt);
    }

    [Fact]
    public void BuildUserPrompt_IncludesAttachmentMetadataAndExcerpt()
    {
        var attachment = new AttachmentContent("invoice.pdf", "application/pdf", 1024, "/tmp/invoice.pdf", "Total due: $42");
        var message = new MailMessageContent("id1", "Subject", "a@b.com", DateTimeOffset.UtcNow, "Body", [attachment]);

        var prompt = ClassificationPromptBuilder.BuildUserPrompt(FolderIndexSnapshot.Empty, message, DefaultOptions);

        Assert.Contains("invoice.pdf (application/pdf, 1024 bytes)", prompt);
        Assert.Contains("Total due: $42", prompt);
    }
}
