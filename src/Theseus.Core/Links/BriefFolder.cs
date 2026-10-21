namespace Theseus.Core.Links;

/// <summary>
/// Where a spawned workstream's brief and report go: <c>&lt;working directory&gt;\&lt;folder&gt;\&lt;child key&gt;\</c>.
/// Relative to the spawning workstream's working directory, so nothing names one machine's layout.
/// </summary>
public static class BriefFolder
{
    /// <summary>The folder name a board starts with.</summary>
    public const string DefaultName = "proposals";

    /// <summary>The folder for one child.</summary>
    public static string For(string workingDirectory, string folderName, string childKey) =>
        Path.Combine(workingDirectory, string.IsNullOrWhiteSpace(folderName) ? DefaultName : folderName.Trim(), childKey);

    /// <summary>Where a child writes the report its parent waits on.</summary>
    public static string ReportPath(string workingDirectory, string folderName, string childKey) =>
        Path.Combine(For(workingDirectory, folderName, childKey), "REPORT.md");
}
