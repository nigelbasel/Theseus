using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Media;
using NUnit.Framework;

namespace Theseus.Tests.Accessibility;

/// <summary>
/// The UI Automation peer tree of a window or view, walked whole, and what a screen reader would announce wrongly in it
/// (baseline W51, E16; checklist G165, G167, G170, G172): a control that needs a name and has none, or a name that is a
/// .NET type name or a record's ToString.
/// </summary>
internal static class AutomationTree
{
    private static readonly HashSet<AutomationControlType> NeedAName =
    [
        AutomationControlType.Button, AutomationControlType.ListItem, AutomationControlType.TreeItem, AutomationControlType.DataItem,
        AutomationControlType.List, AutomationControlType.Tree, AutomationControlType.CheckBox, AutomationControlType.RadioButton,
        AutomationControlType.Hyperlink, AutomationControlType.TabItem, AutomationControlType.Thumb, AutomationControlType.ProgressBar,
    ];

    /// <summary>Each peer under <paramref name="root"/> that is shown, as its control type and name, in tree order.</summary>
    public static IReadOnlyList<(AutomationControlType Type, string Name, string Where)> Walk(UIElement root) =>
        [.. Peers(root).Select(peer => (peer.Type, peer.Name, peer.Where))];

    // Each shown peer, and whether it is in the control view, the one a screen reader reads by default.
    private static IReadOnlyList<(AutomationControlType Type, string Name, string Where, bool Control)> Peers(UIElement root)
    {
        List<(AutomationControlType, string, string, bool)> found = [];

        foreach (AutomationPeer peer in TopPeers(root))
        {
            Walk(peer, Describe(root) + " > " + Describe(peer), found);
        }

        Assert.That(found, Is.Not.Empty, $"the walk under {Describe(root)} found no peer");

        return found;
    }

    /// <summary>What is wrong in the tree under <paramref name="root"/>, one line per peer; empty when nothing is.</summary>
    public static IReadOnlyList<string> Problems(UIElement root) =>
        [.. Peers(root).Where(peer => peer.Control).Select(peer => Problem((peer.Type, peer.Name, peer.Where))).Where(problem => problem is not null).Select(problem => problem!)];

    private static string? Problem((AutomationControlType Type, string Name, string Where) peer)
    {
        if (peer.Name.StartsWith("Theseus.", StringComparison.Ordinal) || peer.Name.Contains(" { ", StringComparison.Ordinal))
        {
            return $"{peer.Type} at {peer.Where} is named by its type: \"{Trim(peer.Name)}\"";
        }

        // An icon-font character is read as an unknown symbol (E10, NE-4): a glyph is drawn, never spoken.
        if (peer.Name.Any(character => character is >= '' and <= ''))
        {
            return $"{peer.Type} at {peer.Where} has an icon glyph in its name: \"{Trim(peer.Name)}\"";
        }

        return NeedAName.Contains(peer.Type) && string.IsNullOrWhiteSpace(peer.Name) ? $"{peer.Type} at {peer.Where} has no name" : null;
    }

    // The root's own peer, or, for a panel, which has none, the peers of the topmost elements under it, as a window's
    // peer would list them. A window built but never shown has no visual children of its own, so a test starts at its content.
    private static IEnumerable<AutomationPeer> TopPeers(DependencyObject element)
    {
        if (element is UIElement shown && UIElementAutomationPeer.CreatePeerForElement(shown) is { } peer)
        {
            yield return peer;
            yield break;
        }

        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
        {
            foreach (AutomationPeer inner in TopPeers(VisualTreeHelper.GetChild(element, index)))
            {
                yield return inner;
            }
        }
    }

    private static void Walk(AutomationPeer? peer, string where, List<(AutomationControlType, string, string, bool)> found)
    {
        if (peer is null || !Shown(peer))
        {
            return;
        }

        // The Fluent text box's clear button is a part of Microsoft's template, named by its glyph in every Fluent app.
        if (Owner(peer) is FrameworkElement { Name: "DeleteButton", TemplatedParent: System.Windows.Controls.TextBox })
        {
            return;
        }

        AutomationControlType type = peer.GetAutomationControlType();
        found.Add((type, peer.GetName() ?? string.Empty, where, IsControlElement(peer)));

        // A scroll bar's line and page buttons are the framework's, read by their role, as in every WPF app.

        if (type == AutomationControlType.ScrollBar)
        {
            return;
        }

        foreach (AutomationPeer child in peer.GetChildren() ?? [])
        {
            Walk(child, where + " > " + Describe(child), found);
        }
    }

    // Whether a screen reader's default view reads the peer. WPF answers false for anything not visible, and a window built
    // but never shown is not, so for those this asks what WPF would answer once shown: icon glyphs and a template's own
    // text are left out, everything else is read.
    private static bool IsControlElement(AutomationPeer peer) => Owner(peer) switch
    {
        UIElement { IsVisible: true } => peer.IsControlElement(),
        _ when peer is Theseus.App.Controls.GlyphTextAutomationPeer => false,
        FrameworkElement { TemplatedParent: not null } when peer is TextBlockAutomationPeer => false,
        _ => true,
    };

    // Laid out off screen, so IsVisible is false throughout: shown means nothing above the peer's element is collapsed or
    // hidden. A window never shown is Collapsed itself, and its laid-out content is still what it would show.
    private static bool Shown(AutomationPeer peer)
    {
        DependencyObject? element = Owner(peer);

        for (DependencyObject? at = element; at is not null; at = VisualTreeHelper.GetParent(at))
        {
            if (at is UIElement { Visibility: not Visibility.Visible } and not Window)
            {
                return false;
            }
        }

        return true;
    }

    private static DependencyObject? Owner(AutomationPeer peer) => peer switch
    {
        UIElementAutomationPeer element => element.Owner,
        ItemAutomationPeer item => (item.ItemsControlAutomationPeer?.Owner as System.Windows.Controls.ItemsControl)?.ItemContainerGenerator
            .ContainerFromItem(item.Item),
        _ => null,
    };

    private static string Describe(AutomationPeer peer) => Owner(peer) switch
    {
        FrameworkElement { Name.Length: > 0 } named => $"{named.GetType().Name} {named.Name}",
        System.Windows.Controls.ItemsControl list when System.Windows.Data.BindingOperations.GetBindingExpression(list,
            System.Windows.Controls.ItemsControl.ItemsSourceProperty) is { } source => $"{list.GetType().Name} of {source.ParentBinding.Path?.Path}",
        _ => peer.GetClassName(),
    };

    private static string Describe(UIElement element) =>
        element is FrameworkElement { Name.Length: > 0 } named ? $"{named.GetType().Name} {named.Name}" : element.GetType().Name;

    private static string Trim(string name) => name.Length > 90 ? name[..90] + "…" : name;
}
