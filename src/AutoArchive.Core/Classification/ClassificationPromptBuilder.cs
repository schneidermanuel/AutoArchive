using System.Text;
using AutoArchive.Core.Models;
using AutoArchive.Core.Options;

namespace AutoArchive.Core.Classification;

public static class ClassificationPromptBuilder
{
    public const string SystemPrompt = """
        You are a filing assistant for a personal document archive. You will be given a list of candidate
        folders (each with a path and a description of what belongs in it, taken from that folder's
        information.md file) and the content of one incoming email, possibly with attachments.

        Decide which single candidate folder the email/attachments belong in. Only choose a folder from the
        given list, using its exact path. If none of the folders are a good fit, respond with "NONE" as the
        matchedFolderPath and instead propose a short, filesystem-safe name for a brand new folder that should
        be created, plus a concise information.md description for it (written the same way the existing
        folders' descriptions are written, so future emails can be matched against it).

        Independently, decide whether the plain email body text itself (as opposed to just attachments)
        contains information worth keeping as its own document - e.g. a forwarded email with no attachment but
        useful content. Do not set this to true for routine/empty/greeting-only bodies (e.g. "see attached").

        Respond with ONLY a single JSON object, no markdown fences, no extra commentary, matching exactly this
        shape:
        {
          "matchedFolderPath": "<one of the given candidate paths, or the literal string \"NONE\">",
          "confidence": <number between 0.0 and 1.0, how confident you are in matchedFolderPath>,
          "reasoning": "<one short sentence explaining the decision>",
          "archiveBodyAsDocument": <true or false>,
          "suggestedNewFolderName": "<short folder name if matchedFolderPath is NONE, otherwise null>",
          "suggestedNewFolderInformationMd": "<information.md content if matchedFolderPath is NONE, otherwise null>"
        }
        """;

    public static string BuildUserPrompt(FolderIndexSnapshot snapshot, MailMessageContent message, OllamaOptions options)
    {
        var sb = new StringBuilder();

        sb.AppendLine("## Candidate folders");
        if (snapshot.Folders.Count == 0)
        {
            sb.AppendLine("(none exist yet - you must propose a new folder)");
        }
        else
        {
            foreach (var folder in snapshot.Folders)
            {
                var info = Truncate(folder.InformationMdContent, options.MaxInformationMdCharsPerFolder);
                sb.AppendLine($"- {folder.RelativePath}: {info}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Email");
        sb.AppendLine($"From: {message.From}");
        sb.AppendLine($"Subject: {message.Subject}");
        sb.AppendLine($"Date: {message.Date:O}");
        sb.AppendLine("Body:");
        sb.AppendLine(Truncate(message.BodyText, options.MaxBodyChars));

        sb.AppendLine();
        sb.AppendLine("## Attachments");
        if (message.Attachments.Count == 0)
        {
            sb.AppendLine("(none)");
        }
        else
        {
            foreach (var attachment in message.Attachments)
            {
                sb.AppendLine($"- {attachment.FileName} ({attachment.ContentType}, {attachment.SizeBytes} bytes)");
                if (!string.IsNullOrWhiteSpace(attachment.ExtractedText))
                {
                    sb.AppendLine($"  excerpt: {Truncate(attachment.ExtractedText, options.MaxAttachmentExcerptChars)}");
                }
            }
        }

        return sb.ToString();
    }

    private static string Truncate(string text, int maxChars) =>
        text.Length <= maxChars ? text : string.Concat(text.AsSpan(0, maxChars), "...(truncated)");
}
