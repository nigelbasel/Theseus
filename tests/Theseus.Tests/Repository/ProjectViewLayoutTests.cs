using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Theseus.App.ViewModels;
using Theseus.App.Views;
using Theseus.Core.Domain;
using Theseus.Core.Projects;
using static Theseus.Tests.Projects.ProjectCards;

namespace Theseus.Tests.Repository;

/// <summary>
/// The project view's own control, laid out as WPF lays it out (method rule P11): the breadcrumb, the tree, a row per
/// objective with workstreams across seven columns, and Waiting on you shown only where a spec waits (AC 14, 15).
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class ProjectViewLayoutTests
{
    private static ProjectViewModel View()
    {
        var reading = ProjectFileReader.Read("p", """
            # P

            **Kit:** method-v0.2
            **Primary branch:** owner/p

            ## Phase 0: Seed

            ### Objective 0.1: First
            - [ ] One.

            **Pipeline:** feature
            | Workstream | Slug | Branch | Repos |
            |---|---|---|---|
            | Waiting work | spec-slug | owner/spec-slug | Repo |
            | Early work | early-slug | owner/early-slug | Repo |
            """);

        var source = new WorkstreamSource("owner/x", "abcdef1234567", false);
        reading = reading with
        {
            Artefacts = new Dictionary<string, WorkstreamArtefacts>
            {
                ["spec-slug"] = new("spec-slug", source, new HashSet<string> { "spec.md" })
                {
                    SpecText = "# Spec", SpecHash = "h", SpecCommit = "abcdef1234567",
                },
                ["early-slug"] = new("early-slug", source, new HashSet<string> { "research.md" }),
            },
        };

        return ProjectViewModel.Build(reading, [new WorkstreamCardViewModel(Card("ad-hoc"))], (_, _) => null, (Workstream _) => false,
            _ => { }, _ => { }, null);
    }

    private static ProjectView LaidOut(ProjectViewModel view)
    {
        var control = new ProjectView { DataContext = view, Width = 1400, Height = 800 };

        control.Measure(new Size(1400, 800));
        control.Arrange(new Rect(0, 0, 1400, 800));
        control.UpdateLayout();

        return control;
    }

    [Test]
    public void TheBreadcrumbTreeAndColumns_AreLaidOut()
    {
        var control = LaidOut(View());

        Assert.That(((Button)control.FindName("BoardCrumb")).Content, Is.EqualTo("Board"));
        Assert.That(Descendants<TextBlock>(control).Any(text => text.Text == "P" && text.Visibility == Visibility.Visible), Is.True);
        Assert.That(Descendants<TreeViewItem>(control).Count(), Is.GreaterThanOrEqualTo(4), "phase, objective, two slugs");
        Assert.That(((ItemsControl)control.FindName("Headings")).Items, Has.Count.EqualTo(7));
        Assert.That(((ItemsControl)control.FindName("RowList")).Items, Has.Count.EqualTo(1));
    }

    [Test]
    public void WaitingOnYou_IsShownOnlyOnTheWaitingSpec_WithItsButtons()
    {
        var control = LaidOut(View());

        var shown = Descendants<Border>(control).Where(border => border.Name == "WaitingOnYou" && border.Visibility == Visibility.Visible).ToList();

        Assert.That(shown, Has.Count.EqualTo(1));
        Assert.That(Descendants<Button>(shown[0]).Select(button => button.Content as string),
            Is.EqualTo(new[] { "Open spec", "Accept spec…" }));
        Assert.That(Descendants<Button>(shown[0]).All(button => button.IsEnabled), Is.True);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);

            if (child is T match)
            {
                yield return match;
            }

            foreach (var deeper in Descendants<T>(child))
            {
                yield return deeper;
            }
        }
    }
}
