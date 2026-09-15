//using System.Text.Json.Serialization;

//namespace SenangRetails.Shared.ApiClient
//{
//    public class ItemMasterAC : BaseAC
//    {
//        private readonly IUserService _userService;
//        public ItemMasterAC(IUserService userService) : base() { 
//            _userService = userService;
//        }

//        public async Task<ApiResponse<List<InventoryItem>>?> LoadProxyAsync(string branchId)
//        {
//            var users = await _userService.GetUsersAsync();
//            var currentUser = users.FirstOrDefault();
//            if (currentUser == null || currentUser.AccessToken == null)
//            {
//                return null;
//            }

//            if (!CreateBearerAuthAsync(currentUser.AccessToken))
//                return null;

//            var request = new
//            {
//                id = branchId
//            };

//            return await PostAsync<object, ApiResponse<List<InventoryItem>>>(
//                "api/Inventory/LoadProxy", request);
//        }


//        public async Task<ApiResponse<string>?> DeleteInventoryItemAsync(string masterAccountId)
//        {
//            var users = await _userService.GetUsersAsync();
//            var currentUser = users.FirstOrDefault();
//            if (currentUser == null || currentUser.AccessToken == null)
//            {
//                return null;
//            }

//            if (!CreateBearerAuthAsync(currentUser.AccessToken))
//                return null;

//            return await DeleteAsync<ApiResponse<string>>(
//                $"api/Inventory/Delete?id={masterAccountId}");
//        }

//        public async Task<ApiResponse<InventoryItemDetail>?> LoadInventoryItemRecordAsync(string masterAccountId)
//        {
//            var users = await _userService.GetUsersAsync();
//            var currentUser = users.FirstOrDefault();
//            if (currentUser == null || currentUser.AccessToken == null)
//            {
//                return null;
//            }

//            if (!CreateBearerAuthAsync(currentUser.AccessToken))
//                return null;

//            var request = new
//            {
//                id = masterAccountId
//            };

//            return await PostAsync<object, ApiResponse<InventoryItemDetail>>(
//                "api/Inventory/LoadRecord", request);
//        }

//        public async Task<ApiResponse<List<InventoryItem>>?> GetModifiedItemsAsync(DateTime date)
//        {
//            var users = await _userService.GetUsersAsync();
//            var currentUser = users.FirstOrDefault();
//            if (currentUser == null || currentUser.AccessToken == null)
//            {
//                return null;
//            }

//            if (!CreateBearerAuthAsync(currentUser.AccessToken))
//                return null;

//            var request = new
//            {
//                date = date.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") 
//            };

//            return await PostAsync<object, ApiResponse<List<InventoryItem>>>(
//                "api/Inventory/GetModifiedItems", request);
//        }

//        public async Task<ApiResponse<List<string>>?> GetDeletedItemsAsync(DateTime date)
//        {
//            var users = await _userService.GetUsersAsync();
//            var currentUser = users.FirstOrDefault();
//            if (currentUser == null || currentUser.AccessToken == null)
//            {
//                return null;
//            }

//            if (!CreateBearerAuthAsync(currentUser.AccessToken))
//                return null;

//            var request = new
//            {
//                date = date.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") 
//            };

//            return await PostAsync<object, ApiResponse<List<string>>>(
//                "api/Inventory/GetDeletedItems", request);
//        }

//    }

//    // API Response Model for Inventory Items
//    public class InventoryItem
//    {
//        [JsonPropertyName("MasterAccountID")]
//        public string? MasterAccountID { get; set; }

//        [JsonPropertyName("AlphaCode")]
//        public string? AlphaCode { get; set; }

//        [JsonPropertyName("NumericCode")]
//        public int NumericCode { get; set; }

//        [JsonPropertyName("DisplayCode")]
//        public string? DisplayCode { get; set; }

//        [JsonPropertyName("AccountName")]
//        public string? AccountName { get; set; }

//        [JsonPropertyName("AccountTypeID")]
//        public int AccountTypeID { get; set; }

//        [JsonPropertyName("CreatedDateTime")]
//        public DateTime CreatedDateTime { get; set; }

//        [JsonPropertyName("ModifiedDateTime")]
//        public DateTime ModifiedDateTime { get; set; }

//        [JsonPropertyName("InventoryTypeID")]
//        public int InventoryTypeID { get; set; }

//        [JsonPropertyName("ItemGroupID")]
//        public string? ItemGroupID { get; set; }

//        [JsonPropertyName("ItemGroupName")]
//        public string? ItemGroupName { get; set; }

//        [JsonPropertyName("SalesDescription")]
//        public string? SalesDescription { get; set; }

//        [JsonPropertyName("ProductCode")]
//        public string? ProductCode { get; set; }

//        [JsonPropertyName("ImagePath")]
//        public string? ImagePath { get; set; }

//        [JsonPropertyName("ImageFileName")]
//        public string? ImageFileName { get; set; }

//        [JsonPropertyName("SalesPrice")]
//        public decimal SalesPrice { get; set; }

//        [JsonPropertyName("SalesStandardCost")]
//        public decimal SalesStandardCost { get; set; }

//        [JsonPropertyName("IsSold")]
//        public bool IsSold { get; set; }

//        [JsonPropertyName("PurchasePrice")]
//        public decimal PurchasePrice { get; set; }

//        [JsonPropertyName("PurchaseDescription")]
//        public string? PurchaseDescription { get; set; }

//        [JsonPropertyName("VendorItemCode")]
//        public string? VendorItemCode { get; set; }

//        [JsonPropertyName("AccountStatus")]
//        public string? AccountStatus { get; set; }

//        [JsonPropertyName("BranchID")]
//        public string? BranchID { get; set; }

//        [JsonPropertyName("StockReorderLevel")]
//        public decimal StockReorderLevel { get; set; }

//        [JsonPropertyName("StockMaxLevel")]
//        public decimal StockMaxLevel { get; set; }

//        [JsonPropertyName("StockPackLevel")]
//        public decimal StockPackLevel { get; set; }

//        [JsonPropertyName("UnitOfMeasureID")]
//        public string? UnitOfMeasureID { get; set; }

//        [JsonPropertyName("ItemTaxGroupID")]
//        public string? ItemTaxGroupID { get; set; }

//        [JsonPropertyName("ItemTaxGroupName")]
//        public string? ItemTaxGroupName { get; set; }

//        [JsonPropertyName("IsTaxInclusive")]
//        public bool IsTaxInclusive { get; set; }
//        [JsonPropertyName("TaxCodeID")]
//        public string? TaxCodeID { get; set; }
//    }

//    // API Response Model for Inventory Item Detail (LoadRecord)
//    public class InventoryItemDetail
//    {
//        [JsonPropertyName("MasterAccountID")]
//        public string? MasterAccountID { get; set; }

//        [JsonPropertyName("AlphaCode")]
//        public string? AlphaCode { get; set; }

//        [JsonPropertyName("NumericCode")]
//        public int NumericCode { get; set; }

//        [JsonPropertyName("DisplayCode")]
//        public string? DisplayCode { get; set; }

//        [JsonPropertyName("AccountName")]
//        public string? AccountName { get; set; }

//        [JsonPropertyName("CreatedDateTime")]
//        public DateTime CreatedDateTime { get; set; }

//        [JsonPropertyName("ModifiedDateTime")]
//        public DateTime ModifiedDateTime { get; set; }

//        [JsonPropertyName("ItemGroupID")]
//        public string? ItemGroupID { get; set; }

//        [JsonPropertyName("ItemGroupName")]
//        public string? ItemGroupName { get; set; }

//        [JsonPropertyName("SalesDescription")]
//        public string? SalesDescription { get; set; }

//        [JsonPropertyName("ProductCode")]
//        public string? ProductCode { get; set; }

//        [JsonPropertyName("ImagePath")]
//        public string? ImagePath { get; set; }

//        [JsonPropertyName("ImageFileName")]
//        public string? ImageFileName { get; set; }

//        [JsonPropertyName("SalesPrice")]
//        public decimal SalesPrice { get; set; }

//        [JsonPropertyName("SalesStandardCost")]
//        public decimal SalesStandardCost { get; set; }

//        [JsonPropertyName("IsSold")]
//        public bool IsSold { get; set; }

//        [JsonPropertyName("PurchasePrice")]
//        public decimal PurchasePrice { get; set; }

//        [JsonPropertyName("PurchaseDescription")]
//        public string? PurchaseDescription { get; set; }

//        [JsonPropertyName("AccountStatus")]
//        public string? AccountStatus { get; set; }

//        [JsonPropertyName("BranchID")]
//        public string? BranchID { get; set; }

//        [JsonPropertyName("IsTaxInclusive")]
//        public bool IsTaxInclusive { get; set; }

//        [JsonPropertyName("TaxCodeID")]
//        public string? TaxCodeID { get; set; }

//        [JsonPropertyName("VendorItemCode")]
//        public string? VendorItemCode { get; set; }

//        [JsonPropertyName("UnitOfMeasureID")]
//        public string? UnitOfMeasureID { get; set; }

//        [JsonPropertyName("ItemTaxGroupID")]
//        public string? ItemTaxGroupID { get; set; }

//        [JsonPropertyName("ItemTaxGroupName")]
//        public string? ItemTaxGroupName { get; set; }

//        [JsonPropertyName("StockReorderLevel")]
//        public decimal StockReorderLevel { get; set; }
//    }
//}