using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;

namespace Beauty_Aesthetics_WebPos.APIClient;

/// <summary>
/// Reads the shared EBI stock-document LoadRecord response shape without making
/// the whole response fail when an optional header value is returned as null.
/// </summary>
internal static class StockRecordResponseReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public static async Task<ApiCallResult<TEnvelope>> ReadAsync<TEnvelope, TDocument>(
        HttpResponseMessage response,
        IReadOnlyCollection<string> documentPropertyNames,
        Func<TDocument?, List<JsonObject>, TEnvelope> createEnvelope,
        bool allowEmptyDocument,
        string requestFailureMessage,
        string invalidResponseMessage,
        CancellationToken cancellationToken)
        where TDocument : class
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ApiCallResult<TEnvelope>.Unauthorized(response.StatusCode);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return ApiCallResult<TEnvelope>.Failure(
                response.StatusCode,
                ReadMessage(body) ?? $"{requestFailureMessage} ({(int)response.StatusCode}).");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return ApiCallResult<TEnvelope>.Failure(response.StatusCode, invalidResponseMessage);
        }

        try
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;

            if (TryGet(root, "statusCode", out var statusElement))
            {
                var apiStatusCode = ReadInt(statusElement);
                if (apiStatusCode > 0 && apiStatusCode is < 200 or >= 300)
                {
                    return ApiCallResult<TEnvelope>.Failure(
                        response.StatusCode,
                        ReadString(root, "message") ?? requestFailureMessage);
                }
            }

            var payload = TryGet(root, "result", out var resultElement)
                ? resultElement
                : root;

            if (payload.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return allowEmptyDocument
                    ? ApiCallResult<TEnvelope>.Ok(response.StatusCode, createEnvelope(null, []))
                    : ApiCallResult<TEnvelope>.Failure(response.StatusCode, invalidResponseMessage);
            }

            var container = FindDocumentContainer(payload, documentPropertyNames);
            JsonElement documentElement = default;
            var hasDocument = false;

            if (container.HasValue)
            {
                foreach (var propertyName in documentPropertyNames)
                {
                    if (TryGet(container.Value, propertyName, out documentElement))
                    {
                        hasDocument = documentElement.ValueKind == JsonValueKind.Object;
                        break;
                    }
                }
            }
            else if (LooksLikeDocument(payload))
            {
                documentElement = payload;
                hasDocument = true;
            }

            TDocument? document = null;
            if (hasDocument)
            {
                document = DeserializeDocument<TDocument>(documentElement);
                if (document is null)
                {
                    return ApiCallResult<TEnvelope>.Failure(response.StatusCode, invalidResponseMessage);
                }
            }
            else if (!allowEmptyDocument)
            {
                return ApiCallResult<TEnvelope>.Failure(response.StatusCode, invalidResponseMessage);
            }

            var lines = ReadDocumentLines(container ?? payload);
            return ApiCallResult<TEnvelope>.Ok(
                response.StatusCode,
                createEnvelope(document, lines));
        }
        catch (JsonException)
        {
            return ApiCallResult<TEnvelope>.Failure(response.StatusCode, invalidResponseMessage);
        }
    }

    private static TDocument? DeserializeDocument<TDocument>(JsonElement element)
        where TDocument : class
    {
        var node = JsonNode.Parse(element.GetRawText());
        if (node is not JsonObject documentObject)
        {
            return null;
        }

        // EBI may send null for optional value-type fields. Removing only those
        // null values lets the DTO keep its normal default while retaining every
        // other API field, including extension data used again during UpdateRecord.
        foreach (var property in documentObject.ToList())
        {
            if (property.Value is null)
            {
                documentObject.Remove(property.Key);
            }
        }

        return documentObject.Deserialize<TDocument>(JsonOptions);
    }

    private static List<JsonObject> ReadDocumentLines(JsonElement container)
    {
        if (!TryGet(container, "lstDocumentLine", out var linesElement) ||
            linesElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var lines = new List<JsonObject>();
        foreach (var lineElement in linesElement.EnumerateArray())
        {
            if (lineElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (JsonNode.Parse(lineElement.GetRawText()) is JsonObject line)
            {
                lines.Add(line);
            }
        }

        return lines;
    }

    private static JsonElement? FindDocumentContainer(
        JsonElement element,
        IReadOnlyCollection<string> documentPropertyNames)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (documentPropertyNames.Any(propertyName => TryGet(element, propertyName, out _)))
        {
            return element;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var nested = FindDocumentContainer(property.Value, documentPropertyNames);
            if (nested.HasValue)
            {
                return nested;
            }
        }

        return null;
    }

    private static bool LooksLikeDocument(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object &&
        (TryGet(element, "documentID", out _) ||
         TryGet(element, "displayCode", out _) ||
         TryGet(element, "documentTypeID", out _));

    private static int ReadInt(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number))
        {
            return number;
        }

        return int.TryParse(element.ToString(), out var parsed) ? parsed : 0;
    }

    private static string? ReadMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var json = JsonDocument.Parse(body);
            return ReadString(json.RootElement, "message") ??
                   ReadString(json.RootElement, "detail") ??
                   ReadString(json.RootElement, "title");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return TryGet(element, propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool TryGet(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }
}
