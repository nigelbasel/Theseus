namespace Theseus.App.ViewModels;

/// <summary>The tabs of a card's details, in the order they are shown.</summary>
public enum DrawerTab
{
    /// <summary>What is going on with the card: its title, state, Jira issues and place in a chain.</summary>
    Overview,

    /// <summary>The card's links to other cards, and adding new ones.</summary>
    Links,

    /// <summary>The card's runs and workflow runs.</summary>
    History,
}
