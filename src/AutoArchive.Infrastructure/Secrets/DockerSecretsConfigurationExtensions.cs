using Microsoft.Extensions.Configuration;

namespace AutoArchive.Infrastructure.Secrets;

/// <summary>Supports the Docker secrets "*_FILE" convention: an env var like IMAP__PASSWORD_FILE pointing at
/// /run/secrets/imap_password is read and injected as config key Imap:Password, alongside plain env vars.</summary>
public static class DockerSecretsConfigurationExtensions
{
    private const string FileSuffix = "_FILE";

    public static IConfigurationBuilder AddDockerSecrets(this IConfigurationBuilder builder)
    {
        var secretValues = new Dictionary<string, string?>();

        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is not string key || !key.EndsWith(FileSuffix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (entry.Value is not string filePath || string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                continue;
            }

            var configKey = key[..^FileSuffix.Length].Replace("__", ConfigurationPath.KeyDelimiter, StringComparison.Ordinal);
            secretValues[configKey] = File.ReadAllText(filePath).Trim();
        }

        return builder.AddInMemoryCollection(secretValues);
    }
}
