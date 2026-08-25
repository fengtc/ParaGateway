using ParaGateway.Frontend.Models;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class ApplicationShellParityTests
{
    [Fact]
    public void MainLayoutExposesOfficialHeaderContextBalanceAndPersistentControls()
    {
        var layout = ReadSource("Layout", "MainLayout.razor");

        foreach (var title in new[]
        {
            "运营总览", "账户总览", "运维管理", "用户管理", "内容风控", "提示风控",
            "请求审计", "分组管理", "订阅管理", "账号管理", "公告管理", "代理管理",
            "用量记录", "审计日志", "系统设置"
        })
        {
            Assert.Contains(title, layout, StringComparison.Ordinal);
        }
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
        var riskControl = menu.IndexOf("href=\"/admin/risk-control\"", StringComparison.Ordinal);
        var promptUpgrade = menu.IndexOf("href=\"/admin/prompt-audit\"", StringComparison.Ordinal);
        var requestAudit = menu.IndexOf("href=\"/admin/request-audit\"", StringComparison.Ordinal);
        var groups = menu.IndexOf("href=\"/admin/groups\"", StringComparison.Ordinal);
        var accounts = menu.IndexOf("href=\"/admin/accounts\"", StringComparison.Ordinal);
        var settings = menu.IndexOf("href=\"/admin/settings\"", StringComparison.Ordinal);

        Assert.True(dashboard >= 0 && dashboard < ops && ops < users && users < riskControl && riskControl < promptUpgrade && promptUpgrade < requestAudit && requestAudit < groups && groups < accounts && accounts < settings);
        Assert.Contains("运营总览", menu, StringComparison.Ordinal);
        Assert.Contains("运维管理", menu, StringComparison.Ordinal);
        Assert.Contains("内容风控", menu, StringComparison.Ordinal);
        Assert.Contains("提示风控", menu, StringComparison.Ordinal);
        Assert.Contains("请求审计", menu, StringComparison.Ordinal);
        Assert.DoesNotContain("安全审计", menu, StringComparison.Ordinal);
        Assert.DoesNotContain("nav-group-button", menu, StringComparison.Ordinal);
        Assert.Contains("我的账户", menu, StringComparison.Ordinal);
        Assert.Contains("账号管理", menu, StringComparison.Ordinal);
        Assert.Contains("代理管理", menu, StringComparison.Ordinal);
        Assert.Contains("用量记录", menu, StringComparison.Ordinal);
        Assert.DoesNotContain("用量明细", menu, StringComparison.Ordinal);
        Assert.Contains("审计日志", menu, StringComparison.Ordinal);
        Assert.Contains("账户总览", menu, StringComparison.Ordinal);
        Assert.DoesNotContain("GetSystemVersionAsync", menu, StringComparison.Ordinal);
        Assert.DoesNotContain("CheckSystemUpdatesAsync", menu, StringComparison.Ordinal);
        Assert.DoesNotContain("version-badge", menu, StringComparison.Ordinal);
        Assert.DoesNotContain("v0.1.179", menu, StringComparison.Ordinal);
        Assert.DoesNotContain("扩展管理", menu, StringComparison.Ordinal);
        foreach (var misplacedRoute in new[]
        {
            "/provider-oauth", "/admin/models", "/admin/user-attributes", "/admin/backup",
            "/admin/data-management", "/admin/error-passthrough", "/admin/tls-fingerprints"
        })
        {
            Assert.DoesNotContain($"href=\"{misplacedRoute}\"", menu, StringComparison.OrdinalIgnoreCase);
        }
        foreach (var hiddenLabel in new[] { "渠道管理", "兑换码", "优惠码", "邀请返利" })
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
    public void PrimaryNavigationTypographyMatchesTopbarTitle()
    {
        var navigation = ReadSource("Layout", "NavMenu.razor.css");
        var layout = ReadSource("Layout", "MainLayout.razor.css");

        foreach (var declaration in new[] { "font-size: 1rem;", "font-weight: 700;", "line-height: 1.25;" })
        {
            Assert.Contains(declaration, navigation, StringComparison.Ordinal);
            Assert.Contains(declaration, layout, StringComparison.Ordinal);
        }
        Assert.Contains(".admin-section .nav-label-copy", navigation, StringComparison.Ordinal);
        Assert.Contains(".personal-section .nav-label-copy", navigation, StringComparison.Ordinal);
        Assert.Contains(".user-section .nav-label-copy", navigation, StringComparison.Ordinal);
    }

    [Fact]
    public void AdminDashboardModelDistributionUsesRankedHorizontalBars()
    {
        var dashboard = ReadSource("Components", "AdminDashboardPanel.razor");

        Assert.Contains("<DxChart T=\"ModelDistributionRow\"", dashboard, StringComparison.Ordinal);
        Assert.Contains("Data=\"@ModelDistributionRows\"", dashboard, StringComparison.Ordinal);
        Assert.Contains("Rotated=\"true\"", dashboard, StringComparison.Ordinal);
        Assert.Contains("<DxChartBarSeries T=\"ModelDistributionRow\"", dashboard, StringComparison.Ordinal);
        Assert.Contains("<DxChartArgumentAxis Inverted=\"true\" />", dashboard, StringComparison.Ordinal);
        Assert.Contains("class=\"distribution-layout model-distribution-layout\"", dashboard, StringComparison.Ordinal);
        Assert.DoesNotContain("<DxPieChart Data=\"@ModelRows\"", dashboard, StringComparison.Ordinal);
    }

    [Fact]
    public void AdminPersonalNavigationMatchesOfficialThreeEntryMenu()
    {
        var menu = ReadSource("Layout", "NavMenu.razor");
        var sectionStart = menu.IndexOf("<div class=\"sidebar-section personal-section\">", StringComparison.Ordinal);
        var sectionEnd = menu.IndexOf("<div class=\"sidebar-section user-section\">", sectionStart, StringComparison.Ordinal);

        Assert.True(sectionStart >= 0 && sectionEnd > sectionStart);
        var personalSection = menu[sectionStart..sectionEnd];

        Assert.Contains("href=\"/api-keys\"", personalSection, StringComparison.Ordinal);
        Assert.Contains("href=\"/usage\"", personalSection, StringComparison.Ordinal);
        Assert.Contains("href=\"/account\"", personalSection, StringComparison.Ordinal);
        Assert.Equal(3, personalSection.Split("<NavLink ", StringSplitOptions.None).Length - 1);

        foreach (var extraEntry in new[] { "批量生图", "可用渠道", "渠道状态", "我的订阅" })
        {
            Assert.DoesNotContain(extraEntry, personalSection, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ExtensionCapabilitiesStayWithTheirOfficialOwningPages()
    {
        var menu = ReadSource("Layout", "NavMenu.razor");
        var accounts = ReadSource("Pages", "Providers.razor");
        var users = ReadSource("Pages", "Users.razor");
        var settings = ReadSource("Pages", "AdminSettings.razor");
        var createAccount = ReadSource("Components", "NativeAccountCreateModal.razor");

        Assert.DoesNotContain("扩展管理", menu, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/provider-oauth\"", menu, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"/provider-oauth\"", accounts, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("错误透传规则", accounts, StringComparison.Ordinal);
        Assert.Contains("TLS 指纹模板", accounts, StringComparison.Ordinal);
        Assert.Contains("OpenAttributesConfig", users, StringComparison.Ordinal);
        Assert.Contains("href=\"/admin/data-management\"", settings, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=\"/admin/backups\"", settings, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GitHub Copilot", createAccount, StringComparison.Ordinal);
        Assert.Contains("AccountModelRestrictionEditor", createAccount, StringComparison.Ordinal);
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
