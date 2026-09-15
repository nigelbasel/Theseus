using System.Windows;
using System.Windows.Shell;
using Theseus.Core.Diagnostics;
using Theseus.Core.Sessions;

namespace Theseus.App.Services;

/// <summary>
/// The live sessions, offered on the taskbar button's own menu. Theseus is often behind whichever
/// terminal is being read at the time, so the shortest route to a session should not begin with
/// finding Theseus.
///
/// The menu is committed only when its entries change. Committing is a blocking call into the shell
/// on the UI thread, and making it on every refresh froze the board on 2026-09-15.
/// </summary>
public static class SessionJumpList
{
    private const string Category = "Sessions";

    private static IReadOnlyList<SessionMenuEntry>? _applied;

    /// <summary>Makes the taskbar menu list the live sessions, if it does not already.</summary>
    public static void Show(Application application, IEnumerable<LiveSession> sessions)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(sessions);

        var entries = SessionMenu.Entries(sessions);

        if (SessionMenu.Unchanged(_applied, entries))
        {
            return;
        }

        if (Environment.ProcessPath is not { } executable)
        {
            TheseusLog.Warning("The taskbar menu was skipped because the running executable has no path");
            return;
        }

        var list = new JumpList { ShowFrequentCategory = false, ShowRecentCategory = false };

        foreach (var entry in entries)
        {
            list.JumpItems.Add(Task(executable, entry));
        }

        try
        {
            JumpList.SetJumpList(application, list);
            _applied = entries;
        }
        catch (Exception exception)
        {
            TheseusLog.Warning(exception, "The taskbar menu could not be updated");
        }
    }

    private static JumpTask Task(string executable, SessionMenuEntry entry) => new()
    {
        Title = entry.Title,
        Description = entry.Description,
        ApplicationPath = executable,
        IconResourcePath = executable,
        Arguments = FocusRequest.ArgumentsFor(entry.SessionId),
        CustomCategory = Category,
    };
}
