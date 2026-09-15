using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Models.DTOs
{
    public class AppointmentDTO
    {
        public bool IsLoading { get; set; }

        public string AppointmentID { get; set; } = string.Empty;

        public string? BookingID { get; set; }

        public string EmployeeID { get; set; } = string.Empty;

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public int BusyStatus { get; set; }

        public int Label { get; set; }

        public string? Memo { get; set; }

        public bool AllDay { get; set; }

        public string? RecurranceInfo { get; set; }

        public string? ReminderInfo { get; set; }

        public string? ManualEmployeeID { get; set; }

        public string CustomerID { get; set; } = string.Empty;

        public string CustomerName { get; set; } = string.Empty;

        public string BranchID { get; set; } = string.Empty;

        public int PaymentStatus { get; set; }

        public string? AppointmentSubject { get; set; }

        public string AppointmentLocation { get; set; } = string.Empty;

        public int AppointmentType { get; set; }

        public string? Createdby { get; set; }

        public DateTime CreatedDateTime { get; set; }

        public string? Modifiedby { get; set; }

        public DateTime ModifiedDateTime { get; set; }

        public DateTime TransactionTimeStamp { get; set; }

        public string? InventoryIDs { get; set; }

        public string? SalesDescriptions { get; set; }

        public string? SalesPersonCode { get; set; }

        public string RequestedBy { get; set; } = string.Empty;

        public int SaveAction { get; set; }

        public bool IsDirty { get; set; }
    }
    

    public class CreateAppointmentRequest
    {
        public bool isLoading { get; set; } = false;
        public string appointmentID { get; set; } = string.Empty;
        public string bookingID { get; set; } = string.Empty;
        public string employeeID { get; set; } = string.Empty;
        public DateTime startTime { get; set; }
        public DateTime endTime { get; set; }
        public int busyStatus { get; set; }
        public int label { get; set; }
        public string memo { get; set; } = string.Empty;
        public bool allDay { get; set; } = false;
        public string recurranceInfo { get; set; } = string.Empty;
        public string reminderInfo { get; set; } = string.Empty;
        public string manualEmployeeID { get; set; } = string.Empty;
        public string customerID { get; set; } = string.Empty;
        public string customerName { get; set; } = string.Empty;
        public string branchID { get; set; } = string.Empty;
        public int paymentStatus { get; set; }
        public string appointmentSubject { get; set; } = string.Empty;
        public string appointmentLocation { get; set; } = string.Empty;
        public int appointmentType { get; set; }
        public string createdby { get; set; } = string.Empty;
        public DateTime createdDateTime { get; set; }
        public string modifiedby { get; set; } = string.Empty;
        public DateTime modifiedDateTime { get; set; }
        public DateTime transactionTimeStamp { get; set; }
        public string inventoryIDs { get; set; } = string.Empty;
        public string salesDescriptions { get; set; } = string.Empty;
        public string salesPersonCode { get; set; } = string.Empty;
        public string requestedBy { get; set; } = string.Empty;
        public string saveAction { get; set; } = "Added";
        public bool isDirty { get; set; } = true;
    }

    public class CreateAppointmentResponse
    {
        public string Id { get; set; } = string.Empty;
        public string? DisplayCode { get; set; }
        public string SuccessMessage { get; set; } = string.Empty;
    }

    public class GetAppointmentsRequest
    {
        public string customerId { get; set; } = string.Empty;
        public DateTime startDate { get; set; }
        public DateTime endDate { get; set; }
    }

    public class GetByEmployeeAndStatusRequest
    {
        public DateTime startDate { get; set; }
        public DateTime endDate { get; set; }
        public string employeeId { get; set; } = string.Empty;
        public int status { get; set; }
    }
}
