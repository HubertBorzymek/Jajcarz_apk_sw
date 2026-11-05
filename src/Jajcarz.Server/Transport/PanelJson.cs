using System.Text.Json;
using System.Text.Json.Serialization;
using Jajcarz.Application.Panels;

namespace Jajcarz.Server.Transport;

/// <summary>Format JSON z docs/PROTOCOL.md.</summary>
public static class PanelJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static byte[] Serialize(PanelMessage message) =>
        JsonSerializer.SerializeToUtf8Bytes(message, message.GetType(), Options);
}
