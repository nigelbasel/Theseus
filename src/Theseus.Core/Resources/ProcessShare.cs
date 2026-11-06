namespace Theseus.Core.Resources;

/// <summary>One process's part in what a session is using, for the card's tooltip.</summary>
/// <param name="Name">The executable's name.</param>
/// <param name="CpuPercent">Its share of the whole machine's processors.</param>
/// <param name="PrivateBytes">The memory it holds.</param>
public sealed record ProcessShare(string Name, double CpuPercent, long PrivateBytes);
