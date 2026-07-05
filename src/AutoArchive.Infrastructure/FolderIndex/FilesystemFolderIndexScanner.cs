using AutoArchive.Core.Abstractions;
using AutoArchive.Core.Models;
using AutoArchive.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoArchive.Infrastructure.FolderIndex;

/// <summary>Recursively rescans the archive root on demand. A directory is a filing target only if it directly
/// contains an information.md; scanning continues into subdirectories regardless, so nested target folders work too.</summary>
public sealed class FilesystemFolderIndexScanner(
    IOptions<ArchiveOptions> options,
    ILogger<FilesystemFolderIndexScanner> logger) : IFolderIndex
{
    private FolderIndexSnapshot _current = FolderIndexSnapshot.Empty;

    public FolderIndexSnapshot Current => Volatile.Read(ref _current);

    public Task RefreshAsync(CancellationToken cancellationToken)
    {
        var archiveOptions = options.Value;
        var folders = new List<FolderInfo>();

        if (Directory.Exists(archiveOptions.RootPath))
        {
            ScanDirectory(archiveOptions.RootPath, archiveOptions.RootPath, archiveOptions.InformationFileName, folders);
        }
        else
        {
            logger.LogWarning("Archive root {RootPath} does not exist; folder index will be empty this cycle", archiveOptions.RootPath);
        }

        Interlocked.Exchange(ref _current, new FolderIndexSnapshot(folders));
        return Task.CompletedTask;
    }

    private void ScanDirectory(string rootPath, string currentPath, string informationFileName, List<FolderInfo> folders)
    {
        string[] subDirectories;
        try
        {
            subDirectories = Directory.GetDirectories(currentPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Failed listing directory {Path}; skipping its subtree for this scan cycle", currentPath);
            return;
        }

        foreach (var directory in subDirectories)
        {
            var informationPath = Path.Combine(directory, informationFileName);
            if (File.Exists(informationPath))
            {
                try
                {
                    var content = File.ReadAllText(informationPath);
                    var relativePath = Path.GetRelativePath(rootPath, directory).Replace(Path.DirectorySeparatorChar, '/');
                    folders.Add(new FolderInfo(relativePath, directory, content));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    logger.LogError(ex, "Failed reading {InformationPath}; skipping this folder for this scan cycle", informationPath);
                }
            }

            ScanDirectory(rootPath, directory, informationFileName, folders);
        }
    }
}
