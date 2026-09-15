using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SenangRetails.Shared.Models.DTOs
{
    public class BranchItem
    {
        [JsonPropertyName("BranchID")] public string? BranchID { get; set; }
        [JsonPropertyName("Branch")] public string? Branch { get; set; }

        [JsonPropertyName("BranchGroupID")] public string? BranchGroupID { get; set; }
        [JsonPropertyName("Address1")] public string? Address1 { get; set; }
        [JsonPropertyName("Address2")] public string? Address2 { get; set; }
        [JsonPropertyName("Address3")] public string? Address3 { get; set; }
        [JsonPropertyName("Email")] public string? Email { get; set; }
        [JsonPropertyName("Phone")] public string? Phone { get; set; }

        [JsonPropertyName("CompanyName")] public string? CompanyName { get; set; }

        [JsonPropertyName("CoRegistrationNo")] public string? CoRegistrationNo { get; set; }

        [JsonPropertyName("CurrencyID")] public string? CurrencyID { get; set; }
        [JsonPropertyName("CurrencyName")] public string? CurrencyName { get; set; }
        [JsonPropertyName("CountryName")] public string? CountryName { get; set; }
        [JsonPropertyName("TaxTypeID")] public string? TaxTypeID { get; set; }
        [JsonPropertyName("TaxTypeName")] public string? TaxTypeName { get; set; }
        [JsonPropertyName("DefaultSalesTaxCodeID")] public string? DefaultSalesTaxCodeID { get; set; }
        [JsonPropertyName("LogoPath")] public string? LogoPath { get; set; }

        [JsonPropertyName("CustomerID")] public string? CustomerID { get; set; }
        [JsonPropertyName("CustomerName")] public string? CustomerName { get; set; }

        [JsonPropertyName("GSTStartDate")] public DateTime? GSTStartDate { get; set; }

        [JsonPropertyName("TIN")] public string? TIN { get; set; }

        [JsonPropertyName("eInvoiceType")] public string? eInvoiceType { get; set; }
        [JsonPropertyName("eInvoiceClientID")] public string? eInvoiceClientID { get; set; }
        [JsonPropertyName("eInvoiceClientSecret")] public string? eInvoiceClientSecret { get; set; }
        [JsonPropertyName("PeppolID")] public string? PeppolID { get; set; }

        [JsonPropertyName("eInvoiceLiveDate")] public DateTime? eInvoiceLiveDate { get; set; }

        [JsonPropertyName("ImagePath")] public string? ImagePath { get; set; }
        [JsonPropertyName("Website")] public string? Website { get; set; }
    }
}
