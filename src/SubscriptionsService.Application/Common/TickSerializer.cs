using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using SubscriptionsService.Domain.ValueObjects;

namespace SubscriptionsService.Application.Common;

/// <summary>
/// Serializes a <see cref="TickMessage"/> back into the wire schema forwarded to clients
/// (camelCase fields, "MM/dd/yyyy HH:mm:ss" timestamp — matching the Redis Pub/Sub schema).
/// </summary>
public static class TickSerializer
{
    private const string DateFormat = "MM/dd/yyyy HH:mm:ss";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new TickDateTimeConverter() }
    };

    public static string Serialize(TickMessage tick) => JsonSerializer.Serialize(tick, Options);

    public static TickMessage? Deserialize(string json) => JsonSerializer.Deserialize<TickMessage>(json, Options);

    private sealed class TickDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var raw = reader.GetString()!;

            // Market Data publishes ReceivedAt as ISO 8601 with variable-precision fractional
            // seconds (trailing zeros trimmed), not the documented MM/dd/yyyy HH:mm:ss wire
            // format. ParseExact("O") rejects the trimmed form, so fall back to a general parse.
            if (DateTime.TryParseExact(raw, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var legacy))
            {
                return legacy;
            }

            return DateTime.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString(DateFormat, CultureInfo.InvariantCulture));
    }
}
