using SenangRetails.Shared.Models;
using SenangRetails.Shared.Pages;
using static SenangRetails.Shared.Components.Reports.BalanceSummaryReport;
using static SenangRetails.Shared.Components.Reports.BalanceTransactionReport;
using static SenangRetails.Shared.Components.Reports.MemberCreditBalanceReport;
using static SenangRetails.Shared.Components.Reports.PackageBalanceReport;
using static SenangRetails.Shared.Components.Reports.PackageTransactionReport;
using static SenangRetails.Shared.Components.Reports.StaffSalesReport;
using static SenangRetails.Shared.Components.Reports.StockBalanceWithCostReport;
using static SenangRetails.Shared.Components.Reports.StockMovementDetailReport;
using static SenangRetails.Shared.Components.Reports.StockMovementSummaryReport;
using static SenangRetails.Shared.Pages.CollectionReport;
using static SenangRetails.Shared.Pages.DashboardReport;
using static SenangRetails.Shared.Pages.EInvoices;
using static SenangRetails.Shared.Pages.Home;
using static SenangRetails.Shared.Pages.LastVisitReport;
using static SenangRetails.Shared.Pages.MemberListReport;
using static SenangRetails.Shared.Pages.SalesByItemReport;
using static SenangRetails.Shared.Pages.SalesByTypeReport;
using static SenangRetails.Shared.Pages.TopSellingReport;
using static SenangRetails.Shared.Pages.TopSpendingReport;
using static SenangRetails.Shared.Pages.TransactionReport;
using static SenangRetails.Shared.Services.DashboardService.ReportService;

namespace SenangRetails.Shared.Services.DashboardService
{
    public interface IReportService
    {
        Task<ApiResponse<List<HourlySalesItem>>?> GetHourlySalesAsync(DateTime startDate, DateTime endDate, string branchID);
        Task<ApiResponse<List<PaymentSalesItem>>?> GetSalesByCollectionAsync(DateTime startDate, DateTime endDate, string branchID);
        Task<ApiResponse<List<Visitor>>?> GetSalesByVisitorsAsync(DateTime startDate, DateTime endDate, string branchID);
        Task<ApiResponse<SalesByType>?> GetSalesByTypeAsync(DateTime startDate, DateTime endDate, string branchID);
        Task<ApiResponse<List<SalesByItemType>>?> GetSalesByItemTypeAsync(DateTime startDate, DateTime endDate, string branchID, int inventoryTypeID);
        Task<ApiResponse<List<SalesItemTypeByItemDetail>>?> GetSalesItemTypeByItemDetailByDateAsync(DateTime startDate, DateTime endDate, string branchID, int inventoryTypeID);
        Task<ApiResponse<List<SalesBySKUItem>>?> GetSalesByItemAsync(DateTime startDate, DateTime endDate, string branchID, int inventoryTypeID);
        Task<ApiResponse<List<SalesCollectionSummaryItem>>?> GetSalesCollectionSummaryAsync(DateTime startDate, DateTime endDate, string branchID);
        Task<ApiResponse<List<TaxPayableSummaryItem>>?> GetGSTPayableSummaryByDateAsync(DateTime startDate, DateTime endDate, string branchID, string taxGroupID);
        Task<ApiResponse<List<EmployeeSalesByItemType>>?> GetEmployeeSalesByItemTypeAsync(DateTime startDate, DateTime endDate, string branchID);
        Task<ApiResponse<List<TopSalesItem>>?> GetTopSalesItemAsync(DateTime startDate, DateTime endDate, int inventoryTypeID, int recordCount, string rankBy, string branches);
        Task<ApiResponse<List<TopSalesCustomer>>?> GetTopSalesCustomerAsync(DateTime startDate, DateTime endDate, int inventoryTypeID, int recordCount, string rankBy, string branches);
        Task<ApiResponse<List<CustomerLastVisit>>?> GetCustomerLastVisitAsync(int daysRangeFrom, int daysRangeTo, int pageNumber, int pageSize);
        Task<ApiResponse<MemberStatisticSummary>?> GetMemberStatisticSummaryAsync(string id);
        Task<ApiResponse<List<MemberStatisticDetail>>?> GetMemberStatisticDetailAsync(string branchID, DateTime startDate, DateTime endDate, int pageNumber, int pageSize);
        Task<ApiResponse<List<TransactionDetails>>?> GetTransactionAsync(string strID, DateTime startDate, DateTime endDate);
        Task<ApiResponse<List<PackageBalance>>?> GetBalanceByCustomerID_NonExpiredAsync(string customerID, DateTime cutOffDate, string branches);
        Task<ApiResponse<List<PackageBalance>>?> GetBalanceByCustomerID_ExpiredAsync(string customerID, DateTime cutOffDate, string branches);
        Task<ApiResponse<List<PackageBalance>>?> GetBalanceByCustomerID_FullyRedeemedAsync(string customerID, DateTime cutOffDate, string branches);
        Task<ApiResponse<List<PackageMovement>>?> GetPackageMovementAsync(string customerID, DateTime startDate, DateTime endDate, string branches, string movementType);
        Task<ApiResponse<List<EInvoiceDetail>>?> GetAppSalesListAsync(string strID, DateTime startDate, DateTime endDate);
        Task<ApiResponse<List<MemberOtherBalanceSummary>>?> GetMemberOtherBalanceSummaryAsync(string id, DateTime cutOffDate);
        Task<ApiResponse<Dictionary<string, MemberBalanceDetail>>?> GetMemberOtherBalanceDetailAsync(string id, DateTime startDate, string balanceType);
        Task<ApiResponse<List<MemberCreditBalance>>?> GetMemberCreditBalanceSummary_WithExpiry_NonExpiredAsync(string customerID, DateTime cutOffDate, string branches);
        Task<ApiResponse<List<MemberCreditBalance>>?> GetMemberCreditBalanceSummary_WithExpiry_ExpiredAsync(string customerID, DateTime cutOffDate, string branches);
        Task<ApiResponse<List<MemberCreditBalance>>?> GetMemberCreditBalanceSummary_WithExpiry_FullyRedeemedAsync(string customerID, DateTime cutOffDate, string branches);
        Task<ApiResponse<List<LowStockItem>>?> GetStockBelowReorderPointAsync(string id);
        Task<ApiResponse<List<StockBalanceWithCost>>?> GetStockBalanceWithCostAsync(string branchIDs, DateTime endDate, string inventoryIDs);
        Task<ApiResponse<List<StockMovementSummary>>?> GetStockMovementSummaryAsync(string branchIDs, DateTime startDate, DateTime endDate, bool combineBranchValues);
        Task<ApiResponse<List<StockMovementDetail>>?> GetStockMovementDetailAsync(string branchIDs, string inventoryID, DateTime startDate, DateTime endDate);
    }
}