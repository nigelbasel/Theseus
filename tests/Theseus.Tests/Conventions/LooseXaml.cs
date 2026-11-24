using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Markup;

namespace Theseus.Tests.Conventions;

/// <summary>
/// Parses a resource dictionary from the source tree, as the tests of the shared styles read it. Compiled XAML names the
/// app's own namespaces without an assembly, which loose XAML cannot resolve, so the app's assembly is added here.
/// </summary>
public static class LooseXaml
{
    /// <summary>The dictionary at <c>src/Theseus.App/</c> plus <paramref name="path"/>.</summary>
    public static ResourceDictionary Dictionary(params string[] path)
    {
        string text = File.ReadAllText(SourceFile(path))
            .Replace("\"clr-namespace:Theseus.App.Controls\"", "\"clr-namespace:Theseus.App.Controls;assembly=Theseus\"", StringComparison.Ordinal);

        return (ResourceDictionary)XamlReader.Parse(text);
    }

    /// <summary>A file of the app's source tree.</summary>
    public static string SourceFile(params string[] path) => Path.Combine([RepositoryRoot(), "src", "Theseus.App", .. path]);

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
