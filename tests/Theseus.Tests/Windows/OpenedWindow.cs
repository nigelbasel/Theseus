using System.Windows;
using System.Windows.Controls;

namespace Theseus.Tests.Windows;

/// <summary>
/// One of the secondary windows, built and opened off-screen in a scratch folder of its own; disposing closes it and
/// deletes the folder.
/// </summary>
public sealed class OpenedWindow : IDisposable
{
    private readonly string _folder;

    private OpenedWindow(Window window, string folder)
    {
        Window = window;
        _folder = folder;
    }

    /// <summary>The window, loaded.</summary>
    public Window Window { get; }

    /// <summary>Builds and opens the window called <paramref name="name"/> in <see cref="SecondaryWindows.All"/>.</summary>
    public static OpenedWindow Of(string name)
    {
        string folder = Path.Combine(Path.GetTempPath(), "theseus-wb5-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        Window window = SecondaryWindows.All(folder).Single(each => each.Name == name).Build();

        return new OpenedWindow(DialogHost.Open(window), folder);
    }

    /// <summary>The element called <paramref name="name"/>.</summary>
    public T Named<T>(string name)
        where T : class =>
        Window.FindName(name) as T ?? throw new InvalidOperationException($"{name} is not a {typeof(T).Name} in the window");

    /// <summary>Every element of the window's logical tree, which includes collapsed parts but not templates' insides.</summary>
    public IReadOnlyList<DependencyObject> Logical()
    {
        List<DependencyObject> found = [];
        Walk(Window, found);

        return found;
    }

    /// <summary>The buttons of the window, from its logical tree.</summary>
    public IReadOnlyList<Button> Buttons() => [.. Logical().OfType<Button>()];

    /// <inheritdoc />
    public void Dispose()
    {
        Window.Close();

        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // The spec window's watcher can still hold the folder for a moment; the temp folder is cleared in time.
        }
    }

    private static void Walk(DependencyObject parent, List<DependencyObject> found)
    {
        foreach (object child in LogicalTreeHelper.GetChildren(parent))
        {
            if (child is DependencyObject element)
            {
                found.Add(element);
                Walk(element, found);
            }
        }
    }
}
