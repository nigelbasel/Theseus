using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Theseus.App.Controls;
using Theseus.App.ViewModels;
using Theseus.App.Views;
using Theseus.Tests.Conventions;
using Theseus.Tests.Projects;
using Theseus.Tests.ViewModels;
using static Theseus.Tests.Projects.ClarityFixtures;

namespace Theseus.Tests.Windows;

/// <summary>
/// Two banners as WPF builds them from the app's own markup (Nigel, 2026-11-27, 1.3e): the project view's change banner and
/// the board's filtered line hold their text in selectable boxes, and their Copy takes all of it. Set
/// THESEUS_BANNER_SCREENSHOTS to a folder to keep pictures of the change banner, for the record of the change.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
public class BannerLayoutTests
{
    private TestProjectsFolder _folder = null!;
    private ProjectViews _views = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = TestProjectsFolder.Create();
        _views = new ProjectViews(_folder);
    }

    [TearDown]
    public void TearDown() => _folder.Dispose();

    [Test]
    public void TheChangeBanner_HoldsBothLinesSelectable_AndCopiesThemBoth()
    {
        RoundViewModel round = ProjectViewModel.Build(WriteBym(_folder), BymPieces(_folder),
            ProjectViewFixtures.LinksOn(BymLinks(), BymBoard()), [], _views.Settings, _views.Actions).Rounds[1];
        DataTemplate template = (DataTemplate)new ProjectView().Resources[new DataTemplateKey(typeof(RoundViewModel))];
        ContentControl content = new() { Content = round, ContentTemplate = template, Width = 760 };
        ResourceDictionary resources = new();
        resources.MergedDictionaries.Add(LooseXaml.Dictionary("Themes", "StateColours.Light.xaml"));
        Window window = SelectableTextTests.Host(new Border { Background = Brushes.White, Padding = new Thickness(16), Child = content }, resources, 800);

        try
        {
            Border banner = Descendants<Border>(content).Single(border => border.Name == "ChangeBanner");
            List<TextBox> lines = [.. Descendants<TextBox>(banner)];

            Assert.Multiple(() =>
            {
                Assert.That(lines.Select(line => line.Text), Is.EqualTo(new[] { round.Banner, round.BannerObjectives }));
                Assert.That(lines.Select(line => line.IsReadOnly), Has.All.True);
                Assert.That(Banner.TextOf(banner), Is.EqualTo(round.Banner + Environment.NewLine + round.BannerObjectives));
                Assert.That(Descendants<Button>(banner).Single().Name, Is.EqualTo("CopyBanner"));
            });

            Keep(banner, "change-banner.png");

            lines[0].Focus();
            lines[0].Select(lines[0].Text.IndexOf("The tool", StringComparison.Ordinal), "The tool catalogue".Length);
            window.UpdateLayout();
            Assert.That(lines[0].SelectedText, Is.EqualTo("The tool catalogue"), "part of a line can be selected");
        }
        finally
        {
            window.Close();
        }
    }

    [Test]
    public void TheFilteredLine_IsSelectable_AndCopiesWhatItSays() =>
        MainWindowFixture.Run((window, shell) =>
        {
            shell.Board.Search.Text = "bym";
            FrameworkElement content = (FrameworkElement)window.Content;
            content.UpdateLayout();
            Border line = (Border)window.FindName("FilteredLine");

            Assert.That(line.Visibility, Is.EqualTo(Visibility.Visible));
            Assert.That(Banner.TextOf(line), Is.EqualTo(shell.Board.Search.FilteredLine).And.Not.Empty);
        }, board => board.With("bym-w7").Retitle("bym-w7", "BYM MCP W7 - GUI parity").With("gdpr").Retitle("gdpr", "GDPR whitepaper"));

    private static void Keep(FrameworkElement element, string name)
    {
        if (Environment.GetEnvironmentVariable("THESEUS_BANNER_SCREENSHOTS") is not { Length: > 0 } folder)
        {
            return;
        }

        RenderTargetBitmap bitmap = new((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        PngBitmapEncoder png = new();
        png.Frames.Add(BitmapFrame.Create(bitmap));

        using FileStream file = File.Create(Path.Combine(folder, name));
        png.Save(file);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);

            if (child is T match)
            {
                yield return match;
            }

            foreach (T deeper in Descendants<T>(child))
            {
                yield return deeper;
            }
        }
    }
}
