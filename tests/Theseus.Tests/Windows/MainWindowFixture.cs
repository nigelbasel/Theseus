using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Theseus.Tests.Conventions;
using Theseus.App;
using Theseus.Tests.ViewModels;

namespace Theseus.Tests.Windows;

/// <summary>
/// The main window, built and laid out off screen with a fake shell and fake board data (baseline section 4, WB6), so
/// its keys, menus, toolbar and board can be tested as WPF builds them rather than as text.
///
/// The real ShellViewModel cannot be used: it is sealed and reads and writes Theseus's own data folder. MainWindow's
/// bindings are by name, so <see cref="FakeShell"/> stands in with the members a test needs, and a missing member only
/// leaves its binding empty. The window is never shown, so nothing appears on the screen and no focus is taken.
///
/// MainWindow's StaticResources resolve only through the Application, and a process holds one Application, owned by the
/// thread that made it. So every window is built on one dispatcher thread kept for the purpose, and the Application
/// holds App.xaml's resources only while a test runs here, leaving every other test as it would be without it.
/// </summary>
internal static class MainWindowFixture
{
    private static readonly Lazy<Dispatcher> Host = new(Start, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Builds the window over the fake shell and board given, lays it out, and runs the test on its thread.</summary>
    /// <param name="test">The test, given the window and its shell.</param>
    /// <param name="board">The fake board's cards; none when not given.</param>
    /// <param name="size">The size to lay the window out at; 1400 by 760, its default, when not given.</param>
    public static void Run(Action<MainWindow, FakeShell> test, Action<BoardFixture>? board = null, Size? size = null)
    {
        ArgumentNullException.ThrowIfNull(test);

        Host.Value.Invoke(() =>
        {
            BoardFixture fixture = new();
            board?.Invoke(fixture);
            fixture.Board.Refresh();

            FakeShell shell = new(fixture.Board);
            MainWindow window = new() { DataContext = shell };
            Size at = size ?? new Size(1400, 760);
            FrameworkElement content = (FrameworkElement)window.Content;

            content.Measure(at);
            content.Arrange(new Rect(at));
            content.UpdateLayout();

            test(window, shell);
        });
    }

    private static Dispatcher Start()
    {
        TaskCompletionSource<Dispatcher> started = new();
        Thread thread = new(() =>
        {
            try
            {
                // A plain Application, run: App's own startup would open the real board, and an Application that is never run
                // counts itself as shutting down and refuses to load a second window's XAML.
                Application application = new() { ShutdownMode = ShutdownMode.OnExplicitShutdown, Resources = AppResources() };
                application.Startup += (_, _) => started.SetResult(Dispatcher.CurrentDispatcher);
                application.Run();
            }
            catch (Exception exception)
            {
                started.TrySetException(exception);
            }
        })
        {
            IsBackground = true,
            Name = "MainWindowFixture",
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return started.Task.GetAwaiter().GetResult();
    }

    // App.xaml's resources, from the source tree: the dictionary inside Application.Resources, with the app's namespaces
    // given their assembly and its theme files their pack addresses, as loose XAML needs.
    private static ResourceDictionary AppResources()
    {
        string text = File.ReadAllText(LooseXaml.SourceFile("App.xaml"));
        int open = text.IndexOf("<ResourceDictionary>", StringComparison.Ordinal);
        int close = text.LastIndexOf("</ResourceDictionary>", StringComparison.Ordinal) + "</ResourceDictionary>".Length;
        string body = text[open..close]
            .Replace("<ResourceDictionary>", "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" "
                + "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:sys=\"clr-namespace:System;assembly=System.Runtime\" "
                + "xmlns:converters=\"clr-namespace:Theseus.App.Converters;assembly=Theseus\">", StringComparison.Ordinal)
            .Replace("Source=\"Themes/", "Source=\"pack://application:,,,/Theseus;component/Themes/", StringComparison.Ordinal);

        return (ResourceDictionary)XamlReader.Parse(body);
    }
}
