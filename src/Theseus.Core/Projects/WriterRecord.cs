namespace Theseus.Core.Projects;

/// <summary>A scenario-writer card as Clear up… judges it.</summary>
/// <param name="Key">Its card.</param>
/// <param name="Title">Its title.</param>
/// <param name="Project">The project it writes for, or null for cards in no project.</param>
/// <param name="Folder">The folder it writes in: its working directory.</param>
/// <param name="IsRunning">Whether its session is running.</param>
/// <param name="IsArchived">Whether it is archived already.</param>
public sealed record WriterRecord(string Key, string Title, string? Project, string Folder, bool IsRunning, bool IsArchived);
