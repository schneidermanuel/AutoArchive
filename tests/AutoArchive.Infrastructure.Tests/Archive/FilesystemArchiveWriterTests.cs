using AutoArchive.Core.Options;
using AutoArchive.Infrastructure.Archive;
using Microsoft.Extensions.Options;

namespace AutoArchive.Infrastructure.Tests.Archive;

public class FilesystemArchiveWriterTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("autoarchive-writer-tests-").FullName;

    [Fact]
    public async Task CreateFolderAsync_CreatesDirectoryAndInformationMd()
    {
        var writer = CreateWriter();

        await writer.CreateFolderAsync("Finance/Invoices", "Invoices go here.", CancellationToken.None);

        var infoPath = Path.Combine(_root, "Finance", "Invoices", "information.md");
        Assert.True(File.Exists(infoPath));
        Assert.Equal("Invoices go here.", await File.ReadAllTextAsync(infoPath));
    }

    [Fact]
    public async Task CreateFolderAsync_DoesNotOverwriteExistingInformationMd()
    {
        var writer = CreateWriter();
        await writer.CreateFolderAsync("Finance", "Original content.", CancellationToken.None);

        await writer.CreateFolderAsync("Finance", "Different content.", CancellationToken.None);

        var infoPath = Path.Combine(_root, "Finance", "information.md");
        Assert.Equal("Original content.", await File.ReadAllTextAsync(infoPath));
    }

    [Fact]
    public async Task FileAttachmentAsync_CopiesSourceFileContentToDestination()
    {
        var writer = CreateWriter();
        var sourcePath = Path.Combine(Path.GetTempPath(), $"autoarchive-source-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(sourcePath, "attachment bytes");

        try
        {
            await writer.FileAttachmentAsync("Finance", "20260705-120000_invoice_scan.txt", sourcePath, CancellationToken.None);

            var destinationPath = Path.Combine(_root, "Finance", "20260705-120000_invoice_scan.txt");
            Assert.Equal("attachment bytes", await File.ReadAllTextAsync(destinationPath));
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    [Fact]
    public async Task FileAttachmentAsync_LeavesNoTempStagingFileBehindOnSuccess()
    {
        var writer = CreateWriter();
        var sourcePath = Path.Combine(Path.GetTempPath(), $"autoarchive-source-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(sourcePath, "data");

        try
        {
            await writer.FileAttachmentAsync("Finance", "file.txt", sourcePath, CancellationToken.None);

            var entries = Directory.GetFileSystemEntries(Path.Combine(_root, "Finance"));
            Assert.Single(entries);
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    [Fact]
    public async Task FileBodyDocumentAsync_WritesContentDirectly()
    {
        var writer = CreateWriter();

        await writer.FileBodyDocumentAsync("Finance", "body.md", "# Forwarded note\n\nHello.", CancellationToken.None);

        var destinationPath = Path.Combine(_root, "Finance", "body.md");
        Assert.Equal("# Forwarded note\n\nHello.", await File.ReadAllTextAsync(destinationPath));
    }

    private FilesystemArchiveWriter CreateWriter() =>
        new(Options.Create(new ArchiveOptions { RootPath = _root, InformationFileName = "information.md" }));

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
