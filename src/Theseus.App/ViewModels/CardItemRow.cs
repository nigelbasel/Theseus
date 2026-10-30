using System.Diagnostics;

namespace Theseus.App.ViewModels;

/// <summary>One line of a card's Jira or pull request drop-down: the id and title, opening it when chosen.</summary>
public sealed class CardItemRow
{
    /// <summary>Creates a row.</summary>
    /// <param name="id">The Jira key or "PR 22174".</param>
    /// <param name="title">Its title, when a session reported one.</param>
    /// <param name="url">Where it opens, or null when the board cannot work that out.</param>
    public CardItemRow(string id, string? title, string? url)
    {
        Id = id;
        Title = title;
        Url = url;
        OpenCommand = new RelayCommand(_ => Process.Start(new ProcessStartInfo(url!) { UseShellExecute = true }), _ => url is not null);
    }

    /// <summary>The Jira key or pull request.</summary>
    public string Id { get; }

    /// <summary>Its title, or null.</summary>
    public string? Title { get; }

    /// <summary>Where it opens, or null.</summary>
    public string? Url { get; }

    /// <summary>What the drop-down shows: the id, then the title when there is one.</summary>
    public string Display => Title is null ? Id : $"{Id}   {Title}";

    /// <summary>What hovering says: its title, then where it opens or why it cannot.</summary>
    public string Tooltip => string.Join("\n", new[]
    {
        Title ?? "No title yet. A session reports it when it works on this item.",
        Url ?? "Set your Jira address in Tools > Options to open it from here.",
    });

    /// <summary>Opens it in the browser.</summary>
    public RelayCommand OpenCommand { get; }
}
