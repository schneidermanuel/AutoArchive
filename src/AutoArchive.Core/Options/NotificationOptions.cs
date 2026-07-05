using System.ComponentModel.DataAnnotations;

namespace AutoArchive.Core.Options;

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    [Required, EmailAddress]
    public string RecipientEmail { get; set; } = string.Empty;
}
