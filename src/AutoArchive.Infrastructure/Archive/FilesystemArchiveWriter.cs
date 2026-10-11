using AutoArchive.Core.Abstractions;
using AutoArchive.Core.Naming;
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

        var stagingPath = StagingPathFor(folderPath, safeFileName);

        await using (var source = File.OpenRead(sourceTempFilePath))
        await using (var destination = File.Create(stagingPath))
        {
            await source.CopyToAsync(destination, cancellationToken);
        }

        PlaceStagedFile(folderPath, safeFileName, stagingPath);
    }

    public async Task FileBodyDocumentAsync(string folderRelativePath, string safeFileName, string content, CancellationToken cancellationToken)
    {
        var folderPath = EnsureFolder(folderRelativePath);

        var stagingPath = StagingPathFor(folderPath, safeFileName);

        await File.WriteAllTextAsync(stagingPath, content, cancellationToken);
        PlaceStagedFile(folderPath, safeFileName, stagingPath);
    }

    /// <summary>Moves the staged file to its final name. If that name is taken by a file with identical content
    /// (e.g. a retry after an earlier cycle failed later in the pipeline) the staged copy is simply dropped; if the
    /// content differs (e.g. two same-named attachments in one mail) a numeric suffix is appended instead of failing.</summary>
    private static void PlaceStagedFile(string folderPath, string safeFileName, string stagingPath)
    {
        try
        {
            while (true)
            {
                var candidateName = CollisionSafeNamer.ResolveCollision(
                    safeFileName, name => File.Exists(Path.Combine(folderPath, name)) && !HasSameContent(Path.Combine(folderPath, name), stagingPath));
                var destinationPath = Path.Combine(folderPath, candidateName);

                if (File.Exists(destinationPath))
                {
                    return; // identical content already archived
                }

                try
                {
                    File.Move(stagingPath, destinationPath, overwrite: false);
                }
                catch (IOException) when (File.Exists(destinationPath))
                {
                    continue; // lost a race with another writer; re-resolve
                }

                File.SetUnixFileMode(destinationPath, FilePermissions);
                return;
            }
        }
        finally
        {
            if (File.Exists(stagingPath))
            {
                File.Delete(stagingPath);
            }
        }
    }

    private static bool HasSameContent(string pathA, string pathB)
    {
        var infoA = new FileInfo(pathA);
        var infoB = new FileInfo(pathB);
        if (infoA.Length != infoB.Length)
        {
            return false;
        }

        using var a = infoA.OpenRead();
        using var b = infoB.OpenRead();
        var bufferA = new byte[81920];
        var bufferB = new byte[81920];
        int read;
        while ((read = a.Read(bufferA, 0, bufferA.Length)) > 0)
        {
            b.ReadExactly(bufferB, 0, read);
            if (!bufferA.AsSpan(0, read).SequenceEqual(bufferB.AsSpan(0, read)))
            {
                return false;
            }
        }

        return true;
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
