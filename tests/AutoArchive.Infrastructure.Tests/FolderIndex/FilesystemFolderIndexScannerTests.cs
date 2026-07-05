using AutoArchive.Core.Options;
using AutoArchive.Infrastructure.FolderIndex;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AutoArchive.Infrastructure.Tests.FolderIndex;

public class FilesystemFolderIndexScannerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("autoarchive-tests-").FullName;

    [Fact]
    public async Task RefreshAsync_FindsFoldersThatContainInformationMd()
    {
        var invoicesDir = Directory.CreateDirectory(Path.Combine(_root, "Finance", "Invoices"));
        await File.WriteAllTextAsync(Path.Combine(invoicesDir.FullName, "information.md"), "Invoices go here.");
        Directory.CreateDirectory(Path.Combine(_root, "Empty")); // no information.md - not a target

        var scanner = CreateScanner();
        await scanner.RefreshAsync(CancellationToken.None);

        var folder = Assert.Single(scanner.Current.Folders);
        Assert.Equal("Finance/Invoices", folder.RelativePath);
        Assert.Equal("Invoices go here.", folder.InformationMdContent);
    }

    [Fact]
    public async Task RefreshAsync_SkipsUnreadableInformationMdWithoutAbortingWholeScan()
    {
        var goodDir = Directory.CreateDirectory(Path.Combine(_root, "Good"));
        await File.WriteAllTextAsync(Path.Combine(goodDir.FullName, "information.md"), "Good folder.");

        var badDir = Directory.CreateDirectory(Path.Combine(_root, "Bad"));
        var badInfoPath = Path.Combine(badDir.FullName, "information.md");
        Directory.CreateDirectory(badInfoPath); // a directory where a file is expected -> unreadable as text

        var scanner = CreateScanner();
        await scanner.RefreshAsync(CancellationToken.None);

        var folder = Assert.Single(scanner.Current.Folders);
        Assert.Equal("Good", folder.RelativePath);
    }

    [Fact]
    public async Task RefreshAsync_WhenRootDoesNotExist_ProducesEmptySnapshotInsteadOfThrowing()
    {
        var scanner = new FilesystemFolderIndexScanner(
            Options.Create(new ArchiveOptions { RootPath = Path.Combine(_root, "does-not-exist"), InformationFileName = "information.md" }),
            NullLogger<FilesystemFolderIndexScanner>.Instance);

        await scanner.RefreshAsync(CancellationToken.None);

        Assert.Empty(scanner.Current.Folders);
    }

    [Fact]
    public async Task RefreshAsync_FindsNestedTargetFoldersUnderNonTargetParents()
    {
        var nested = Directory.CreateDirectory(Path.Combine(_root, "Parent", "Child"));
        await File.WriteAllTextAsync(Path.Combine(nested.FullName, "information.md"), "Nested folder.");

        var scanner = CreateScanner();
        await scanner.RefreshAsync(CancellationToken.None);

        Assert.True(scanner.Current.Contains("Parent/Child"));
    }

    private FilesystemFolderIndexScanner CreateScanner() =>
        new(Options.Create(new ArchiveOptions { RootPath = _root, InformationFileName = "information.md" }),
            NullLogger<FilesystemFolderIndexScanner>.Instance);

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
