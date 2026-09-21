using System.Text.Json;
using System.Text.Json.Serialization;

namespace Beauty_Aesthetics_WebPos.Models.DTOs;

public sealed class PointConversionDM
{
    public bool IsLoading { get; set; }
    public string? PointID { get; set; }
    public decimal ForEveryXDollar { get; set; }
    public decimal EqualToXPoint { get; set; }
    public decimal RMConversionRatio { get; set; }
    public bool RoundDownToInteger { get; set; }
    public string? MemberTypeID { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int ExpiryOption { get; set; }
    public int ExpiryMonth { get; set; }
    public string? VisibleToBranchID { get; set; }
    public int DaysCovered { get; set; }
    public bool ExcludeTaxAmount { get; set; }
    public bool ConvertRebateIntoCashVoucher { get; set; }
    public decimal MinimumSpend { get; set; }

    [JsonPropertyName("saveAction")]
    [JsonConverter(typeof(StringOrNumberActionConverter))]
    public string SaveAction { get; set; } = "Changed";

    [JsonPropertyName("isDirty")]
    public bool IsDirty { get; set; } = true;
}

public sealed class StringOrNumberActionConverter : JsonConverter<string>
{
    public override string Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() ?? string.Empty,
            JsonTokenType.Number => reader.GetInt32().ToString(),
            JsonTokenType.Null => string.Empty,
            _ => throw new JsonException("SaveAction must be a string or number.")
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        string value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
