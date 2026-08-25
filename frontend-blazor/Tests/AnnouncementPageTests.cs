using System.Net;
using System.Text;
using Microsoft.JSInterop;
using ParaGateway.Frontend.Services;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class AnnouncementPageTests
{
    [Fact]
    public void GlobalBellAndAnnouncementCenterMatchTheOfficialUserSurface()
    {
        var layout = ReadSource("Layout", "MainLayout.razor");
        var bell = ReadSource("Components", "AnnouncementBell.razor");
        var page = ReadSource("Pages", "UserAnnouncements.razor");
        var program = ReadSource("Program.cs");

        Assert.Contains("<AnnouncementBell ViewerId=\"@Auth.User.Id\"", layout, StringComparison.Ordinal);
        Assert.Contains("<SectionOutlet SectionName=\"announcement-overlays\" />", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/announcements\" title=\"公告\"", layout, StringComparison.Ordinal);
        Assert.Contains("Announcements.Reset()", layout, StringComparison.Ordinal);
        Assert.Contains("AddScoped<AnnouncementService>", program, StringComparison.Ordinal);

        foreach (var text in new[] { "条未读公告", "全部标记已读", "暂无公告", "公告详情", "标记已读", "未读公告" })
            Assert.Contains(text, bell, StringComparison.Ordinal);
        Assert.Contains("Announcements.CurrentPopup", bell, StringComparison.Ordinal);
        Assert.Contains("<SectionContent SectionName=\"announcement-overlays\">", bell, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"announcement-popup-dismiss\"", bell, StringComparison.Ordinal);
        Assert.Contains("announcement-popup-title-row", bell, StringComparison.Ordinal);
        Assert.Contains("announcement-popup-read", bell, StringComparison.Ordinal);
        Assert.Contains("MarkdownRenderer.ToSafeHtml", bell, StringComparison.Ordinal);

        foreach (var text in new[] { "公告中心", "公告总数", "未读公告", "仅看未读", "重要通知", "MarkdownRenderer.ToSafeHtml" })
            Assert.Contains(text, page, StringComparison.Ordinal);
        Assert.DoesNotContain("private List<UserAnnouncementDto> items", page, StringComparison.Ordinal);
    }

    [Fact]
    public void AnnouncementPopupKeepsItsActionsInsideTheViewport()
    {
        var styles = ReadSource("Components", "AnnouncementBell.razor.css");

        Assert.Contains("max-height: calc(100dvh - 48px);", styles, StringComparison.Ordinal);
        Assert.Contains("flex-direction: column;", styles, StringComparison.Ordinal);
        Assert.Contains("min-height: 0; max-height: none;", styles, StringComparison.Ordinal);
        Assert.Contains("flex: 1 1 auto; overflow-y: auto;", styles, StringComparison.Ordinal);
        Assert.Contains("overscroll-behavior: contain;", styles, StringComparison.Ordinal);
        Assert.Contains("z-index: 1000;", styles, StringComparison.Ordinal);
        Assert.DoesNotContain("padding: 8vh 20px 24px;", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void AnnouncementApiAndStoreUseTypedThrottledSharedState()
    {
        var client = ReadSource("Services", "ApiClient.cs");
        var service = ReadSource("Services", "AnnouncementService.cs");

        Assert.Contains("Task<List<UserAnnouncementDto>> GetUserAnnouncementsAsync", client, StringComparison.Ordinal);
        Assert.DoesNotContain("GetUserAnnouncementsAsync(bool unreadOnly = false)\n    {", client.Replace("\r", string.Empty), StringComparison.Ordinal);
        Assert.Contains("TimeSpan.FromMinutes(20)", service, StringComparison.Ordinal);
        Assert.Contains("notify_mode=popup", service, StringComparison.Ordinal);
        Assert.Contains("string.Equals(item.NotifyMode, \"popup\"", service, StringComparison.Ordinal);
        Assert.Contains("Task.WhenAll", service, StringComparison.Ordinal);
        Assert.Contains("SetViewer", service, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnnouncementStoreQueuesPopupsAndKeepsReadStateInSync()
    {
        var handler = new AnnouncementHandler();
        var api = new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://paragateway.test") }, new NullJsRuntime());
        var store = new AnnouncementService(api);

        store.SetViewer("42");
        await store.FetchAsync(force: true);

        Assert.Equal(3, store.Items.Count);
        Assert.Equal(3, store.UnreadCount);
        Assert.Equal(1, store.CurrentPopup?.Id);

        await store.DismissPopupAsync();
        Assert.Equal(2, store.CurrentPopup?.Id);
        Assert.Equal(2, store.UnreadCount);

        await store.DismissPopupAsync();
        Assert.Null(store.CurrentPopup);
        Assert.Equal(1, store.UnreadCount);

        await store.MarkAllReadAsync();
        Assert.Equal(0, store.UnreadCount);
        Assert.Equal(new[] { 1L, 2L, 3L }, handler.ReadIds.Order().ToArray());

        store.SetViewer("84");
        Assert.Empty(store.Items);
        Assert.Null(store.CurrentPopup);
    }

    private static string ReadSource(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(path)) return File.ReadAllText(path);
            directory = directory.Parent;
        }
        throw new FileNotFoundException($"Could not locate {Path.Combine(parts)}");
    }

    private sealed class NullJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => ValueTask.FromResult(default(TValue)!);
    }

    private sealed class AnnouncementHandler : HttpMessageHandler
    {
        public List<long> ReadIds { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == "/api/v1/announcements")
            {
                const string json = """
                    {"code":0,"message":"success","data":[
                      {"id":1,"title":"维护公告","content":"# 维护","notify_mode":"popup","read_at":null,"created_at":"2026-08-15T01:00:00Z"},
                      {"id":2,"title":"升级公告","content":"升级完成","notify_mode":"popup","read_at":null,"created_at":"2026-08-15T00:50:00Z"},
                      {"id":3,"title":"普通通知","content":"欢迎使用","notify_mode":"silent","read_at":null,"created_at":"2026-08-15T00:40:00Z"}
                    ]}
                    """;
                return Task.FromResult(Response(HttpStatusCode.OK, json));
            }

            if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath is { } path && path.EndsWith("/read", StringComparison.Ordinal))
            {
                var value = path.Split('/', StringSplitOptions.RemoveEmptyEntries)[3];
                ReadIds.Add(long.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
                return Task.FromResult(Response(HttpStatusCode.OK, "{\"code\":0,\"message\":\"success\",\"data\":{\"message\":\"ok\"}}"));
            }

            return Task.FromResult(Response(HttpStatusCode.NotFound, "{\"code\":404,\"message\":\"not found\"}"));
        }

        private static HttpResponseMessage Response(HttpStatusCode status, string body) => new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }
}
