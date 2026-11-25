namespace Theseus.Core.Duties;

/// <summary>What became of an instruction given to a card, and why.</summary>
/// <param name="Kind">Delivered, deferred, skipped or failed.</param>
/// <param name="Detail">Why, for anything but Delivered; how, for Delivered ("handed in" or "started").</param>
public sealed record DutyDelivery(DutyDeliveryKind Kind, string Detail)
{
    /// <summary>A delivery that reached the card.</summary>
    public static DutyDelivery Delivered(string how) => new(DutyDeliveryKind.Delivered, how);

    /// <summary>A delivery that failed.</summary>
    public static DutyDelivery Failed(string why) => new(DutyDeliveryKind.Failed, why);
}
