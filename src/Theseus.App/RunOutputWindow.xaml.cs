using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Theseus.Core.Running;

namespace Theseus.App;

/// <summary>Follows a headless run's log, rendered one readable line per message, tool call and turn.</summary>
public partial class RunOutputWindow : Window
{
    private readonly string _log;
    private readonly DispatcherTimer _timer;
    private long _read;

    /// <summary>Opens the window on one run's log.</summary>
    public RunOutputWindow(string title, string log)
    {
        InitializeComponent();
        _log = log;
        Title = $"Output: {title}";
        Intro.Text = $"What {title}'s headless run has written, newest at the end. It is followed while the run goes.";
        _timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => Follow(), Dispatcher);
        Closed += (_, _) => _timer.Stop();
        Follow();
    }

    private void Follow()
    {
        string added;

        try
        {
            using var stream = new FileStream(_log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            if (stream.Length <= _read)
            {
                return;
            }

            stream.Seek(_read, SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            added = reader.ReadToEnd();
        }
        catch (IOException)
        {
            return;
        }

        var complete = added.LastIndexOf('\n');

        if (complete < 0)
        {
            return;
        }

        _read += Encoding.UTF8.GetByteCount(added[..(complete + 1)]);

        var lines = added[..complete].Split('\n').Select(RunOutputText.Render).OfType<string>().ToList();

        if (lines.Count == 0)
        {
            return;
        }

        Output.AppendText(string.Join(Environment.NewLine, lines) + Environment.NewLine);
        Output.ScrollToEnd();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
