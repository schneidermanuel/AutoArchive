namespace AutoArchive.Core.Abstractions;

/// <summary>Thrown when Ollama cannot be reached or returns an unusable response, so the resilience pipeline
/// can retry and, failing that, the caller can leave the message unprocessed for the next poll cycle.</summary>
public sealed class OllamaUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
