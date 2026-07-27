using System.ComponentModel.DataAnnotations;

namespace AutoArchive.Core.Options;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    [Required]
    public string BaseUrl { get; set; } = "http://ollama:11434";

    [Required]
    public string Model { get; set; } = "llama3.1:8b";

    [Range(0.0, 1.0)]
    public double ConfidenceThreshold { get; set; } = 0.6;

    [Range(1, int.MaxValue)]
    public int MaxInformationMdCharsPerFolder { get; set; } = 2000;

    [Range(1, int.MaxValue)]
    public int MaxBodyChars { get; set; } = 4000;

    [Range(1, int.MaxValue)]
    public int MaxAttachmentExcerptChars { get; set; } = 2000;

    [Range(1, int.MaxValue)]
    public int RequestTimeoutSeconds { get; set; } = 120;

    /// <summary>Ollama's context window size in tokens. The prompt (all candidate folders' information.md content
    /// plus the email body/attachment excerpts) can easily exceed Ollama's 4096-token default, which makes it
    /// reject the request outright rather than truncate - so this needs to be set explicitly and generously.</summary>
    [Range(1, int.MaxValue)]
    public int ContextSizeTokens { get; set; } = 8192;
}
