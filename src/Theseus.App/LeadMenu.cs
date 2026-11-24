using System.Windows.Controls;
using Theseus.App.Text;

namespace Theseus.App;

/// <summary>The items of Project properties' lead chooser: one choice of a set, as radio items (W32, C12, C-m3).</summary>
internal static class LeadMenu
{
    /// <summary>One radio item per card, the lead marked, each title escaped so its underscores show.</summary>
    /// <param name="leads">Each card's key and what it is called.</param>
    /// <param name="current">The lead's key now.</param>
    /// <param name="choose">Makes the clicked card the lead.</param>
    public static IReadOnlyList<MenuItem> Items(IEnumerable<KeyValuePair<string, string>> leads, string? current, Action<string> choose)
    {
        List<MenuItem> items = [];

        foreach ((string key, string shown) in leads)
        {
            MenuItem item = new()
            {
                Header = AccessKeyText.Escape(shown),
                IsChecked = string.Equals(key, current, StringComparison.OrdinalIgnoreCase),
            };
            item.SetResourceReference(Control.TemplateProperty, "CompactRadioItem");
            item.Click += (_, _) => choose(key);
            items.Add(item);
        }

        return items;
    }
}
