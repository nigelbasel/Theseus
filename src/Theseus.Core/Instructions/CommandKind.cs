namespace Theseus.Core.Instructions;

/// <summary>What a session can be told to run, or hand work to.</summary>
public enum CommandKind
{
    /// <summary>A <c>SKILL.md</c>, run as a slash command or picked up when relevant.</summary>
    Skill,

    /// <summary>A command file in the older <c>.claude/commands</c> format.</summary>
    Command,

    /// <summary>A subagent definition.</summary>
    Agent,
}
