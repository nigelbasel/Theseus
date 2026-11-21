using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Theseus.App.ViewModels;
using Theseus.Core.Projects;

namespace Theseus.App.Controls;

/// <summary>
/// One count chip of a project's summary, such as "◆ 3 waiting on you" (1.1d AC 19, 39): the state's colour on its soft
/// fill, from the state colour dictionaries. One element, so the project view's summary and the project card draw the
/// same chip.
/// </summary>
public sealed class StateCountChip : Border
{
    private readonly TextBlock _label = new() { FontWeight = FontWeights.Medium };

    /// <summary>Creates the chip; its data context is a <see cref="StateCountViewModel"/>.</summary>
    public StateCountChip()
    {
        CornerRadius = new CornerRadius(10);
        Padding = new Thickness(10, 3, 10, 3);
        Margin = new Thickness(0, 0, 8, 4);
        _label.SetBinding(TextBlock.TextProperty, new Binding(nameof(StateCountViewModel.Text)));
        Child = _label;
        DataContextChanged += (_, _) => Colour();
        Colour();
    }

    /// <summary>The resource key of a state's colour, as the state colour dictionaries name it.</summary>
    public static string BrushKey(StageNodeState state) => state switch
    {
        StageNodeState.Done => "StateDoneBrush",
        StageNodeState.WaitingOnYou => "StateWaitingBrush",
        StageNodeState.Busy => "StateBusyBrush",
        StageNodeState.Stuck => "StateStuckBrush",
        StageNodeState.HoldoutsNotRun => "StateHoldoutsBrush",
        _ => "StateToDoBrush",
    };

    /// <summary>The resource key of a state's soft fill.</summary>
    public static string SoftBrushKey(StageNodeState state) => BrushKey(state).Replace("Brush", "SoftBrush", StringComparison.Ordinal);

    private void Colour()
    {
        var state = (DataContext as StateCountViewModel)?.State ?? StageNodeState.ToDo;

        SetResourceReference(BackgroundProperty, SoftBrushKey(state));
        _label.SetResourceReference(TextBlock.ForegroundProperty, BrushKey(state));
    }
}
