using AutoArchive.Core.Abstractions;
using AutoArchive.Core.Options;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace AutoArchive.Infrastructure.Storage;

/// <summary>SQLite-backed record of processed Message-IDs. This is the authoritative dedup source - a message is
/// only marked here after it has been fully filed, so a crash/outage before that point simply retries next poll.</summary>
public sealed class SqliteProcessedMessageStore(IOptions<StorageOptions> options, TimeProvider timeProvider) : IProcessedMessageStore
{
    private readonly string _connectionString = $"Data Source={options.Value.DatabasePath}";

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(options.Value.DatabasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS ProcessedMessages (
                MessageId TEXT PRIMARY KEY,
                ProcessedAtUtc TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlySet<string>> GetProcessedMessageIdsAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT MessageId FROM ProcessedMessages;";

        var ids = new HashSet<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(reader.GetString(0));
        }

        return ids;
    }

    public async Task MarkProcessedAsync(string messageId, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO ProcessedMessages (MessageId, ProcessedAtUtc) VALUES ($messageId, $processedAtUtc);";
        command.Parameters.AddWithValue("$messageId", messageId);
        command.Parameters.AddWithValue("$processedAtUtc", timeProvider.GetUtcNow().ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
