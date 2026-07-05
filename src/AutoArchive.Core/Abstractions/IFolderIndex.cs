using AutoArchive.Core.Models;

namespace AutoArchive.Core.Abstractions;

/// <summary>Holds the current, periodically-refreshed view of the archive folder tree.</summary>
public interface IFolderIndex
{
    FolderIndexSnapshot Current { get; }

    Task RefreshAsync(CancellationToken cancellationToken);
}
