using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SenangRetails.Shared.Models
{
    public class EInvoiceProfileDM
    {

        [Required]
        public string BusinessName { get; set; } = String.Empty;

        [Required]
        public string TIN { get; set; } = String.Empty;

        public string TINType { get; set; } = String.Empty;

        [Required]
        public string MSIC { get; set; } = String.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = String.Empty;

        [Required]
        [RegularExpression(@"^\d{9,10}$", ErrorMessage = "Invalid phone number")]
        public string PhoneNumber { get; set; } = String.Empty;

        [Required]
        public string Address1 { get; set; } = String.Empty;

        public string Address2 { get; set; } = String.Empty;
        public string Address3 { get; set; } = String.Empty;

        [Required]
        public string PostalCode { get; set; } = String.Empty;

        [Required]
        public string City { get; set; } = String.Empty;

        [Required]
        public string State { get; set; } = String.Empty;

        [Required]
        public string Country { get; set; } = String.Empty;

        //[Required]
        public string SubmissionType { get; set; } = String.Empty;

        public bool SendToTestingServer { get; set; } = false;
    }
}
