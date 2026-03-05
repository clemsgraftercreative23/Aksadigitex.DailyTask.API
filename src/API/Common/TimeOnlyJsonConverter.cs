using System.Text.Json;
using System.Text.Json.Serialization;

namespace API.Common;

public class TimeOnlyJsonConverter : JsonConverter<TimeOnly>
{
    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
            throw new JsonException("TimeOnly value cannot be empty");

        // Try to parse as TimeOnly first (HH:mm:ss or HH:mm)
        if (TimeOnly.TryParse(value, out var timeOnly))
            return timeOnly;

        // If that fails, try to parse as full DateTime and extract time
        if (DateTime.TryParse(value, out var dateTime))
            return TimeOnly.FromDateTime(dateTime);

        throw new JsonException($"Unable to parse '{value}' as TimeOnly. Expected format: HH:mm:ss or HH:mm or ISO datetime");
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("HH:mm:ss"));
    }
}
