using System.Windows;
using System.Windows.Shell;
using Theseus.Core.Diagnostics;
using Theseus.Core.Sessions;

namespace Theseus.App.Services;

/// <summary>
/// The live sessions, offered on the taskbar button's own menu. Theseus is often behind whichever
/// terminal is being read at the time, so the shortest route to a session should not begin with
/// finding Theseus.
/// </summary>
public static class SessionJumpList
{
    private const string Category = "Sessions";

    /// <summary>Replaces the taskbar menu with one entry per live session.</summary>
    public static void Show(Application application, IEnumerable<LiveSession> sessions)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(sessions);

        if (Environment.ProcessPath is not { } executable)
        {
            TheseusLog.Warning("The taskbar menu was skipped because the running executable has no path");
            return;
        }

        var list = new JumpList { ShowFrequentCategory = false, ShowRecentCategory = false };

        foreach (var session in sessions.Where(session => session.IsAdoptable))
        {
            list.JumpItems.Add(Task(executable, session));
        }

        try
        {
            JumpList.SetJumpList(application, list);
        }
        catch (Exception exception)
        {
            TheseusLog.Warning(exception, "The taskbar menu could not be updated");
        }
    }

    private static JumpTask Task(string executable, LiveSession session) => new()
    {
        Title = SessionLabel.For(session),
        Description = session.Cwd ?? string.Empty,
        ApplicationPath = executable,
        IconResourcePath = executable,
        Arguments = FocusRequest.ArgumentsFor(session.SessionId),
        CustomCategory = Category,
    };
}
