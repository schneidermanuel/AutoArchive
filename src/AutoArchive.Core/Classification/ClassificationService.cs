using AutoArchive.Core.Abstractions;
using AutoArchive.Core.Models;
using AutoArchive.Core.Naming;
using AutoArchive.Core.Options;
using Microsoft.Extensions.Options;

namespace AutoArchive.Core.Classification;

public sealed class ClassificationService(
    IOllamaClient ollamaClient,
    IOptions<OllamaOptions> ollamaOptions,
    TimeProvider timeProvider)
{
    private readonly OllamaOptions _options = ollamaOptions.Value;

    public async Task<FilingDecision> ClassifyAsync(
        MailMessageContent message,
        FolderIndexSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var userPrompt = ClassificationPromptBuilder.BuildUserPrompt(snapshot, message, _options);
        var rawResponse = await ollamaClient.ChatJsonAsync(
            ClassificationPromptBuilder.SystemPrompt, userPrompt, ClassificationResponseParser.ResponseJsonSchema, cancellationToken);
        var result = ClassificationResponseParser.Parse(rawResponse);

        if (result.MatchedFolderRelativePath is not null &&
            result.Confidence >= _options.ConfidenceThreshold &&
            snapshot.TryResolve(result.MatchedFolderRelativePath, out var resolvedFolderPath))
        {
            return new FilingDecision(
                resolvedFolderPath,
                IsNewFolder: false,
                NewFolderInformationMd: null,
                result.ArchiveBodyAsDocument,
                result.Confidence,
                result.Reasoning);
        }

        var fallbackName = $"Unsorted-{timeProvider.GetUtcNow():yyyyMMdd-HHmmss}";
        var sanitizedName = FilenameSanitizer.SanitizeRelativePath(result.SuggestedNewFolderName, fallbackName);
        var finalName = CollisionSafeNamer.ResolveCollision(sanitizedName, snapshot.Contains);

        var informationMd = string.IsNullOrWhiteSpace(result.SuggestedNewFolderInformationMd)
            ? $"# {finalName.Split('/')[^1]}\n\nAuto-created by AutoArchive. No description was proposed for this folder yet."
            : result.SuggestedNewFolderInformationMd;

        return new FilingDecision(
            finalName,
            IsNewFolder: true,
            informationMd,
            result.ArchiveBodyAsDocument,
            result.Confidence,
            result.Reasoning);
    }
}
