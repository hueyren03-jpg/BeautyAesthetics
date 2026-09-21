namespace Beauty_Aesthetics_WebPos.Components.Services.Feedback;

public enum AppFeedbackKind
{
    Success,
    Error,
    Warning,
    Info,
    Loading
}

public sealed record AppFeedbackMessage(
    Guid Id,
    AppFeedbackKind Kind,
    string Title,
    string Message,
    int? DurationMs,
    bool IsDismissible,
    string? ActionLabel,
    Func<Task>? Action,
    DateTimeOffset CreatedAt);

public sealed class AppFeedbackService : IDisposable
{
    private const int MaxMessages = 4;
    private readonly object sync = new();
    private readonly List<AppFeedbackMessage> messages = [];
    private readonly Dictionary<Guid, CancellationTokenSource> dismissTimers = [];

    public event Action? Changed;

    public IReadOnlyList<AppFeedbackMessage> Messages
    {
        get
        {
            lock (sync)
            {
                return messages
                    .OrderBy(item => item.CreatedAt)
                    .ToArray();
            }
        }
    }

    public Guid Success(
        string message,
        string title = "Success",
        int durationMs = 3600,
        string? actionLabel = null,
        Func<Task>? action = null) =>
        Show(AppFeedbackKind.Success, title, message, durationMs, true, actionLabel, action);

    public Guid Error(
        string message,
        string title = "Something went wrong",
        int durationMs = 7000,
        string? actionLabel = null,
        Func<Task>? action = null) =>
        Show(AppFeedbackKind.Error, title, message, durationMs, true, actionLabel, action);

    public Guid Warning(
        string message,
        string title = "Attention needed",
        int durationMs = 5200,
        string? actionLabel = null,
        Func<Task>? action = null) =>
        Show(AppFeedbackKind.Warning, title, message, durationMs, true, actionLabel, action);

    public Guid Info(
        string message,
        string title = "Information",
        int durationMs = 4500,
        string? actionLabel = null,
        Func<Task>? action = null) =>
        Show(AppFeedbackKind.Info, title, message, durationMs, true, actionLabel, action);

    public Guid Loading(
        string message,
        string title = "Working",
        bool isDismissible = false) =>
        Show(AppFeedbackKind.Loading, title, message, null, isDismissible, null, null);

    public Guid Show(
        AppFeedbackKind kind,
        string title,
        string message,
        int? durationMs = 4500,
        bool isDismissible = true,
        string? actionLabel = null,
        Func<Task>? action = null)
    {
        var item = new AppFeedbackMessage(
            Guid.NewGuid(),
            kind,
            string.IsNullOrWhiteSpace(title) ? DefaultTitle(kind) : title.Trim(),
            string.IsNullOrWhiteSpace(message) ? DefaultMessage(kind) : message.Trim(),
            durationMs,
            isDismissible,
            string.IsNullOrWhiteSpace(actionLabel) ? null : actionLabel.Trim(),
            action,
            DateTimeOffset.UtcNow);

        List<Guid> removedIds = [];

        lock (sync)
        {
            messages.Add(item);

            while (messages.Count > MaxMessages)
            {
                var removable = messages.FirstOrDefault(existing =>
                        existing.Id != item.Id &&
                        existing.Kind != AppFeedbackKind.Loading)
                    ?? messages.FirstOrDefault(existing => existing.Id != item.Id)
                    ?? messages[0];

                removedIds.Add(removable.Id);
                messages.Remove(removable);
            }
        }

        foreach (var removedId in removedIds)
        {
            CancelTimer(removedId);
        }

        StartDismissTimer(item);
        RaiseChanged();
        return item.Id;
    }

    public void Resolve(
        Guid id,
        string message,
        string title = "Completed",
        int durationMs = 3600) =>
        Replace(id, AppFeedbackKind.Success, title, message, durationMs, true);

    public void Fail(
        Guid id,
        string message,
        string title = "Something went wrong",
        int durationMs = 7000) =>
        Replace(id, AppFeedbackKind.Error, title, message, durationMs, true);

    public void Update(
        Guid id,
        string message,
        string? title = null)
    {
        AppFeedbackMessage? updated = null;

        lock (sync)
        {
            var index = messages.FindIndex(item => item.Id == id);
            if (index < 0)
            {
                return;
            }

            var current = messages[index];
            updated = current with
            {
                Title = string.IsNullOrWhiteSpace(title) ? current.Title : title.Trim(),
                Message = string.IsNullOrWhiteSpace(message) ? current.Message : message.Trim()
            };
            messages[index] = updated;
        }

        RaiseChanged();
    }

    public void Dismiss(Guid id)
    {
        var removed = false;

        lock (sync)
        {
            var index = messages.FindIndex(item => item.Id == id);
            if (index >= 0)
            {
                messages.RemoveAt(index);
                removed = true;
            }
        }

        CancelTimer(id);

        if (removed)
        {
            RaiseChanged();
        }
    }

    public void DismissAll()
    {
        Guid[] ids;

        lock (sync)
        {
            ids = messages.Select(item => item.Id).ToArray();
            messages.Clear();
        }

        foreach (var id in ids)
        {
            CancelTimer(id);
        }

        RaiseChanged();
    }

    public async Task ExecuteActionAsync(Guid id)
    {
        AppFeedbackMessage? item;

        lock (sync)
        {
            item = messages.FirstOrDefault(message => message.Id == id);
        }

        if (item?.Action is null)
        {
            return;
        }

        try
        {
            await item.Action();
            Dismiss(id);
        }
        catch
        {
            Fail(id, "The action could not be completed. Please try again.");
        }
    }

    private void Replace(
        Guid id,
        AppFeedbackKind kind,
        string title,
        string message,
        int? durationMs,
        bool isDismissible)
    {
        AppFeedbackMessage? updated = null;

        lock (sync)
        {
            var index = messages.FindIndex(item => item.Id == id);
            if (index < 0)
            {
                return;
            }

            var current = messages[index];
            updated = current with
            {
                Kind = kind,
                Title = string.IsNullOrWhiteSpace(title) ? DefaultTitle(kind) : title.Trim(),
                Message = string.IsNullOrWhiteSpace(message) ? DefaultMessage(kind) : message.Trim(),
                DurationMs = durationMs,
                IsDismissible = isDismissible,
                ActionLabel = null,
                Action = null
            };

            messages[index] = updated;
        }

        CancelTimer(id);

        if (updated is not null)
        {
            StartDismissTimer(updated);
        }

        RaiseChanged();
    }

    private void StartDismissTimer(AppFeedbackMessage item)
    {
        if (item.DurationMs is not > 0)
        {
            return;
        }

        var cts = new CancellationTokenSource();

        lock (sync)
        {
            dismissTimers[item.Id] = cts;
        }

        _ = AutoDismissAsync(item.Id, item.DurationMs.Value, cts.Token);
    }

    private async Task AutoDismissAsync(Guid id, int durationMs, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(durationMs, cancellationToken);
            Dismiss(id);
        }
        catch (OperationCanceledException)
        {
            // The message was dismissed or replaced before the timer completed.
        }
    }

    private void CancelTimer(Guid id)
    {
        CancellationTokenSource? cts = null;

        lock (sync)
        {
            if (dismissTimers.Remove(id, out var timer))
            {
                cts = timer;
            }
        }

        if (cts is not null)
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    private void RaiseChanged() => Changed?.Invoke();

    private static string DefaultTitle(AppFeedbackKind kind) => kind switch
    {
        AppFeedbackKind.Success => "Success",
        AppFeedbackKind.Error => "Something went wrong",
        AppFeedbackKind.Warning => "Attention needed",
        AppFeedbackKind.Info => "Information",
        AppFeedbackKind.Loading => "Working",
        _ => "Notification"
    };

    private static string DefaultMessage(AppFeedbackKind kind) => kind switch
    {
        AppFeedbackKind.Success => "The action was completed successfully.",
        AppFeedbackKind.Error => "The action could not be completed.",
        AppFeedbackKind.Warning => "Please review this item before continuing.",
        AppFeedbackKind.Info => "There is an update for this action.",
        AppFeedbackKind.Loading => "Please wait while this action completes.",
        _ => "There is an update."
    };

    public void Dispose()
    {
        Guid[] ids;

        lock (sync)
        {
            ids = dismissTimers.Keys.ToArray();
            messages.Clear();
        }

        foreach (var id in ids)
        {
            CancelTimer(id);
        }

        Changed = null;
    }
}
