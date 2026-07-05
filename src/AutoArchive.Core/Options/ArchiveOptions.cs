using System.ComponentModel.DataAnnotations;

namespace AutoArchive.Core.Options;

public sealed class ArchiveOptions
{
    public const string SectionName = "Archive";

    [Required]
    public string RootPath { get; set; } = string.Empty;

    public string InformationFileName { get; set; } = "information.md";

    [Range(1, int.MaxValue)]
    public int FolderIndexRescanIntervalSeconds { get; set; } = 300;
}
