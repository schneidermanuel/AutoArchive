using AutoArchive.Core.Models;

namespace AutoArchive.Core.Abstractions;

/// <summary>One poll-cycle session against the mailbox: connect, fetch, move, disconnect.</summary>
public interface IMailboxClient : IAsyncDisposable
{
    Task ConnectAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<MailMessageContent>> FetchNewMessagesAsync(
        IReadOnlySet<string> processedMessageIds,
        CancellationToken cancellationToken);

    Task MoveToProcessedAsync(string messageId, CancellationToken cancellationToken);
}

/// <summary>Creates a fresh <see cref="IMailboxClient"/> session per poll cycle (the underlying IMAP client is not reusable/thread-safe).</summary>
public interface IMailboxClientFactory
{
    IMailboxClient Create();
}
