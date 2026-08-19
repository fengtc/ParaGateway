using ParaGateway.Frontend.Models;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class ApplicationShellParityTests
{
    [Fact]
    public void MainLayoutExposesOfficialHeaderContextBalanceAndPersistentControls()
    {
        var layout = ReadSource("Layout", "MainLayout.razor");

        Assert.Contains("管理控制台", layout, StringComparison.Ordinal);
        Assert.Contains("系统概览与统计数据", layout, StringComparison.Ordinal);
        Assert.Contains("PageContext.Title", layout, StringComparison.Ordinal);
        Assert.Contains("PageContext.Description", layout, StringComparison.Ordinal);
        Assert.Contains("Auth.User.Balance", layout, StringComparison.Ordinal);
        Assert.Contains("Auth.User.FrozenBalance", layout, StringComparison.Ordinal);
        Assert.Contains("paraGateway.getTheme", layout, StringComparison.Ordinal);
        Assert.Contains("paraGateway.setTheme", layout, StringComparison.Ordinal);
        Assert.Contains("paraGateway.getSidebarCollapsed", layout, StringComparison.Ordinal);
        Assert.Contains("paraGateway.setSidebarCollapsed", layout, StringComparison.Ordinal);
        Assert.Contains("<AnnouncementBell", layout, StringComparison.Ordinal);
        Assert.Contains("href=\"/model-plaza?embedded=1\"", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void NavigationKeepsCoreOrderAndHidesRetiredMenuEntries()
    {
        var menu = ReadSource("Layout", "NavMenu.razor");

        var dashboard = menu.IndexOf("href=\"/admin/dashboard\"", StringComparison.Ordinal);
        var ops = menu.IndexOf("href=\"/admin/ops\"", StringComparison.Ordinal);
        var users = menu.IndexOf("href=\"/admin/users\"", StringComparison.Ordinal);
        var groups = menu.IndexOf("href=\"/admin/groups\"", StringComparison.Ordinal);
        var accounts = menu.IndexOf("href=\"/admin/accounts\"", StringComparison.Ordinal);
        var settings = menu.IndexOf("href=\"/admin/settings\"", StringComparison.Ordinal);

        Assert.True(dashboard >= 0 && dashboard < ops && ops < users && users < groups && groups < accounts && accounts < settings);
        Assert.Contains("安全审计", menu, StringComparison.Ordinal);
        Assert.Contains("我的账户", menu, StringComparison.Ordinal);
        Assert.Contains("扩展管理", menu, StringComparison.Ordinal);
        Assert.Contains("官方 OAuth", menu, StringComparison.Ordinal);
        Assert.Contains("账号管理", menu, StringComparison.Ordinal);
        Assert.Contains("GetSystemVersionAsync", menu, StringComparison.Ordinal);
        Assert.Contains("CheckSystemUpdatesAsync", menu, StringComparison.Ordinal);
        foreach (var hiddenLabel in new[] { "渠道管理", "上游账号", "兑换码", "优惠码", "邀请返利" })
        {
            Assert.DoesNotContain(hiddenLabel, menu, StringComparison.Ordinal);
        }
        foreach (var hiddenRoute in new[]
        {
            "/admin/channels/", "/admin/upstream-accounts", "/admin/redeem", "/admin/promo-codes",
            "/admin/affiliates/", "/redeem", "/affiliate"
        })
        {
            Assert.DoesNotContain($"href=\"{hiddenRoute}", menu, StringComparison.OrdinalIgnoreCase);
        }
        Assert.DoesNotContain("<small>v1</small>", menu, StringComparison.Ordinal);
        Assert.DoesNotContain("/purchase", menu, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/orders", menu, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("支付", menu, StringComparison.Ordinal);
    }

    [Fact]
    public void ThemeBootstrapSwitchesBothApplicationAndDevExpressThemes()
    {
        var script = ReadSource("wwwroot", "js", "paragateway.js");
        var index = ReadSource("wwwroot", "index.html");
        var css = ReadSource("wwwroot", "css", "app.css");

        Assert.Contains("preferredTheme", script, StringComparison.Ordinal);
        Assert.Contains("document.documentElement", script, StringComparison.Ordinal);
        Assert.Contains("dx-light-theme", script, StringComparison.Ordinal);
        Assert.Contains("dx-dark-theme", script, StringComparison.Ordinal);
        Assert.Contains("office-white.bs5.min.css", index, StringComparison.Ordinal);
        Assert.Contains("blazing-dark.bs5.min.css", index, StringComparison.Ordinal);
        Assert.Contains("css/app.css?v=", index, StringComparison.Ordinal);
        Assert.Contains("js/paragateway.js?v=", index, StringComparison.Ordinal);
        Assert.Contains("html[data-theme=\"dark\"]", css, StringComparison.Ordinal);
        Assert.Contains("--sidebar-bg", css, StringComparison.Ordinal);
        Assert.Contains("--topbar-bg", css, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthUserPreservesAvailableAndFrozenBalances()
    {
        var auth = AuthUser.From(new GoUser
        {
            Id = 7,
            Email = "admin@example.test",
            Username = "管理员",
            Role = "admin",
            Balance = 12.34m,
            FrozenBalance = 5.67m
        });

        Assert.Equal(12.34m, auth.Balance);
        Assert.Equal(5.67m, auth.FrozenBalance);
    }

    [Fact]
    public void EveryOfficialNonPaymentRouteHasABlazorPage()
    {
        var pages = ReadAllSourceFiles("Pages", "*.razor");
        var expected = new[]
        {
            "/setup", "/home", "/login", "/register", "/email-verify", "/auth/callback",
            "/auth/linuxdo/callback", "/auth/wechat/callback", "/auth/dingtalk/callback",
            "/auth/dingtalk/email-completion", "/auth/oidc/callback", "/forgot-password",
            "/reset-password", "/key-usage", "/legal/{DocumentId}", "/model-plaza", "/",
            "/dashboard", "/keys", "/batch-image", "/usage", "/redeem", "/affiliate",
            "/available-channels", "/profile", "/subscriptions", "/custom/{Id}", "/admin",
            "/admin/dashboard", "/admin/ops", "/admin/audit-logs", "/admin/users", "/admin/groups",
            "/admin/channels", "/admin/channels/pricing", "/admin/channels/monitor", "/monitor",
            "/admin/subscriptions", "/admin/accounts", "/admin/upstream-accounts", "/admin/announcements", "/admin/proxies",
            "/admin/redeem", "/admin/promo-codes", "/admin/settings", "/admin/risk-control",
            "/admin/prompt-audit", "/admin/usage", "/admin/affiliates",
            "/admin/affiliates/invites", "/admin/affiliates/rebates", "/admin/affiliates/transfers"
        };

        foreach (var route in expected)
        {
            Assert.Contains($"@page \"{route}\"", pages, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string ReadSource(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {Path.Combine(parts)}");
    }

    private static string ReadAllSourceFiles(string directoryName, string pattern)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, directoryName);
            if (Directory.Exists(candidate))
            {
                return string.Join('\n', Directory.GetFiles(candidate, pattern, SearchOption.AllDirectories).Select(File.ReadAllText));
            }
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(directoryName);
    }
}
