using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Navigation;
using Microsoft.Win32;
using Theseus.App.ViewModels;

namespace Theseus.App;

/// <summary>
/// Options, as a window of its own rather than a panel inside the board. Every Change… here opens a picker or a small
/// dialog and sets the value, which is written at once (W6); nothing is typed into the page itself (options-made-clear item 6).
/// </summary>
public partial class SettingsWindow : Window
{
    /// <summary>Creates the window.</summary>
    public SettingsWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => FitToWorkArea();

        // The selected tab, not the TabControl, so its focus ring shows and the arrows switch tabs at once (W43, G66).
        Loaded += (_, _) => (Tabs.SelectedItem as System.Windows.Controls.TabItem ?? Tabs.Items.OfType<System.Windows.Controls.TabItem>().First()).Focus();
    }

    private SettingsViewModel? Settings => DataContext as SettingsViewModel;

    private void FitToWorkArea() =>
        MaxHeight = SystemParameters.WorkArea.Height - 80;

    private void OnChangeJira(object sender, RoutedEventArgs e)
    {
        if (Settings is { } settings
            && Ask("Jira", "The Jira site issue keys link to, such as https://example.atlassian.net. Leave empty for none.", settings.JiraBaseUrl, allowsEmpty: true) is { } url)
        {
            settings.JiraBaseUrl = url;
        }
    }

    private void OnChangeLanes(object sender, RoutedEventArgs e)
    {
        if (Settings is { } settings && Ask("Board stages", "The board's stages, one per line, in order.", settings.LaneList, isList: true) is { } lanes)
        {
            settings.LaneList = lanes;
        }
    }

    private void OnChangeDutyCap(object sender, RoutedEventArgs e) =>
        ChangeDuty("Daily cap", "The most runs a duty gives in a day.", duties => duties.CapText, (duties, text) => duties.SetCap(text));

    private void OnChangeDutyFailures(object sender, RoutedEventArgs e) =>
        ChangeDuty("Pauses itself", "Failed runs in a row before a duty pauses itself.", duties => duties.FailureText,
            (duties, text) => duties.SetFailures(text));

    private void OnChangeQuietHours(object sender, RoutedEventArgs e) =>
        ChangeDuty("Quiet hours", "off, or a start and an end such as 22:00-07:00.", duties => duties.QuietText,
            (duties, text) => duties.SetQuiet(text));

    private void OnChangeQueue(object sender, RoutedEventArgs e) =>
        ChangeDuty("Overnight queue", "How long a card is idle before it is resumed, then the most times a day, such as 30m, 6.",
            duties => duties.QueueText, (duties, text) => duties.SetQueue(text));

    // Asked again with the reason until it reads or is cancelled.
    private void ChangeDuty(string title, string label, Func<DutyOptionsViewModel, string> current, Func<DutyOptionsViewModel, string, string?> set)
    {
        if (Settings?.Duties is not { } duties)
        {
            return;
        }

        var text = current(duties);
        var prompt = label;

        while (Ask(title, prompt, text) is { } answer)
        {
            if (set(duties, answer) is not { } refusal)
            {
                return;
            }

            text = answer;
            prompt = $"{label} {refusal}";
        }
    }

    private void OnChangeAreas(object sender, RoutedEventArgs e)
    {
        if (Settings is { } settings && Ask("Board areas", "The board's areas, one per line, in order. Each gets its own terminal window.", settings.AreaList, isList: true) is { } areas)
        {
            settings.AreaList = areas;
        }
    }

    private void OnChangeWindowName(object sender, RoutedEventArgs e)
    {
        if (Settings is { } settings && Ask("Terminal window name", "The name each area's terminal window starts with.", settings.TerminalWindowName) is { } name)
        {
            settings.TerminalWindowName = name;
        }
    }

    private void OnChangeHoldoutRemote(object sender, RoutedEventArgs e)
    {
        if (Settings is { } settings
            && Ask("Holdout remote address", "The private holdout repository's address, such as host/owner/repo. Leave empty for none.", settings.HoldoutRemote, allowsEmpty: true) is { } remote)
        {
            settings.HoldoutRemote = remote;
        }
    }

    private void OnChooseProjectsFolder(object sender, RoutedEventArgs e)
    {
        if (Settings is { } settings && ChooseFolder("Choose the projects folder", settings.ProjectsFolder) is { } folder)
        {
            settings.ProjectsFolder = folder;
        }
    }

    private void OnChooseWorktreesFolder(object sender, RoutedEventArgs e)
    {
        if (Settings is { } settings && ChooseFolder("Choose the worktrees folder", settings.WorktreesFolder) is { } folder)
        {
            settings.WorktreesFolder = folder;
        }
    }

    private void OnChooseClonesFolder(object sender, RoutedEventArgs e)
    {
        if (Settings is { } settings && ChooseFolder("Choose the clones folder", settings.ClonesFolder) is { } folder)
        {
            settings.ClonesFolder = folder;
        }
    }

    private void OnChoosePlanDirectory(object sender, RoutedEventArgs e)
    {
        if (Settings is { } settings && ChooseFolder("Choose the plan directory", settings.PlanDirectory) is { } folder)
        {
            settings.PlanDirectory = folder;
        }
    }

    private void OnChooseTerminal(object sender, RoutedEventArgs e)
    {
        if (Settings is { } settings && ChooseProgram("Choose Windows Terminal", settings.TerminalExecutablePath) is { } program)
        {
            settings.TerminalExecutablePath = program;
        }
    }

    private void OnChooseClaude(object sender, RoutedEventArgs e)
    {
        if (Settings is { } settings && ChooseProgram("Choose Claude Code", settings.ClaudeExecutablePath) is { } program)
        {
            settings.ClaudeExecutablePath = program;
        }
    }

    private void OnEditIdentity(object sender, RoutedEventArgs e)
    {
        if (Settings is { } settings && Edit("Who the session is", "Opens every first run and fresh start. Keep {handoff} so sessions know where their notes go.", settings.IdentityPrompt) is { } text)
        {
            settings.IdentityPrompt = text;
        }
    }

    private void OnEditStanding(object sender, RoutedEventArgs e)
    {
        if (Settings is { } settings && Edit("Standing prompt", "Put in front of every workstream's own opening prompt. Leave empty for none.", settings.SessionPreamble) is { } text)
        {
            settings.SessionPreamble = text;
        }
    }

    private void OnEditContinue(object sender, RoutedEventArgs e)
    {
        if (Settings is { } settings && Edit("Continuing a workstream", "Replaces the workstream's own prompt on a fresh start once it has run before. Leave empty for the built-in one.", settings.ContinuePrompt) is { } text)
        {
            settings.ContinuePrompt = text;
        }
    }

    private void OnEditStop(object sender, RoutedEventArgs e)
    {
        if (Settings is { } settings && Edit("Stopping a session", "Put on the clipboard when you press Stop. Leave empty for the built-in one.", settings.StopPrompt) is { } text)
        {
            settings.StopPrompt = text;
        }
    }

    private string? Ask(string title, string label, string current, bool allowsEmpty = false, bool isList = false)
    {
        TextPromptWindow ask = new(title, label, current) { Owner = this, AllowsEmpty = allowsEmpty, IsList = isList, ActionLabel = "_Save" };

        return ask.ShowDialog() == true ? ask.Text : null;
    }

    private string? Edit(string title, string explanation, string current)
    {
        var edit = new PromptEditWindow(title, explanation, current) { Owner = this };

        return edit.ShowDialog() == true ? edit.Text : null;
    }

    // Nothing is chosen for the developer: the panel only suggests (Nigel, 2026-11-20, question 1).
    private string? ChooseFolder(string title, string current)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            InitialDirectory = !string.IsNullOrWhiteSpace(current) && Directory.Exists(current)
                ? current
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };

        return dialog.ShowDialog(this) == true ? dialog.FolderName : null;
    }

    private string? ChooseProgram(string title, string current)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = "Programs (*.exe;*.cmd)|*.exe;*.cmd|All files (*.*)|*.*",
            InitialDirectory = !string.IsNullOrWhiteSpace(current) && File.Exists(current) ? Path.GetDirectoryName(current) : null,
        };

        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    // One dialog at a time (W8, G17): Options closes, then About opens over the board, as Help > About Theseus opens it.
    private void OnAbout(object sender, RoutedEventArgs e)
    {
        Window? owner = Owner;
        object? board = owner?.DataContext;
        Close();

        owner?.Dispatcher.BeginInvoke(() =>
        {
            (board as ShellViewModel)?.Help.Check();
            new AboutWindow { Owner = owner, DataContext = board }.ShowDialog();
        });
    }

    private void OnLink(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
