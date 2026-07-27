using System.Text.Json;
using System.Text.Json.Serialization;
using AutoArchive.Core.Models;

namespace AutoArchive.Core.Classification;

public static class ClassificationResponseParser
{
    /// <summary>JSON Schema for Ollama's structured-output "format" field, constraining replies to exactly the
    /// shape the Dto below expects - keep the property names in sync with Dto's JsonPropertyName attributes.</summary>
    public const string ResponseJsonSchema = """
        {
          "type": "object",
          "properties": {
            "matchedFolderPath": { "type": "string" },
            "confidence": { "type": "number" },
            "reasoning": { "type": "string" },
            "archiveBodyAsDocument": { "type": "boolean" },
            "suggestedNewFolderName": { "type": "string" },
            "suggestedNewFolderInformationMd": { "type": "string" }
          },
          "required": [
            "matchedFolderPath",
            "confidence",
            "reasoning",
            "archiveBodyAsDocument",
            "suggestedNewFolderName",
            "suggestedNewFolderInformationMd"
          ]
        }
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Defensively parses Ollama's reply: strips markdown code fences, tolerates missing/malformed
    /// fields, clamps confidence, and normalizes "NONE"/empty matches to null.</summary>
    public static ClassificationResult Parse(string rawResponse)
    {
        var jsonText = StripCodeFences(rawResponse);

        Dto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<Dto>(jsonText, JsonOptions);
        }
        catch (JsonException)
        {
            return ClassificationResult.NoMatch("Ollama response was not valid JSON.");
        }

        if (dto is null)
        {
            return ClassificationResult.NoMatch("Ollama response was empty.");
        }

        var matchedPath = dto.MatchedFolderPath;
        if (string.IsNullOrWhiteSpace(matchedPath) || matchedPath.Trim().Equals("NONE", StringComparison.OrdinalIgnoreCase))
        {
            matchedPath = null;
        }

        var confidence = double.IsFinite(dto.Confidence) ? Math.Clamp(dto.Confidence, 0.0, 1.0) : 0.0;

        return new ClassificationResult(
            matchedPath,
            confidence,
            dto.Reasoning ?? string.Empty,
            dto.ArchiveBodyAsDocument,
            NullIfBlank(dto.SuggestedNewFolderName),
            NullIfBlank(dto.SuggestedNewFolderInformationMd));
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string StripCodeFences(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline < 0)
        {
            return trimmed;
        }

        var withoutOpeningFence = trimmed[(firstNewline + 1)..];
        var closingFenceIndex = withoutOpeningFence.LastIndexOf("```", StringComparison.Ordinal);
        return closingFenceIndex >= 0 ? withoutOpeningFence[..closingFenceIndex].Trim() : withoutOpeningFence.Trim();
    }

    private sealed class Dto
    {
        [JsonPropertyName("matchedFolderPath")]
        public string? MatchedFolderPath { get; set; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonPropertyName("reasoning")]
        public string? Reasoning { get; set; }

        [JsonPropertyName("archiveBodyAsDocument")]
        public bool ArchiveBodyAsDocument { get; set; }

        [JsonPropertyName("suggestedNewFolderName")]
        public string? SuggestedNewFolderName { get; set; }

        [JsonPropertyName("suggestedNewFolderInformationMd")]
        public string? SuggestedNewFolderInformationMd { get; set; }
    }
}
