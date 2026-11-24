using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Xml.Linq;

namespace Theseus.Tests.Conventions;

/// <summary>
/// The ratchet of the Windows conventions batches (baseline W5; checklist G86, G94, G109, G159, G165, G172). Every XAML
/// file of the app is scanned for colour literals outside the state colour dictionaries, the Segoe MDL2 Assets font,
/// font sizes off the type ramp and spacing off the 4 epx grid; every C# file for MessageBox.Show.
/// <para>
/// What a decision keeps on purpose is in <c>AllowList.txt</c>, with its reason, and stays. What still fails is in
/// <c>Exemptions/</c>, one file per source file, one line per rule and value with how many times it occurs. A count may
/// only fall: a batch that fixes something lowers its count or deletes its line, and the last batch leaves the folder
/// empty. A new occurrence fails the test.
/// </para>
/// </summary>
[TestFixture]
public class XamlConventionTests
{
    private static readonly double[] Ramp = [12, 14, 18, 20, 28];

    private static readonly HashSet<string> ColourProperties = new(StringComparer.Ordinal)
    {
        "Background", "Foreground", "BorderBrush", "Fill", "Stroke", "Color", "OpacityMask", "CaretBrush", "SelectionBrush",
    };

    private static readonly HashSet<string> NamedColours = new(
        typeof(Colors).GetProperties(BindingFlags.Public | BindingFlags.Static).Select(property => property.Name), StringComparer.OrdinalIgnoreCase);

    private static readonly Regex HexColour = new("^#([0-9a-fA-F]{3,4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$", RegexOptions.CultureInvariant);

    [Test]
    public void EveryFile_MeetsTheConventions_OrIsExemptedForNoMoreThanItHasNow()
    {
        IReadOnlyList<Allowance> allowed = AllowList();
        Dictionary<string, Dictionary<(string Rule, string Value), int>> found = Scan(allowed);
        List<string> problems = [];

        foreach (string file in found.Keys.Union(ExemptedFiles()).Order(StringComparer.Ordinal))
        {
            Dictionary<(string Rule, string Value), int> now = found.GetValueOrDefault(file) ?? [];
            Dictionary<(string Rule, string Value), int> exempt = Exemptions(file);

            if (!File.Exists(Path.Combine(AppRoot(), file)))
            {
                problems.Add($"{file}: the file is gone; delete Exemptions/{file}.txt");
                continue;
            }

            foreach ((string Rule, string Value) key in now.Keys.Union(exempt.Keys).Order())
            {
                int count = now.GetValueOrDefault(key);
                int limit = exempt.GetValueOrDefault(key);

                if (count > limit)
                {
                    problems.Add($"{file}: {key.Rule} {key.Value} occurs {count} times; {limit} exempted. Fix it, or add it to AllowList.txt with a decision.");
                }
                else if (count < limit)
                {
                    problems.Add($"{file}: {key.Rule} {key.Value} now occurs {count} times; lower Exemptions/{file}.txt to " +
                        (count == 0 ? "nothing (delete the line)" : $"{key.Rule} | {key.Value} | {count}"));
                }
            }
        }

        Assert.That(problems, Is.Empty, string.Join(Environment.NewLine, problems));
    }

    [Test]
    public void TheExemptionFolder_IsEmpty_SoEveryFileMeetsTheConventions_OrADecisionKeepsWhatItDoesNot()
    {
        Assert.That(ExemptedFiles(), Is.Empty, "the last batch, WB9, leaves no exemption; what a decision keeps is in AllowList.txt");
    }

    // Values the copied Fluent MenuItem templates hold as Microsoft wrote them; Theseus's own tightened margins are not among them.
    [TestCase("0,0,6,0")]
    [TestCase("0,3,0,0")]
    [TestCase("0,3,0,3")]
    [TestCase("12,0,12,18")]
    [TestCase("12,10,12,18")]
    [TestCase("25,0,0,0")]
    public void TheCopiedMenuTemplatesSpacing_IsAllowedThere_AndNowhereElse(string value)
    {
        Assert.That(AllowList().Any(allowance => allowance.Allows("Spacing", value, "Themes/CompactMenu.xaml", glyph: false)), Is.True);
        Assert.That(AllowList().Any(allowance => allowance.Allows("Spacing", value, "MainWindow.xaml", glyph: false)), Is.False);
    }

    [Test]
    public void EveryAllowance_SaysWhyItIsKept()
    {
        Assert.That(AllowList(), Is.Not.Empty);
        Assert.That(AllowList().Where(allowance => string.IsNullOrWhiteSpace(allowance.Reason)).Select(allowance => allowance.ToString()), Is.Empty);
    }

    [TestCase("FontSize", "11", false)]
    [TestCase("FontSize", "16", true)]
    [TestCase("Spacing", "0,1,0,0", true)]
    [TestCase("Spacing", "0,2", true)]
    [TestCase("Spacing", "1,0,0,0", false)]
    [TestCase("Spacing", "6,4", false)]
    [TestCase("Spacing", "8,2", false)]
    [TestCase("Spacing", "0,1,4,0", false)]
    [TestCase("Colour", "Transparent", true)]
    [TestCase("Colour", "#D68910", false)]
    public void TheAllowList_KeepsOnlyWhatADecisionKeeps(string rule, string value, bool kept)
    {
        Assert.That(AllowList().Any(allowance => allowance.Allows(rule, value, "Some/Window.xaml", glyph: rule == "FontSize")), Is.EqualTo(kept));
    }

    [Test]
    public void TheScanner_FindsEachKindOfDefect()
    {
        XDocument sample = XDocument.Parse("""
            <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <Grid.Resources>
                <Style x:Key="Note" TargetType="TextBlock">
                  <Setter Property="FontSize" Value="11" />
                  <Setter Property="Margin" Value="0,14,0,4" />
                  <Setter Property="Foreground" Value="#D68910" />
                </Style>
              </Grid.Resources>
              <TextBlock FontFamily="Segoe MDL2 Assets" FontSize="8" Text="x" Foreground="White" Padding="6,4" Margin="0,1,0,0" />
              <TextBlock FontSize="{Binding Size}" Margin="{StaticResource DialogGutter}" Background="Transparent" />
              <Border BorderThickness="1"><Border.Padding><Thickness>6,3</Thickness></Border.Padding></Border>
              <SolidColorBrush x:Key="Odd"><SolidColorBrush.Color><Color>#123456</Color></SolidColorBrush.Color></SolidColorBrush>
            </Grid>
            """);

        List<Finding> findings = [.. Findings(sample, "Sample.xaml", [])];

        Assert.That(findings.Select(finding => (finding.Rule, finding.Value, finding.Glyph)), Is.EquivalentTo(new[]
        {
            ("FontSize", "11", false), ("Spacing", "0,14,0,4", false), ("Colour", "#D68910", false),
            ("Font", "Segoe MDL2 Assets", false), ("FontSize", "8", true), ("Colour", "White", false), ("Spacing", "6,4", false),
            ("Spacing", "0,1,0,0", false), ("Colour", "Transparent", false), ("Spacing", "6,3", false), ("Colour", "#123456", false),
        }));
    }

    private static Dictionary<string, Dictionary<(string Rule, string Value), int>> Scan(IReadOnlyList<Allowance> allowed)
    {
        Dictionary<string, Dictionary<(string Rule, string Value), int>> found = new(StringComparer.Ordinal);
        List<(string File, XDocument Document)> xaml = [.. SourceFiles("*.xaml").Select(file => (file, XDocument.Load(Path.Combine(AppRoot(), file))))];
        HashSet<string> glyphStyles = GlyphStyles(xaml.Select(each => each.Document));

        foreach ((string file, XDocument document) in xaml)
        {
            foreach (Finding finding in Findings(document, file, glyphStyles))
            {
                if (!allowed.Any(allowance => allowance.Allows(finding.Rule, finding.Value, file, finding.Glyph)))
                {
                    Count(found, file, finding.Rule, finding.Value);
                }
            }
        }

        foreach (string file in SourceFiles("*.cs"))
        {
            int calls = Regex.Matches(File.ReadAllText(Path.Combine(AppRoot(), file)), @"\bMessageBox\.Show\s*\(").Count;

            for (int call = 0; call < calls; call++)
            {
                Count(found, file, "MessageBox", "Show");
            }
        }

        return found;
    }

    private static void Count(Dictionary<string, Dictionary<(string Rule, string Value), int>> found, string file, string rule, string value)
    {
        if (!found.TryGetValue(file, out Dictionary<(string Rule, string Value), int>? counts))
        {
            counts = [];
            found[file] = counts;
        }

        counts[(rule, value)] = counts.GetValueOrDefault((rule, value)) + 1;
    }

    private static IEnumerable<Finding> Findings(XDocument document, string file, HashSet<string> glyphStyles)
    {
        bool stateColours = Path.GetFileName(file).StartsWith("StateColours", StringComparison.Ordinal);

        foreach (XElement element in document.Descendants())
        {
            if (element.Name.LocalName == "Setter")
            {
                string property = Local((string?)element.Attribute("Property") ?? string.Empty);
                string? value = (string?)element.Attribute("Value");

                if (value is not null)
                {
                    foreach (Finding finding in Check(property, value, IsGlyphSetter(element, glyphStyles), stateColours))
                    {
                        yield return finding;
                    }
                }

                continue;
            }

            if (!element.HasElements && ContentProperty(element) is { } content)
            {
                foreach (Finding finding in Check(content, element.Value, false, stateColours))
                {
                    yield return finding;
                }
            }

            bool glyph = IsGlyphElement(element, glyphStyles);

            foreach (XAttribute attribute in element.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration))
            {
                foreach (Finding finding in Check(Local(attribute.Name.LocalName), attribute.Value, glyph, stateColours))
                {
                    yield return finding;
                }
            }
        }
    }

    private static IEnumerable<Finding> Check(string property, string value, bool glyph, bool stateColours)
    {
        string text = value.Trim();

        if (text.StartsWith('{'))
        {
            yield break;
        }

        if (text.Contains("Segoe MDL2 Assets", StringComparison.Ordinal))
        {
            yield return new Finding("Font", "Segoe MDL2 Assets", false);
        }

        if (property == "FontSize" && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double size) && !Ramp.Contains(size))
        {
            yield return new Finding("FontSize", size.ToString(CultureInfo.InvariantCulture), glyph);
        }

        if (property is "Margin" or "Padding" && Thickness(text) is { } parts && parts.Any(part => part % 4 != 0))
        {
            yield return new Finding("Spacing", string.Join(",", parts.Select(part => part.ToString(CultureInfo.InvariantCulture))), false);
        }

        if (!stateColours && (ColourProperties.Contains(property) || property.EndsWith("Brush", StringComparison.Ordinal))
            && (HexColour.IsMatch(text) || NamedColours.Contains(text)))
        {
            yield return new Finding("Colour", text, false);
        }
    }

    // A value written as element content: a Thickness for a margin or padding (property element, setter value or a keyed
    // spacing token, not a border's), a Color, or a FontFamily. Other content, such as a BorderThickness, is not a convention this test checks.
    private static string? ContentProperty(XElement element)
    {
        string parent = element.Parent?.Name.LocalName ?? string.Empty;
        string setterProperty = parent == "Setter.Value" ? Local((string?)element.Parent!.Parent?.Attribute("Property") ?? string.Empty) : string.Empty;

        return element.Name.LocalName switch
        {
            "Thickness" when Key(element) is { } key && !key.Contains("Border", StringComparison.Ordinal) => "Margin",
            "Thickness" when parent.EndsWith(".Margin", StringComparison.Ordinal) || parent.EndsWith(".Padding", StringComparison.Ordinal) => "Margin",
            "Thickness" when setterProperty is "Margin" or "Padding" => setterProperty,
            "Color" => "Color",
            "FontFamily" => "FontFamily",
            _ => null,
        };
    }

    private static double[]? Thickness(string text)
    {
        string[] parts = text.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
        double[] values = new double[parts.Length];

        for (int index = 0; index < parts.Length; index++)
        {
            if (!double.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out values[index]))
            {
                return null;
            }
        }

        return values.Length is 1 or 2 or 4 ? values : null;
    }

    private static string Local(string name) => name[(name.LastIndexOf('.') + 1)..];

    // A style is a glyph's when it sets the icon font, or is based on a style that does: its sizes follow W22, not the ramp.
    private static HashSet<string> GlyphStyles(IEnumerable<XDocument> documents)
    {
        List<XElement> styles = [.. documents.SelectMany(document => document.Descendants()).Where(element => element.Name.LocalName == "Style" && Key(element) is not null)];
        HashSet<string> glyph = new(styles.Where(style => FontOf(style) is { } font && IsIconFont(font)).Select(style => Key(style)!), StringComparer.Ordinal);

        for (bool grew = true; grew;)
        {
            grew = false;

            // A style based on a glyph's that sets a text font of its own, such as a toolbar toggle with a count, is not a glyph's.
            foreach (XElement style in styles.Where(style => !glyph.Contains(Key(style)!) && FontOf(style) is null
                && Referenced((string?)style.Attribute("BasedOn")) is { } basis && glyph.Contains(basis)))
            {
                grew |= glyph.Add(Key(style)!);
            }
        }

        return glyph;
    }

    private static bool IsGlyphElement(XElement element, HashSet<string> glyphStyles) =>
        IsIconFont((string?)element.Attribute("FontFamily")) || (Referenced((string?)element.Attribute("Style")) is { } style && glyphStyles.Contains(style));

    // A setter in a style, or in one of its triggers, belongs to a glyph when the style is a glyph's or sets the icon font.
    private static bool IsGlyphSetter(XElement setter, HashSet<string> glyphStyles) =>
        setter.Ancestors().FirstOrDefault(each => each.Name.LocalName == "Style") is { } style
        && ((Key(style) is { } key && glyphStyles.Contains(key))
            || (FontOf(style) is { } font ? IsIconFont(font)
                : Referenced((string?)style.Attribute("BasedOn")) is { } basis && glyphStyles.Contains(basis)));

    private static string? FontOf(XElement style) =>
        (string?)style.Elements().FirstOrDefault(setter => setter.Name.LocalName == "Setter"
            && Local((string?)setter.Attribute("Property") ?? string.Empty) == "FontFamily")?.Attribute("Value");

    private static bool IsIconFont(string? value) =>
        value is not null && (value.Contains("SymbolThemeFontFamily", StringComparison.Ordinal) || value.Contains("Icons", StringComparison.Ordinal)
            || value.Contains("MDL2", StringComparison.Ordinal));

    private static string? Key(XElement element) => (string?)element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "Key");

    private static string? Referenced(string? markup)
    {
        Match match = Regex.Match(markup ?? string.Empty, @"^\{(?:StaticResource|DynamicResource)\s+(?:ResourceKey=)?([\w.]+)\}$");

        return match.Success ? match.Groups[1].Value : null;
    }

    private static IReadOnlyList<Allowance> AllowList() =>
        [.. Lines(Path.Combine(ConventionsRoot(), "AllowList.txt")).Select(Allowance.Parse)];

    private static Dictionary<(string Rule, string Value), int> Exemptions(string file)
    {
        string path = Path.Combine(ConventionsRoot(), "Exemptions", file + ".txt");
        Dictionary<(string Rule, string Value), int> exempt = [];

        if (!File.Exists(path))
        {
            return exempt;
        }

        foreach (string line in Lines(path))
        {
            string[] parts = line.Split('|', StringSplitOptions.TrimEntries);
            exempt[(parts[0], parts[1])] = int.Parse(parts[2], CultureInfo.InvariantCulture);
        }

        return exempt;
    }

    private static IEnumerable<string> ExemptedFiles()
    {
        string root = Path.Combine(ConventionsRoot(), "Exemptions");

        return Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "*.txt", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(root, path)[..^4].Replace('\\', '/'))
            : [];
    }

    private static IEnumerable<string> Lines(string path) =>
        File.ReadAllLines(path).Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith('#'));

    private static IEnumerable<string> SourceFiles(string pattern) =>
        Directory.EnumerateFiles(AppRoot(), pattern, SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(AppRoot(), path).Replace('\\', '/'))
            .Where(path => !path.StartsWith("bin/", StringComparison.Ordinal) && !path.StartsWith("obj/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);

    private static string AppRoot() => Path.Combine(RepositoryRoot(), "src", "Theseus.App");

    private static string ConventionsRoot() => Path.Combine(RepositoryRoot(), "tests", "Theseus.Tests", "Conventions");

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
