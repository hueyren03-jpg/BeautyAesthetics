using System.Net;
using System.Text.Json;
using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.ViewModels;
using Beauty_Aesthetics_WebPos.Components.Services.Feedback;
using EBI.DM;
using EBI.Enum;

namespace Beauty_Aesthetics_WebPos.Components.Services.Inventory;

public sealed class ServiceItemService : IServiceItemService
{
    private const int ServiceInventoryTypeId = 3;
    private const string ServiceInventoryTypeName = "Non-Stock/Service";
    private static readonly DateTime InventoryAvailableFrom = new(2000, 1, 1);
    private static readonly DateTime InventoryAvailableTo = new(2049, 12, 31);

    private readonly ServiceInventoryAC serviceInventoryAC;
    private readonly AppFeedbackService feedback;

    public ServiceItemService(ServiceInventoryAC serviceInventoryAC, AppFeedbackService feedback)
    {
        this.serviceInventoryAC = serviceInventoryAC;
        this.feedback = feedback;
    }

    public async Task<ApiCallResult<IReadOnlyList<ServiceViewModel.ServiceItem>>> LoadServiceItemsAsync(
        string branchId = "hq",
        CancellationToken cancellationToken = default)
    {
        var result = await serviceInventoryAC.LoadProxyByItemGroupAsync(branchId, cancellationToken);
        if (!result.Success || result.Value is null)
        {
            return ApiCallResult<IReadOnlyList<ServiceViewModel.ServiceItem>>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to load service records.");
        }

        var services = result.Value
            .SelectMany(group => group.Value ?? new List<InventoryDM>())
            .Where(record => record.InventoryTypeID == ServiceInventoryTypeId)
            .Select(ToServiceItem)
            .OrderBy(service => service.ServiceName)
            .ToList();

        return ApiCallResult<IReadOnlyList<ServiceViewModel.ServiceItem>>.Ok(result.StatusCode, services);
    }

    public async Task<ApiCallResult<bool>> CreateServiceAsync(
        ServiceViewModel.ServiceItem service,
        string branchId = "hq",
        CancellationToken cancellationToken = default)
    {
        var record = CreateServiceRecord(service, branchId);
        record.SaveAction = EntityState.Added;
        record.IsDirty = true;

        var result = await serviceInventoryAC.CreateSimpleAsync(record, cancellationToken);

        var saveResult = ToSaveResult(result, "Unable to create service.");
        if (saveResult.Success)
        {
            feedback.Success("Service created successfully.", "Service created");
        }
        else
        {
            feedback.Error(saveResult.ErrorMessage ?? "Unable to create service.", "Service not created");
        }

        return saveResult;
    }

    public async Task<ApiCallResult<bool>> UpdateServiceAsync(
        ServiceViewModel.ServiceItem service,
        string branchId = "hq",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(service.MasterAccountId))
        {
            return ApiCallResult<bool>.Failure(
                HttpStatusCode.BadRequest,
                "The selected service has no record ID.");
        }

        var loadResult = await serviceInventoryAC.LoadRecordAsync(service.MasterAccountId, cancellationToken);
        if (!loadResult.Success || loadResult.Value is null)
        {
            return ApiCallResult<bool>.Failure(
                loadResult.StatusCode,
                loadResult.ErrorMessage ?? "Unable to load the service before updating it.");
        }

        ApplyServiceValues(loadResult.Value, service, branchId);
        loadResult.Value.SaveAction = EntityState.Changed;
        loadResult.Value.IsDirty = true;

        var result = await serviceInventoryAC.UpdateSimpleAsync(loadResult.Value, cancellationToken);

        var saveResult = ToSaveResult(result, "Unable to update service.");
        if (saveResult.Success)
        {
            feedback.Success("Service updated successfully.", "Service updated");
        }
        else
        {
            feedback.Error(saveResult.ErrorMessage ?? "Unable to update service.", "Service not updated");
        }

        return saveResult;
    }

    public async Task<ApiCallResult<bool>> DeleteServiceAsync(
        string masterAccountId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(masterAccountId))
        {
            return ApiCallResult<bool>.Failure(
                HttpStatusCode.BadRequest,
                "The selected service has no record ID.");
        }

        var result = await serviceInventoryAC.DeleteSimpleAsync(masterAccountId, cancellationToken);
        if (result.Success)
        {
            feedback.Success("Service deleted successfully.", "Service deleted");
        }
        else
        {
            feedback.Error(result.ErrorMessage ?? "Unable to delete service.", "Service not deleted");
        }

        return result;
    }
    private static ServiceViewModel.ServiceItem ToServiceItem(InventoryDM record)
    {
        var sku = FirstNonEmpty(record.DisplayCode, record.VendorItemCode, record.MasterAccountID) ?? string.Empty;
        var name = FirstNonEmpty(record.AccountName, record.SalesDescription, sku) ?? string.Empty;
        var category = FirstNonEmpty(record.ItemGroupName, record.ItemCategoryName, record.ItemGroupID) ?? string.Empty;
        var duration = Math.Max(0, record.ServiceMinutes);
        var buffer = Math.Max(0, record.BufferMinutes);

        return new ServiceViewModel.ServiceItem(
            sku,
            name,
            category,
            $"{duration} mins",
            buffer,
            record.HasPackage,
            record.SalesPrice,
            record.MasterAccountID,
            FirstNonEmpty(record.AccountStatus, "Active") ?? "Active",
            record.BranchID ?? string.Empty,
            FirstNonEmpty(record.InventoryTypeName, ServiceInventoryTypeName) ?? ServiceInventoryTypeName,
            record.SalesDescription ?? string.Empty,
            record.AvailableDateFrom,
            record.AvailableDateTo,
            record.AvailableTimeFrom,
            record.AvailableTimeTo,
            record.eInvoiceClassificationCode ?? string.Empty,
            record.ImagePath ?? string.Empty,
            record.ImageFileName ?? string.Empty);
    }

    private static InventoryDM CreateServiceRecord(
        ServiceViewModel.ServiceItem service,
        string branchId)
    {
        var record = new InventoryDM();
        ApplyServiceValues(record, service, branchId);
        record.AccountTypeID = 4;
        record.AccountStatus = "Active";
        record.IsSold = true;
        record.IsPurchased = false;
        record.CreatedDateTime = DateTime.Now;
        record.AvailableDateFrom = InventoryAvailableFrom;
        record.AvailableDateTo = InventoryAvailableTo;
        record.AvailableTimeFrom = TimeSpan.Zero;
        record.AvailableTimeTo = new TimeSpan(23, 59, 59);
        record.QuantityFactor = 1;
        record.UnitOfMeasureID = "UNIT";
        record.ValidityDays = 8888;
        record.MemberCreditSettlementRatio = 1;
        record.KitchenCopies = 1;
        record.UOMBase = 1;
        record.ReportingUOMBase = 1;
        record.DefaultDosage = 1;
        record.eInvoiceClassificationCode = "022";
        return record;
    }

    private static void ApplyServiceValues(
        InventoryDM record,
        ServiceViewModel.ServiceItem service,
        string branchId)
    {
        record.InventoryTypeID = ServiceInventoryTypeId;
        record.InventoryTypeName = ServiceInventoryTypeName;
        record.AccountName = service.ServiceName.Trim();
        record.SalesDescription = service.ServiceName.Trim();
        record.DisplayCode = service.Sku.Trim();
        record.ItemGroupName = service.Category.Trim();
        record.ServiceMinutes = ParseDuration(service.DurationSpend);
        record.BufferMinutes = Math.Max(0, service.BufferTimeMinutes);
        record.HasPackage = service.IsBundle;
        record.SalesPrice = service.Price;
        record.BranchID = NormalizeBranchId(branchId);
    }

    private static string NormalizeBranchId(string branchId)
    {
        return string.IsNullOrWhiteSpace(branchId)
            ? "HQ"
            : branchId.Trim().ToUpperInvariant();
    }

    private static int ParseDuration(string duration)
    {
        var firstPart = duration.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return int.TryParse(firstPart, out var minutes) ? Math.Max(0, minutes) : 0;
    }

    private static ApiCallResult<bool> ToSaveResult(
        ApiCallResult<JsonElement> result,
        string fallbackMessage)
    {
        return result.Success
            ? ApiCallResult<bool>.Ok(result.StatusCode, true)
            : ApiCallResult<bool>.Failure(result.StatusCode, result.ErrorMessage ?? fallbackMessage);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}

