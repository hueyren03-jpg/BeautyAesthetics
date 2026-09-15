namespace SenangRetails.Shared.Models.DTOs
{
    public class MaintainDocumentNumberModel
    {
        public int DocType { get; set; }
        public string Category { get; set; } = "Document"; // "Document" or "Account"
        public string Name { get; set; } = string.Empty;
        public string Prefix { get; set; } = string.Empty;
        public long LastNumberUsed { get; set; } = 1;
        public int CharacterCount { get; set; } = 15;

        public string GetFormattedPreview()
        {
            if (CharacterCount <= 0) return Prefix + LastNumberUsed.ToString();
            
            int numericLength = CharacterCount - Prefix.Length;
            if (numericLength <= 0) numericLength = 1;

            string numberPadded = LastNumberUsed.ToString().PadLeft(numericLength, '0');
            return string.IsNullOrEmpty(Prefix) ? numberPadded : $"{Prefix}-{numberPadded}";
        }
    }
}
