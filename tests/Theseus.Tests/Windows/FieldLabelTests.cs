using System.Windows;
using System.Windows.Automation;
using Theseus.App;

namespace Theseus.Tests.Windows;

/// <summary>
/// The drawer's fields are linked to the labels shown above them, so a screen reader says the label when a field takes
/// focus (baseline WB7; finding E12; W50; checklist G168).
/// </summary>
[TestFixture]
[NonParallelizable]
public class FieldLabelTests
{
    [TestCase("DrawerTitle", "DrawerTitleLabel")]
    [TestCase("LinkKind", "LinkKindLabel")]
    [TestCase("LinkDocument", "LinkDocumentLabel")]
    public void AField_IsLabelledByItsVisibleLabel(string field, string label) =>
        MainWindowFixture.Run((window, _) =>
            Assert.That(AutomationProperties.GetLabeledBy((DependencyObject)window.FindName(field)), Is.SameAs(window.FindName(label))));

    [Test]
    public void TheOtherCardsList_IsNamed() =>
        MainWindowFixture.Run((window, _) =>
            Assert.That(AutomationProperties.GetName((DependencyObject)window.FindName("LinkTarget")), Is.EqualTo("The other card")));
}
