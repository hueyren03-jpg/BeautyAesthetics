using System;
using System.Collections.Generic;

namespace Beauty_Aesthetics_WebPos.Components.Models
{
    public class Customer
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string? SystemID { get; set; } = "";
        public string AccountStatus { get; set; } = "Active";


        // VIP
        public bool IsVip { get; set; }

        // Personal Information
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string IdentificationNumber { get; set; } = "";
        public string Email { get; set; } = "";
        public DateTime? DateOfBirth { get; set; }
        public string Gender { get; set; } = "";
        public string Salutation { get; set; } = "";
        public string ContactNumber1 { get; set; } = "";
        public string ContactNumber2 { get; set; } = "";
        public string Consultant { get; set; } = "";
        public string ConsultantId { get; set; } = "";
        public string MembershipType { get; set; } = "";
        public string MembershipTypeId { get; set; } = "";
        public string BranchId { get; set; } = "";
        public string Source { get; set; } = "";
        public string ExternalCode { get; set; } = "";

        // E-Invoice
        public bool UseConsolidatedEInvoice { get; set; } = true;
        public string InvoiceIdentificationType { get; set; } = "";
        public string InvoiceIdentificationNumber { get; set; } = "";
        public string TaxIdentificationNumber { get; set; } = "";
        public bool UseExistingIdentification { get; set; } = false;

        // Address
        public string Address1 { get; set; } = "";
        public string Address2 { get; set; } = "";
        public string Address3 { get; set; } = "";
        public string Country { get; set; } = "";
        public string State { get; set; } = "";
        public string City { get; set; } = "";
        public string Postcode { get; set; } = "";

        // Medical Information
        public string Smoker { get; set; } = ""; // Yes/No
        public string DrugAllergies { get; set; } = "";
        public string CurrentIllness { get; set; } = "";
        public string CustomerTag { get; set; } = "";
        public string AlertAllergies { get; set; } = "";

        // Employment / Payment
        public string Department { get; set; } = "";
        public string EmployeeNo { get; set; } = "";
        public string PayeeOrigin { get; set; } = ""; // Self-Paid / Insurance

        // Miscellaneous
        public string Race { get; set; } = "";
        public string Religion { get; set; } = "";
        public string Occupation { get; set; } = "";
        public string IncomeRange { get; set; } = "";
        public string MaritalStatus { get; set; } = "";
        public string PreferredLanguage { get; set; } = "";
        public string Referrer { get; set; } = "";
        public string ReferrerContact { get; set; } = "";
        public string ReferrerRelationship { get; set; } = "";

        // Relationships & Guardians
        public List<ContactRelation> Relationships { get; set; } = new();
        public List<ContactRelation> Guardians { get; set; } = new();

        // Notes
        public string Notes { get; set; } = "";

        // Notification & Marketing
        public bool OptInNotifications { get; set; } = false;
        public bool OptInPromotions { get; set; } = false;

        // Profile Photo
        public string? PhotoPath { get; set; } = "";
        public string? PhotoFileName { get; set; }


    }

    public class ContactRelation
    {
        public string Name { get; set; } = "";
        public string Contact { get; set; } = "";
        public string Relationship { get; set; } = "";
        public bool IsEmergencyContact { get; set; } = false;
    }
}
