using EBI.DM;
using EBI.EF;
using EBI.UC;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.Entities;
using SenangRetails.Shared.Services;
using SenangRetails.Shared.Services.AuthService;
using static SenangRetails.Shared.Pages.Home;
using InventoryFullRequest = SenangRetails.Shared.Models.DTOs.InventoryFullCreateRequest;

namespace SenangRetails.Shared.ApiClient
{
    public class InventoryAC : BaseAC
    {
        private readonly IStoreTokenService _tokenService;
        private readonly AppState _appState;

        public InventoryAC(IStoreTokenService tokenService, AppState appState) : base()
        {
            _tokenService = tokenService;
            _appState = appState;
        }

        private async Task<bool> SetBearerToken()
        {
            var token = await _tokenService.GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return false;
            return CreateBearerAuthAsync(token);
        }

        /// <summary>Load inventory items by branch. Pass empty string to load all.</summary>
        public async Task<ApiResponseRoot<List<InventoryDM>>?> LoadProxyAsync(string branchId = "")
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<object, ApiResponseRoot<List<InventoryDM>>>(
                "api/Inventory/LoadProxy", new { id = string.IsNullOrEmpty(branchId) ? null : branchId });
        }

        /// <summary>Load inventory items grouped by category for a specific branch.</summary>
        public async Task<ApiResponseRoot<Dictionary<string, List<InventoryDM>>>?> LoadProxyByItemGroupAsync(string branchId)
        {
            if (!await SetBearerToken()) return null;
            var payload = new { id = string.IsNullOrEmpty(branchId) ? null : branchId };
            return await PostAsync<object, ApiResponseRoot<Dictionary<string, List<InventoryDM>>>>(
                "api/Inventory/LoadProxyByItemGroup", payload);
        }

        /// <summary>Load the InventoryDM section of a single item.</summary>
        public async Task<ApiResponseRoot<InventoryDM>?> LoadRecordAsync(string masterAccountId)
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<object, ApiResponseRoot<InventoryDM>>(
                "api/Inventory/LoadRecord", new { id = masterAccountId });
        }

        public async Task<ApiResponseRoot<PackageSaveResult>?> CreateAsync(Inventory model)
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<Inventory, ApiResponseRoot<PackageSaveResult>>(
                "api/InventoryFull/Create", model);
        }

        public async Task<ApiResponseRoot<object>?> UpdateAsync(Inventory model)
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<Inventory, ApiResponseRoot<object>>(
                "api/InventoryFull/Update", model);
        }

        /// <summary>Load the full Inventory aggregate (including lstPackage, lstMembershipCredit) for a single item.</summary>
        public async Task<Inventory?> LoadFullAsync(string masterAccountId)
        {
            if (!await SetBearerToken()) return null;
            var response = await PostAsync<object, ApiResponseRoot<Inventory>>(
                "api/InventoryFull/LoadRecord", new { id = masterAccountId });
            return (response?.statusCode >= 200 && response.statusCode < 300) ? response.result : null;
        }

        /// <summary>
        /// Load the full Inventory aggregate using our own DTO (guaranteed public setters).
        /// Avoids EBI DLL deserialization issues where lstMembershipCredit may not be writable.
        /// </summary>
        public async Task<InventoryFullLoadDetail?> LoadFullDetailAsync(string masterAccountId)
        {
            if (!await SetBearerToken()) return null;
            var response = await PostAsync<object, ApiResponseRoot<InventoryFullLoadDetail>>(
                "api/InventoryFull/LoadRecord", new { id = masterAccountId });
            System.Diagnostics.Debug.WriteLine($"[LoadFullDetailAsync] raw={LastPostResponseBody}");
            return (response?.statusCode >= 200 && response.statusCode < 300) ? response.result : null;
        }

        /// <summary>
        /// Save a package/topup (insert OR update) with membership credits at root level.
        /// Both insert and update use api/InventoryFull/Create; objInventory.saveAction
        /// ("Added"/"Changed") tells the server whether to insert or update.
        /// </summary>
        public async Task<ApiResponseRoot<PackageSaveResult>?> CreatePackageFullAsync(InventoryFullRequest request)
        {
            if (!await SetBearerToken()) return null;
            System.Diagnostics.Debug.WriteLine($"[CreatePackageFullAsync] {System.Text.Json.JsonSerializer.Serialize(request)}");
            return await PostAsync<InventoryFullRequest, ApiResponseRoot<PackageSaveResult>>(
                "api/InventoryFull/Create", request);
        }

        /// <summary>
        /// Update a package/topup with membership credits at root level.
        /// Uses api/InventoryFull/Update (distinct from Create) so the server
        /// runs its update code path which processes lstMembershipCredit at root level.
        /// </summary>
        public async Task<ApiResponseRoot<PackageSaveResult>?> UpdatePackageFullAsync(InventoryFullRequest request)
        {
            if (!await SetBearerToken()) return null;
            System.Diagnostics.Debug.WriteLine($"[UpdatePackageFullAsync] {System.Text.Json.JsonSerializer.Serialize(request)}");
            return await PostAsync<InventoryFullRequest, ApiResponseRoot<PackageSaveResult>>(
                "api/InventoryFull/Update", request);
        }

        /// <summary>
        /// Update inventory record via the simple api/Inventory/Update endpoint.
        /// This endpoint updates all InventoryDM columns directly (including Remarks),
        /// used as a belt-and-suspenders call after InventoryFull/Update for Product/Service.
        /// </summary>
        public async Task<ApiResponseRoot<object>?> UpdateDirectAsync(InventoryCreateModel model)
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<InventoryCreateModel, ApiResponseRoot<object>>("api/Inventory/Update", model);
        }

        /// <summary>
        /// Load the full current record, convert its PascalCase keys to camelCase,
        /// patch the supplied fields, and POST the complete payload to api/Inventory/Update.
        /// Sending the full ~100-field object avoids the server overwriting unset fields with null.
        /// </summary>
        public async Task<ApiResponseRoot<object>?> UpdateFieldsFullPayloadAsync(
            string masterAccountId,
            string? remarks,
            string? unitOfMeasureID,
            string? unitOfMeasureName,
            decimal? pointToRedeem = null,
            bool? allowPointRedemption = null,
            decimal? stockReorderLevel = null)
        {
            if (!await SetBearerToken()) return null;

            // Load full current record so we have all existing field values
            await PostAsync<object, ApiResponseRoot<object>>("api/Inventory/LoadRecord", new { id = masterAccountId });
            var rawBody = LastPostResponseBody;

            System.Text.Json.JsonElement resultElement;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(rawBody);
                System.Text.Json.JsonElement temp;
                if (!doc.RootElement.TryGetProperty("result", out temp) &&
                    !doc.RootElement.TryGetProperty("Result", out temp))
                    return null;
                resultElement = temp.Clone();
            }
            catch { return null; }

            if (resultElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                return null;

            // Convert PascalCase keys → camelCase to match api/Inventory/Update schema
            var node = new System.Text.Json.Nodes.JsonObject();
            foreach (var prop in resultElement.EnumerateObject())
            {
                var camelKey = prop.Name.Length > 0
                    ? char.ToLower(prop.Name[0]) + prop.Name.Substring(1)
                    : prop.Name;
                try { node[camelKey] = System.Text.Json.Nodes.JsonNode.Parse(prop.Value.GetRawText()); }
                catch { node[camelKey] = null; }
            }

            // Patch only the fields we want to update
            if (remarks != null) node["remarks"] = remarks;
            if (unitOfMeasureID != null) node["unitOfMeasureID"] = unitOfMeasureID;
            if (unitOfMeasureName != null) node["unitOfMeasureName"] = unitOfMeasureName;
            if (pointToRedeem != null) node["pointToRedeem"] = pointToRedeem.Value;
            if (allowPointRedemption != null) node["allowPointRedemption"] = allowPointRedemption.Value;
            if (stockReorderLevel != null) node["stockReorderLevel"] = stockReorderLevel.Value;
            node["saveAction"] = "Changed";
            node["isDirty"] = true;

            System.Diagnostics.Debug.WriteLine($"[UpdateFieldsFullPayloadAsync] payload={node.ToJsonString()}");
            return await PostRawJsonAsync<ApiResponseRoot<object>>("api/Inventory/Update", node.ToJsonString());
        }

        /// <summary>Delete an inventory item by its MasterAccountID.</summary>
        public async Task<ApiResponseRoot<object>?> DeleteAsync(string masterAccountId)
        {
            if (!await SetBearerToken()) return null;
            return await DeleteAsync<ApiResponseRoot<object>>(
                $"api/InventoryFull/Delete?id={Uri.EscapeDataString(masterAccountId)}");
        }

        public async Task<ApiResponseRoot<List<Models.DTOs.LowStockItem>>?> GetStockBelowReorderPoint()
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<object, ApiResponseRoot<List<Models.DTOs.LowStockItem>>>(
                "api/Inventory/GetStockBelowReorderPoint", new { id = _appState.CurrentBranch?.BranchID });
        }

        public async Task<ApiResponseRoot<Dictionary<string, StockBalanceItem>>?> GetStockBalanceByBranchAndByItem(StockBalanceRequest request)
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<StockBalanceRequest, ApiResponseRoot<Dictionary<string, StockBalanceItem>>>(
                    "api/Inventory/GetStockBalanceByBranchAndByItem", request);
        }

        public async Task<ApiResponseRoot<string>> DocStockGRNCreateRecord(EBI.UC.Doc_Stock_GRN request)
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<EBI.UC.Doc_Stock_GRN, ApiResponseRoot<string>>(
                    "api/Doc_Stock_GRN/CreateRecord", request);
        }

        public async Task<ApiResponseRoot<List<InventoryMovement_PendingAcceptDM>>?> GetPendingAcceptDocumentByBranchId()
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<object, ApiResponseRoot<List<InventoryMovement_PendingAcceptDM>>>(
                    "api/InventoryMovement_PendingAccept/GetPendingAcceptDocumentByBranchID", new { id = _appState.CurrentBranch?.BranchID}
                );
        }

        public async Task<ApiResponseRoot<List<InventoryMovement_PendingAcceptDM>>> GetPendingAcceptDocumentDetails(string documentId)
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<object, ApiResponseRoot<List<InventoryMovement_PendingAcceptDM>>>(
                    "api/InventoryMovement_PendingAccept/GetPendingAcceptDocumentDetails", new { id = documentId }
                );
        }

        public async Task<ApiResponseRoot<string>> AcceptStockIn(string documentId)
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<object, ApiResponseRoot<string>>(
                    "api/InventoryMovement_PendingAccept/AcceptStockIn", new { id = documentId }
                );
        }
    }
}
