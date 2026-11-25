using Theseus.Core.Duties;

namespace Theseus.App.ViewModels;

/// <summary>One duty in the duties checklist: its card and name, and below them its cadence, runs, state and instruction.</summary>
public sealed class DutyRowViewModel : ObservableObject
{
    private readonly Action _ticked;
    private bool _isTicked;

    /// <summary>Creates the row.</summary>
    public DutyRowViewModel(Duty duty, string cardTitle, string setBy, bool isTicked, Action ticked)
    {
        Duty = duty;
        _isTicked = isTicked;
        _ticked = ticked;
        Text = $"{cardTitle} · {duty.Name}";
        Detail = $"{DutyFace.Line(duty, TimeZoneInfo.Local)}{Environment.NewLine}{duty.FirstLine(120)}{Environment.NewLine}"
                 + $"Set by {setBy} · {duty.End.Describe()}{(duty.State.LastSkipReason is { } skip ? $" · last skip: {skip}" : string.Empty)}";
    }

    /// <summary>The duty.</summary>
    public Duty Duty { get; }

    /// <summary>Its card and name.</summary>
    public string Text { get; }

    /// <summary>Its state, instruction and who set it.</summary>
    public string Detail { get; }

    /// <summary>Whether it is ticked.</summary>
    public bool IsTicked
    {
        get => _isTicked;
        set
        {
            if (Set(ref _isTicked, value))
            {
                _ticked();
            }
        }
    }
}
