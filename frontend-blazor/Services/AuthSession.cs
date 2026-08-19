using System.Net;
using System.Text.Json;
using ParaGateway.Frontend.Models;

namespace ParaGateway.Frontend.Services;

public sealed class AuthSession
{
    private readonly ApiClient api;

    public AuthSession(ApiClient api)
    {
        this.api = api;
        api.Unauthorized += Clear;
    }

    public AuthUser? User { get; private set; }
    public bool IsAuthenticated => User is not null;
    public bool IsInitialized { get; private set; }
    public event Action? Changed;

    public async Task InitializeAsync()
    {
        if (IsInitialized)
        {
            return;
        }

        try
        {
            User = await api.GetMeAsync();
        }
        catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            User = null;
        }
        catch (ApiException)
        {
            User = null;
        }
        finally
        {
            IsInitialized = true;
            Changed?.Invoke();
        }
    }

    public async Task LoginAsync(LoginRequest request)
    {
        var auth = await LoginPasswordAsync(request);
        if (auth.Requires2FA)
        {
            throw new ApiException("需要二次验证。", HttpStatusCode.Unauthorized)
            {
                Code = "TOTP_REQUIRED",
                Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["temp_token"] = auth.TempToken ?? string.Empty,
                    ["user_email_masked"] = auth.UserEmailMasked ?? string.Empty
                }
            };
        }
        await api.AcceptOAuthTokensAsync(auth);
        User = AuthUser.From(auth.User ?? throw new ApiException("服务器未返回登录用户信息。", HttpStatusCode.OK));
        IsInitialized = true;
        Changed?.Invoke();
    }

    public Task<AuthResponse> LoginPasswordAsync(LoginRequest request) => api.LoginPasswordAsync(request);

    public async Task CompletePasswordTwoFactorAsync(string tempToken, string code)
    {
        var auth = await api.Login2FAAsync(tempToken, code);
        await api.AcceptOAuthTokensAsync(auth);
        User = AuthUser.From(auth.User ?? throw new ApiException("服务器未返回登录用户信息。", HttpStatusCode.OK));
        IsInitialized = true;
        Changed?.Invoke();
    }

    public async Task LoginWithPasskeyAsync(string sessionToken, JsonElement credential)
    {
        User = await api.CompletePasskeyLoginAsync(sessionToken, credential);
        IsInitialized = true;
        Changed?.Invoke();
    }

    public async Task RefreshAsync()
    {
        User = await api.GetMeAsync();
        IsInitialized = true;
        Changed?.Invoke();
    }

    public async Task LogoutAsync()
    {
        try
        {
            await api.LogoutAsync();
        }
        finally
        {
            Clear();
        }
    }

    public void Clear()
    {
        if (User is null && IsInitialized)
        {
            return;
        }

        User = null;
        IsInitialized = true;
        Changed?.Invoke();
    }
}
