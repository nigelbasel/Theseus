using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using Theseus.Core.Diagnostics;
using Theseus.Core.Instructions;

namespace Theseus.App.ViewModels;

/// <summary>
/// What a workstream's sessions will read and can run: every CLAUDE.md, rule and import in the order a
/// session reads them, and every skill, command and agent with the ones that lose their name marked.
///
/// It only looks. Editing belongs in an editor, so every line opens its file in VS Code. It is worked
/// out from Claude Code's documentation rather than asked of a running session, so <c>/context</c> in
/// a session remains the last word on what actually loaded.
/// </summary>
public sealed class InstructionsViewModel : ObservableObject
{
    private readonly string _userClaudeDirectory;
    private readonly string _managedFile;
    private IReadOnlyList<CommandEntry> _commands = [];
    private IReadOnlyList<InstructionFile> _files = [];
    private bool _isOpen;
    private bool _isScanning;
    private bool _showPluginItems;
    private string _heading = string.Empty;
    private string _summary = string.Empty;

    /// <summary>Creates the viewer over the user's own Claude Code directory and this machine's managed policy.</summary>
    public InstructionsViewModel(string? userClaudeDirectory = null, string? managedFile = null)
    {
        _userClaudeDirectory = userClaudeDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
        _managedFile = managedFile
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ClaudeCode", "CLAUDE.md");

        OpenCommand = new RelayCommand(parameter => _ = OpenAsync(parameter as DetailDrawerViewModel),
            parameter => parameter is DetailDrawerViewModel);
        CloseCommand = new RelayCommand(_ => IsOpen = false);
        OpenFileCommand = new RelayCommand(parameter => OpenInEditor(parameter as string), parameter => parameter is string);
    }

    /// <summary>Scans the workstream shown in the details and opens the viewer.</summary>
    public RelayCommand OpenCommand { get; }

    /// <summary>Hides the viewer.</summary>
    public RelayCommand CloseCommand { get; }

    /// <summary>Opens one file in VS Code.</summary>
    public RelayCommand OpenFileCommand { get; }

    /// <summary>The instruction files, in reading order.</summary>
    public ObservableCollection<InstructionRow> Files { get; } = [];

    /// <summary>The skills, commands and agents.</summary>
    public ObservableCollection<InstructionRow> Commands { get; } = [];

    /// <summary>Whether the viewer is showing.</summary>
    public bool IsOpen
    {
        get => _isOpen;
        set => Set(ref _isOpen, value);
    }

    /// <summary>Whether the scan is still running.</summary>
    public bool IsScanning
    {
        get => _isScanning;
        private set => Set(ref _isScanning, value);
    }

    /// <summary>Whose sessions these are.</summary>
    public string Heading
    {
        get => _heading;
        private set => Set(ref _heading, value);
    }

    /// <summary>The counts, in a sentence.</summary>
    public string Summary
    {
        get => _summary;
        private set => Set(ref _summary, value);
    }

    /// <summary>
    /// Whether plugin skills are listed. Off to start with: one plugin here brings over a hundred and
    /// sixty, which would bury everything else, and none of them can take another's name.
    /// </summary>
    public bool ShowPluginItems
    {
        get => _showPluginItems;
        set
        {
            if (Set(ref _showPluginItems, value))
            {
                FillCommands();
            }
        }
    }

    private async Task OpenAsync(DetailDrawerViewModel? drawer)
    {
        if (drawer is null)
        {
            return;
        }

        var workingDirectory = drawer.WorkingDirectory;
        var added = drawer.AddDirectories.ToList();

        Heading = $"What a {drawer.Title} session reads and can run";
        Summary = "Looking…";
        Files.Clear();
        Commands.Clear();
        IsOpen = true;
        IsScanning = true;

        try
        {
            var loadAdded = Environment.GetEnvironmentVariable(InstructionScan.AdditionalDirectoriesVariable) == "1";

            (_files, _commands) = await Task.Run(() => (
                InstructionScan.For(workingDirectory, added, _userClaudeDirectory, _managedFile, loadAdded),
                CommandScan.For(workingDirectory, added, _userClaudeDirectory, InstalledPlugins.Read(_userClaudeDirectory))));

            foreach (var file in _files)
            {
                Files.Add(InstructionRow.For(file));
            }

            FillCommands();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception, "Could not work out what {Directory} sessions read", workingDirectory);
            Summary = $"Could not look through {workingDirectory}: {exception.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void FillCommands()
    {
        Commands.Clear();

        var shown = _commands
            .Where(entry => _showPluginItems || entry.Origin != CommandOrigin.Plugin)
            .OrderBy(entry => entry.Kind)
            .ThenBy(entry => entry.Origin)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var entry in shown)
        {
            Commands.Add(InstructionRow.For(entry));
        }

        var plugins = _commands.Count(entry => entry.Origin == CommandOrigin.Plugin);
        var hidden = _commands.Count(entry => entry.HiddenBy is not null);

        Summary = $"{_files.Count(file => file.Load == InstructionLoad.AtStart)} instruction files read at start, "
                  + $"{_files.Count(file => file.Load == InstructionLoad.OnDemand)} on demand, "
                  + $"{_files.Count(file => file.Load == InstructionLoad.NotLoaded)} not read. "
                  + $"{_commands.Count(entry => entry.Kind == CommandKind.Skill)} skills, "
                  + $"{_commands.Count(entry => entry.Kind == CommandKind.Command)} commands, "
                  + $"{_commands.Count(entry => entry.Kind == CommandKind.Agent)} agents"
                  + (plugins > 0 ? $", {plugins} of them from plugins{(_showPluginItems ? string.Empty : " and not listed")}" : string.Empty)
                  + (hidden > 0 ? $". {hidden} lose their name to another." : ".");
    }

    /// <summary>
    /// Opens a file in VS Code, and in whatever opens it by default when VS Code is not installed.
    /// VS Code puts <c>code.cmd</c> on the path; the editor itself is beside that folder, and starting
    /// it directly avoids a console window flashing up and cmd's quoting rules.
    /// </summary>
    private static void OpenInEditor(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            if (VisualStudioCode() is { } editor)
            {
                Process.Start(new ProcessStartInfo(editor) { ArgumentList = { path }, UseShellExecute = false });

                return;
            }

            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            TheseusLog.Warning(exception, "Could not open {Path}", path);
        }
    }

    private static string? VisualStudioCode()
    {
        var folders = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var folder in folders)
        {
            try
            {
                if (File.Exists(Path.Combine(folder, "code.cmd"))
                    && Path.GetFullPath(Path.Combine(folder, "..", "Code.exe")) is var editor
                    && File.Exists(editor))
                {
                    return editor;
                }
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // A malformed entry on the path says nothing about the rest of it.
            }
        }

        return null;
    }
}
