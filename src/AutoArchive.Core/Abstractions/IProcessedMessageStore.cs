namespace AutoArchive.Core.Abstractions;

/// <summary>Durable record of which messages have already been fully filed, so a crash/outage never causes a lost email.</summary>
public interface IProcessedMessageStore
{
    Task InitializeAsync(CancellationToken cancellationToken);

    Task<IReadOnlySet<string>> GetProcessedMessageIdsAsync(CancellationToken cancellationToken);

    Task MarkProcessedAsync(string messageId, CancellationToken cancellationToken);
}
