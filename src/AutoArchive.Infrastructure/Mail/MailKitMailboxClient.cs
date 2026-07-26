using System.Text.RegularExpressions;
using AutoArchive.Core.Abstractions;
using AutoArchive.Core.Models;
using AutoArchive.Core.Options;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AutoArchive.Infrastructure.Mail;

/// <summary>One poll-cycle IMAP session: connect, fetch unprocessed messages (extracting attachments/body to a
/// temp dir), move filed messages to the Processed folder, disconnect. Not reused across cycles.</summary>
public sealed class MailKitMailboxClient(
    IOptions<ImapOptions> options,
    IEnumerable<ITextExtractor> textExtractors,
    ILogger<MailKitMailboxClient> logger) : IMailboxClient
{
    private readonly ImapOptions _options = options.Value;
    private readonly Dictionary<string, UniqueId> _messageIdToUid = new();
    private ImapClient? _client;

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        var client = new ImapClient();
        var socketOptions = _options.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await client.ConnectAsync(_options.Host, _options.Port, socketOptions, cancellationToken);
        await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
        await client.Inbox.OpenAsync(FolderAccess.ReadWrite, cancellationToken);
        _client = client;
    }

    public async Task<IReadOnlyList<MailMessageContent>> FetchNewMessagesAsync(
        IReadOnlySet<string> processedMessageIds,
        CancellationToken cancellationToken)
    {
        var inbox = RequireConnectedInbox();

        var uids = await inbox.SearchAsync(SearchQuery.All, cancellationToken);
        if (uids.Count == 0)
        {
            return [];
        }

        var summaries = await inbox.FetchAsync(uids, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId, cancellationToken);

        var results = new List<MailMessageContent>();
        foreach (var summary in summaries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var messageId = summary.Envelope?.MessageId;
            if (string.IsNullOrEmpty(messageId))
            {
                logger.LogWarning("Message UID {Uid} has no Message-ID header; skipping (cannot be tracked for dedup).", summary.UniqueId);
                continue;
            }

            if (processedMessageIds.Contains(messageId))
            {
                continue;
            }

            var mimeMessage = await inbox.GetMessageAsync(summary.UniqueId, cancellationToken);
            _messageIdToUid[messageId] = summary.UniqueId;
            results.Add(await ExtractContentAsync(messageId, mimeMessage, cancellationToken));
        }

        return results;
    }

    public async Task MoveToProcessedAsync(string messageId, CancellationToken cancellationToken)
    {
        var inbox = RequireConnectedInbox();

        if (!_messageIdToUid.TryGetValue(messageId, out var uid))
        {
            logger.LogWarning("No cached UID for message {MessageId}; cannot move it to the Processed folder.", messageId);
            return;
        }

        var processedFolder = await GetOrCreateProcessedFolderAsync(cancellationToken);
        await inbox.MoveToAsync(uid, processedFolder, cancellationToken);
    }

    private IMailFolder RequireConnectedInbox() =>
        _client?.Inbox ?? throw new InvalidOperationException("Mailbox client is not connected; call ConnectAsync first.");

    private async Task<IMailFolder> GetOrCreateProcessedFolderAsync(CancellationToken cancellationToken)
    {
        var personal = _client!.GetFolder(_client.PersonalNamespaces[0]);
        try
        {
            return await personal.GetSubfolderAsync(_options.ProcessedFolderName, cancellationToken);
        }
        catch (FolderNotFoundException)
        {
            return await personal.CreateAsync(_options.ProcessedFolderName, isMessageFolder: true, cancellationToken)
                ?? throw new InvalidOperationException($"IMAP server did not return the newly created '{_options.ProcessedFolderName}' folder.");
        }
    }

    private async Task<MailMessageContent> ExtractContentAsync(string messageId, MimeMessage message, CancellationToken cancellationToken)
    {
        var bodyText = message.TextBody ?? StripHtmlTags(message.HtmlBody) ?? string.Empty;
        var attachments = new List<AttachmentContent>();

        foreach (var entity in message.Attachments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entity is not MimePart { Content: not null } mimePart)
            {
                continue;
            }

            var fileName = mimePart.FileName ?? mimePart.ContentType.Name ?? "attachment";
            var tempPath = Path.Combine(Path.GetTempPath(), $"autoarchive-{Guid.NewGuid():N}-{fileName}");

            await using (var stream = File.Create(tempPath))
            {
                await mimePart.Content.DecodeToAsync(stream, cancellationToken);
            }

            var sizeBytes = new FileInfo(tempPath).Length;
            if (sizeBytes > _options.MaxAttachmentSizeBytes)
            {
                logger.LogWarning(
                    "Attachment {FileName} on message {MessageId} is {SizeBytes} bytes, exceeding the {MaxBytes} byte cap; skipping.",
                    fileName, messageId, sizeBytes, _options.MaxAttachmentSizeBytes);
                File.Delete(tempPath);
                continue;
            }

            var contentType = mimePart.ContentType.MimeType;
            var extractedText = await TryExtractTextAsync(fileName, contentType, tempPath, cancellationToken);

            attachments.Add(new AttachmentContent(fileName, contentType, sizeBytes, tempPath, extractedText));
        }

        var from = message.From.ToString();
        return new MailMessageContent(messageId, message.Subject ?? string.Empty, from, message.Date, bodyText, attachments);
    }

    private async Task<string?> TryExtractTextAsync(string fileName, string contentType, string filePath, CancellationToken cancellationToken)
    {
        var extractor = textExtractors.FirstOrDefault(e => e.CanExtract(fileName, contentType));
        if (extractor is null)
        {
            return null;
        }

        try
        {
            return await extractor.ExtractTextAsync(filePath, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to extract text from attachment {FileName}; continuing without an excerpt.", fileName);
            return null;
        }
    }

    private static string? StripHtmlTags(string? html) =>
        string.IsNullOrEmpty(html) ? null : Regex.Replace(html, "<[^>]+>", " ");

    public async ValueTask DisposeAsync()
    {
        if (_client is null)
        {
            return;
        }

        if (_client.IsConnected)
        {
            await _client.DisconnectAsync(quit: true);
        }

        _client.Dispose();
    }
}
