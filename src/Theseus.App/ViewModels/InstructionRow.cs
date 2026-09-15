using System.Windows;
using Theseus.Core.Instructions;

namespace Theseus.App.ViewModels;

/// <summary>One line in the instructions viewer: a file a session reads, or something it can run.</summary>
public sealed class InstructionRow
{
    private const int LongestDescription = 220;

    /// <summary>What the line is called: a path for a file, <c>/name</c> for a skill or command.</summary>
    public required string Title { get; init; }

    /// <summary>When a file is read, or what a skill says it does, or what hides it.</summary>
    public required string Detail { get; init; }

    /// <summary>The file to open.</summary>
    public required string Path { get; init; }

    /// <summary>The short label at the start of the line.</summary>
    public required string Badge { get; init; }

    /// <summary>How far in the line sits: imports sit under the file that imports them.</summary>
    public Thickness Indent { get; init; }

    /// <summary>Whether the line is something a session will not actually get.</summary>
    public bool IsDimmed { get; init; }

    /// <summary>The line for an instruction file.</summary>
    public static InstructionRow For(InstructionFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return new InstructionRow
        {
            Title = file.Path,
            Detail = file.When,
            Path = file.Path,
            Badge = file.Load switch
            {
                InstructionLoad.AtStart => "At start",
                InstructionLoad.OnDemand => "On demand",
                _ => "Not read",
            },
            Indent = new Thickness(file.ImportDepth * 22, 0, 0, 0),
            IsDimmed = file.Load == InstructionLoad.NotLoaded,
        };
    }

    /// <summary>The line for a skill, command or agent.</summary>
    public static InstructionRow For(CommandEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var where = entry.Origin switch
        {
            CommandOrigin.Personal => "personal",
            CommandOrigin.Project => "project",
            CommandOrigin.Nested => "below the working directory",
            CommandOrigin.AdditionalDirectory => "added directory",
            _ => "plugin",
        };

        var description = entry.Description.Length > LongestDescription
            ? entry.Description[..LongestDescription].TrimEnd() + "…"
            : entry.Description;

        return new InstructionRow
        {
            Title = entry.Kind == CommandKind.Agent ? entry.Name : "/" + entry.Name,
            Detail = entry.HiddenBy is { } winner
                ? $"Not used: {winner} takes this name."
                : entry.LoadsOnDemand ? $"Once files there are worked on. {description}" : description,
            Path = entry.Path,
            Badge = $"{entry.Kind} · {where}",
            IsDimmed = entry.HiddenBy is not null,
        };
    }
}
