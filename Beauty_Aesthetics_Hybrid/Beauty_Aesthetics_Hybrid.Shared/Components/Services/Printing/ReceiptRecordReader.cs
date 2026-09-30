using System.Text.Json.Nodes;

namespace Beauty_Aesthetics_WebPos.Components.Services.Printing;

public static class ReceiptRecordReader
{
    public static string Text(JsonNode? record, params string[] names)
    {
        foreach (var name in names)
        {
            var value = Find(record, name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static string Find(JsonNode? node, string name)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj)
            {
                if (string.Equals(property.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    if (property.Value is JsonValue jsonValue &&
                        jsonValue.TryGetValue<string>(out var text))
                    {
                        return text ?? string.Empty;
                    }

                    var rendered = property.Value?.ToJsonString().Trim('"');
                    if (!string.IsNullOrWhiteSpace(rendered) && rendered != "null")
                    {
                        return rendered;
                    }
                }

                var nested = Find(property.Value, name);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                var nested = Find(item, name);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }

        return string.Empty;
    }
}
