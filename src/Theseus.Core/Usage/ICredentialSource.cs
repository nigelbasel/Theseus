namespace Theseus.Core.Usage;

/// <summary>Supplies the account's OAuth credentials at the moment they are needed.</summary>
public interface ICredentialSource
{
    /// <summary>Reads the credentials, or null when none are available.</summary>
    ClaudeCredentials? Read();
}
