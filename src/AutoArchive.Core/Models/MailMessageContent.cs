namespace AutoArchive.Core.Models;

/// <summary>The extracted, provider-agnostic content of one incoming email.</summary>
public sealed record MailMessageContent(
    string MessageId,
    string Subject,
    string From,
    DateTimeOffset Date,
    string BodyText,
    IReadOnlyList<AttachmentContent> Attachments);
