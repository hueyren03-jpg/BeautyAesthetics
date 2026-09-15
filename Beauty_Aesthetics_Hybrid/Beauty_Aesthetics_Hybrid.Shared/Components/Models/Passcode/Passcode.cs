namespace Beauty_Aesthetics_WebPos.Components.Models.Passcode
{
    public class PasscodeModel
    {
        public string? Code { get; set; }
        public string? Permission { get; set; }

        public string? UsedBy { get; set; }
        public string? UsedByEmployeeId { get; set; }
        public string? UsedDate { get; set; }
        public string? CreatedBy { get; set; }
        public string? CreatedDate { get; set; }
        public string? Status { get; set; }

        public PasscodeModel() { }

        public PasscodeModel(
            string code,
            string permission,
            string usedBy,
            string usedDate,
            string createdBy,
            string createdDate,
            string status)
        {
            Code = code;
            Permission = permission;
            UsedBy = usedBy;
            UsedDate = usedDate;
            CreatedBy = createdBy;
            CreatedDate = createdDate;
            Status = status;
        }
    }
}
