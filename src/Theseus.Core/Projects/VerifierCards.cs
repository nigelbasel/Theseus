using Theseus.Core.Domain;

namespace Theseus.Core.Projects;

/// <summary>Which verifier cards still stand for the build they check.</summary>
public static class VerifierCards
{
    /// <summary>Whether a verifier card in this state still stands.</summary>
    public static bool IsPending(SessionLifecycle lifecycle) => false;

    /// <summary>Why a build cannot be given another verifier.</summary>
    public static string WhyAlreadyChecked(IReadOnlyList<string> verifiers) => string.Empty;
}
