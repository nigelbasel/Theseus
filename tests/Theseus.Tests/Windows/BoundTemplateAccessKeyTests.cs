using System.Windows;
using System.Windows.Controls;
using Theseus.App;

namespace Theseus.Tests.Windows;

/// <summary>
/// The button templates whose content can be bound recognise access keys, now that every bound label is escaped
/// (baseline section 8, WB1 D-6 and D-12; findings B1, B12; checklist G81, G82): a literal "_" marks the key, and a
/// name's own underscore is doubled at the binding, so it still shows.
/// </summary>
[TestFixture]
[NonParallelizable]
public class BoundTemplateAccessKeyTests
{
    [TestCase("ToolButton")]
    [TestCase("ChipButton")]
    [TestCase("TagChipButton")]
    public void TheTemplate_RecognisesAccessKeys(string style) =>
        MainWindowFixture.Run((window, _) =>
        {
            Button button = new() { Style = (Style)window.FindResource(style), Content = "_Open" };
            Border host = new() { Child = button };
            host.Measure(new Size(200, 40));
            host.Arrange(new Rect(0, 0, 200, 40));
            button.ApplyTemplate();

            ContentPresenter label = (ContentPresenter)button.Template.FindName("Label", button);

            Assert.That(label.RecognizesAccessKey, Is.True);
        });

    [Test]
    public void ATagsLabel_IsEscapedWhereItIsBound() =>
        MainWindowFixture.Run(
            (window, _) =>
            {
                Button chip = MainWindowFixtureTests.Descendants<Button>((DependencyObject)window.Content)
                    .First(button => button.Style == window.FindResource("TagChipButton"));
                System.Windows.Data.Binding binding = System.Windows.Data.BindingOperations.GetBinding(chip, ContentControl.ContentProperty)!;

                Assert.That(binding.Converter, Is.SameAs(window.FindResource("AccessKeyEscape")));
            },
            board => board.With("alpha").Tag("alpha", "security"));
}
