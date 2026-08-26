using System.Text.Json;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Usage;

/// <summary>
/// Reads which account Claude Code is signed in as.
///
/// Only the identifying fields are taken. The configuration file holds far more, and the
/// credentials file beside it holds live tokens for the account and for every connected MCP server;
/// neither the tokens nor the account and organisation identifiers are read, and nothing read here
/// is logged.
///
/// The answer is the same for every session on the machine: Claude Code signs in once per user
/// profile, and a session inherits that. This says which account the board's sessions run as, not
/// which account any one of them runs as.
/// </summary>
public sealed class ClaudeAccountReader
{
    private readonly string _configPath;
    private readonly string _credentialsPath;

    /// <summary>Creates a reader over the configuration and credentials files.</summary>
    public ClaudeAccountReader(string? configPath = null, string? credentialsPath = null)
    {
        _configPath = configPath ?? DefaultConfigPath;
        _credentialsPath = credentialsPath ?? CredentialFileSource.DefaultPath;
    }

    /// <summary>Where Claude Code keeps its configuration.</summary>
    public static string DefaultConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude.json");

    /// <summary>The signed-in account, or an empty one when it cannot be established.</summary>
    public ClaudeAccount Read()
    {
        var account = FromConfig();

        return account with { SubscriptionType = FromCredentials() };
    }

    private ClaudeAccount FromConfig()
    {
        if (Parse(_configPath) is not { } document)
        {
            return new ClaudeAccount(null, null, null);
        }

        using (document)
        {
            return document.RootElement.TryGetProperty("oauthAccount", out var oauth)
                ? new ClaudeAccount(Text(oauth, "emailAddress"), Text(oauth, "organizationName"), null)
                : new ClaudeAccount(null, null, null);
        }
    }

    private string? FromCredentials()
    {
        if (Parse(_credentialsPath) is not { } document)
        {
            return null;
        }

        using (document)
        {
            return document.RootElement.TryGetProperty("claudeAiOauth", out var oauth)
                ? Text(oauth, "subscriptionType")
                : null;
        }
    }

    private static JsonDocument? Parse(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            return JsonDocument.Parse(stream);
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            TheseusLog.Debug("Could not establish the signed-in account from {Path}: {Reason}",
                path, exception.Message);

            return null;
        }
    }

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
