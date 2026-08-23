using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class UserAccessPageTests
{
    [Fact]
    public void RegistrationIsPublicAndStandardUsersAreRestrictedToPersonalPages()
    {
        var guard = ReadSource("Components", "RouteGuard.razor");
        var register = ReadSource("Pages", "Register.razor");
        var login = ReadSource("Pages", "Login.razor");

        Assert.Contains("typeof(Pages.Register)", guard, StringComparison.Ordinal);
        Assert.Contains("typeof(Pages.Account)", guard, StringComparison.Ordinal);
        Assert.Contains("typeof(Pages.ApiKeys)", guard, StringComparison.Ordinal);
        Assert.Contains("typeof(Pages.Usage)", guard, StringComparison.Ordinal);
        Assert.Contains("Auth.User?.IsAdmin == true", guard, StringComparison.Ordinal);
        Assert.Contains("Navigation.NavigateTo(\"/account\"", guard, StringComparison.Ordinal);
        Assert.Contains("Api.RegisterAsync(model)", register, StringComparison.Ordinal);
        Assert.Contains("管理员启用账户后即可登录", register, StringComparison.Ordinal);
        Assert.Contains("href=\"/register\"", login, StringComparison.Ordinal);
    }

    [Fact]
    public void StandardUserNavigationContainsOnlyPersonalAccountFunctions()
    {
        var menu = ReadSource("Layout", "NavMenu.razor");
        var sectionStart = menu.IndexOf("<div class=\"sidebar-section user-section\">", StringComparison.Ordinal);
        var sectionEnd = menu.IndexOf("</nav>", sectionStart, StringComparison.Ordinal);

        Assert.Contains("@if (IsAdmin)", menu, StringComparison.Ordinal);
        Assert.Contains("[Parameter] public bool IsAdmin", menu, StringComparison.Ordinal);
        Assert.Contains("HomeHref => IsAdmin ? \"/\" : \"/account\"", menu, StringComparison.Ordinal);
        Assert.True(sectionStart >= 0 && sectionEnd > sectionStart);

        var userSection = menu[sectionStart..sectionEnd];
        foreach (var route in new[] { "/dashboard", "/api-keys", "/usage", "/subscriptions", "/account" })
            Assert.Contains($"href=\"{route}\"", userSection, StringComparison.Ordinal);
        Assert.Equal(5, userSection.Split("<NavLink ", StringSplitOptions.None).Length - 1);

        foreach (var hiddenEntry in new[] { "批量生图", "可用渠道", "渠道状态", "/batch-image", "/available-channels", "/monitor" })
            Assert.DoesNotContain(hiddenEntry, userSection, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthenticationChangesImmediatelyRebuildRoleSpecificNavigation()
    {
        var layout = ReadSource("Layout", "MainLayout.razor");
        var menu = ReadSource("Layout", "NavMenu.razor");
        var callback = ReadSource("Pages", "AuthCallback.razor");

        Assert.Contains("Auth.Changed += OnAuthChanged", layout, StringComparison.Ordinal);
        Assert.Contains("Auth.Changed -= OnAuthChanged", layout, StringComparison.Ordinal);
        Assert.Contains("private void OnAuthChanged() => _ = InvokeAsync(StateHasChanged)", layout, StringComparison.Ordinal);
        Assert.Contains("@key=\"AuthIdentityKey\"", layout, StringComparison.Ordinal);
        Assert.Contains("IsAdmin=\"@IsAdmin\"", layout, StringComparison.Ordinal);
        Assert.Contains("Auth.User.Id}:{Auth.User.Role}", layout, StringComparison.Ordinal);
        Assert.Contains("[Parameter] public bool IsAdmin", menu, StringComparison.Ordinal);
        Assert.DoesNotContain("@inject AuthSession Auth", menu, StringComparison.Ordinal);
        Assert.Contains("@layout AuthLayout", callback, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountPageLoadsAndUpdatesTheCurrentProfileAndUsesGeneralPasswordCopy()
    {
        var account = ReadSource("Pages", "Account.razor");

        Assert.Contains("Api.GetProfileAsync()", account, StringComparison.Ordinal);
        Assert.Contains("Api.UpdateProfileAsync(profileModel)", account, StringComparison.Ordinal);
        Assert.Contains("await Auth.RefreshAsync()", account, StringComparison.Ordinal);
        Assert.Contains("Api.ChangePasswordAsync(passwordModel)", account, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"profile-overview-hero\"", account, StringComparison.Ordinal);
        Assert.Contains("资料与头像", account, StringComparison.Ordinal);
        Assert.Contains("登录方式绑定", account, StringComparison.Ordinal);
        Assert.Contains("Api.GetPublicSettingsAsync()", account, StringComparison.Ordinal);
        Assert.Contains("PublicSettings=\"publicSettings\"", account, StringComparison.Ordinal);
        Assert.Contains("修改密码", account, StringComparison.Ordinal);
        Assert.DoesNotContain("账户状态", account, StringComparison.Ordinal);
        Assert.DoesNotContain("修改管理员密码", account, StringComparison.Ordinal);
    }

    [Fact]
    public void ApiKeysAreAlwaysPersonalAndUsageRoutesAreSelectedByPath()
    {
        var keys = ReadSource("Pages", "ApiKeys.razor");
        var usage = ReadSource("Pages", "Usage.razor");

        Assert.Contains("Api.GetMyApiKeysPageAsync", keys, StringComparison.Ordinal);
        Assert.Contains("Api.CreateMyApiKeyAsync", keys, StringComparison.Ordinal);
        Assert.Contains("Api.UpdateMyApiKeyAsync", keys, StringComparison.Ordinal);
        Assert.Contains("Api.DeleteMyApiKeyAsync", keys, StringComparison.Ordinal);
        Assert.DoesNotContain("Api.GetApiKeysAsync", keys, StringComparison.Ordinal);
        Assert.DoesNotContain("IsAdmin", keys, StringComparison.Ordinal);
        Assert.Contains("IsAdminRoute", usage, StringComparison.Ordinal);
        Assert.Contains("<AdminUsagePanel />", usage, StringComparison.Ordinal);
        Assert.Contains("<UserUsagePanel />", usage, StringComparison.Ordinal);
        Assert.Contains("我的使用记录", usage, StringComparison.Ordinal);
    }

    [Fact]
    public void ApiKeysPageLetsAdministratorsCreateAKeyForThemselves()
    {
        var keys = ReadSource("Pages", "ApiKeys.razor");

        Assert.Contains("创建密钥", keys, StringComparison.Ordinal);
        Assert.Contains("Api.CreateMyApiKeyAsync(form)", keys, StringComparison.Ordinal);
        Assert.DoesNotContain("AuthSession", keys, StringComparison.Ordinal);
        Assert.DoesNotContain("IsAdmin", keys, StringComparison.Ordinal);
    }

    [Fact]
    public void UserAdministrationPreservesRolesAndSupportsStatusAndPasswordUpdates()
    {
        var users = ReadSource("Pages", "Users.razor");

        Assert.Contains("Role = user.Role", users, StringComparison.Ordinal);
        Assert.Contains("[\"role\"] = editor.Role", users, StringComparison.Ordinal);
        Assert.Contains("[\"password\"] = editor.Password", users, StringComparison.Ordinal);
        Assert.Contains("user.LastActiveAt", users, StringComparison.Ordinal);
        Assert.Contains("用户已启用", users, StringComparison.Ordinal);
        Assert.Contains("请输入初始密码", users, StringComparison.Ordinal);
        Assert.Contains("密码至少需要 6 个字符", users, StringComparison.Ordinal);
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
}
