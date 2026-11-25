namespace Theseus.Core.Trust;

/// <summary>What trusting folders, or undoing it, came to, in words for the owner.</summary>
/// <param name="Succeeded">Whether it did what was asked.</param>
/// <param name="Message">What happened.</param>
public sealed record FolderTrustOutcome(bool Succeeded, string Message);
