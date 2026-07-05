using AutoArchive.Core.Abstractions;
using AutoArchive.Core.Classification;
using AutoArchive.Core.Models;
using AutoArchive.Core.Naming;
using AutoArchive.Core.Options;
using Microsoft.Extensions.Options;

namespace AutoArchive.Worker.HostedServices;

/// <summary>The main pipeline: poll the mailbox, classify each new message with Ollama, file it (creating a new
/// folder if nothing matches), and only then mark it processed - so a crash/outage never loses an email.</summary>
public sealed class MailProcessingService(
    IMailboxClientFactory mailboxClientFactory,
    IFolderIndex folderIndex,
    ClassificationService classificationService,
    IArchiveWriter archiveWriter,
    IProcessedMessageStore processedMessageStore,
    INotificationService notificationService,
    IOptions<ImapOptions> imapOptions,
    ILogger<MailProcessingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(imapOptions.Value.PollIntervalSeconds));
        do
        {
            try
            {
                await RunOnePollCycleAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Mail poll cycle failed; will retry next interval.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnePollCycleAsync(CancellationToken cancellationToken)
    {
        var processedIds = await processedMessageStore.GetProcessedMessageIdsAsync(cancellationToken);

        await using var mailbox = mailboxClientFactory.Create();
        await mailbox.ConnectAsync(cancellationToken);

        var newMessages = await mailbox.FetchNewMessagesAsync(processedIds, cancellationToken);
        if (newMessages.Count == 0)
        {
            return;
        }

        logger.LogInformation("Found {Count} new message(s) to process.", newMessages.Count);

        foreach (var message in newMessages)
        {
            try
            {
                await ProcessMessageAsync(message, mailbox, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed processing message {MessageId} ({Subject}); leaving unprocessed for retry next cycle.",
                    message.MessageId, message.Subject);
            }
            finally
            {
                foreach (var attachment in message.Attachments)
                {
                    TryDeleteTempFile(attachment.TempFilePath);
                }
            }
        }
    }

    private async Task ProcessMessageAsync(MailMessageContent message, IMailboxClient mailbox, CancellationToken cancellationToken)
    {
        var snapshot = folderIndex.Current;
        var decision = await classificationService.ClassifyAsync(message, snapshot, cancellationToken);

        if (decision.IsNewFolder)
        {
            await archiveWriter.CreateFolderAsync(decision.TargetFolderRelativePath, decision.NewFolderInformationMd!, cancellationToken);
        }

        foreach (var attachment in message.Attachments)
        {
            var fileName = CollisionSafeNamer.BuildAttachmentFileName(message.Date, message.Subject, attachment.FileName);
            await archiveWriter.FileAttachmentAsync(decision.TargetFolderRelativePath, fileName, attachment.TempFilePath, cancellationToken);
        }

        if (decision.ArchiveBodyAsDocument)
        {
            var bodyFileName = CollisionSafeNamer.BuildBodyDocumentFileName(message.Date, message.Subject);
            var bodyContent = $"""
                # {message.Subject}

                From: {message.From}
                Date: {message.Date:O}

                {message.BodyText}
                """;
            await archiveWriter.FileBodyDocumentAsync(decision.TargetFolderRelativePath, bodyFileName, bodyContent, cancellationToken);
        }

        // Only after filing succeeds is the message marked processed - this is the "never lose an email" guarantee.
        await processedMessageStore.MarkProcessedAsync(message.MessageId, cancellationToken);

        try
        {
            await mailbox.MoveToProcessedAsync(message.MessageId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Filed message {MessageId} but failed to move it to the IMAP Processed folder (best-effort only, DB is authoritative).",
                message.MessageId);
        }

        if (decision.IsNewFolder)
        {
            try
            {
                await notificationService.SendNewFolderCreatedNotificationAsync(decision.TargetFolderRelativePath, decision.Reasoning, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send new-folder notification for {Folder} (best-effort only).", decision.TargetFolderRelativePath);
            }
        }
    }

    private void TryDeleteTempFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Failed to delete temp attachment file {Path}.", path);
        }
    }
}
