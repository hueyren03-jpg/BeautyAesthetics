using SenangRetails.Shared.Models;
using System.Collections.Generic;
using System.Text.Json;

namespace SenangRetails.Shared.Services.NotificationService
{
    public class NotificationService : INotificationService
    {
        private List<AppNotification> _notifications = new();

        public IReadOnlyList<AppNotification> Notifications => _notifications;

        public event Action? OnChanged;

        public void Add(AppNotification notification)
        {
            _notifications.Insert(0, notification);
            OnChanged?.Invoke();
        }

        public void MarkRead(string id)
        {
            var n = _notifications.FirstOrDefault(x => x.Id == id);
            if (n != null)
            {
                n.IsRead = true;
                OnChanged?.Invoke();
            }
        }

        public void MarkAllRead()
        {
            foreach (var n in _notifications)
                n.IsRead = true;
            OnChanged?.Invoke();
        }

        public void ClearAll()
        {
            _notifications.Clear();
            OnChanged?.Invoke();
        }

        public void LoadFromJson(string json)
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var list = JsonSerializer.Deserialize<List<AppNotification>>(json, options);
                if (list != null)
                    _notifications = list;
            }
            catch
            {
                _notifications = new List<AppNotification>();
            }
        }

        public string ToJson()
        {
            return JsonSerializer.Serialize(_notifications);
        }
    }
}
