using System.Text.Json.Serialization;

namespace Beauty_Aesthetics_WebPos.APIClient.ResultPattern
{
    public sealed class ApiResponse<T>
    {
        [JsonPropertyName("statusCode")]
        public int StatusCode { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("result")]
        public T? Result { get; set; }

        [JsonIgnore]
        public bool IsSuccess => StatusCode is >= 200 and < 300;
    }
}
