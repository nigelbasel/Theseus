namespace Theseus.Core.Resources;

/// <summary>Where the cores-held-back warning stands after one sample.</summary>
/// <param name="Standing">Whether the warning is showing.</param>
/// <param name="Started">Whether it began with this sample.</param>
/// <param name="Stopped">Whether it ended with this sample.</param>
public sealed record CoreClampState(bool Standing, bool Started, bool Stopped);
