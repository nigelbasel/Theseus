using System.Text.Json;

namespace Theseus.Core.Messaging;

/// <summary>One line of the shim protocol: a message kind and its undecoded payload.</summary>
/// <param name="Kind">Which message this is.</param>
/// <param name="Payload">The payload, decoded on demand by the receiver.</param>
public sealed record ShimEnvelope(ShimMessageKind Kind, JsonElement Payload);
