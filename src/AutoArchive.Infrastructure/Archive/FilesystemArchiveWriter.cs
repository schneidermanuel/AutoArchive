using AutoArchive.Core.Abstractions;
using AutoArchive.Core.Options;
using Microsoft.Extensions.Options;

namespace AutoArchive.Infrastructure.Archive;

/// <summary>Writes to the archive tree using a write-to-temp-then-rename pattern so a crash mid-write never
/// leaves a partially-written file visible at its final name.</summary>
public sealed class FilesystemArchiveWriter(IOptions<ArchiveOptions> options) : IArchiveWriter
{
    // Explicit group-writable (+ setgid, so nested new folders keep inheriting the shared group) permissions -
    // the container's default umask would otherwise leave new entries group-readable but not group-writable,
    // which blocks the archive's other writer (e.g. Nextcloud) from ever deleting/modifying what this creates.
    private const UnixFileMode DirectoryPermissions =
        UnixFileMode.SetGroup |
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    private const UnixFileMode FilePermissions =
        UnixFileMode.UserRead | UnixFileMode.UserWrite |
        UnixFileMode.GroupRead | UnixFileMode.GroupWrite |
        UnixFileMode.OtherRead;

    private readonly ArchiveOptions _options = options.Value;

    public Task CreateFolderAsync(string folderRelativePath, string informationMdContent, CancellationToken cancellationToken)
    {
        var folderPath = EnsureFolder(folderRelativePath);

        var informationPath = Path.Combine(folderPath, _options.InformationFileName);
        if (!File.Exists(informationPath))
        {
            AtomicWrite(informationPath, informationMdContent);
        }

        return Task.CompletedTask;
    }

    public async Task FileAttachmentAsync(string folderRelativePath, string safeFileName, string sourceTempFilePath, CancellationToken cancellationToken)
    {
        var folderPath = EnsureFolder(folderRelativePath);

        var destinationPath = Path.Combine(folderPath, safeFileName);
        var stagingPath = StagingPathFor(folderPath, safeFileName);

        await using (var source = File.OpenRead(sourceTempFilePath))
        await using (var destination = File.Create(stagingPath))
        {
            await source.CopyToAsync(destination, cancellationToken);
        }

        File.Move(stagingPath, destinationPath, overwrite: false);
        File.SetUnixFileMode(destinationPath, FilePermissions);
    }

    public async Task FileBodyDocumentAsync(string folderRelativePath, string safeFileName, string content, CancellationToken cancellationToken)
    {
        var folderPath = EnsureFolder(folderRelativePath);

        var destinationPath = Path.Combine(folderPath, safeFileName);
        var stagingPath = StagingPathFor(folderPath, safeFileName);

        await File.WriteAllTextAsync(stagingPath, content, cancellationToken);
        File.Move(stagingPath, destinationPath, overwrite: false);
        File.SetUnixFileMode(destinationPath, FilePermissions);
    }

    /// <summary>Creates (if missing) every directory level of the relative path and enforces
    /// <see cref="DirectoryPermissions"/> on each, since Directory.CreateDirectory only creates
    /// intermediate levels without letting us set their mode individually.</summary>
    private string EnsureFolder(string folderRelativePath)
    {
        var currentPath = _options.RootPath;
        foreach (var segment in folderRelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);
            Directory.CreateDirectory(currentPath);
            File.SetUnixFileMode(currentPath, DirectoryPermissions);
        }

        return currentPath;
    }

    private static void AtomicWrite(string destinationPath, string content)
    {
        var directory = Path.GetDirectoryName(destinationPath)!;
        var stagingPath = StagingPathFor(directory, Path.GetFileName(destinationPath));
        File.WriteAllText(stagingPath, content);
        File.Move(stagingPath, destinationPath, overwrite: false);
        File.SetUnixFileMode(destinationPath, FilePermissions);
    }

    private static string StagingPathFor(string folderPath, string finalFileName) =>
        Path.Combine(folderPath, $".{finalFileName}.tmp-{Guid.NewGuid():N}");
}
