using System.Windows.Input;

namespace Theseus.App.ViewModels;

/// <summary>
/// One button of the summary's Waiting on you band (1.1d AC 20): "Accept the spec of &lt;piece&gt;…" or "Answer
/// &lt;workstream&gt; in its terminal".
/// </summary>
/// <param name="Label">What it says.</param>
/// <param name="Command">Opens the piece's box, selects its waiting stage, then accepts or focuses.</param>
public sealed record WaitingButtonViewModel(string Label, ICommand Command);
