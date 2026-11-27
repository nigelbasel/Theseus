using System.Windows.Controls;
using Theseus.App.Views;

namespace Theseus.Tests.Repository;

/// <summary>
/// The view's Ctrl+Z keeps working after a banner's text is clicked (1.3e): only a text box the owner can type in keeps the
/// key, and a read-only one, such as the clear-up notice's selectable text, lets it through to Undo.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class OutsideTextBoxCommandTests
{
    [Test]
    public void ATextBoxTheOwnerTypesIn_KeepsTheKey()
    {
        TextBox box = new();
        StackPanel view = new() { Children = { box } };

        Assert.That(OutsideTextBoxCommand.KeepsTheKey(box, view), Is.True);
    }

    [Test]
    public void AReadOnlyBox_SuchAsABannersText_LetsTheKeyThrough()
    {
        TextBox box = new() { IsReadOnly = true };
        StackPanel view = new() { Children = { box } };

        Assert.That(OutsideTextBoxCommand.KeepsTheKey(box, view), Is.False);
    }

    [Test]
    public void ATextBoxOutsideTheView_DoesNotKeepIt()
    {
        TextBox box = new();

        Assert.That(OutsideTextBoxCommand.KeepsTheKey(box, new StackPanel()), Is.False);
    }
}
