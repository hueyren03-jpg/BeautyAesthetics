using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Services.Auth;
using Beauty_Aesthetics_WebPos.Models.DTOs;
using EBI.DM;

namespace Beauty_Aesthetics_WebPos.APIClient;

public sealed class InventoryAC
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly MediaTypeHeaderValue JsonPatchMediaType =
        MediaTypeHeaderValue.Parse("application/json-patch+json");

    private readonly IAuthService authService;

    public InventoryAC(IAuthService authService)
    {
        this.authService = authService;
    }

    public async Task<ApiCallResult<List<InventoryDM>>> LoadProxyAsync(
        string? branchId = null,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Inventory/LoadProxy")
        {
            Content = JsonContent.Create(
                new { id = string.IsNullOrWhiteSpace(branchId) ? null : branchId },
                mediaType: JsonPatchMediaType,
                options: JsonOptions)
        };

        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        return await ReadApiResponseAsync<List<InventoryDM>>(
            response,
            "Inventory response was invalid.",
            cancellationToken);
    }

    public async Task<ApiCallResult<Dictionary<string, rpt_StockBalanceByBranchByItemsDM>>> GetStockBalanceByBranchAndByItemAsync(
        StockBalanceRequestDTO requestDto,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Inventory/GetStockBalanceByBranchAndByItem")
        {
            Content = JsonContent.Create(requestDto, mediaType: JsonPatchMediaType, options: JsonOptions)
        };

        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        return await ReadApiResponseAsync<Dictionary<string, rpt_StockBalanceByBranchByItemsDM>>(
            response,
            "Stock balance response was invalid.",
            cancellationToken);
    }
    public async Task<ApiCallResult<InventoryDM>> LoadRecordAsync(
        string masterAccountId,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Inventory/LoadRecord")
        {
            Content = JsonContent.Create(new { id = masterAccountId }, mediaType: JsonPatchMediaType, options: JsonOptions)
        };

        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        return await ReadApiResponseAsync<InventoryDM>(
            response,
            "Inventory record response was invalid.",
            cancellationToken);
    }

    public Task<ApiCallResult<JsonElement>> UpdateAsync(
        InventoryDM inventory,
        CancellationToken cancellationToken = default) =>
        SendInventoryAsync("/api/Inventory/Update", inventory, cancellationToken);

    public Task<ApiCallResult<JsonElement>> CreateSimpleAsync(
        InventoryDM inventory,
        CancellationToken cancellationToken = default) =>
        SendInventoryAsync("/api/Inventory/CreateSimple", inventory, cancellationToken);

    public async Task<ApiCallResult<bool>> DeleteAsync(
        string masterAccountId,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/Inventory/Delete?id={Uri.EscapeDataString(masterAccountId)}");
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        return await ReadMutationResponseAsync(response, cancellationToken);
    }

    public async Task<ApiCallResult<bool>> DeleteFullAsync(
        string masterAccountId,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/InventoryFull/Delete?id={Uri.EscapeDataString(masterAccountId)}");
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        return await ReadMutationResponseAsync(response, cancellationToken);
    }
    public Task<ApiCallResult<InventorySaveResultDTO>> CreateFullAsync(
        InventoryPackageRequestDTO inventory,
        CancellationToken cancellationToken = default) =>
        SendFullInventoryAsync("/api/InventoryFull/Create", inventory, cancellationToken);

    public Task<ApiCallResult<InventorySaveResultDTO>> UpdateFullAsync(
        InventoryPackageRequestDTO inventory,
        CancellationToken cancellationToken = default) =>
        SendFullInventoryAsync("/api/InventoryFull/Update", inventory, cancellationToken);

    private async Task<ApiCallResult<JsonElement>> SendInventoryAsync(
        string endpoint,
        InventoryDM inventory,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(inventory, mediaType: JsonPatchMediaType, options: JsonOptions)
        };

        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        return await ReadApiResponseAsync<JsonElement>(
            response,
            "Inventory save response was invalid.",
            cancellationToken);
    }

    private async Task<ApiCallResult<InventorySaveResultDTO>> SendFullInventoryAsync(
        string endpoint,
        InventoryPackageRequestDTO inventory,
        CancellationToken cancellationToken)
    {
        var serializedInventory = JsonSerializer.SerializeToElement(inventory.ObjInventory, JsonOptions);
        var inventoryPayload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in serializedInventory.EnumerateObject())
        {
            inventoryPayload[property.Name] = property.Value.Clone();
        }

        if (inventory.SellingUnits.Count > 0)
        {
            inventoryPayload["lstSKU"] = inventory.SellingUnits;
            inventoryPayload["hasUOM"] = true;
            inventoryPayload["UOMBase"] = 1;
        }
        else
        {
            inventoryPayload["lstSKU"] = Array.Empty<InventoryProductSkuDTO>();
            inventoryPayload["hasUOM"] = false;
        }

        inventoryPayload["staffCommissionA"] = inventory.StaffCommissionA;
        inventoryPayload["staffCommissionB"] = inventory.StaffCommissionB;
        inventoryPayload["staffCommissionC"] = inventory.StaffCommissionC;
        inventoryPayload["PointToRedeem"] = inventory.PointToRedeem;
        inventoryPayload["AllowPointRedemption"] = inventory.AllowPointRedemption;

        var payload = new
        {
            objInventory = inventoryPayload,
            lstMasterAccount_Branch = inventory.Branches,
            lstVendor_InventorySupplies = inventory.VendorSupplies,
            lstMasterAccount_Location = inventory.Locations,
            lstInventory_CommissionByGroup = inventory.CommissionGroups,
            PointToRedeem = inventory.PointToRedeem,
            AllowPointRedemption = inventory.AllowPointRedemption
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(payload, mediaType: JsonPatchMediaType, options: JsonOptions)
        };

        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        return await ReadFullInventoryMutationResponseAsync(
            response,
            "Inventory full save response was invalid.",
            cancellationToken);
    }

    private static async Task<ApiCallResult<InventorySaveResultDTO>> ReadFullInventoryMutationResponseAsync(
        HttpResponseMessage response,
        string invalidResponseMessage,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized)
        {
            return ApiCallResult<InventorySaveResultDTO>.Unauthorized(response.StatusCode);
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.StatusCode is HttpStatusCode.Forbidden)
        {
            return ApiCallResult<InventorySaveResultDTO>.Failure(
                response.StatusCode,
                BuildApiErrorMessage(
                    response.StatusCode,
                    responseBody,
                    "The signed-in account does not have permission to create or update inventory."));
        }

        if (!response.IsSuccessStatusCode)
        {
            return ApiCallResult<InventorySaveResultDTO>.Failure(
                response.StatusCode,
                BuildApiErrorMessage(response.StatusCode, responseBody, "Inventory full API request failed."));
        }

        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return ApiCallResult<InventorySaveResultDTO>.Ok(
                response.StatusCode,
                new InventorySaveResultDTO());
        }

        try
        {
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(responseBody, JsonOptions);
            if (apiResponse?.StatusCode > 0)
            {
                if (!apiResponse.IsSuccess)
                {
                    return ApiCallResult<InventorySaveResultDTO>.Failure(
                        response.StatusCode,
                        string.IsNullOrWhiteSpace(apiResponse.Message) ? invalidResponseMessage : apiResponse.Message);
                }

                var result = new InventorySaveResultDTO();
                if (apiResponse.Result.ValueKind == JsonValueKind.Object)
                {
                    result.Id = FindString(apiResponse.Result, "Id");
                    result.DisplayCode = FindString(apiResponse.Result, "DisplayCode");
                    result.SuccessMessage = FindString(apiResponse.Result, "SuccessMessage");
                }

                return ApiCallResult<InventorySaveResultDTO>.Ok(response.StatusCode, result);
            }
        }
        catch (JsonException)
        {
            // Some InventoryFull deployments return plain text on success.
            return ApiCallResult<InventorySaveResultDTO>.Ok(
                response.StatusCode,
                new InventorySaveResultDTO());
        }

        return ApiCallResult<InventorySaveResultDTO>.Ok(
            response.StatusCode,
            new InventorySaveResultDTO());
    }

    private static async Task<ApiCallResult<bool>> ReadMutationResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized)
        {
            return ApiCallResult<bool>.Unauthorized(response.StatusCode);
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.StatusCode is HttpStatusCode.Forbidden)
        {
            return ApiCallResult<bool>.Failure(
                response.StatusCode,
                BuildApiErrorMessage(
                    response.StatusCode,
                    responseBody,
                    "The signed-in account does not have permission to delete inventory."));
        }

        if (!response.IsSuccessStatusCode)
        {
            return ApiCallResult<bool>.Failure(
                response.StatusCode,
                BuildApiErrorMessage(response.StatusCode, responseBody, "Inventory delete request failed."));
        }

        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            try
            {
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(responseBody, JsonOptions);
                if (apiResponse?.StatusCode > 0 && !apiResponse.IsSuccess)
                {
                    return ApiCallResult<bool>.Failure(
                        response.StatusCode,
                        string.IsNullOrWhiteSpace(apiResponse.Message)
                            ? "Unable to delete inventory record."
                            : apiResponse.Message);
                }
            }
            catch (JsonException)
            {
                // A successful non-envelope response still represents a completed delete.
            }
        }

        return ApiCallResult<bool>.Ok(response.StatusCode, true);
    }

    private static async Task<ApiCallResult<T>> ReadApiResponseAsync<T>(
        HttpResponseMessage response,
        string invalidResponseMessage,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized)
        {
            return ApiCallResult<T>.Unauthorized(response.StatusCode);
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode is HttpStatusCode.Forbidden)
        {
            return ApiCallResult<T>.Failure(
                response.StatusCode,
                BuildApiErrorMessage(
                    response.StatusCode,
                    responseBody,
                    "The signed-in account does not have permission for this inventory action."));
        }

        if (!response.IsSuccessStatusCode)
        {
            return ApiCallResult<T>.Failure(
                response.StatusCode,
                BuildApiErrorMessage(response.StatusCode, responseBody, "Inventory API request failed."));
        }

        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return ApiCallResult<T>.Failure(response.StatusCode, invalidResponseMessage);
        }

        try
        {
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<T>>(responseBody, JsonOptions);
            if (apiResponse?.StatusCode > 0)
            {
                if (!apiResponse.IsSuccess)
                {
                    return ApiCallResult<T>.Failure(
                        response.StatusCode,
                        string.IsNullOrWhiteSpace(apiResponse.Message) ? "Inventory API request failed." : apiResponse.Message);
                }

                return apiResponse.Result is null
                    ? ApiCallResult<T>.Failure(response.StatusCode, invalidResponseMessage)
                    : ApiCallResult<T>.Ok(response.StatusCode, apiResponse.Result);
            }

            var directResult = JsonSerializer.Deserialize<T>(responseBody, JsonOptions);
            return directResult is null
                ? ApiCallResult<T>.Failure(response.StatusCode, invalidResponseMessage)
                : ApiCallResult<T>.Ok(response.StatusCode, directResult);
        }
        catch (JsonException)
        {
            return ApiCallResult<T>.Failure(response.StatusCode, invalidResponseMessage);
        }
    }

    private static string BuildApiErrorMessage(HttpStatusCode statusCode, string responseBody, string fallbackMessage)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return $"{fallbackMessage} ({(int)statusCode}).";
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            var message = FindString(root, "message");
            if (!string.IsNullOrWhiteSpace(message))
            {
                return message;
            }

            var title = FindString(root, "title");
            var detail = FindString(root, "detail");
            if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(detail))
            {
                return $"{title}: {detail}";
            }

            if (!string.IsNullOrWhiteSpace(title))
            {
                return $"{title} ({(int)statusCode}).";
            }
        }
        catch (JsonException)
        {
            return $"{fallbackMessage} ({(int)statusCode}).";
        }

        return $"{fallbackMessage} ({(int)statusCode}).";
    }

    private static string? FindString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString();
            }
        }

        return null;
    }
}
