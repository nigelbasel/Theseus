using System.Xml.Linq;

namespace Theseus.Tests.Conventions;

/// <summary>
/// A Label is a control, so a style of its own that is not based on the theme's draws in the system's control text
/// colour, black, whatever the theme: unreadable in dark mode. Each such style sets its Foreground from the theme.
/// </summary>
[TestFixture]
public class LabelStyleForegroundTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Test]
    public void EveryLabelStyleNotBasedOnAnother_SetsItsForeground()
    {
        string root = Path.GetDirectoryName(LooseXaml.SourceFile("App.xaml"))!;
        List<string> missing = [];

        foreach (string file in Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories)
                     .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            foreach (XElement style in XDocument.Load(file).Descendants(Presentation + "Style"))
            {
                bool isLabel = (string?)style.Attribute("TargetType") is "Label" or "{x:Type Label}";
                bool setsForeground = style.Elements(Presentation + "Setter").Any(setter => (string?)setter.Attribute("Property") == "Foreground");

                if (isLabel && style.Attribute("BasedOn") is null && !setsForeground)
                {
                    missing.Add($"{Path.GetRelativePath(root, file)}: {(string?)style.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")) ?? "(implicit)"}");
                }
            }
        }

        Assert.That(missing, Is.Empty);
    }
}
