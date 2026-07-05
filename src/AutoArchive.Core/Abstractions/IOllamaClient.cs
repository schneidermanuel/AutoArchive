namespace AutoArchive.Core.Abstractions;

/// <summary>Thin client for a single Ollama chat completion request expected to return JSON.</summary>
public interface IOllamaClient
{
    /// <summary>Sends the prompt and returns the raw text content of the model's reply (expected to be JSON, possibly fenced/imperfect).</summary>
    Task<string> ChatJsonAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken);
}
