namespace AutoArchive.Core.Abstractions;

/// <summary>Sends outbound notifications. Never sends to the polled mailbox itself (loop-prevention is enforced by config validation).</summary>
public interface INotificationService
{
    Task SendNewFolderCreatedNotificationAsync(string folderRelativePath, string reasoning, CancellationToken cancellationToken);
}
