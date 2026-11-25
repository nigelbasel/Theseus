namespace Theseus.Core.Stopping;

/// <summary>Why a card's session is ended without anyone pressing Stop.</summary>
public enum CardEndingReason
{
    /// <summary>A verifier has filed its report.</summary>
    ReportFiled,

    /// <summary>The owner accepted the card's report, and nothing more waits for it.</summary>
    ReportAccepted,

    /// <summary>A scenario writer has written its scenarios and gone quiet.</summary>
    ScenariosWritten,
}
