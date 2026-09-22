using System.Text.Json.Serialization;

namespace Beauty_Aesthetics_WebPos.Models
{
    public class CustomerRating
    {
        [JsonPropertyName("ratingID")]
        public string RatingID { get; set; } = string.Empty;

        [JsonPropertyName("customerID")]
        public string CustomerID { get; set; } = string.Empty;

        [JsonPropertyName("documentID")]
        public string DocumentID { get; set; } = string.Empty;

        [JsonPropertyName("rating")]
        public int Rating { get; set; }

        [JsonPropertyName("comment")]
        public string Comment { get; set; } = string.Empty;

        [JsonPropertyName("submissionDate")]
        public DateTime SubmissionDate { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("isGoogleReviewRedirected")]
        public bool IsGoogleReviewRedirected { get; set; }

        [JsonPropertyName("branchID")]
        public string BranchID { get; set; } = string.Empty;

        [JsonPropertyName("groupID")]
        public string GroupID { get; set; } = string.Empty;

        [JsonPropertyName("saveAction")]
        public string SaveAction { get; set; } = "Added";

        [JsonPropertyName("isDirty")]
        public bool IsDirty { get; set; } = true;
    }
}
