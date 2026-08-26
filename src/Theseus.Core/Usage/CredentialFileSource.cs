using System.Text.Json;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Usage;

/// <summary>
/// Reads the account's OAuth credentials from the file Claude Code keeps them in.
///
/// Only the account's own block is read; the file also holds tokens for every connected MCP server,
/// and this application has no business with those. Nothing read here is logged, cached to disk, or
/// held beyond the request that needs it.
/// </summary>
public sealed class CredentialFileSource : ICredentialSource
{
    private readonly string _path;

    /// <summary>Creates a source over the credentials file.</summary>
    public CredentialFileSource(string? path = null) => _path = path ?? DefaultPath;

    /// <summary>Where Claude Code keeps the credentials.</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", ".credentials.json");

    /// <inheritdoc />
    public ClaudeCredentials? Read()
    {
        string text;

        try
        {
            using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            text = reader.ReadToEnd();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning("Could not read the credentials file, so usage cannot be shown: {Reason}", exception.Message);

            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(text);

            if (!document.RootElement.TryGetProperty("claudeAiOauth", out var account)
                || account.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!account.TryGetProperty("accessToken", out var token)
                || token.ValueKind != JsonValueKind.String
                || token.GetString() is not { Length: > 0 } accessToken)
            {
                return null;
            }

            var expires = account.TryGetProperty("expiresAt", out var expiresAt)
                          && expiresAt.ValueKind == JsonValueKind.Number
                ? DateTimeOffset.FromUnixTimeMilliseconds(expiresAt.GetInt64())
                : DateTimeOffset.MaxValue;

            var subscription = account.TryGetProperty("subscriptionType", out var plan)
                               && plan.ValueKind == JsonValueKind.String
                ? plan.GetString()
                : null;

            return new ClaudeCredentials(accessToken, expires, subscription);
        }
        catch (JsonException exception)
        {
            TheseusLog.Warning("The credentials file did not parse: {Reason}", exception.Message);

            return null;
        }
    }
}
