namespace Theseus.Core.Messaging;

/// <summary>Either direction: the request could not be honoured.</summary>
/// <param name="Message">What went wrong.</param>
public sealed record ErrorPayload(string Message);
