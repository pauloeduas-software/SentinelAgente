using System.Text.Json;
using System.Text.Json.Serialization;

namespace SentinelAgente.Protocol;

/// <summary>Uma mensagem pronta para o fio: o tipo, para o log, e o JSON.</summary>
public sealed record OutgoingMessage(string Type, ReadOnlyMemory<byte> Json);

/// <summary>Um comando do servidor: a ação e o UUID que a auditoria de lá grava.</summary>
public sealed record CommandPayload(string Action, string CommandId);

/// <summary>
/// O que chegou do servidor. <see cref="Command"/> só vem preenchido quando o
/// tipo é <c>Command</c>; qualquer outro tipo chega sem conteúdo, para o log.
/// </summary>
public sealed record IncomingMessage(string Type, CommandPayload? Command);

/// <summary>
/// O envelope <c>{ "Type": …, "Payload": { "Hwid": …, … } }</c>, nos dois
/// sentidos. Quem monta e lê o JSON é só esta classe: a conexão não conhece
/// campo nenhum.
/// </summary>
public static class Messages
{
    public const string Handshake = "Handshake";
    public const string Telemetry = "Telemetry";
    public const string Ping = "Ping";
    public const string Command = "Command";

    public static OutgoingMessage CreateHandshake(HandshakePayload payload) =>
        new(Handshake, JsonSerializer.SerializeToUtf8Bytes(
            new Envelope<HandshakePayload>(Handshake, payload), ProtocolJson.Default.EnvelopeHandshakePayload));

    public static OutgoingMessage CreatePing(string hwid) =>
        new(Ping, JsonSerializer.SerializeToUtf8Bytes(
            new Envelope<PingPayload>(Ping, new PingPayload(hwid)), ProtocolJson.Default.EnvelopePingPayload));

    /// <summary>O corpo do cadastro (<c>POST /agent-hub/enroll</c>): só o HWID.</summary>
    public static byte[] CreateEnrollRequest(string hwid) =>
        JsonSerializer.SerializeToUtf8Bytes(new EnrollRequest(hwid), ProtocolJson.Default.EnrollRequest);

    /// <summary>O token que o cadastro devolveu, ou <c>null</c> se a resposta não traz um.</summary>
    public static string? ReadEnrollResponse(ReadOnlySpan<byte> json)
    {
        try
        {
            var token = JsonSerializer.Deserialize(json, ProtocolJson.Default.EnrollResponse)?.Token;
            return string.IsNullOrWhiteSpace(token) ? null : token;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Lê uma mensagem do servidor. Devolve <c>null</c> quando o texto não é um
    /// envelope, ou quando é um <c>Command</c> sem ação ou sem id.
    /// </summary>
    public static IncomingMessage? Read(ReadOnlySpan<byte> json)
    {
        IncomingEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize(json, ProtocolJson.Default.IncomingEnvelope);
        }
        catch (JsonException)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(envelope?.Type)) return null;
        if (envelope.Type != Command) return new IncomingMessage(envelope.Type, null);

        CommandPayload? command;
        try
        {
            command = envelope.Payload?.Deserialize(ProtocolJson.Default.CommandPayload);
        }
        catch (JsonException)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(command?.Action) || string.IsNullOrWhiteSpace(command.CommandId)) return null;
        return new IncomingMessage(Command, command);
    }
}

internal sealed record Envelope<TPayload>(string Type, TPayload Payload);

internal sealed record PingPayload(string Hwid);

internal sealed record IncomingEnvelope(string? Type, JsonElement? Payload);

internal sealed record EnrollRequest(string Hwid);

internal sealed record EnrollResponse(string? Token);

// PascalCase no fio, que é o que o agente falso do SentinelWeb manda; na
// leitura, sem diferença de maiúscula.
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Envelope<HandshakePayload>))]
[JsonSerializable(typeof(Envelope<PingPayload>))]
[JsonSerializable(typeof(IncomingEnvelope))]
[JsonSerializable(typeof(CommandPayload))]
[JsonSerializable(typeof(EnrollRequest))]
[JsonSerializable(typeof(EnrollResponse))]
internal sealed partial class ProtocolJson : JsonSerializerContext;
