namespace AutoArchive.Core.Abstractions;

/// <summary>Writes filed content to the archive tree. Folder creation and content writes are atomic per-call
/// (temp-file-then-move) so a crash never leaves a half-written file behind.</summary>
public interface IArchiveWriter
{
    Task CreateFolderAsync(string folderRelativePath, string informationMdContent, CancellationToken cancellationToken);

    Task FileAttachmentAsync(string folderRelativePath, string safeFileName, string sourceTempFilePath, CancellationToken cancellationToken);

    Task FileBodyDocumentAsync(string folderRelativePath, string safeFileName, string content, CancellationToken cancellationToken);
}
