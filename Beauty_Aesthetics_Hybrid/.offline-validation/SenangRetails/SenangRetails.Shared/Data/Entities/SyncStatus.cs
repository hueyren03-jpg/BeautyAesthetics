namespace SenangRetails.Shared.Data.Entities
{
    public enum SyncStatus
    {
        PendingSync = 0,
        Syncing = 1,
        Synced = 2,
        Failed = 3,
        Conflict = 4
    }
}
