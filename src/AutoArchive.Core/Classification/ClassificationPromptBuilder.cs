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

        Decide which single candidate folder the email/attachments belong in.

        matchedFolderPath MUST be copied character-for-character from the given candidate list's paths (the
        part before the colon on each line) - never invent, guess, or construct a path yourself, even if a
        folder's own description mentions an internal structure (e.g. "one subdirectory per year, one per
        trip"). If a folder's description happens to contain a path-like string (e.g. a note about where it
        used to live), ignore that and use only the path given in the candidate list itself. If the specific
        matching folder does not literally appear in the candidate list, this is NOT a match - treat it the
        same as "no existing folder fits" below, even if a *parent* category exists.

        If none of the folders are a good fit, respond with "NONE" as the matchedFolderPath and instead
        propose a new folder to create, plus a concise information.md description for it (written the same
        way the existing folders' descriptions are written, so future emails can be matched against it).
        This new folder can be nested under an existing candidate folder when that folder's description
        calls for it - use "/" to separate segments in that case. For an entirely new, unrelated category,
        propose a short flat top-level name instead.

        Worked example of the nested case: candidate list contains "Reisen: one subdirectory per year, then
        one per trip", and there is no candidate folder for a Rinerhorn trip in 2026. Even though you know
        exactly where this belongs conceptually, no such folder exists yet, so this is NOT a match:
        {
          "matchedFolderPath": "NONE",
          "confidence": 0.0,
          "reasoning": "No existing folder for this specific trip; proposing a new one nested under Reisen.",
          "archiveBodyAsDocument": false,
          "suggestedNewFolderName": "Reisen/2026/Rinerhorn",
          "suggestedNewFolderInformationMd": "# Rinerhorn 2026\n\nDocuments for the 2026 Rinerhorn trip."
        }
        Do NOT put "Reisen/2026/Rinerhorn" (or any path built the same way) into matchedFolderPath just
        because you're confident about where it belongs - matchedFolderPath is ONLY for paths that already
        exist verbatim in the candidate list above.

        Independently, decide whether the plain email body text itself (as opposed to just attachments)
        contains information worth keeping as its own document - e.g. a forwarded email with no attachment but
        useful content. Do not set this to true for routine/empty/greeting-only bodies (e.g. "see attached").

        Respond with ONLY a single JSON object, no markdown fences, no extra commentary, matching exactly this
        shape:
        {
          "matchedFolderPath": "<one of the given candidate paths, copied exactly, or the literal string \"NONE\">",
          "confidence": <number between 0.0 and 1.0, how confident you are in matchedFolderPath>,
          "reasoning": "<one short sentence explaining the decision>",
          "archiveBodyAsDocument": <true or false>,
          "suggestedNewFolderName": "<short folder name (optionally nested with '/') if matchedFolderPath is NONE, otherwise the literal string \"NONE\">",
          "suggestedNewFolderInformationMd": "<information.md content if matchedFolderPath is NONE, otherwise the literal string \"NONE\">"
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
