using System.Text.Json;
using System.Text.Json.Serialization;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Messaging;

/// <summary>
/// Newline-delimited UTF-8 JSON, one envelope per line. A line that cannot be understood is
/// discarded rather than thrown: a shim from a different build must not be able to fault the app.
/// </summary>
public static class ShimProtocol
{
    /// <summary>The default name of the launch pipe.</summary>
    public const string DefaultPipeName = "Theseus.Launch";

    /// <summary>The serialiser settings both ends use.</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>Serialises a message to a single line, without its newline terminator.</summary>
    public static string Serialise<TPayload>(ShimMessageKind kind, TPayload payload)
    {
        var envelope = new
        {
            kind,
            payload,
        };

        return JsonSerializer.Serialize(envelope, Json);
    }

    /// <summary>Parses a line, returning false for anything this build does not understand.</summary>
    public static bool TryParse(string? line, out ShimEnvelope envelope)
    {
        envelope = default!;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;

            if (!root.TryGetProperty("kind", out var kindElement)
                || !Enum.TryParse<ShimMessageKind>(kindElement.GetString(), ignoreCase: true, out var kind))
            {
                return false;
            }

            var payload = root.TryGetProperty("payload", out var payloadElement)
                ? payloadElement.Clone()
                : default;

            envelope = new ShimEnvelope(kind, payload);
            return true;
        }
        catch (JsonException exception)
        {
            TheseusLog.Debug("Discarded an unreadable shim message: {Reason}", exception.Message);

            return false;
        }
    }

    /// <summary>Decodes an envelope's payload, returning null when it does not fit.</summary>
    public static TPayload? PayloadAs<TPayload>(ShimEnvelope envelope)
        where TPayload : class
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (envelope.Payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return null;
        }

        try
        {
            return envelope.Payload.Deserialize<TPayload>(Json);
        }
        catch (JsonException exception)
        {
            TheseusLog.Warning("A {Kind} shim message did not fit its expected shape: {Reason}", envelope.Kind, exception.Message);

            return null;
        }
    }
}
