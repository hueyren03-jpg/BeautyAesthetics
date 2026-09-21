using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Beauty_Aesthetics_WebPos.Models
{
    public class CustomerRating
    {
        [Key][DatabaseGenerated(DatabaseGeneratedOption.Identity)] 
        public int RatingID { get; set; } // Auto-increment int

        public string MasterAccountID { get; set; } = string.Empty; // Get Customer ID
        public string AccountName { get; set; } = string.Empty; // Get Customer Name
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime SubmittedDate { get; set; } = DateTime.UtcNow;
        public bool IsGoogleReviewRedirected { get; set; }
    }
}
