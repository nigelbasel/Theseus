namespace Theseus.Core.Updates;

/// <summary>The newest published build, as the release feed describes it.</summary>
/// <param name="Version">Its version stamp, without the tag's leading "v".</param>
/// <param name="Installer">Where its MSI downloads from.</param>
/// <param name="Checksums">Where its SHA256SUMS.txt downloads from, or null when the release has none.</param>
/// <param name="Page">The release's page, for a person to read.</param>
public sealed record AvailableRelease(string Version, Uri Installer, Uri? Checksums, Uri Page);
