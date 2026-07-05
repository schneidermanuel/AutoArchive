using AutoArchive.Core.Options;
using AutoArchive.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace AutoArchive.Infrastructure.Tests.Storage;

public class SqliteProcessedMessageStoreTests : IDisposable
{
    private readonly string _tempDir = Directory.CreateTempSubdirectory("autoarchive-sqlite-tests-").FullName;

    [Fact]
    public async Task GetProcessedMessageIdsAsync_BeforeAnyMarked_ReturnsEmptySet()
    {
        var store = CreateStore();
        await store.InitializeAsync(CancellationToken.None);

        var ids = await store.GetProcessedMessageIdsAsync(CancellationToken.None);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task MarkProcessedAsync_ThenGet_RoundTripsTheMessageId()
    {
        var store = CreateStore();
        await store.InitializeAsync(CancellationToken.None);

        await store.MarkProcessedAsync("<abc123@example.com>", CancellationToken.None);
        var ids = await store.GetProcessedMessageIdsAsync(CancellationToken.None);

        Assert.Contains("<abc123@example.com>", ids);
    }

    [Fact]
    public async Task MarkProcessedAsync_CalledTwiceForSameId_DoesNotThrow()
    {
        var store = CreateStore();
        await store.InitializeAsync(CancellationToken.None);

        await store.MarkProcessedAsync("<dup@example.com>", CancellationToken.None);
        await store.MarkProcessedAsync("<dup@example.com>", CancellationToken.None);
        var ids = await store.GetProcessedMessageIdsAsync(CancellationToken.None);

        Assert.Single(ids);
    }

    [Fact]
    public async Task InitializeAsync_CreatesParentDirectoryForDatabaseFile()
    {
        var nestedPath = Path.Combine(_tempDir, "nested", "sub", "autoarchive.db");
        var store = new SqliteProcessedMessageStore(Options.Create(new StorageOptions { DatabasePath = nestedPath }), TimeProvider.System);

        await store.InitializeAsync(CancellationToken.None);

        Assert.True(File.Exists(nestedPath));
    }

    private SqliteProcessedMessageStore CreateStore() =>
        new(Options.Create(new StorageOptions { DatabasePath = Path.Combine(_tempDir, "autoarchive.db") }), TimeProvider.System);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);
}
