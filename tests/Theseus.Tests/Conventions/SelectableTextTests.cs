using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Theseus.Tests.Conventions;

/// <summary>
/// Banner text can be selected and copied and still looks as it did (Nigel, 2026-11-27, 1.3e): the shared SelectableText
/// style draws a read-only text box as the TextBlock it replaces, in the light, dark and contrast
/// colours, and offers Copy and Select all on its right-click menu.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
public class SelectableTextTests
{
    private const string Sentence = "Round 2 began on 27 November: the owner changed the plan, so objectives 1.3 and 1.4 start again.";

    [Test]
    public void TheStyle_IsReadOnly_Borderless_Transparent_AndWraps()
    {
        TextBox box = new() { Style = SelectableText(), Text = Sentence };

        Assert.Multiple(() =>
        {
            Assert.That(box.IsReadOnly, Is.True);
            Assert.That(box.IsReadOnlyCaretVisible, Is.False);
            Assert.That(box.IsInactiveSelectionHighlightEnabled, Is.True, "a selection stays shown with the window behind another");
            Assert.That(box.BorderThickness, Is.EqualTo(new Thickness(0)));
            Assert.That(box.Background, Is.EqualTo(Brushes.Transparent));
            Assert.That(box.TextWrapping, Is.EqualTo(TextWrapping.Wrap));
            Assert.That(box.Focusable && box.IsTabStop, Is.True, "the keyboard reaches it, to select with Shift and the arrows");
        });
    }

    [Test]
    public void AnEmptyBox_IsPassedByOnTab()
    {
        TextBox box = new() { Style = SelectableText() };

        Assert.That(box.IsTabStop, Is.False);
    }

    [Test]
    public void ItsRightClickMenu_OffersCopyAndSelectAll_OneMenuToEachBox()
    {
        TextBox first = new() { Style = SelectableText(), Text = Sentence };
        TextBox second = new() { Style = SelectableText(), Text = Sentence };
        StackPanel both = new() { Children = { first, second } };
        Window window = Host(both, Shared(), null);

        try
        {
            Assert.That(first.ContextMenu!.Items.OfType<MenuItem>().Select(item => (item.Header, item.Command)),
                Is.EqualTo(new (object, ICommand)[] { ("_Copy", ApplicationCommands.Copy), ("Select _all", ApplicationCommands.SelectAll) }));
            Assert.That(second.ContextMenu, Is.Not.SameAs(first.ContextMenu), "a shared menu belongs to the first thread that built it");
        }
        finally
        {
            window.Close();
        }
    }

    [Test]
    public void SelectAllThenCopy_ReachTheBox_TheMenuSendsThemTo()
    {
        TextBox box = new() { Style = SelectableText(), Text = Sentence };
        Window window = Host(box, Shared(), null);

        try
        {
            ApplicationCommands.SelectAll.Execute(null, box);

            Assert.That(box.SelectedText, Is.EqualTo(Sentence));
            Assert.That(ApplicationCommands.Copy.CanExecute(null, box), Is.True, "a read-only box still copies");
            Assert.That(ApplicationCommands.Cut.CanExecute(null, box), Is.False, "and nothing can be cut out of a banner");
        }
        finally
        {
            window.Close();
        }
    }

    [TestCase("StateColours.Light.xaml", "ChangeBrush", 14, true)]
    [TestCase("StateColours.Dark.xaml", "ChangeBrush", 14, true)]
    [TestCase("StateColours.HighContrast.xaml", "ChangeBrush", 14, true)]
    [TestCase("StateColours.HighContrast.xaml", "TextFillColorPrimaryBrush", 14, false)]
    [TestCase("StateColours.HighContrast.xaml", "CriticalTextBrush", 14, false)]
    [TestCase("StateColours.Light.xaml", "TextFillColorSecondaryBrush", 12, false)]
    public void TheBox_DrawsItsTextAsTheTextBlockItReplaces_SameLinesSamePlaceSameColour(string colours, string brush, double size, bool semibold)
    {
        ResourceDictionary resources = Resources(colours);
        FontWeight weight = semibold ? FontWeights.SemiBold : FontWeights.Normal;

        TextBlock block = new() { Text = Sentence, TextWrapping = TextWrapping.Wrap, FontSize = size, FontWeight = weight };
        block.SetResourceReference(TextBlock.ForegroundProperty, brush);
        TextBox box = new() { Style = SelectableText(), Text = Sentence, FontSize = size, FontWeight = weight };
        box.SetResourceReference(Control.ForegroundProperty, brush);

        byte[] drawn = Pixels(block, resources, out Size blockSize);
        byte[] selectable = Pixels(box, resources, out Size boxSize);

        Assert.That(boxSize, Is.EqualTo(blockSize), "the same lines, the same height");
        // Anti-aliasing may round a glyph's edge by a step or two in 255; a moved, rewrapped or recoloured glyph differs by far more.
        Assert.That(LargestDifference(drawn, selectable), Is.LessThanOrEqualTo(2), "no pixel visibly changes");
    }

    [Test]
    public void TheComparison_CatchesTextMovedByOnePixel()
    {
        ResourceDictionary resources = Resources("StateColours.Light.xaml");
        TextBlock block = new() { Text = Sentence, TextWrapping = TextWrapping.Wrap, FontSize = 14 };
        TextBlock moved = new() { Text = Sentence, TextWrapping = TextWrapping.Wrap, FontSize = 14, Padding = new Thickness(1, 0, 0, 0) };

        Assert.That(LargestDifference(Pixels(block, resources, out _), Pixels(moved, resources, out _)), Is.GreaterThan(64));
    }

    [Test]
    public void WithNoBrushOfItsOwn_TheBoxTakesItsParentsTextBrush_AsATextBlockInheritsIt()
    {
        SolidColorBrush onAccent = new(Colors.White);
        TextBox box = new() { Style = SelectableText(), Text = Sentence };
        Border bar = new() { Child = box };
        TextElement.SetForeground(bar, onAccent);
        Window window = Host(bar, Shared(), null);

        try
        {
            Assert.That(box.Foreground, Is.SameAs(onAccent));
        }
        finally
        {
            window.Close();
        }
    }

    internal static Style SelectableText() => (Style)Shared()["SelectableText"];

    internal static ResourceDictionary Shared() => LooseXaml.Dictionary("Themes", "Interactive.xaml");

    private static ResourceDictionary Resources(string colours)
    {
        ResourceDictionary resources = new();
        resources.MergedDictionaries.Add(Shared());
        resources.MergedDictionaries.Add(LooseXaml.Dictionary("Themes", colours));

        // The Fluent theme's own text brushes, which the fixture does not load: any fixed colour shows the box follows the key.
        resources["TextFillColorPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0x1a, 0x1a, 0x1a));
        resources["TextFillColorSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0x5d, 0x5d, 0x5d));

        return resources;
    }

    private static byte[] Pixels(FrameworkElement content, ResourceDictionary resources, out Size size)
    {
        Border frame = new() { Width = 320, Background = Brushes.White, Child = content };
        Window window = Host(frame, resources, 320);

        try
        {
            size = new Size(content.ActualWidth, content.ActualHeight);
            RenderTargetBitmap bitmap = new((int)frame.ActualWidth, (int)Math.Ceiling(frame.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(frame);
            byte[] pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);

            return pixels;
        }
        finally
        {
            window.Close();
        }
    }

    private static int LargestDifference(byte[] first, byte[] second) =>
        first.Length != second.Length ? int.MaxValue : Enumerable.Range(0, first.Length).Max(index => Math.Abs(first[index] - second[index]));

    internal static Window Host(FrameworkElement content, ResourceDictionary resources, double? width)
    {
        Window window = new()
        {
            Content = content, Resources = resources, Left = -30000, Top = 0, Width = width + 40 ?? 400, SizeToContent = SizeToContent.Height,
            ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None, WindowStartupLocation = WindowStartupLocation.Manual,
        };
        window.Show();
        window.UpdateLayout();

        return window;
    }
}
