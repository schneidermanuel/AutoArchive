using AutoArchive.Core.Abstractions;
using AutoArchive.Core.Options;
using Microsoft.Extensions.Options;

namespace AutoArchive.Infrastructure.Archive;

/// <summary>Writes to the archive tree using a write-to-temp-then-rename pattern so a crash mid-write never
/// leaves a partially-written file visible at its final name.</summary>
public sealed class FilesystemArchiveWriter(IOptions<ArchiveOptions> options) : IArchiveWriter
{
    private readonly ArchiveOptions _options = options.Value;

    public Task CreateFolderAsync(string folderRelativePath, string informationMdContent, CancellationToken cancellationToken)
    {
        var folderPath = Path.Combine(_options.RootPath, folderRelativePath);
        Directory.CreateDirectory(folderPath);

        var informationPath = Path.Combine(folderPath, _options.InformationFileName);
        if (!File.Exists(informationPath))
        {
            AtomicWrite(informationPath, informationMdContent);
        }

        return Task.CompletedTask;
    }

    public async Task FileAttachmentAsync(string folderRelativePath, string safeFileName, string sourceTempFilePath, CancellationToken cancellationToken)
    {
        var folderPath = Path.Combine(_options.RootPath, folderRelativePath);
        Directory.CreateDirectory(folderPath);

        var destinationPath = Path.Combine(folderPath, safeFileName);
        var stagingPath = StagingPathFor(folderPath, safeFileName);

        await using (var source = File.OpenRead(sourceTempFilePath))
        await using (var destination = File.Create(stagingPath))
        {
            await source.CopyToAsync(destination, cancellationToken);
        }

        File.Move(stagingPath, destinationPath, overwrite: false);
    }

    public async Task FileBodyDocumentAsync(string folderRelativePath, string safeFileName, string content, CancellationToken cancellationToken)
    {
        var folderPath = Path.Combine(_options.RootPath, folderRelativePath);
        Directory.CreateDirectory(folderPath);

        var destinationPath = Path.Combine(folderPath, safeFileName);
        var stagingPath = StagingPathFor(folderPath, safeFileName);

        await File.WriteAllTextAsync(stagingPath, content, cancellationToken);
        File.Move(stagingPath, destinationPath, overwrite: false);
    }

    private static void AtomicWrite(string destinationPath, string content)
    {
        var directory = Path.GetDirectoryName(destinationPath)!;
        var stagingPath = StagingPathFor(directory, Path.GetFileName(destinationPath));
        File.WriteAllText(stagingPath, content);
        File.Move(stagingPath, destinationPath, overwrite: false);
    }

    private static string StagingPathFor(string folderPath, string finalFileName) =>
        Path.Combine(folderPath, $".{finalFileName}.tmp-{Guid.NewGuid():N}");
}
