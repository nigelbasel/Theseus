using System.Windows.Media;

namespace Theseus.Tests.Theming;

/// <summary>
/// Every text colour in the light and dark state dictionaries reaches 4.5:1 against what it is drawn on (W48, G161):
/// the page, measured against both Fluent page backgrounds of its theme; its own soft fill; a kind bar, with and
/// without the word wash; and the search highlight.
/// </summary>
[TestFixture]
public class StateColoursContrastTests
{
    private const double Required = 4.5;

    private static readonly string[] States = ["Done", "Waiting", "Busy", "Stuck", "Holdouts", "ToDo", "HandedOn"];

    private static readonly string[] Kinds = ["Spec", "Build", "Verify", "Bug", "Freeform"];

    private static readonly string[] PageText =
        ["LinkBrush", "CautionTextBrush", "SuccessTextBrush", "CriticalTextBrush", "ChangeBrush"];

    private static readonly (string File, string[] Pages)[] Themes =
    [
        ("StateColours.Light.xaml", ["#FFFFFF", "#F3F3F3"]),
        ("StateColours.Dark.xaml", ["#2B2B2B", "#202020"]),
    ];

    private static IEnumerable<TestCaseData> Pairs()
    {
        foreach (var (file, pages) in Themes)
        {
            var colours = ThemeFiles.Colours(file);

            foreach (var page in pages)
            {
                foreach (var text in States.Select(state => $"State{state}Brush").Concat(PageText))
                {
                    yield return Case(file, text, colours[text], page, ThemeFiles.Parse(page));
                }
            }

            foreach (var state in States)
            {
                yield return Case(file, $"State{state}Brush", colours[$"State{state}Brush"], $"State{state}SoftBrush",
                    colours[$"State{state}SoftBrush"]);
            }

            yield return Case(file, "ChangeBrush", colours["ChangeBrush"], "ChangeSoftBrush", colours["ChangeSoftBrush"]);
            yield return Case(file, "SearchHighlightTextBrush", colours["SearchHighlightTextBrush"], "SearchHighlightBrush",
                colours["SearchHighlightBrush"]);

            foreach (var kind in Kinds)
            {
                var fill = colours[$"Kind{kind}Brush"];

                yield return Case(file, "OnKindTextBrush", colours["OnKindTextBrush"], $"Kind{kind}Brush", fill);
                yield return Case(file, "OnKindTextBrush", colours["OnKindTextBrush"], $"Kind{kind}Brush+KindWordWashBrush",
                    ThemeFiles.Over(colours["KindWordWashBrush"], fill));
            }
        }
    }

    private static TestCaseData Case(string file, string text, Color foreground, string backgroundName, Color background) =>
        new TestCaseData(foreground, background).SetArgDisplayNames(file, text, backgroundName);

    [TestCaseSource(nameof(Pairs))]
    public void ATextColour_ReachesFourAndAHalfToOne_OnWhatItIsDrawnOn(Color foreground, Color background)
    {
        Assert.That(ThemeFiles.Contrast(foreground, background), Is.GreaterThanOrEqualTo(Required));
    }

    [Test]
    public void TheContrastRatio_IsTheWcagOne()
    {
        Assert.That(ThemeFiles.Contrast(Colors.Black, Colors.White), Is.EqualTo(21).Within(0.001));
        Assert.That(ThemeFiles.Contrast(ThemeFiles.Parse("#D68910"), Colors.White), Is.EqualTo(2.82).Within(0.01),
            "the caution literal E4 measured");
    }
}
