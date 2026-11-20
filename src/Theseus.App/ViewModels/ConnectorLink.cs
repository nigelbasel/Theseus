namespace Theseus.App.ViewModels;

/// <summary>One line the project view draws between two boxes (1.1d AC 25).</summary>
/// <param name="From">The box it starts from.</param>
/// <param name="To">The box its arrow points at.</param>
/// <param name="Kind">Which line it is.</param>
public sealed record ConnectorLink(PieceNodeViewModel From, PieceNodeViewModel To, ConnectorKind Kind);
