using SenangRetails.Shared.Models.Entities;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace SenangRetails.Shared.ApiClient
{
    public abstract class BaseAC
    {
        /// <summary>Stores the raw body of the most recent POST response for debug popups.</summary>
        public static string LastPostResponseBody { get; private set; } = "";
        /// <summary>Stores the serialized body of the most recent POST request for debug popups.</summary>
        public static string LastPostRequestBody { get; private set; } = "";

        // Shared across ALL BaseAC instances — one connection pool, one TLS handshake per session.
        private static readonly HttpClient _httpClient;
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        static BaseAC()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, __, ___, ____) => true
            };
            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://ebisoftware.com.my:5000/"),
                Timeout = TimeSpan.FromSeconds(100)
            };
        }

        /// <summary>
        /// Deserializes body as TResponse. When body is empty or not valid JSON,
        /// builds a synthetic {"statusCode": httpStatus, "message": "..."} response
        /// so callers always receive a non-null, human-readable error instead of null.
        /// </summary>
        private static TResponse? DeserializeOrFallback<TResponse>(string body, int httpStatus, string? overrideMessage = null)
        {
            // Try to deserialize the real body first.
            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    var result = JsonSerializer.Deserialize<TResponse>(body, _jsonOptions);
                    // If the deserialized result implements IApiResponse with statusCode=0 and no message,
                    // the API returned a JSON structure we didn't expect (e.g. no statusCode/message fields).
                    // Patch the response so the caller always sees a useful error instead of silent failure.
                    if (result is IApiResponse apiResp && apiResp.statusCode == 0 && string.IsNullOrWhiteSpace(apiResp.message))
                    {
                        string truncated = body.Length > 300 ? body[..300] + "..." : body;
                        apiResp.statusCode = httpStatus;
                        apiResp.message = $"Unexpected API response (HTTP {httpStatus}): {truncated}";
                    }
                    return result;
                }
                catch (Exception ex) { overrideMessage = $"Deserialize error: {ex.Message}"; }
            }

            // Build a synthetic JSON response so the caller can surface a meaningful error message.
            string message = overrideMessage
                ?? (httpStatus > 0 ? $"HTTP {httpStatus}" : "No response from server.");
            // Escape quotes in the message for safe JSON embedding.
            string safeMsg = message.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string synthetic = $"{{\"statusCode\":{httpStatus},\"message\":\"{safeMsg}\"}}";
            try { return JsonSerializer.Deserialize<TResponse>(synthetic, _jsonOptions); }
            catch { return default; }
        }

        public bool CreateBearerAuthAsync(string accessToken)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            return true;
        }

        protected async Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            try
            {
                var requestJson = JsonSerializer.Serialize(data);
                LastPostRequestBody = requestJson;
                System.Diagnostics.Debug.WriteLine($"[API POST {endpoint}] Request Body: {requestJson}");
                System.Console.WriteLine($"[API POST {endpoint}] Request Body: {requestJson}");
                var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
                };
                // Copy current auth header onto this specific request so concurrent calls don't interfere.
                if (_httpClient.DefaultRequestHeaders.Authorization is { } auth)
                    request.Headers.Authorization = auth;

                var response = await _httpClient.SendAsync(request);
                string body = await response.Content.ReadAsStringAsync();
                LastPostResponseBody = body;
                System.Diagnostics.Debug.WriteLine($"[API POST {endpoint}] HTTP {(int)response.StatusCode} | Body: {body}");

                return DeserializeOrFallback<TResponse>(body, (int)response.StatusCode);
            }
            catch (TaskCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[API TIMEOUT] POST {endpoint}");
                return DeserializeOrFallback<TResponse>("", 0, "Request timed out. Check your connection.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API ERROR] POST {endpoint}: {ex.Message}");
                return DeserializeOrFallback<TResponse>("", 0, ex.Message);
            }
        }

        protected async Task<TResponse?> PostRawJsonAsync<TResponse>(string endpoint, string rawJson)
        {
            try
            {
                LastPostRequestBody = rawJson;
                System.Diagnostics.Debug.WriteLine($"[API POST RAW {endpoint}] Request Body: {rawJson}");
                System.Console.WriteLine($"[API POST RAW {endpoint}] Request Body: {rawJson}");
                var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = new StringContent(rawJson, Encoding.UTF8, "application/json")
                };
                if (_httpClient.DefaultRequestHeaders.Authorization is { } auth)
                    request.Headers.Authorization = auth;
                var response = await _httpClient.SendAsync(request);
                string body = await response.Content.ReadAsStringAsync();
                LastPostResponseBody = body;
                System.Diagnostics.Debug.WriteLine($"[API POST RAW {endpoint}] HTTP {(int)response.StatusCode} | Body: {body}");
                return DeserializeOrFallback<TResponse>(body, (int)response.StatusCode);
            }
            catch (TaskCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[API TIMEOUT] POST RAW {endpoint}");
                return DeserializeOrFallback<TResponse>("", 0, "Request timed out. Check your connection.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API ERROR] POST RAW {endpoint}: {ex.Message}");
                return DeserializeOrFallback<TResponse>("", 0, ex.Message);
            }
        }

        protected async Task<TResponse?> GetAsync<TResponse>(string endpoint)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                if (_httpClient.DefaultRequestHeaders.Authorization is { } auth)
                    request.Headers.Authorization = auth;

                var response = await _httpClient.SendAsync(request);
                string body = await response.Content.ReadAsStringAsync();
                return DeserializeOrFallback<TResponse>(body, (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API ERROR] GET {endpoint}: {ex.Message}");
                return DeserializeOrFallback<TResponse>("", 0, ex.Message);
            }
        }

        protected async Task<TResponse?> PutAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Put, endpoint)
                {
                    Content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json")
                };
                if (_httpClient.DefaultRequestHeaders.Authorization is { } auth)
                    request.Headers.Authorization = auth;

                var response = await _httpClient.SendAsync(request);
                string body = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[API PUT {endpoint}] HTTP {(int)response.StatusCode} | Body: {body}");

                return DeserializeOrFallback<TResponse>(body, (int)response.StatusCode);
            }
            catch (TaskCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[API TIMEOUT] PUT {endpoint}");
                return DeserializeOrFallback<TResponse>("", 0, "Request timed out. Check your connection.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API ERROR] PUT {endpoint}: {ex.Message}");
                return DeserializeOrFallback<TResponse>("", 0, ex.Message);
            }
        }

        protected async Task<TResponse?> DeleteAsync<TResponse>(string endpoint)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);
                if (_httpClient.DefaultRequestHeaders.Authorization is { } auth)
                    request.Headers.Authorization = auth;

                var response = await _httpClient.SendAsync(request);
                string body = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[API DELETE {endpoint}] HTTP {(int)response.StatusCode} | Body: {body}");

                return DeserializeOrFallback<TResponse>(body, (int)response.StatusCode);
            }
            catch (TaskCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[API TIMEOUT] DELETE {endpoint}");
                return DeserializeOrFallback<TResponse>("", 0, "Request timed out. Check your connection.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API ERROR] DELETE {endpoint}: {ex.Message}");
                return DeserializeOrFallback<TResponse>("", 0, ex.Message);
            }
        }
    }
}