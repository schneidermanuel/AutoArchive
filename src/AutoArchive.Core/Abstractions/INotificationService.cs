namespace AutoArchive.Core.Abstractions;

/// <summary>Sends outbound notifications. Never sends to the polled mailbox itself (loop-prevention is enforced by config validation).</summary>
public interface INotificationService
{
    Task SendMessageFiledNotificationAsync(
        string messageSubject,
        string folderRelativePath,
        bool isNewFolder,
        string reasoning,
        CancellationToken cancellationToken);
}
