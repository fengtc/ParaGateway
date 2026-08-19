using ParaGateway.Frontend.Models;

namespace ParaGateway.Frontend.Services;

/// <summary>
/// 与官方 Pinia announcement store 对齐的会话级公告状态：20 分钟节流、
/// 未读计数、全部已读，以及 notify_mode=popup 的顺序弹出队列。
/// </summary>
public sealed class AnnouncementService
{
    private static readonly TimeSpan FetchThrottle = TimeSpan.FromMinutes(20);
    private readonly ApiClient api;
    private readonly SemaphoreSlim fetchGate = new(1, 1);
    private readonly HashSet<long> shownPopupIds = [];
    private readonly Queue<UserAnnouncementDto> popupQueue = new();
    private List<UserAnnouncementDto> items = [];
    private DateTimeOffset? lastFetchAt;
    private string? viewerId;

    public AnnouncementService(ApiClient api) => this.api = api;

    public IReadOnlyList<UserAnnouncementDto> Items => items;
    public bool Loading { get; private set; }
    public UserAnnouncementDto? CurrentPopup { get; private set; }
    public int UnreadCount => items.Count(item => item.ReadAt is null);
    public event Action? Changed;

    public void SetViewer(string? value)
    {
        if (string.Equals(viewerId, value, StringComparison.Ordinal)) return;
        viewerId = value;
        ResetState();
    }

    public async Task FetchAsync(bool force = false)
    {
        if (!force && lastFetchAt.HasValue && DateTimeOffset.UtcNow - lastFetchAt.Value < FetchThrottle) return;
        await fetchGate.WaitAsync();
        try
        {
            if (!force && lastFetchAt.HasValue && DateTimeOffset.UtcNow - lastFetchAt.Value < FetchThrottle) return;
            lastFetchAt = DateTimeOffset.UtcNow;
            Loading = true;
            NotifyChanged();
            try
            {
                items = (await api.GetUserAnnouncementsAsync()).Take(20).ToList();
                EnqueueNewPopups();
            }
            catch
            {
                lastFetchAt = null;
                throw;
            }
            finally
            {
                Loading = false;
                NotifyChanged();
            }
        }
        finally
        {
            fetchGate.Release();
        }
    }

    public async Task MarkReadAsync(long id)
    {
        var item = items.FirstOrDefault(value => value.Id == id);
        if (item?.ReadAt is not null) return;
        await api.MarkAnnouncementReadAsync(id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (item is not null) item.ReadAt = DateTimeOffset.UtcNow;
        NotifyChanged();
    }

    public async Task MarkAllReadAsync()
    {
        var unread = items.Where(item => item.ReadAt is null).ToList();
        if (unread.Count == 0) return;
        Loading = true;
        NotifyChanged();
        try
        {
            await Task.WhenAll(unread.Select(item => api.MarkAnnouncementReadAsync(item.Id.ToString(System.Globalization.CultureInfo.InvariantCulture))));
            var now = DateTimeOffset.UtcNow;
            foreach (var item in unread) item.ReadAt = now;
        }
        finally
        {
            Loading = false;
            NotifyChanged();
        }
    }

    public async Task DismissPopupAsync()
    {
        var popup = CurrentPopup;
        if (popup is null) return;
        CurrentPopup = null;
        NotifyChanged();
        try
        {
            await MarkReadAsync(popup.Id);
        }
        finally
        {
            if (popupQueue.Count > 0) await Task.Delay(300);
            ShowNextPopup();
        }
    }

    public void Reset()
    {
        viewerId = null;
        ResetState();
    }

    private void EnqueueNewPopups()
    {
        foreach (var item in items.Where(item =>
                     string.Equals(item.NotifyMode, "popup", StringComparison.OrdinalIgnoreCase)
                     && item.ReadAt is null
                     && !shownPopupIds.Contains(item.Id)))
        {
            if (CurrentPopup?.Id != item.Id && popupQueue.All(queued => queued.Id != item.Id)) popupQueue.Enqueue(item);
        }
        if (CurrentPopup is null) ShowNextPopup();
    }

    private void ShowNextPopup()
    {
        CurrentPopup = popupQueue.Count > 0 ? popupQueue.Dequeue() : null;
        if (CurrentPopup is not null) shownPopupIds.Add(CurrentPopup.Id);
        NotifyChanged();
    }

    private void ResetState()
    {
        items = [];
        lastFetchAt = null;
        shownPopupIds.Clear();
        popupQueue.Clear();
        CurrentPopup = null;
        Loading = false;
        NotifyChanged();
    }

    private void NotifyChanged() => Changed?.Invoke();
}
