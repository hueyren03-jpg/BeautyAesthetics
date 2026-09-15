using SenangRetails.Shared.Models;
using System.Collections.Generic;

namespace SenangRetails.Shared.Services.NotificationService
{
    public interface INotificationService
    {
        IReadOnlyList<AppNotification> Notifications { get; }
        event Action? OnChanged;

        void Add(AppNotification notification);
        void MarkRead(string id);
        void MarkAllRead();
        void ClearAll();

        void LoadFromJson(string json);
        string ToJson();
    }
}
