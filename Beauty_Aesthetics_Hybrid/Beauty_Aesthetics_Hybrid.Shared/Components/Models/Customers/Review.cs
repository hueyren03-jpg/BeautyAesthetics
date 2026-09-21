namespace Beauty_Aesthetics_WebPos.Components.Models
{
    public class Review
    {
        public int Id { get; set; }
        public string Author { get; set; } = "";
        public int Rating { get; set; }
        public string Text { get; set; } = "";
        public string? Reply { get; set; }
        public bool isEdittingReply { get; set; } = false;
        public string Source { get; set; } = "google";
    }
}
