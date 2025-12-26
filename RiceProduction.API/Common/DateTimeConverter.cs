using System.Text.Json;
using System.Text.Json.Serialization;

namespace RiceProduction.API.Common;

/// <summary>
/// Custom JSON converter to serialize DateTime as MM/DD/YYYY format
/// </summary>
public class CustomDateTimeConverter : JsonConverter<DateTime>
{
    private const string DateFormat = "MM/dd/yyyy";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dateString = reader.GetString();
        if (string.IsNullOrEmpty(dateString))
        {
            throw new JsonException("Date string is null or empty");
        }

        // Try parsing MM/DD/YYYY format first
        if (DateTime.TryParseExact(dateString, DateFormat, null, System.Globalization.DateTimeStyles.None, out var date))
        {
            return date;
        }

        // Fallback to default parsing (ISO 8601)
        if (DateTime.TryParse(dateString, out var fallbackDate))
        {
            return fallbackDate;
        }

        throw new JsonException($"Unable to parse date: {dateString}");
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(DateFormat));
    }
}

/// <summary>
/// Custom JSON converter to serialize nullable DateTime as MM/DD/YYYY format
/// </summary>
public class CustomNullableDateTimeConverter : JsonConverter<DateTime?>
{
    private const string DateFormat = "MM/dd/yyyy";

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dateString = reader.GetString();
        if (string.IsNullOrEmpty(dateString))
        {
            return null;
        }

        // Try parsing MM/DD/YYYY format first
        if (DateTime.TryParseExact(dateString, DateFormat, null, System.Globalization.DateTimeStyles.None, out var date))
        {
            return date;
        }

        // Fallback to default parsing (ISO 8601)
        if (DateTime.TryParse(dateString, out var fallbackDate))
        {
            return fallbackDate;
        }

        return null;
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteStringValue(value.Value.ToString(DateFormat));
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}

