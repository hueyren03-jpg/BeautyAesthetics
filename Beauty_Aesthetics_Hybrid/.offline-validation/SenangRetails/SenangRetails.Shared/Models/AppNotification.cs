using System;
using System.Text.Json.Serialization;

namespace SenangRetails.Shared.Models
{
    public class AppNotification
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Icon { get; set; } = "🔔";

        /// <summary>Language key for the notification title (e.g. "NotifNewMemberTitle").</summary>
        public string TitleKey { get; set; } = "";

        /// <summary>Language key for the notification message body (e.g. "NotifNewMemberMsg").</summary>
        public string MessageKey { get; set; } = "";

        /// <summary>Optional variable substituted into {0} in the translated message (e.g. member name).</summary>
        public string? MessageParam { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsRead { get; set; } = false;

        [JsonIgnore]
        public string TimeAgo
        {
            get
            {
                var diff = DateTime.Now - CreatedAt;
                if (diff.TotalSeconds < 60) return "Just now";
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
                if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
                if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
                return CreatedAt.ToString("dd MMM yyyy");
            }
        }
    }
}
