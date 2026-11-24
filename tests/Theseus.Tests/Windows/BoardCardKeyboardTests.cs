using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Theseus.App;
using Theseus.App.ViewModels;

namespace Theseus.Tests.Windows;

/// <summary>
/// Board cards from the keyboard (baseline WB7; W44; findings B4, B15): a card face is a Tab stop, Space selects it alone,
/// Ctrl+Space adds or removes it, and Enter opens Properties; the board surface itself is not a Tab stop, and Esc still
/// clears the selection. The keys are driven through their bindings, never by moving the machine's keyboard focus.
/// </summary>
[TestFixture]
[NonParallelizable]
public class BoardCardKeyboardTests
{
    private static Border Face(MainWindow window, string name, string title)
    {
        FrameworkElement board = (FrameworkElement)window.FindName("BoardSurface");

        return MainWindowFixtureTests.Descendants<Border>(board)
            .First(border => border.Name == name && MainWindowFixtureTests.Descendants<TextBlock>(border).Any(text => text.Text == title));
    }

    private static void Press(UIElement element, Key key, ModifierKeys modifiers)
    {
        KeyBinding binding = element.InputBindings.OfType<KeyBinding>().FirstOrDefault(candidate =>
                candidate.Gesture is KeyGesture gesture && gesture.Key == key && gesture.Modifiers == modifiers)
            ?? throw new AssertionException($"{modifiers}+{key} is not bound on {element}");

        Assert.That(binding.Command, Is.Not.Null, $"{modifiers}+{key} has a command");
        Assert.That(binding.Command.CanExecute(binding.CommandParameter), Is.True, $"{modifiers}+{key} can run");
        binding.Command.Execute(binding.CommandParameter);
    }

    [Test]
    public void ACardFace_IsFocusable() =>
        MainWindowFixture.Run(
            (window, _) => Assert.That(Face(window, "CardFace", "alpha").Focusable, Is.True, "a card is a Tab stop (G64)"),
            board => board.With("alpha"));

    [Test]
    public void SpaceOnACard_SelectsItAlone() =>
        MainWindowFixture.Run(
            (window, shell) =>
            {
                WorkstreamCardViewModel beta = shell.Board.Cards.Single(card => card.Key == "beta");
                beta.IsSelected = true;

                Press(Face(window, "CardFace", "alpha"), Key.Space, ModifierKeys.None);

                Assert.That(shell.Board.ChosenCards.Select(card => card.Key), Is.EqualTo(new[] { "alpha" }));
            },
            board => board.With("alpha").With("beta"));

    [Test]
    public void CtrlSpaceOnACard_AddsItToTheSelection() =>
        MainWindowFixture.Run(
            (window, shell) =>
            {
                shell.Board.Cards.Single(card => card.Key == "beta").IsSelected = true;

                Press(Face(window, "CardFace", "alpha"), Key.Space, ModifierKeys.Control);

                Assert.That(shell.Board.ChosenCards.Select(card => card.Key), Is.EquivalentTo(new[] { "alpha", "beta" }));
            },
            board => board.With("alpha").With("beta"));

    [Test]
    public void EnterOnACard_OpensItsProperties() =>
        MainWindowFixture.Run(
            (window, shell) =>
            {
                WorkstreamCardViewModel alpha = shell.Board.Cards.Single(card => card.Key == "alpha");
                int opened = 0;
                alpha.PropertiesCommand = new RelayCommand(_ => opened++);
                Border face = Face(window, "CardFace", "alpha");

                foreach (InputBinding binding in face.InputBindings)
                {
                    System.Windows.Data.BindingOperations.GetBindingExpression(binding, InputBinding.CommandProperty)?.UpdateTarget();
                }

                Press(face, Key.Return, ModifierKeys.None);

                Assert.That(opened, Is.EqualTo(1));
            },
            board => board.With("alpha"));

    [Test]
    public void TheBoardSurface_IsNotATabStop_ButStaysFocusable() =>
        MainWindowFixture.Run((window, _) =>
        {
            FrameworkElement surface = (FrameworkElement)window.FindName("BoardSurface");

            Assert.That(KeyboardNavigation.GetIsTabStop(surface), Is.False, "it shows no focus, so Tab passes it (B15, G68)");
            Assert.That(surface.Focusable, Is.True, "code still focuses it after a click on empty board");
        });

    [Test]
    public void Escape_StillClearsTheSelection() =>
        MainWindowFixture.Run((window, _) =>
        {
            KeyBinding escape = window.InputBindings.OfType<KeyBinding>()
                .Single(binding => binding.Gesture is KeyGesture { Key: Key.Escape, Modifiers: ModifierKeys.None });

            Assert.That(BindingPath(escape), Is.EqualTo("ClearSelectionCommand"));
        });

    private static string? BindingPath(KeyBinding binding) =>
        System.Windows.Data.BindingOperations.GetBinding(binding, InputBinding.CommandProperty)?.Path.Path;
}
