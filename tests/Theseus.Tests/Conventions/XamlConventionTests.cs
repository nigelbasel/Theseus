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
    public void TheSharedStylesAndApp_AreClean_SoHaveNoExemptionFile()
    {
        Assert.That(ExemptedFiles(), Has.None.EqualTo("App.xaml").And.None.EqualTo("Themes/Interactive.xaml"), "WB1 cleaned both");
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
            </Grid>
            """);

        List<Finding> findings = [.. Findings(sample, "Sample.xaml", [])];

        Assert.That(findings.Select(finding => (finding.Rule, finding.Value, finding.Glyph)), Is.EquivalentTo(new[]
        {
            ("FontSize", "11", false), ("Spacing", "0,14,0,4", false), ("Colour", "#D68910", false),
            ("Font", "Segoe MDL2 Assets", false), ("FontSize", "8", true), ("Colour", "White", false), ("Spacing", "6,4", false),
            ("Spacing", "0,1,0,0", false), ("Colour", "Transparent", false),
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
                string? value = (string?)element.Attribute("Value")
                    ?? element.Elements().FirstOrDefault(child => child.Name.LocalName == "Setter.Value")?.Elements().FirstOrDefault(child => child.Name.LocalName == "Thickness")?.Value;

                if (value is not null)
                {
                    foreach (Finding finding in Check(property, value, IsGlyphSetter(element, glyphStyles), stateColours))
                    {
                        yield return finding;
                    }
                }

                continue;
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
        HashSet<string> glyph = new(styles.Where(style => style.Elements().Any(setter => setter.Name.LocalName == "Setter"
            && Local((string?)setter.Attribute("Property") ?? string.Empty) == "FontFamily" && IsIconFont((string?)setter.Attribute("Value")))).Select(style => Key(style)!), StringComparer.Ordinal);

        for (bool grew = true; grew;)
        {
            grew = false;

            foreach (XElement style in styles.Where(style => !glyph.Contains(Key(style)!) && Referenced((string?)style.Attribute("BasedOn")) is { } basis && glyph.Contains(basis)))
            {
                grew |= glyph.Add(Key(style)!);
            }
        }

        return glyph;
    }

    private static bool IsGlyphElement(XElement element, HashSet<string> glyphStyles) =>
        IsIconFont((string?)element.Attribute("FontFamily")) || (Referenced((string?)element.Attribute("Style")) is { } style && glyphStyles.Contains(style));

    private static bool IsGlyphSetter(XElement setter, HashSet<string> glyphStyles) =>
        setter.Parent is { } style && style.Name.LocalName == "Style"
        && ((Key(style) is { } key && glyphStyles.Contains(key))
            || (Referenced((string?)style.Attribute("BasedOn")) is { } basis && glyphStyles.Contains(basis))
            || style.Elements().Any(each => each.Name.LocalName == "Setter" && Local((string?)each.Attribute("Property") ?? string.Empty) == "FontFamily"
                && IsIconFont((string?)each.Attribute("Value"))));

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
