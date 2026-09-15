namespace Theseus.Core.Attention;

/// <summary>Which clock an interval belongs to. The two are never added together.</summary>
public enum AttentionKind
{
    /// <summary>A workstream's tab was in front and someone was at the keyboard. At most one at a time.</summary>
    Attended,

    /// <summary>A workstream's session was working. Any number at once.</summary>
    Machine,
}
