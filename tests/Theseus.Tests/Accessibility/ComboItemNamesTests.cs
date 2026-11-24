using Theseus.App.ViewModels;


namespace Theseus.Tests.Accessibility;

/// <summary>
/// A combo box's items are read by their ToString, which DisplayMemberPath does not change (W50, G170): a record's
/// default would read "ProjectOption { Name = … }" to a screen reader, so each says what the box shows.
/// </summary>
[TestFixture]
public class ComboItemNamesTests
{
    [Test]
    public void AProjectOption_IsReadAsItsTitle() =>
        Assert.That(new ProjectOption("theseus", "Theseus", [("1.9", "Windows conventions")]).ToString(), Is.EqualTo("Theseus"));

    [Test]
    public void AThemeChoice_IsReadAsItsLabel() =>
        Assert.That(new ThemeChoice(Theseus.Core.Settings.AppTheme.Dark, "Dark").ToString(), Is.EqualTo("Dark"));
}
