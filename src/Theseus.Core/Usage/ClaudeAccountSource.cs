using Theseus.Core.Diagnostics;

namespace Theseus.Core.Usage;

/// <summary>
/// The signed-in account, kept current. Signing out and back in as another account rewrites the
/// same two files rather than moving them, so the account is re-read when either file changes and
/// left alone otherwise: a status bar showing the account you left is worse than showing none,
/// because it is the reading you would act on.
/// </summary>
public sealed class ClaudeAccountSource
{
    private readonly ClaudeAccountReader _reader;
    private readonly string _configPath;
    private readonly string _credentialsPath;

    private (DateTime Config, DateTime Credentials) _seen;

    /// <summary>Creates a source over the files that name the account.</summary>
    public ClaudeAccountSource(string? configPath = null, string? credentialsPath = null)
    {
        _configPath = configPath ?? ClaudeAccountReader.DefaultConfigPath;
        _credentialsPath = credentialsPath ?? CredentialFileSource.DefaultPath;
        _reader = new ClaudeAccountReader(_configPath, _credentialsPath);

        Current = _reader.Read();
        _seen = Stamp();
    }

    /// <summary>The account as it was last read.</summary>
    public ClaudeAccount Current { get; private set; }

    /// <summary>
    /// Re-reads the account if either file has been written since last time, and reports whether the
    /// account itself is now a different one.
    /// </summary>
    public bool Refresh()
    {
        var stamp = Stamp();

        if (stamp == _seen)
        {
            return false;
        }

        _seen = stamp;

        var account = _reader.Read();
        var changed = !string.Equals(account.EmailAddress, Current.EmailAddress, StringComparison.OrdinalIgnoreCase);

        Current = account;

        if (changed)
        {
            TheseusLog.Information("The signed-in Claude account changed");
        }

        return changed;
    }

    private (DateTime, DateTime) Stamp() => (Written(_configPath), Written(_credentialsPath));

    private static DateTime Written(string path)
    {
        try
        {
            return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return DateTime.MinValue;
        }
    }
}
