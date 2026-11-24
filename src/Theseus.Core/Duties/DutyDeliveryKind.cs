namespace Theseus.Core.Duties;

/// <summary>What became of an instruction given to a card.</summary>
public enum DutyDeliveryKind
{
    /// <summary>It reached the card: handed in as its next turn, or as the start of a new session.</summary>
    Delivered,

    /// <summary>It could not go now, and goes later.</summary>
    Deferred,

    /// <summary>It was not given, on purpose, for a reason recorded as a skip.</summary>
    Skipped,

    /// <summary>It was tried and failed.</summary>
    Failed,
}
