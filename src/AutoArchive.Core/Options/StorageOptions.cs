using System.ComponentModel.DataAnnotations;

namespace AutoArchive.Core.Options;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    [Required]
    public string DatabasePath { get; set; } = "/data/autoarchive.db";
}
