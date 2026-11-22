using System.Windows.Input;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>One line of "Waits for you", with the button that does it, or none for something outside Theseus (1.1f G36).</summary>
public sealed class WaitItemViewModel
{
    internal WaitItemViewModel(WaitItem item, ICommand? command)
    {
        Item = item;
        Command = item.Button is null ? null : command;
    }

    /// <summary>The item.</summary>
    public WaitItem Item { get; }

    /// <summary>The line, in plain words.</summary>
    public string Text => Item.Text;

    /// <summary>The button's label, or null.</summary>
    public string? Button => Command is null ? null : Item.Button;

    /// <summary>Whether it has a button.</summary>
    public bool HasButton => Command is not null;

    /// <summary>What the button does.</summary>
    public ICommand? Command { get; }
}
