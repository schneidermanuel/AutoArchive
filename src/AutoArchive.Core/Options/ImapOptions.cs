using System.ComponentModel.DataAnnotations;

namespace AutoArchive.Core.Options;

public sealed class ImapOptions
{
    public const string SectionName = "Imap";

    [Required]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 993;

    public bool UseSsl { get; set; } = true;

    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    public string ProcessedFolderName { get; set; } = "Processed";

    [Range(1, int.MaxValue)]
    public int PollIntervalSeconds { get; set; } = 60;

    [Range(1, long.MaxValue)]
    public long MaxAttachmentSizeBytes { get; set; } = 500L * 1024 * 1024;
}
