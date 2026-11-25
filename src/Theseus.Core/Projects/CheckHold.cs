using Theseus.Core.Domain;

namespace Theseus.Core.Projects;

public static class CheckHold
{
    public static WaitItem? For(string project, bool projectStopped, IReadOnlyList<PieceOfWork> waiting, Func<PieceOfWork, bool> isNamed,
        Workstream? writer, bool writerHeld) => null;
}
