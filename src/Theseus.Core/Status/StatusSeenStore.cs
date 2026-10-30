namespace Theseus.Core.Status;

/// <summary>
/// Which Theseus each session it launched last saw, one small file per session under
/// <c>%LOCALAPPDATA%\Theseus\status-seen</c>. Written at launch, so only Theseus's own sessions are
/// ever told anything: the hook runs in every session on the machine.
/// </summary>
public sealed class StatusSeenStore
{
    private readonly string _directory;

    /// <summary>Creates the store over a folder.</summary>
    public StatusSeenStore(string? directory = null) => _directory = directory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Theseus", "status-seen");

    /// <summary>What this session last saw, or null for a session Theseus did not launch.</summary>
    public string? Read(string sessionId)
    {
        try
        {
            var path = PathFor(sessionId);

            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Records what this session has now been told about.</summary>
    public void Write(string sessionId, string marker)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(PathFor(sessionId), marker);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
        }
    }

    private string PathFor(string sessionId) =>
        Path.Combine(_directory, string.Concat(sessionId.Where(character => char.IsLetterOrDigit(character) || character == '-')) + ".txt");
}
