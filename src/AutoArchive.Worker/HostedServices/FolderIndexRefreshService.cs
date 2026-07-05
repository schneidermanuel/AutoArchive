using AutoArchive.Core.Abstractions;
using AutoArchive.Core.Options;
using Microsoft.Extensions.Options;

namespace AutoArchive.Worker.HostedServices;

/// <summary>Periodically rescans the archive tree. The very first scan runs synchronously in Program.cs before
/// the host starts serving, so this loop only needs to handle the recurring rescans.</summary>
public sealed class FolderIndexRefreshService(
    IFolderIndex folderIndex,
    IOptions<ArchiveOptions> options,
    ILogger<FolderIndexRefreshService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.FolderIndexRescanIntervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await folderIndex.RefreshAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Folder index rescan failed; will retry next cycle.");
            }
        }
    }
}
