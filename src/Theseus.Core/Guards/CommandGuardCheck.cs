namespace Theseus.Core.Guards;

/// <summary>A check of a guard that reads a shell command: one command it must refuse and one it must pass.</summary>
/// <param name="Refused">A command the guard must refuse.</param>
/// <param name="RefusedSaid">The refused command in words, such as "a push to main".</param>
/// <param name="Passed">A command the guard must pass.</param>
/// <param name="PassedSaid">The passed command in words.</param>
/// <param name="Environment">Variables the guard is run with, so the check reaches nothing real.</param>
public sealed record CommandGuardCheck(
    string Refused,
    string RefusedSaid,
    string Passed,
    string PassedSaid,
    IReadOnlyDictionary<string, string> Environment);
