namespace Theseus.Core.Locks;

/// <summary>
/// Installs the lock client, its guard and the command classes into Claude Code's <c>bin</c> folder, from Options and
/// never by a session. Each file is written beside its target and renamed over it, never edited in place: bash reads a
/// running script by offset, and a run part way through must not find its file rewritten under it. The copy each
/// install replaces is kept, so Undo can put it back.
/// </summary>
public sealed class LockToolsInstallation
{
    private const string PreviousSuffix = ".previous";

    private readonly string _bin;

    public LockToolsInstallation(string configFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configFolder);

        _bin = Path.Combine(Path.GetFullPath(configFolder), "bin");
    }

    public LockToolsState State() =>
        new([.. LockToolsBundle.FileNames.Select(name =>
        {
            string path = Path.Combine(_bin, name);
            bool installed = File.Exists(path);

            return new LockToolFile(name, path, installed,
                installed && LockToolsBundle.VersionOf(File.ReadAllText(path)) == LockToolsBundle.VersionOf(LockToolsBundle.Text(name)),
                File.Exists(path + PreviousSuffix));
        })]);

    public LockToolsOutcome Install()
    {
        List<string> written = [];

        try
        {
            Directory.CreateDirectory(_bin);

            foreach (LockToolFile file in State().Files.Where(file => !file.IsUpToDate))
            {
                if (file.IsInstalled)
                {
                    File.Copy(file.Path, file.Path + PreviousSuffix, overwrite: true);
                }

                string staged = file.Path + ".new";
                string text = LockToolsBundle.Text(file.Name);
                File.WriteAllText(staged, file.Name.EndsWith(".sh", StringComparison.Ordinal) ? text.Replace("\r\n", "\n", StringComparison.Ordinal) : text);
                File.Move(staged, file.Path, overwrite: true);
                written.Add(file.Name);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new LockToolsOutcome(false, $"Could not install the lock tools in {_bin}: {exception.Message}");
        }

        return new LockToolsOutcome(true, written.Count == 0
            ? $"The lock tools in {_bin} are this Theseus's already."
            : $"Installed {string.Join(", ", written)} in {_bin}; the copies they replaced are kept for Undo.");
    }

    public LockToolsOutcome Undo()
    {
        List<string> restored = [];

        try
        {
            foreach (LockToolFile file in State().Files.Where(file => file.CanUndo))
            {
                File.Move(file.Path + PreviousSuffix, file.Path, overwrite: true);
                restored.Add(file.Name);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new LockToolsOutcome(false, $"Could not put back the earlier lock tools in {_bin}: {exception.Message}");
        }

        return new LockToolsOutcome(restored.Count > 0, restored.Count == 0
            ? "There is no earlier copy to put back."
            : $"Put back the earlier {string.Join(", ", restored)}.");
    }
}
