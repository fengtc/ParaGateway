using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;
using ParaGateway.Frontend.Models;

namespace ParaGateway.Frontend.Services;

public sealed class ApiClient(HttpClient http, IJSRuntime js)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private const string AccessTokenKey = "paragateway.access_token";
    private const string RefreshTokenKey = "paragateway.refresh_token";
    private const string ApiPrefix = "/api/v1";
    private readonly SemaphoreSlim tokenRefreshLock = new(1, 1);

    public event Action? Unauthorized;

    private async Task<string?> GetTokenAsync(string key) =>
        await js.InvokeAsync<string?>("localStorage.getItem", key);

    private async Task SetTokenAsync(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            await js.InvokeVoidAsync("localStorage.removeItem", key);
            return;
        }
        await js.InvokeVoidAsync("localStorage.setItem", key, value);
    }

    private async Task StoreTokensAsync(AuthResponse auth)
    {
        await SetTokenAsync(AccessTokenKey, auth.AccessToken);
        await SetTokenAsync(RefreshTokenKey, auth.RefreshToken);
    }

    private async Task StoreTokensAsync(OAuthTokenResponseDto auth)
    {
        await SetTokenAsync(AccessTokenKey, auth.AccessToken);
        await SetTokenAsync(RefreshTokenKey, auth.RefreshToken);
    }

    public Task AcceptOAuthTokensAsync(OAuthTokenResponseDto auth)
    {
        if (string.IsNullOrWhiteSpace(auth.AccessToken))
        {
            throw new ApiException("OAuth 响应未包含 access_token。", HttpStatusCode.BadRequest);
        }
        return StoreTokensAsync(auth);
    }

    public Task AcceptOAuthTokensAsync(AuthResponse auth) => AcceptOAuthTokensAsync(new OAuthTokenResponseDto
    {
        AccessToken = auth.AccessToken,
        RefreshToken = auth.RefreshToken,
        ExpiresIn = auth.ExpiresIn,
        TokenType = auth.TokenType
    });

    private async Task ClearTokensAsync()
    {
        await SetTokenAsync(AccessTokenKey, null);
        await SetTokenAsync(RefreshTokenKey, null);
    }

    private async Task<bool> RefreshTokensAsync()
    {
        var refreshToken = await GetTokenAsync(RefreshTokenKey);
        if (string.IsNullOrWhiteSpace(refreshToken)) return false;

        await tokenRefreshLock.WaitAsync();
        try
        {
            var current = await GetTokenAsync(RefreshTokenKey);
            if (string.IsNullOrWhiteSpace(current)) return false;
            using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, $"{ApiPrefix}/auth/refresh")
            {
                Content = JsonContent.Create(new { refresh_token = current }, options: JsonOptions)
            };
            using var response = await http.SendAsync(refreshRequest);
            if (!response.IsSuccessStatusCode) return false;
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);
            var payload = Unwrap(document.RootElement, null);
            var result = payload.Deserialize<AuthResponse>(JsonOptions);
            if (result is null || string.IsNullOrWhiteSpace(result.AccessToken)) return false;
            await StoreTokensAsync(result);
            return true;
        }
        catch (JSException)
        {
            return false;
        }
        finally
        {
            tokenRefreshLock.Release();
        }
    }

    public async Task<AuthResponse> LoginPasswordAsync(LoginRequest request) =>
        await SendAsync<AuthResponse>(HttpMethod.Post, $"{ApiPrefix}/auth/login", request);

    public async Task<AuthUser> LoginAsync(LoginRequest request)
    {
        var auth = await LoginPasswordAsync(request);
        if (auth.Requires2FA || string.IsNullOrWhiteSpace(auth.AccessToken))
            throw new ApiException("登录需要完成二次验证。", HttpStatusCode.Unauthorized);
        await StoreTokensAsync(auth);
        return auth.User is null
            ? throw new ApiException("服务器未返回登录用户信息。", HttpStatusCode.OK)
            : AuthUser.From(auth.User);
    }

    public Task<AuthResponse> Login2FAAsync(string tempToken, string totpCode) =>
        SendAsync<AuthResponse>(HttpMethod.Post, $"{ApiPrefix}/auth/login/2fa", new
        {
            temp_token = tempToken.Trim(), totp_code = totpCode.Trim()
        });

    public Task<JsonElement> BeginPasskeyLoginAsync(CaptchaProof? proof = null) =>
        SendAsync<JsonElement>(HttpMethod.Post, $"{ApiPrefix}/auth/passkey/login/begin", proof ?? new CaptchaProof());

    public Task<AuthResponse> FinishPasskeyLoginAsync(string sessionToken, JsonElement credential) =>
        SendAsync<AuthResponse>(HttpMethod.Post, $"{ApiPrefix}/auth/passkey/login/finish", new { session_token = sessionToken, credential });

    public async Task<AuthUser> CompletePasskeyLoginAsync(string sessionToken, JsonElement credential)
    {
        var auth = await FinishPasskeyLoginAsync(sessionToken, credential);
        await StoreTokensAsync(auth);
        return auth.User is null
            ? throw new ApiException("服务器未返回登录用户信息。", HttpStatusCode.OK)
            : AuthUser.From(auth.User);
    }

    public async Task<RegisterResult> RegisterAsync(RegisterRequest request)
    {
        var auth = await SendAsync<AuthResponse>(HttpMethod.Post, $"{ApiPrefix}/auth/register", request);
        await StoreTokensAsync(auth);
        var user = auth.User;
        return new RegisterResult
        {
            Id = user?.Id.ToString() ?? string.Empty,
            Email = user?.Email ?? string.Empty,
            DisplayName = user?.Username ?? string.Empty,
            Role = user?.Role ?? "user",
            IsActive = string.Equals(user?.Status, "active", StringComparison.OrdinalIgnoreCase),
            RequiresActivation = !string.Equals(user?.Status, "active", StringComparison.OrdinalIgnoreCase)
        };
    }

    public Task<SendVerifyCodeResponse> SendVerifyCodeAsync(SendVerifyCodeRequest request) =>
        SendAsync<SendVerifyCodeResponse>(HttpMethod.Post, $"{ApiPrefix}/auth/send-verify-code", request);

    public Task<JsonElement> ForgotPasswordAsync(ForgotPasswordRequest request) =>
        SendAsync<JsonElement>(HttpMethod.Post, $"{ApiPrefix}/auth/forgot-password", request);

    public Task<OAuthLoginStartDto> StartOAuthLoginAsync(string provider, string redirect = "/dashboard", CaptchaProof? proof = null, string? mode = null)
    {
        var query = new List<string> { $"redirect={Uri.EscapeDataString(redirect.StartsWith('/') ? redirect : "/dashboard")}" };
        if (!string.IsNullOrWhiteSpace(mode)) query.Add($"mode={Uri.EscapeDataString(mode)}");
        return SendAsync<OAuthLoginStartDto>(HttpMethod.Post,
            $"{ApiPrefix}/auth/oauth/{Uri.EscapeDataString(provider.Trim().ToLowerInvariant())}/start?{string.Join("&", query)}",
            proof ?? new CaptchaProof());
    }

    public Task<JsonElement> ResetPasswordAsync(ResetPasswordRequest request) =>
        SendAsync<JsonElement>(HttpMethod.Post, $"{ApiPrefix}/auth/reset-password", new
        {
            email = request.Email.Trim(), token = request.Token.Trim(), new_password = request.NewPassword
        });

    public Task<OAuthCompletionDto> ExchangePendingOAuthCompletionAsync() =>
        SendAsync<OAuthCompletionDto>(HttpMethod.Post, $"{ApiPrefix}/auth/oauth/pending/exchange", new { });

    public Task<OAuthCompletionDto> ExchangePendingOAuthCompletionAsync(bool? adoptDisplayName, bool? adoptAvatar) =>
        SendAsync<OAuthCompletionDto>(HttpMethod.Post, $"{ApiPrefix}/auth/oauth/pending/exchange", OAuthAdoptionPayload(adoptDisplayName, adoptAvatar));

    public Task<OAuthCompletionDto> SendPendingOAuthVerifyCodeAsync(SendVerifyCodeRequest request) =>
        SendAsync<OAuthCompletionDto>(HttpMethod.Post, $"{ApiPrefix}/auth/oauth/pending/send-verify-code", request);

    public Task<OAuthCompletionDto> CreatePendingOAuthAccountAsync(object request) =>
        SendAsync<OAuthCompletionDto>(HttpMethod.Post, $"{ApiPrefix}/auth/oauth/pending/create-account", request);

    public Task<OAuthCompletionDto> BindPendingOAuthLoginAsync(object request) =>
        SendAsync<OAuthCompletionDto>(HttpMethod.Post, $"{ApiPrefix}/auth/oauth/pending/bind-login", request);

    public Task<OAuthCompletionDto> CompleteEmailOAuthRegistrationAsync(
        string provider,
        string password,
        string? invitationCode = null,
        string? affiliateCode = null) =>
        SendAsync<OAuthCompletionDto>(
            HttpMethod.Post,
            $"{ApiPrefix}/auth/oauth/{Uri.EscapeDataString(provider)}/complete-registration",
            BuildOAuthCompletionPayload(password, invitationCode, affiliateCode, null, null));

    public Task<OAuthCompletionDto> CompleteProviderOAuthRegistrationAsync(
        string provider,
        string invitationCode,
        string? affiliateCode,
        bool? adoptDisplayName,
        bool? adoptAvatar) =>
        SendAsync<OAuthCompletionDto>(
            HttpMethod.Post,
            $"{ApiPrefix}/auth/oauth/{Uri.EscapeDataString(provider)}/complete-registration",
            BuildOAuthCompletionPayload(null, invitationCode, affiliateCode, adoptDisplayName, adoptAvatar));

    private static Dictionary<string, object?> OAuthAdoptionPayload(bool? adoptDisplayName, bool? adoptAvatar)
    {
        var payload = new Dictionary<string, object?>();
        if (adoptDisplayName.HasValue) payload["adopt_display_name"] = adoptDisplayName.Value;
        if (adoptAvatar.HasValue) payload["adopt_avatar"] = adoptAvatar.Value;
        return payload;
    }

    private static Dictionary<string, object?> BuildOAuthCompletionPayload(
        string? password,
        string? invitationCode,
        string? affiliateCode,
        bool? adoptDisplayName,
        bool? adoptAvatar)
    {
        var payload = OAuthAdoptionPayload(adoptDisplayName, adoptAvatar);
        if (!string.IsNullOrWhiteSpace(password)) payload["password"] = password;
        if (!string.IsNullOrWhiteSpace(invitationCode)) payload["invitation_code"] = invitationCode.Trim();
        if (!string.IsNullOrWhiteSpace(affiliateCode)) payload["aff_code"] = affiliateCode.Trim();
        return payload;
    }

    public async Task<AuthUser> GetMeAsync()
    {
        var user = await SendAsync<GoUser>(HttpMethod.Get, $"{ApiPrefix}/auth/me");
        return AuthUser.From(user);
    }

    public async Task LogoutAsync()
    {
        var refresh = await GetTokenAsync(RefreshTokenKey);
        try
        {
            await SendAsync(HttpMethod.Post, $"{ApiPrefix}/auth/logout", string.IsNullOrWhiteSpace(refresh) ? null : new { refresh_token = refresh });
        }
        finally
        {
            await ClearTokensAsync();
        }
    }

    public Task ChangePasswordAsync(ChangePasswordRequest request) =>
        SendAsync(HttpMethod.Put, $"{ApiPrefix}/user/password", new { old_password = request.CurrentPassword, new_password = request.NewPassword });

    public async Task<ProfileDto> GetProfileAsync()
    {
        var profile = await SendAsync<GoUser>(HttpMethod.Get, $"{ApiPrefix}/user/profile");
        return ProfileDto.From(profile);
    }

    public async Task<ProfileDto> UpdateProfileAsync(ProfileUpdate input)
    {
        var payload = new Dictionary<string, object?> { ["username"] = input.DisplayName };
        if (input.AvatarUrl is not null) payload["avatar_url"] = input.AvatarUrl;
        if (input.BalanceNotifyEnabled.HasValue) payload["balance_notify_enabled"] = input.BalanceNotifyEnabled.Value;
        if (input.BalanceNotifyThreshold.HasValue) payload["balance_notify_threshold"] = input.BalanceNotifyThreshold.Value;
        var profile = await SendAsync<GoUser>(HttpMethod.Put, $"{ApiPrefix}/user", payload);
        return ProfileDto.From(profile);
    }

    public Task<TotpStatusDto> GetTotpStatusAsync() =>
        SendAsync<TotpStatusDto>(HttpMethod.Get, $"{ApiPrefix}/user/totp/status");

    public Task<TotpVerificationMethodDto> GetTotpVerificationMethodAsync() =>
        SendAsync<TotpVerificationMethodDto>(HttpMethod.Get, $"{ApiPrefix}/user/totp/verification-method");

    public Task SendTotpVerifyCodeAsync() =>
        SendAsync(HttpMethod.Post, $"{ApiPrefix}/user/totp/send-code");

    public Task<TotpSetupResponseDto> SetupTotpAsync(string? emailCode, string? password) =>
        SendAsync<TotpSetupResponseDto>(HttpMethod.Post, $"{ApiPrefix}/user/totp/setup", new
        {
            email_code = string.IsNullOrWhiteSpace(emailCode) ? null : emailCode.Trim(),
            password = string.IsNullOrWhiteSpace(password) ? null : password
        });

    public Task EnableTotpAsync(string totpCode, string setupToken) =>
        SendAsync(HttpMethod.Post, $"{ApiPrefix}/user/totp/enable", new { totp_code = totpCode.Trim(), setup_token = setupToken });

    public Task DisableTotpAsync(string? emailCode, string? password) =>
        SendAsync(HttpMethod.Post, $"{ApiPrefix}/user/totp/disable", new
        {
            email_code = string.IsNullOrWhiteSpace(emailCode) ? null : emailCode.Trim(),
            password = string.IsNullOrWhiteSpace(password) ? null : password
        });

    public Task<StepUpVerificationDto> VerifyTotpStepUpAsync(string code) =>
        SendAsync<StepUpVerificationDto>(HttpMethod.Post, $"{ApiPrefix}/user/totp/step-up", new { code = code.Trim() });

    public Task<List<PasskeyCredentialDto>> GetPasskeysAsync() =>
        SendAsync<List<PasskeyCredentialDto>>(HttpMethod.Get, $"{ApiPrefix}/user/passkeys");

    public Task<JsonElement> BeginPasskeyRegistrationAsync(string password) =>
        SendAsync<JsonElement>(HttpMethod.Post, $"{ApiPrefix}/user/passkeys/register/begin", new { password });

    public Task<PasskeyCredentialDto> FinishPasskeyRegistrationAsync(string sessionToken, string name, JsonElement credential) =>
        SendAsync<PasskeyCredentialDto>(HttpMethod.Post, $"{ApiPrefix}/user/passkeys/register/finish", new { session_token = sessionToken, name, credential });

    public Task RenamePasskeyAsync(long id, string name) =>
        SendAsync(HttpMethod.Patch, $"{ApiPrefix}/user/passkeys/{id}", new { name });

    public Task DeletePasskeyAsync(long id, string password) =>
        SendAsync(HttpMethod.Delete, $"{ApiPrefix}/user/passkeys/{id}", new { password });

    public Task SendNotifyEmailCodeAsync(string email) =>
        SendAsync(HttpMethod.Post, $"{ApiPrefix}/user/notify-email/send-code", new { email = email.Trim() });

    public async Task<ProfileDto> VerifyNotifyEmailAsync(string email, string code)
    {
        var user = await SendAsync<GoUser>(HttpMethod.Post, $"{ApiPrefix}/user/notify-email/verify", new { email = email.Trim(), code = code.Trim() });
        return ProfileDto.From(user);
    }

    public async Task<ProfileDto> ToggleNotifyEmailAsync(string email, bool disabled)
    {
        var user = await SendAsync<GoUser>(HttpMethod.Put, $"{ApiPrefix}/user/notify-email/toggle", new { email = email.Trim(), disabled });
        return ProfileDto.From(user);
    }

    public async Task<ProfileDto> RemoveNotifyEmailAsync(string email)
    {
        var user = await SendAsync<GoUser>(HttpMethod.Delete, $"{ApiPrefix}/user/notify-email", new { email = email.Trim() });
        return ProfileDto.From(user);
    }

    public Task<List<UserAttributeDefinitionDto>> GetUserAttributeDefinitionsAsync() =>
        SendAsync<List<UserAttributeDefinitionDto>>(HttpMethod.Get, $"{ApiPrefix}/admin/user-attributes");

    public Task<List<UserAttributeValueDto>> GetUserAttributeValuesAsync(string userId) =>
        SendAsync<List<UserAttributeValueDto>>(HttpMethod.Get, $"{ApiPrefix}/admin/users/{Uri.EscapeDataString(userId)}/attributes");

    public Task<List<UserAttributeValueDto>> UpdateUserAttributeValuesAsync(string userId, Dictionary<long, string> values) =>
        SendAsync<List<UserAttributeValueDto>>(HttpMethod.Put, $"{ApiPrefix}/admin/users/{Uri.EscapeDataString(userId)}/attributes", new { values });

    public Task<UserAttributeDefinitionDto> CreateUserAttributeDefinitionAsync(object payload) =>
        SendAsync<UserAttributeDefinitionDto>(HttpMethod.Post, $"{ApiPrefix}/admin/user-attributes", payload);

    public Task<UserAttributeDefinitionDto> UpdateUserAttributeDefinitionAsync(long id, object payload) =>
        SendAsync<UserAttributeDefinitionDto>(HttpMethod.Put, $"{ApiPrefix}/admin/user-attributes/{id}", payload);

    public Task DeleteUserAttributeDefinitionAsync(long id) =>
        SendAsync(HttpMethod.Delete, $"{ApiPrefix}/admin/user-attributes/{id}");

    public Task PrepareOAuthBindAccessTokenCookieAsync() =>
        SendAsync(HttpMethod.Post, $"{ApiPrefix}/auth/oauth/bind-token");

    public Task<OAuthBindStartDto> StartIdentityBindingAsync(string provider, string redirectTo = "/profile") =>
        SendAsync<OAuthBindStartDto>(HttpMethod.Post, $"{ApiPrefix}/user/auth-identities/bind/start", new
        {
            provider = provider.Trim().ToLowerInvariant(),
            redirect_to = string.IsNullOrWhiteSpace(redirectTo) ? "/profile" : redirectTo
        });

    public Task SendEmailBindingCodeAsync(string email) =>
        SendAsync(HttpMethod.Post, $"{ApiPrefix}/user/account-bindings/email/send-code", new { email = email.Trim() });

    public async Task<ProfileDto> BindEmailIdentityAsync(string email, string verifyCode, string password)
    {
        var profile = await SendAsync<GoUser>(HttpMethod.Post, $"{ApiPrefix}/user/account-bindings/email", new
        {
            email = email.Trim(), verify_code = verifyCode.Trim(), password
        });
        return ProfileDto.From(profile);
    }

    public async Task<ProfileDto> UnbindIdentityAsync(string provider)
    {
        var profile = await SendAsync<GoUser>(HttpMethod.Delete, $"{ApiPrefix}/user/account-bindings/{Uri.EscapeDataString(provider.Trim().ToLowerInvariant())}");
        return ProfileDto.From(profile);
    }

    public async Task<DashboardDto> GetDashboardAsync()
    {
        if (await GetMeAsync() is { IsAdmin: false })
        {
            var stats = await GetUserDashboardStatsAsync();
            return DashboardDto.From(stats);
        }

        var admin = await SendAsync<AdminDashboardStats>(HttpMethod.Get, $"{ApiPrefix}/admin/dashboard/stats");
        return DashboardDto.From(admin);
    }

    public Task<UserDashboardStats> GetUserDashboardStatsAsync() =>
        SendAsync<UserDashboardStats>(HttpMethod.Get, $"{ApiPrefix}/usage/dashboard/stats");

    public Task<UserDashboardTrendResponseDto> GetUserDashboardTrendAsync(UserUsageQuery query, string granularity)
    {
        var filters = BuildUserUsageQuery(query, includePagination: false);
        var prefix = string.IsNullOrWhiteSpace(filters) ? string.Empty : filters + "&";
        var normalizedGranularity = string.Equals(granularity, "hour", StringComparison.OrdinalIgnoreCase) ? "hour" : "day";
        return SendAsync<UserDashboardTrendResponseDto>(HttpMethod.Get,
            $"{ApiPrefix}/usage/dashboard/trend?{prefix}granularity={normalizedGranularity}");
    }

    public Task<AdminDashboardSnapshotDto> GetAdminDashboardSnapshotAsync(
        string startDate,
        string endDate,
        string granularity = "hour",
        bool includeStats = true,
        string? timezone = null)
    {
        var query = new List<string>
        {
            $"start_date={Uri.EscapeDataString(startDate)}",
            $"end_date={Uri.EscapeDataString(endDate)}",
            $"granularity={Uri.EscapeDataString(granularity == "day" ? "day" : "hour")}",
            $"include_stats={includeStats.ToString().ToLowerInvariant()}",
            "include_trend=true",
            "include_model_stats=true",
            "include_group_stats=false",
            "include_users_trend=true",
            "users_trend_limit=12"
        };
        if (!string.IsNullOrWhiteSpace(timezone))
            query.Add($"timezone={Uri.EscapeDataString(timezone)}");

        return SendAsync<AdminDashboardSnapshotDto>(HttpMethod.Get,
            $"{ApiPrefix}/admin/dashboard/snapshot-v2?{string.Join("&", query)}");
    }

    public Task<AdminDashboardRankingResponseDto> GetAdminDashboardRankingAsync(
        string startDate,
        string endDate,
        int limit = 12,
        string? timezone = null)
    {
        var query = new List<string>
        {
            $"start_date={Uri.EscapeDataString(startDate)}",
            $"end_date={Uri.EscapeDataString(endDate)}",
            $"limit={Math.Clamp(limit, 1, 50)}"
        };
        if (!string.IsNullOrWhiteSpace(timezone))
            query.Add($"timezone={Uri.EscapeDataString(timezone)}");

        return SendAsync<AdminDashboardRankingResponseDto>(HttpMethod.Get,
            $"{ApiPrefix}/admin/dashboard/users-ranking?{string.Join("&", query)}");
    }

    public Task<AdminDashboardUserBreakdownResponseDto> GetAdminDashboardUserBreakdownAsync(
        string startDate,
        string endDate,
        string model,
        string modelSource = "requested",
        int limit = 50,
        string? timezone = null) =>
        GetAdminDashboardUserBreakdownAsync(new AdminDashboardUserBreakdownQueryDto
        {
            StartDate = startDate,
            EndDate = endDate,
            Model = model,
            ModelSource = modelSource,
            Limit = limit,
            Timezone = timezone ?? string.Empty
        });

    public Task<AdminDashboardUserBreakdownResponseDto> GetAdminDashboardUserBreakdownAsync(AdminDashboardUserBreakdownQueryDto filter)
    {
        var normalizedSource = filter.ModelSource.Trim().ToLowerInvariant() is "upstream" or "mapping"
            ? filter.ModelSource.Trim().ToLowerInvariant()
            : "requested";
        var query = new List<string>
        {
            $"start_date={Uri.EscapeDataString(filter.StartDate.Trim())}",
            $"end_date={Uri.EscapeDataString(filter.EndDate.Trim())}",
            $"model_source={Uri.EscapeDataString(normalizedSource)}",
            $"limit={Math.Clamp(filter.Limit, 1, 200)}"
        };
        if (!string.IsNullOrWhiteSpace(filter.Timezone)) query.Add($"timezone={Uri.EscapeDataString(filter.Timezone.Trim())}");
        if (!string.IsNullOrWhiteSpace(filter.Model)) query.Add($"model={Uri.EscapeDataString(filter.Model.Trim())}");
        if (!string.IsNullOrWhiteSpace(filter.Endpoint)) query.Add($"endpoint={Uri.EscapeDataString(filter.Endpoint.Trim())}");
        if (!string.IsNullOrWhiteSpace(filter.EndpointType)) query.Add($"endpoint_type={Uri.EscapeDataString(filter.EndpointType.Trim())}");
        if (!string.IsNullOrWhiteSpace(filter.RequestType)) query.Add($"request_type={Uri.EscapeDataString(filter.RequestType.Trim())}");
        if (!string.IsNullOrWhiteSpace(filter.SortBy)) query.Add($"sort_by={Uri.EscapeDataString(filter.SortBy.Trim())}");
        if (filter.UserId is > 0) query.Add($"user_id={filter.UserId.Value}");
        if (filter.ApiKeyId is > 0) query.Add($"api_key_id={filter.ApiKeyId.Value}");
        if (filter.AccountId is > 0) query.Add($"account_id={filter.AccountId.Value}");
        if (filter.GroupId is > 0) query.Add($"group_id={filter.GroupId.Value}");
        if (filter.Stream.HasValue) query.Add($"stream={filter.Stream.Value.ToString().ToLowerInvariant()}");
        if (filter.BillingType.HasValue) query.Add($"billing_type={filter.BillingType.Value}");

        return SendAsync<AdminDashboardUserBreakdownResponseDto>(HttpMethod.Get,
            $"{ApiPrefix}/admin/dashboard/user-breakdown?{string.Join("&", query)}");
    }

    public Task<PagedEnvelope<UserWorkClassificationDto>> GetMyWorkClassificationsAsync(int page = 1, int pageSize = 20) =>
        SendAsync<PagedEnvelope<UserWorkClassificationDto>>(HttpMethod.Get,
            $"{ApiPrefix}/usage/work-classifications?page={Math.Max(1, page)}&page_size={Math.Clamp(pageSize, 1, 200)}");

    public Task<UserWorkClassificationAppealDto> CreateMyWorkClassificationAppealAsync(
        long usageLogId, UserWorkClassificationAppealRequestDto request) =>
        SendAsync<UserWorkClassificationAppealDto>(HttpMethod.Post,
            $"{ApiPrefix}/usage/work-classifications/{usageLogId}/appeals", request);

    public Task<WorkDistributionSummaryDto> GetAdminWorkDistributionSummaryAsync(WorkDistributionSummaryQueryDto filter)
    {
        var query = new List<string>
        {
            $"start_date={Uri.EscapeDataString(filter.StartDate.Trim())}",
            $"end_date={Uri.EscapeDataString(filter.EndDate.Trim())}",
            $"metric={(string.Equals(filter.Metric, "tokens", StringComparison.OrdinalIgnoreCase) ? "tokens" : "requests")}",
            $"min_sample_size={Math.Clamp(filter.MinSampleSize, 5, 1000)}",
            $"min_cohort_size={Math.Clamp(filter.MinCohortSize, 5, 1000)}",
            $"user_limit={Math.Clamp(filter.UserLimit, 1, 500)}"
        };
        if (!string.IsNullOrWhiteSpace(filter.Timezone)) query.Add($"timezone={Uri.EscapeDataString(filter.Timezone.Trim())}");
        if (filter.UserId is > 0) query.Add($"user_id={filter.UserId.Value}");
        if (!string.IsNullOrWhiteSpace(filter.Department)) query.Add($"department={Uri.EscapeDataString(filter.Department.Trim())}");
        if (!string.IsNullOrWhiteSpace(filter.Role)) query.Add($"role={Uri.EscapeDataString(filter.Role.Trim())}");

        return SendAsync<WorkDistributionSummaryDto>(HttpMethod.Get,
            $"{ApiPrefix}/admin/work-distribution/summary?{string.Join("&", query)}");
    }

    public Task<WorkDistributionPagedDto<WorkDistributionRecordDto>> GetAdminWorkDistributionRecordsAsync(WorkDistributionRecordQueryDto filter)
    {
        var query = BuildWorkDistributionScopeQuery(filter.StartDate, filter.EndDate, filter.Timezone,
            filter.UserId, filter.Department, filter.Role);
        query.Add($"page={Math.Max(1, filter.Page)}");
        query.Add($"page_size={Math.Clamp(filter.PageSize, 1, 200)}");
        query.Add($"min_sample_size={Math.Clamp(filter.MinSampleSize, 5, 1000)}");
        query.Add($"min_cohort_size={Math.Clamp(filter.MinCohortSize, 5, 1000)}");
        if (!string.IsNullOrWhiteSpace(filter.Category)) query.Add($"category={Uri.EscapeDataString(filter.Category.Trim())}");
        if (!string.IsNullOrWhiteSpace(filter.WorkRelated)) query.Add($"work_related={Uri.EscapeDataString(filter.WorkRelated.Trim())}");
        if (!string.IsNullOrWhiteSpace(filter.ReviewStatus)) query.Add($"review_status={Uri.EscapeDataString(filter.ReviewStatus.Trim())}");
        return SendAsync<WorkDistributionPagedDto<WorkDistributionRecordDto>>(HttpMethod.Get,
            $"{ApiPrefix}/admin/work-distribution/records?{string.Join("&", query)}");
    }

    public Task<WorkDistributionReviewDto> CreateAdminWorkDistributionCorrectionAsync(
        long usageLogId, string workRelated, string category, string reasonCode) =>
        SendAsync<WorkDistributionReviewDto>(HttpMethod.Post,
            $"{ApiPrefix}/admin/work-distribution/records/{usageLogId}/correction",
            new { work_related = workRelated, category, reason_code = reasonCode });

    public Task<WorkDistributionPagedDto<WorkDistributionReviewDto>> GetAdminWorkDistributionReviewsAsync(WorkDistributionReviewQueryDto filter)
    {
        var query = new List<string>
        {
            $"page={Math.Max(1, filter.Page)}",
            $"page_size={Math.Clamp(filter.PageSize, 1, 200)}"
        };
        if (!string.IsNullOrWhiteSpace(filter.Status)) query.Add($"status={Uri.EscapeDataString(filter.Status.Trim())}");
        if (filter.UserId is > 0) query.Add($"user_id={filter.UserId.Value}");
        return SendAsync<WorkDistributionPagedDto<WorkDistributionReviewDto>>(HttpMethod.Get,
            $"{ApiPrefix}/admin/work-distribution/reviews?{string.Join("&", query)}");
    }

    public Task<WorkDistributionReviewDto> ResolveAdminWorkDistributionReviewAsync(long reviewId, string decision, string resolutionNote) =>
        SendAsync<WorkDistributionReviewDto>(HttpMethod.Post,
            $"{ApiPrefix}/admin/work-distribution/reviews/{reviewId}/resolve",
            new { decision, resolution_note = resolutionNote });

    private static List<string> BuildWorkDistributionScopeQuery(
        string startDate, string endDate, string timezone, long? userId, string department, string role)
    {
        var query = new List<string>
        {
            $"start_date={Uri.EscapeDataString(startDate.Trim())}",
            $"end_date={Uri.EscapeDataString(endDate.Trim())}"
        };
        if (!string.IsNullOrWhiteSpace(timezone)) query.Add($"timezone={Uri.EscapeDataString(timezone.Trim())}");
        if (userId is > 0) query.Add($"user_id={userId.Value}");
        if (!string.IsNullOrWhiteSpace(department)) query.Add($"department={Uri.EscapeDataString(department.Trim())}");
        if (!string.IsNullOrWhiteSpace(role)) query.Add($"role={Uri.EscapeDataString(role.Trim())}");
        return query;
    }

    public async Task<List<UserDto>> GetUsersAsync()
    {
        var page = await SendAsync<PagedEnvelope<GoUser>>(HttpMethod.Get, $"{ApiPrefix}/admin/users?page=1&page_size=1000");
        return page.Items.Select(UserDto.From).ToList();
    }

    public Task<PagedEnvelope<GoUser>> GetAdminUsersAsync(
        int page = 1,
        int pageSize = 20,
        string? search = null,
        string? role = null,
        string? status = null,
        string? groupName = null,
        long? apiKeyGroupId = null,
        IReadOnlyDictionary<long, string>? attributes = null,
        string sortBy = "created_at",
        string sortOrder = "desc",
        bool includeSubscriptions = true)
    {
        var query = new List<string>
        {
            $"page={Math.Max(1, page)}",
            $"page_size={Math.Clamp(pageSize, 1, 100)}",
            $"sort_by={Uri.EscapeDataString(sortBy)}",
            $"sort_order={Uri.EscapeDataString(sortOrder)}",
            $"include_subscriptions={includeSubscriptions.ToString().ToLowerInvariant()}"
        };
        if (!string.IsNullOrWhiteSpace(search)) query.Add($"search={Uri.EscapeDataString(search.Trim())}");
        if (!string.IsNullOrWhiteSpace(role)) query.Add($"role={Uri.EscapeDataString(role)}");
        if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
        if (!string.IsNullOrWhiteSpace(groupName)) query.Add($"group_name={Uri.EscapeDataString(groupName.Trim())}");
        if (apiKeyGroupId is > 0) query.Add($"api_key_group_id={apiKeyGroupId.Value}");
        if (attributes is not null)
        {
            foreach (var (id, value) in attributes)
            {
                if (!string.IsNullOrWhiteSpace(value)) query.Add($"attr%5B{id}%5D={Uri.EscapeDataString(value.Trim())}");
            }
        }
        return SendAsync<PagedEnvelope<GoUser>>(HttpMethod.Get, $"{ApiPrefix}/admin/users?{string.Join("&", query)}");
    }

    public Task<GoUser> CreateAdminUserAsync(object payload) =>
        SendAsync<GoUser>(HttpMethod.Post, $"{ApiPrefix}/admin/users", payload);

    public Task<GoUser> UpdateAdminUserAsync(long id, object payload) =>
        SendAsync<GoUser>(HttpMethod.Put, $"{ApiPrefix}/admin/users/{id}", payload);

    public Task DeleteAdminUserAsync(long id) =>
        SendAsync(HttpMethod.Delete, $"{ApiPrefix}/admin/users/{id}");

    public Task<GoUser> UpdateAdminUserBalanceAsync(long id, double amount, string operation, string? notes = null) =>
        SendAsync<GoUser>(HttpMethod.Post, $"{ApiPrefix}/admin/users/{id}/balance", new
        {
            balance = amount,
            operation,
            notes = notes?.Trim() ?? string.Empty
        });

    public Task<AdminBatchUpdateResultDto> BatchUpdateAdminUserLimitsAsync(
        IEnumerable<long> userIds,
        int? concurrency,
        int? rpmLimit,
        int? tpmLimit = null) =>
        SendAsync<AdminBatchUpdateResultDto>(HttpMethod.Post, $"{ApiPrefix}/admin/users/batch-limits", new
        {
            user_ids = userIds.Distinct().ToArray(),
            all = false,
            concurrency,
            rpm_limit = rpmLimit,
            tpm_limit = tpmLimit
        });

    public Task<AdminBalanceHistoryResponseDto> GetAdminUserBalanceHistoryAsync(
        long userId,
        int page = 1,
        int pageSize = 15,
        string? type = null) =>
        SendAsync<AdminBalanceHistoryResponseDto>(HttpMethod.Get,
            $"{ApiPrefix}/admin/users/{userId}/balance-history?page={Math.Max(1, page)}&page_size={Math.Clamp(pageSize, 1, 100)}"
            + (string.IsNullOrWhiteSpace(type) ? string.Empty : $"&type={Uri.EscapeDataString(type)}"));

    public Task<AdminBatchUsersUsageResponseDto> GetBatchAdminUsersUsageAsync(IEnumerable<long> userIds) =>
        SendAsync<AdminBatchUsersUsageResponseDto>(HttpMethod.Post, $"{ApiPrefix}/admin/dashboard/users-usage", new
        {
            user_ids = userIds.Distinct().ToArray()
        });

    public Task<AdminBatchUserAttributesResponseDto> GetBatchUserAttributesAsync(IEnumerable<long> userIds) =>
        SendAsync<AdminBatchUserAttributesResponseDto>(HttpMethod.Post, $"{ApiPrefix}/admin/user-attributes/batch", new
        {
            user_ids = userIds.Distinct().ToArray()
        });

    public Task<AdminPlatformQuotaResponseDto> GetAdminUserPlatformQuotasAsync(long userId) =>
        SendAsync<AdminPlatformQuotaResponseDto>(HttpMethod.Get, $"{ApiPrefix}/admin/users/{userId}/platform-quotas");

    public Task<AdminPlatformQuotaResponseDto> UpdateAdminUserPlatformQuotasAsync(long userId, IEnumerable<object> quotas) =>
        SendAsync<AdminPlatformQuotaResponseDto>(HttpMethod.Put, $"{ApiPrefix}/admin/users/{userId}/platform-quotas", new { quotas = quotas.ToArray() });

    public Task<AdminPlatformQuotaResponseDto> ResetAdminUserPlatformQuotaAsync(long userId, string platform, string window) =>
        SendAsync<AdminPlatformQuotaResponseDto>(HttpMethod.Post, $"{ApiPrefix}/admin/users/{userId}/platform-quotas/reset", new { platform, window });

    public Task<List<GoGroup>> GetAllAdminGroupsAsync(bool includeInactive = false) =>
        SendAsync<List<GoGroup>>(HttpMethod.Get, $"{ApiPrefix}/admin/groups/all{(includeInactive ? "?include_inactive=true" : string.Empty)}");

    public Task<Dictionary<string, long>> ReplaceAdminUserGroupAsync(long userId, long oldGroupId, long newGroupId) =>
        SendAsync<Dictionary<string, long>>(HttpMethod.Post, $"{ApiPrefix}/admin/users/{userId}/replace-group", new
        {
            old_group_id = oldGroupId,
            new_group_id = newGroupId
        });

    public async Task<UserDto> CreateUserAsync(UserInput input)
    {
        var user = await SendAsync<GoUser>(HttpMethod.Post, $"{ApiPrefix}/admin/users", new
        {
            email = input.Email,
            password = input.Password,
            username = input.DisplayName,
            role = input.Role,
            balance = input.BalanceMicros / 1_000_000m,
            concurrency = input.MaxConcurrency,
            rpm_limit = input.RpmLimit,
            status = input.IsActive ? "active" : "disabled"
        });
        return UserDto.From(user);
    }

    public async Task<UserDto> UpdateUserAsync(string id, UserInput input)
    {
        var payload = new Dictionary<string, object?>
        {
            ["email"] = input.Email,
            ["username"] = input.DisplayName,
            ["role"] = input.Role,
            ["balance"] = input.BalanceMicros / 1_000_000m,
            ["concurrency"] = input.MaxConcurrency,
            ["rpm_limit"] = input.RpmLimit,
            ["status"] = input.IsActive ? "active" : "disabled"
        };
        if (!string.IsNullOrWhiteSpace(input.Password)) payload["password"] = input.Password;
        var user = await SendAsync<GoUser>(HttpMethod.Put, $"{ApiPrefix}/admin/users/{Uri.EscapeDataString(id)}", payload);
        return UserDto.From(user);
    }

    public Task<List<ApiKeyDto>> GetApiKeysAsync(string? userId = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return GetAdminKeysAsync();
        }
        return GetAdminKeysAsync(userId);
    }

    public Task UpdateApiKeyGroupAsync(string id, long? groupId) =>
        SendAsync(HttpMethod.Put, $"{ApiPrefix}/admin/api-keys/{Uri.EscapeDataString(id)}", new { group_id = groupId });

    public async Task<List<ApiKeyDto>> GetMyApiKeysAsync()
    {
        var page = await GetMyApiKeysPageAsync(new ApiKeyListQuery { Page = 1, PageSize = 100 });
        return page.Items;
    }

    public async Task<PagedEnvelope<ApiKeyDto>> GetMyApiKeysPageAsync(ApiKeyListQuery input)
    {
        var query = new List<string>
        {
            $"page={Math.Max(1, input.Page)}",
            $"page_size={Math.Clamp(input.PageSize, 1, 100)}",
            $"sort_by={Uri.EscapeDataString(string.IsNullOrWhiteSpace(input.SortBy) ? "created_at" : input.SortBy)}",
            $"sort_order={(string.Equals(input.SortOrder, "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc")}"
        };
        if (!string.IsNullOrWhiteSpace(input.Search)) query.Add($"search={Uri.EscapeDataString(input.Search.Trim())}");
        if (!string.IsNullOrWhiteSpace(input.Status)) query.Add($"status={Uri.EscapeDataString(input.Status)}");
        if (!string.IsNullOrWhiteSpace(input.GroupId)) query.Add($"group_id={Uri.EscapeDataString(input.GroupId)}");

        var raw = await SendAsync<PagedEnvelope<GoApiKey>>(HttpMethod.Get, $"{ApiPrefix}/keys?{string.Join("&", query)}");
        return new PagedEnvelope<ApiKeyDto>
        {
            Items = raw.Items.Select(ApiKeyDto.From).ToList(),
            Total = raw.Total,
            Page = raw.Page,
            PageSize = raw.PageSize,
            Pages = raw.Pages
        };
    }

    public async Task<ApiKeyDto> CreateMyApiKeyAsync(SelfApiKeyInput input)
    {
        var expiresInDays = input.EnableExpiration && input.ExpiresAt.HasValue
            ? Math.Max(1, (int)Math.Ceiling((input.ExpiresAt.Value - DateTimeOffset.Now).TotalDays))
            : (int?)null;
        var key = await SendAsync<GoApiKey>(HttpMethod.Post, $"{ApiPrefix}/keys", new
        {
            name = input.Name.Trim(),
            group_id = input.GroupId,
            custom_key = input.UseCustomKey ? input.CustomKey.Trim() : null,
            ip_whitelist = SplitApiKeyLines(input.EnableIpRestriction ? input.IpWhitelistText : string.Empty),
            ip_blacklist = SplitApiKeyLines(input.EnableIpRestriction ? input.IpBlacklistText : string.Empty),
            quota = Math.Max(0, input.Quota),
            expires_in_days = expiresInDays,
            rate_limit_5h = input.EnableRateLimit ? Math.Max(0, input.RateLimit5h) : 0,
            rate_limit_1d = input.EnableRateLimit ? Math.Max(0, input.RateLimit1d) : 0,
            rate_limit_7d = input.EnableRateLimit ? Math.Max(0, input.RateLimit7d) : 0
        });
        return ApiKeyDto.From(key);
    }

    public async Task<ApiKeyDto> UpdateMyApiKeyAsync(string id, SelfApiKeyInput input, bool includeStatus = true)
    {
        var payload = new Dictionary<string, object?>
        {
            ["name"] = input.Name.Trim(),
            ["group_id"] = input.GroupId,
            ["ip_whitelist"] = SplitApiKeyLines(input.EnableIpRestriction ? input.IpWhitelistText : string.Empty),
            ["ip_blacklist"] = SplitApiKeyLines(input.EnableIpRestriction ? input.IpBlacklistText : string.Empty),
            ["quota"] = Math.Max(0, input.Quota),
            ["expires_at"] = input.EnableExpiration && input.ExpiresAt.HasValue ? input.ExpiresAt.Value.ToUniversalTime().ToString("O") : string.Empty,
            ["rate_limit_5h"] = input.EnableRateLimit ? Math.Max(0, input.RateLimit5h) : 0,
            ["rate_limit_1d"] = input.EnableRateLimit ? Math.Max(0, input.RateLimit1d) : 0,
            ["rate_limit_7d"] = input.EnableRateLimit ? Math.Max(0, input.RateLimit7d) : 0
        };
        if (includeStatus) payload["status"] = input.Status;
        var key = await SendAsync<GoApiKey>(HttpMethod.Put, $"{ApiPrefix}/keys/{Uri.EscapeDataString(id)}", payload);
        return ApiKeyDto.From(key);
    }

    public async Task<ApiKeyDto> SetMyApiKeyStatusAsync(string id, string status)
    {
        var key = await SendAsync<GoApiKey>(HttpMethod.Put, $"{ApiPrefix}/keys/{Uri.EscapeDataString(id)}", new
        {
            status = string.Equals(status, "active", StringComparison.OrdinalIgnoreCase) ? "active" : "inactive"
        });
        return ApiKeyDto.From(key);
    }

    public async Task<ApiKeyDto> ChangeMyApiKeyGroupAsync(string id, long groupId)
    {
        var key = await SendAsync<GoApiKey>(HttpMethod.Put, $"{ApiPrefix}/keys/{Uri.EscapeDataString(id)}", new { group_id = groupId });
        return ApiKeyDto.From(key);
    }

    public async Task<ApiKeyDto> ResetMyApiKeyQuotaAsync(string id)
    {
        var key = await SendAsync<GoApiKey>(HttpMethod.Put, $"{ApiPrefix}/keys/{Uri.EscapeDataString(id)}", new { reset_quota = true });
        return ApiKeyDto.From(key);
    }

    public async Task<ApiKeyDto> ResetMyApiKeyRateLimitAsync(string id)
    {
        var key = await SendAsync<GoApiKey>(HttpMethod.Put, $"{ApiPrefix}/keys/{Uri.EscapeDataString(id)}", new { reset_rate_limit_usage = true });
        return ApiKeyDto.From(key);
    }

    public Task<ApiKeyUsageBatchDto> GetMyApiKeyUsageBatchAsync(IEnumerable<long> ids) =>
        SendAsync<ApiKeyUsageBatchDto>(HttpMethod.Post, $"{ApiPrefix}/usage/dashboard/api-keys-usage", new { api_key_ids = ids.Distinct().Take(100).ToArray() });

    public Task DeleteMyApiKeyAsync(string id) => SendAsync(HttpMethod.Delete, $"{ApiPrefix}/keys/{Uri.EscapeDataString(id)}");

    private static List<string> SplitApiKeyLines(string value) =>
        value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private async Task<List<ApiKeyDto>> GetAdminKeysAsync(string? userId = null)
    {
        var users = string.IsNullOrWhiteSpace(userId)
            ? await GetUsersAsync()
            : [new UserDto { Id = userId }];
        var all = new List<ApiKeyDto>();
        foreach (var user in users)
        {
            if (!long.TryParse(user.Id, out var numericId)) continue;
            var page = await SendAsync<PagedEnvelope<GoApiKey>>(HttpMethod.Get, $"{ApiPrefix}/admin/users/{numericId}/api-keys?page=1&page_size=1000");
            all.AddRange(page.Items.Select(ApiKeyDto.From));
        }
        return all;
    }

    // 独立 Worker 风格上游账号 API；不得改为 /admin/accounts。
    public Task<List<UpstreamAccountDto>> GetUpstreamAccountsAsync() =>
        SendAsync<List<UpstreamAccountDto>>(HttpMethod.Get, $"{ApiPrefix}/admin/upstream-accounts");

    public Task<UpstreamAccountDto> CreateUpstreamAccountAsync(UpstreamAccountInput input) =>
        SendAsync<UpstreamAccountDto>(HttpMethod.Post, $"{ApiPrefix}/admin/upstream-accounts", input);

    public Task<UpstreamAccountDto> UpdateUpstreamAccountAsync(string id, UpstreamAccountInput input) =>
        SendAsync<UpstreamAccountDto>(HttpMethod.Put,
            $"{ApiPrefix}/admin/upstream-accounts/{Uri.EscapeDataString(id)}", input);

    public Task<UpstreamAccountDto> SetUpstreamAccountSchedulingAsync(string id, bool isActive) =>
        SendAsync<UpstreamAccountDto>(HttpMethod.Patch,
            $"{ApiPrefix}/admin/upstream-accounts/{Uri.EscapeDataString(id)}/scheduling", new { is_active = isActive });

    public Task<UpstreamConnectionTestResultDto> TestUpstreamAccountDraftAsync(UpstreamAccountInput input) =>
        SendAsync<UpstreamConnectionTestResultDto>(HttpMethod.Post,
            $"{ApiPrefix}/admin/upstream-accounts/test-connection", input);

    public Task<UpstreamConnectionTestResultDto> TestUpstreamAccountSavedAsync(string id) =>
        SendAsync<UpstreamConnectionTestResultDto>(HttpMethod.Post,
            $"{ApiPrefix}/admin/upstream-accounts/{Uri.EscapeDataString(id)}/test-connection", new { });

    public Task DeleteUpstreamAccountAsync(string id) =>
        SendAsync(HttpMethod.Delete, $"{ApiPrefix}/admin/upstream-accounts/{Uri.EscapeDataString(id)}");

    public async Task<List<AccountDto>> GetAccountsAsync(string? search = null)
    {
        var page = await GetAccountsPageAsync(new AccountListQuery { PageSize = 1000, Search = search ?? string.Empty });
        return page.Items;
    }

    public async Task<PagedResult<AccountDto>> GetAccountsPageAsync(AccountListQuery input)
    {
        var query = new List<string>
        {
            $"page={Math.Max(1, input.Page)}",
            $"page_size={Math.Clamp(input.PageSize, 1, 1000)}",
            $"sort_by={Uri.EscapeDataString(string.IsNullOrWhiteSpace(input.SortBy) ? "name" : input.SortBy)}",
            $"sort_order={(string.Equals(input.SortOrder, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc")}"
        };
        static void Add(List<string> target, string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) target.Add($"{key}={Uri.EscapeDataString(value.Trim())}");
        }
        Add(query, "search", input.Search);
        Add(query, "platform", input.Platform);
        Add(query, "type", input.Type);
        Add(query, "status", input.Status);
        Add(query, "privacy_mode", input.PrivacyMode);
        Add(query, "group", input.Group);
        if (input.IncludeSchedulerScore) query.Add("include_scheduler_score=true");

        var page = await SendAsync<PagedEnvelope<GoAccount>>(HttpMethod.Get, $"{ApiPrefix}/admin/accounts?{string.Join('&', query)}");
        return new PagedResult<AccountDto>
        {
            Items = page.Items.Select(AccountDto.From).ToList(), Page = page.Page,
            PageSize = page.PageSize, Total = checked((int)Math.Min(int.MaxValue, page.Total)), TotalPages = page.Pages
        };
    }

    public async Task<AccountDto> CreateAccountAsync(AccountInput input)
    {
        var credentials = BuildCredentials(input, requireCredentials: true);
        var extra = ParseObject(input.ExtraJson, "账号扩展配置") ?? new(StringComparer.OrdinalIgnoreCase);
        AddCopilotBillingExtra(extra, input);
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = input.Name.Trim(), ["platform"] = input.Platform, ["type"] = input.Type,
            ["notes"] = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(),
            ["credentials"] = credentials, ["extra"] = extra,
            ["proxy_id"] = input.ProxyId, ["load_factor"] = input.LoadFactor,
            ["concurrency"] = input.Concurrency, ["priority"] = input.Priority,
            ["rate_multiplier"] = input.RateMultiplier,
            ["group_ids"] = input.GroupIds, ["expires_at"] = input.ExpiresAt,
            ["auto_pause_on_expired"] = input.AutoPauseOnExpired
        };
        AddUpstreamBillingFields(payload, input);
        var account = await SendAsync<GoAccount>(HttpMethod.Post, $"{ApiPrefix}/admin/accounts", payload);
        return AccountDto.From(account);
    }

    public async Task<AccountDto> UpdateAccountAsync(string id, AccountInput input)
    {
        var credentials = BuildCredentials(input, requireCredentials: false);
        var extra = ParseObject(input.ExtraJson, "账号扩展配置") ?? new(StringComparer.OrdinalIgnoreCase);
        AddCopilotBillingExtra(extra, input);
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = input.Name.Trim(), ["notes"] = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(),
            ["type"] = input.Type, ["credentials"] = credentials, ["extra"] = extra, ["proxy_id"] = input.ProxyId,
            ["concurrency"] = input.Concurrency, ["load_factor"] = input.LoadFactor, ["priority"] = input.Priority,
            ["rate_multiplier"] = input.RateMultiplier, ["group_ids"] = input.GroupIds,
            ["expires_at"] = input.ExpiresAt, ["auto_pause_on_expired"] = input.AutoPauseOnExpired
        };
        AddUpstreamBillingFields(payload, input);
        var account = await SendAsync<GoAccount>(HttpMethod.Put, $"{ApiPrefix}/admin/accounts/{Uri.EscapeDataString(id)}", payload);
        return AccountDto.From(account);
    }

    public async Task<AccountDto> GetAccountAsync(string id)
    {
        var account = await SendAsync<GoAccount>(HttpMethod.Get, $"{ApiPrefix}/admin/accounts/{Uri.EscapeDataString(id)}");
        return AccountDto.From(account);
    }

    public async Task<AccountDto> SetAccountSchedulableAsync(string id, bool schedulable)
    {
        var account = await SendAsync<GoAccount>(HttpMethod.Post, $"{ApiPrefix}/admin/accounts/{Uri.EscapeDataString(id)}/schedulable", new { schedulable });
        return AccountDto.From(account);
    }

    public async Task<AccountTestResultDto> TestAccountAsync(string id)
    {
        var startedAt = DateTimeOffset.UtcNow;
        using var response = await SendCoreAsync(HttpMethod.Post,
            $"{ApiPrefix}/admin/accounts/{Uri.EscapeDataString(id)}/test", new { });
        string payload;
        try
        {
            payload = await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
        {
            throw new ApiException("读取连接测试结果失败，请重试。", response.StatusCode, ex);
        }
        var events = ParseAccountTestEvents(payload);
        var failure = events.LastOrDefault(item => string.Equals(item.Type, "error", StringComparison.OrdinalIgnoreCase));
        if (failure is not null)
        {
            return new AccountTestResultDto
            {
                Success = false,
                Message = FirstNonEmpty(failure.Error, failure.Text, failure.Status, failure.Code, "连接测试失败"),
                LatencyMs = checked((int)Math.Min(int.MaxValue, (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds))
            };
        }

        var completed = events.LastOrDefault(item => string.Equals(item.Type, "test_complete", StringComparison.OrdinalIgnoreCase));
        if (completed is null)
        {
            return new AccountTestResultDto
            {
                Success = false,
                Message = "连接测试流未返回完成事件。",
                LatencyMs = checked((int)Math.Min(int.MaxValue, (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds))
            };
        }

        var status = events.LastOrDefault(item => string.Equals(item.Type, "status", StringComparison.OrdinalIgnoreCase));
        return new AccountTestResultDto
        {
            Success = completed.Success,
            Message = completed.Success ? FirstNonEmpty(status?.Text, "连接成功") : "连接测试失败",
            LatencyMs = checked((int)Math.Min(int.MaxValue, (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds))
        };
    }

    public async Task<List<string>> PreviewAccountModelsAsync(AccountInput input)
    {
        if (!string.Equals(input.Type, "apikey", StringComparison.OrdinalIgnoreCase))
        {
            throw new ApiException("保存前模型读取仅支持 API Key 账号。", HttpStatusCode.BadRequest);
        }
        if (string.IsNullOrWhiteSpace(input.ApiKey))
        {
            throw new ApiException("请先填写 API Key。", HttpStatusCode.BadRequest);
        }

        var response = await SendAsync<Dictionary<string, List<string>>>(HttpMethod.Post,
            $"{ApiPrefix}/admin/accounts/models/sync-upstream-preview", new
            {
                platform = input.Platform,
                type = input.Type,
                base_url = input.BaseUrl.Trim(),
                api_key = input.ApiKey.Trim()
            });
        return response.TryGetValue("models", out var models) ? models : [];
    }

    public async Task<AccountDto> RefreshAccountAsync(string id)
    {
        var account = await SendAsync<GoAccount>(HttpMethod.Post, $"{ApiPrefix}/admin/accounts/{Uri.EscapeDataString(id)}/refresh");
        return AccountDto.From(account);
    }

    public async Task<AccountDto> ClearAccountErrorAsync(string id)
    {
        var account = await SendAsync<GoAccount>(HttpMethod.Post, $"{ApiPrefix}/admin/accounts/{Uri.EscapeDataString(id)}/clear-error");
        return AccountDto.From(account);
    }

    public Task DeleteAccountAsync(string id) =>
        SendAsync(HttpMethod.Delete, $"{ApiPrefix}/admin/accounts/{Uri.EscapeDataString(id)}");

    public async Task<AccountDto> DuplicateAccountAsync(string id)
    {
        var account = await SendAsync<GoAccount>(HttpMethod.Post, $"{ApiPrefix}/admin/accounts/{Uri.EscapeDataString(id)}/duplicate");
        return AccountDto.From(account);
    }

    public async Task<AccountDto> RecoverAccountStateAsync(string id)
    {
        var account = await SendAsync<GoAccount>(HttpMethod.Post, $"{ApiPrefix}/admin/accounts/{Uri.EscapeDataString(id)}/recover-state");
        return AccountDto.From(account);
    }

    public async Task<AccountDto> ResetAccountQuotaAsync(string id)
    {
        var account = await SendAsync<GoAccount>(HttpMethod.Post, $"{ApiPrefix}/admin/accounts/{Uri.EscapeDataString(id)}/reset-quota");
        return AccountDto.From(account);
    }

    public async Task<AccountDto> ClearAccountRateLimitAsync(string id)
    {
        var account = await SendAsync<GoAccount>(HttpMethod.Post, $"{ApiPrefix}/admin/accounts/{Uri.EscapeDataString(id)}/clear-rate-limit");
        return AccountDto.From(account);
    }

    public Task<AccountUsageStatsDto> GetAccountStatsAsync(string id, int days = 30) =>
        SendAsync<AccountUsageStatsDto>(HttpMethod.Get, $"{ApiPrefix}/admin/accounts/{Uri.EscapeDataString(id)}/stats?days={Math.Clamp(days, 1, 90)}");

    public Task<AccountUsageInfoDto> GetAccountUsageAsync(string id, bool force = false, string source = "passive") =>
        SendAsync<AccountUsageInfoDto>(HttpMethod.Get, $"{ApiPrefix}/admin/accounts/{Uri.EscapeDataString(id)}/usage?source={Uri.EscapeDataString(source)}&force={force.ToString().ToLowerInvariant()}");

    public Task<AccountUsageBatchResponseDto> GetAccountUsageBatchAsync(IEnumerable<string> ids, bool force = false) =>
        SendAsync<AccountUsageBatchResponseDto>(HttpMethod.Post, $"{ApiPrefix}/admin/accounts/usage/batch", new
        {
            account_ids = ids.Select(long.Parse).ToArray(),
            force
        });

    public Task<OpenAIQuotaUsageDto> RefreshOpenAIQuotaAsync(string id) =>
        SendAsync<OpenAIQuotaUsageDto>(HttpMethod.Post, $"{ApiPrefix}/admin/openai/accounts/{Uri.EscapeDataString(id)}/quota/refresh");

    public Task<OpenAIQuotaResetResultDto> ResetOpenAIQuotaAsync(string id) =>
        SendAsync<OpenAIQuotaResetResultDto>(HttpMethod.Post, $"{ApiPrefix}/admin/openai/accounts/{Uri.EscapeDataString(id)}/reset-quota");

    public Task<AccountTodayStatsBatchDto> GetAccountTodayStatsBatchAsync(IEnumerable<string> ids) =>
        SendAsync<AccountTodayStatsBatchDto>(HttpMethod.Post, $"{ApiPrefix}/admin/accounts/today-stats/batch", new
        {
            account_ids = ids.Select(long.Parse).ToArray()
        });

    public Task<AccountBatchResultDto> BatchDeleteAccountsAsync(IEnumerable<string> ids) =>
        SendAsync<AccountBatchResultDto>(HttpMethod.Post, $"{ApiPrefix}/admin/accounts/batch-delete", new
        {
            account_ids = ids.Select(long.Parse).ToArray()
        });

    public Task<AccountBatchResultDto> BatchClearAccountErrorsAsync(IEnumerable<string> ids) =>
        SendAsync<AccountBatchResultDto>(HttpMethod.Post, $"{ApiPrefix}/admin/accounts/batch-clear-error", new
        {
            account_ids = ids.Select(long.Parse).ToArray()
        });

    public Task<AccountBatchResultDto> BatchRefreshAccountsAsync(IEnumerable<string> ids) =>
        SendAsync<AccountBatchResultDto>(HttpMethod.Post, $"{ApiPrefix}/admin/accounts/batch-refresh", new
        {
            account_ids = ids.Select(long.Parse).ToArray()
        });

    public Task<JsonElement> BulkSetAccountsSchedulableAsync(IEnumerable<string> ids, bool schedulable) =>
        SendAsync<JsonElement>(HttpMethod.Post, $"{ApiPrefix}/admin/accounts/bulk-update", new
        {
            account_ids = ids.Select(long.Parse).ToArray(), schedulable
        });

    public Task<AccountBatchResultDto> BulkUpdateAccountsAsync(
        IEnumerable<string> ids,
        IReadOnlyDictionary<string, object?> updates)
    {
        var payload = new Dictionary<string, object?>(updates, StringComparer.OrdinalIgnoreCase)
        {
            ["account_ids"] = ids.Select(long.Parse).Distinct().ToArray()
        };
        return SendAsync<AccountBatchResultDto>(HttpMethod.Post, $"{ApiPrefix}/admin/accounts/bulk-update", payload);
    }

    public Task<CNProviderQuotaProbeResultDto> GetCNProviderQuotaAsync(string id) =>
        SendAsync<CNProviderQuotaProbeResultDto>(HttpMethod.Get,
            $"{ApiPrefix}/admin/cn-providers/accounts/{Uri.EscapeDataString(id)}/quota");

    public Task<CNProviderBalanceResultDto> GetCNProviderBalanceAsync(string id) =>
        SendAsync<CNProviderBalanceResultDto>(HttpMethod.Get,
            $"{ApiPrefix}/admin/cn-providers/accounts/{Uri.EscapeDataString(id)}/balance");

    public Task<OllamaCloudUsageStateDto> RefreshOllamaCloudUsageAsync(string id) =>
        SendAsync<OllamaCloudUsageStateDto>(HttpMethod.Post,
            $"{ApiPrefix}/admin/accounts/{Uri.EscapeDataString(id)}/ollama-cloud-usage/refresh");

    public Task<JsonElement> GetAccountsDataAsync(IEnumerable<string>? ids, bool includeProxies, AccountListQuery? filters = null)
    {
        var query = new List<string> { $"include_proxies={includeProxies.ToString().ToLowerInvariant()}" };
        var selected = ids?.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() ?? [];
        if (selected.Length > 0)
        {
            query.Add($"ids={Uri.EscapeDataString(string.Join(',', selected))}");
        }
        else if (filters is not null)
        {
            static void Add(List<string> target, string key, string? value)
            {
                if (!string.IsNullOrWhiteSpace(value)) target.Add($"{key}={Uri.EscapeDataString(value.Trim())}");
            }
            Add(query, "platform", filters.Platform); Add(query, "type", filters.Type); Add(query, "status", filters.Status);
            Add(query, "group", filters.Group); Add(query, "privacy_mode", filters.PrivacyMode); Add(query, "search", filters.Search);
            Add(query, "sort_by", filters.SortBy); Add(query, "sort_order", filters.SortOrder);
        }
        return SendAsync<JsonElement>(HttpMethod.Get, $"{ApiPrefix}/admin/accounts/data?{string.Join('&', query)}");
    }

    public Task<JsonElement> ImportAccountsDataAsync(JsonElement payload) =>
        SendAsync<JsonElement>(HttpMethod.Post, $"{ApiPrefix}/admin/accounts/data", new
        {
            data = payload, skip_default_group_bind = true
        });

    public Task<JsonElement> PreviewAccountsFromCrsAsync(CrsSyncInput input) =>
        SendAsync<JsonElement>(HttpMethod.Post, $"{ApiPrefix}/admin/accounts/sync/crs/preview", new
        {
            base_url = input.BaseUrl.Trim(), username = input.Username.Trim(), password = input.Password,
            sync_proxies = input.SyncProxies
        });

    public Task<JsonElement> SyncAccountsFromCrsAsync(CrsSyncInput input) =>
        SendAsync<JsonElement>(HttpMethod.Post, $"{ApiPrefix}/admin/accounts/sync/crs", new
        {
            base_url = input.BaseUrl.Trim(), username = input.Username.Trim(), password = input.Password,
            sync_proxies = input.SyncProxies
        });

    public async Task<List<AvailableModelDto>> GetAccountModelsAsync(string accountId)
    {
        var models = await SendAsync<List<GoAvailableModel>>(HttpMethod.Get, $"{ApiPrefix}/admin/accounts/{Uri.EscapeDataString(accountId)}/models");
        return models.Select(AvailableModelDto.From).ToList();
    }

    public async Task<List<AvailableModelDto>> SyncAccountModelsAsync(string accountId)
    {
        var response = await SendAsync<Dictionary<string, List<string>>>(HttpMethod.Post, $"{ApiPrefix}/admin/accounts/{Uri.EscapeDataString(accountId)}/models/sync-upstream");
        return response.TryGetValue("models", out var models)
            ? models.Select(model => new AvailableModelDto { Id = model, DisplayName = model }).ToList()
            : [];
    }

    public async Task<List<GroupDto>> GetGroupsAsync()
    {
        var groups = await SendAsync<List<GoGroup>>(HttpMethod.Get, $"{ApiPrefix}/admin/groups/all?include_inactive=true");
        return groups.Select(GroupDto.From).ToList();
    }

    public async Task<List<GroupDto>> GetActiveGroupsAsync()
    {
        var groups = await SendAsync<List<GoGroup>>(HttpMethod.Get, $"{ApiPrefix}/admin/groups/all");
        return groups.Select(GroupDto.From).ToList();
    }

    public async Task<PagedEnvelope<GroupDto>> GetAdminGroupsAsync(
        int page = 1,
        int pageSize = 20,
        string? search = null,
        string? platform = null,
        string? status = null,
        bool? isExclusive = null,
        string sortBy = "sort_order",
        string sortOrder = "asc")
    {
        var query = new List<string>
        {
            $"page={Math.Max(1, page)}",
            $"page_size={Math.Clamp(pageSize, 1, 100)}",
            $"sort_by={Uri.EscapeDataString(sortBy)}",
            $"sort_order={Uri.EscapeDataString(sortOrder)}"
        };
        if (!string.IsNullOrWhiteSpace(search)) query.Add($"search={Uri.EscapeDataString(search.Trim())}");
        if (!string.IsNullOrWhiteSpace(platform)) query.Add($"platform={Uri.EscapeDataString(platform)}");
        if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
        if (isExclusive.HasValue) query.Add($"is_exclusive={isExclusive.Value.ToString().ToLowerInvariant()}");

        var response = await SendAsync<PagedEnvelope<GoGroup>>(
            HttpMethod.Get,
            $"{ApiPrefix}/admin/groups?{string.Join("&", query)}");
        return new PagedEnvelope<GroupDto>
        {
            Items = response.Items.Select(GroupDto.From).ToList(),
            Total = response.Total,
            Page = response.Page,
            PageSize = response.PageSize,
            Pages = response.Pages
        };
    }

    public async Task<GroupDto> CreateGroupAsync(GroupInput input)
    {
        var payload = ParseObject(input.AdvancedJson, "分组高级配置") ?? new(StringComparer.OrdinalIgnoreCase);
        payload["name"] = input.Name.Trim(); payload["description"] = input.Description.Trim();
        payload["platform"] = input.Platform; payload["rate_multiplier"] = input.RateMultiplier;
        payload["is_exclusive"] = input.IsExclusive; payload["subscription_type"] = input.SubscriptionType;
        payload["daily_limit_usd"] = input.DailyLimitUsd;
        payload["weekly_limit_usd"] = input.WeeklyLimitUsd;
        payload["monthly_limit_usd"] = input.MonthlyLimitUsd;
        payload["rpm_limit"] = input.RpmLimit;
        payload["long_context_pricing_enabled"] = input.LongContextPricingEnabled;
        var group = await SendAsync<GoGroup>(HttpMethod.Post, $"{ApiPrefix}/admin/groups", payload);
        return GroupDto.From(group);
    }

    public async Task<GroupDto> UpdateGroupAsync(string id, GroupInput input, bool active)
    {
        var payload = ParseObject(input.AdvancedJson, "分组高级配置") ?? new(StringComparer.OrdinalIgnoreCase);
        payload["name"] = input.Name.Trim(); payload["description"] = input.Description.Trim();
        payload["platform"] = input.Platform; payload["rate_multiplier"] = input.RateMultiplier;
        payload["is_exclusive"] = input.IsExclusive; payload["subscription_type"] = input.SubscriptionType;
        payload["daily_limit_usd"] = input.DailyLimitUsd;
        payload["weekly_limit_usd"] = input.WeeklyLimitUsd;
        payload["monthly_limit_usd"] = input.MonthlyLimitUsd;
        payload["rpm_limit"] = input.RpmLimit;
        payload["long_context_pricing_enabled"] = input.LongContextPricingEnabled;
        payload["status"] = active ? "active" : "inactive";
        var group = await SendAsync<GoGroup>(HttpMethod.Put, $"{ApiPrefix}/admin/groups/{Uri.EscapeDataString(id)}", payload);
        return GroupDto.From(group);
    }

    public async Task<GroupDto> GetGroupAsync(string id)
    {
        var group = await SendAsync<GoGroup>(HttpMethod.Get, $"{ApiPrefix}/admin/groups/{Uri.EscapeDataString(id)}");
        return GroupDto.From(group);
    }

    public Task DeleteGroupAsync(string id) => SendAsync(HttpMethod.Delete, $"{ApiPrefix}/admin/groups/{Uri.EscapeDataString(id)}");

    public async Task<GroupDto> DuplicateGroupAsync(string id)
    {
        var group = await SendAsync<GoGroup>(
            HttpMethod.Post,
            $"{ApiPrefix}/admin/groups/{Uri.EscapeDataString(id)}/duplicate",
            null,
            headers: new Dictionary<string, string>
            {
                ["Idempotency-Key"] = $"group-duplicate-{Guid.NewGuid():N}"
            });
        return GroupDto.From(group);
    }

    public Task<List<GroupUsageSummaryDto>> GetAdminGroupUsageSummaryAsync() =>
        SendAsync<List<GroupUsageSummaryDto>>(HttpMethod.Get, $"{ApiPrefix}/admin/groups/usage-summary");

    public Task<List<GroupCapacitySummaryDto>> GetAdminGroupCapacitySummaryAsync() =>
        SendAsync<List<GroupCapacitySummaryDto>>(HttpMethod.Get, $"{ApiPrefix}/admin/groups/capacity-summary");

    public Task UpdateAdminGroupSortOrderAsync(IEnumerable<GroupSortOrderUpdateDto> updates) =>
        SendAsync(HttpMethod.Put, $"{ApiPrefix}/admin/groups/sort-order", new { updates = updates.ToArray() });

    public Task<List<GroupUserOverrideDto>> GetAdminGroupUserOverridesAsync(string id) =>
        SendAsync<List<GroupUserOverrideDto>>(
            HttpMethod.Get,
            $"{ApiPrefix}/admin/groups/{Uri.EscapeDataString(id)}/rate-multipliers");

    public Task SaveAdminGroupRateMultipliersAsync(string id, IEnumerable<GroupRateMultiplierInputDto> entries) =>
        SendAsync(
            HttpMethod.Put,
            $"{ApiPrefix}/admin/groups/{Uri.EscapeDataString(id)}/rate-multipliers",
            new { entries = entries.ToArray() });

    public Task ClearAdminGroupRateMultipliersAsync(string id) =>
        SendAsync(HttpMethod.Delete, $"{ApiPrefix}/admin/groups/{Uri.EscapeDataString(id)}/rate-multipliers");

    public Task SaveAdminGroupRpmOverridesAsync(string id, IEnumerable<GroupRpmOverrideInputDto> entries) =>
        SendAsync(
            HttpMethod.Put,
            $"{ApiPrefix}/admin/groups/{Uri.EscapeDataString(id)}/rpm-overrides",
            new { entries = entries.ToArray() });

    public Task ClearAdminGroupRpmOverridesAsync(string id) =>
        SendAsync(HttpMethod.Delete, $"{ApiPrefix}/admin/groups/{Uri.EscapeDataString(id)}/rpm-overrides");

    public Task<List<CompositeModelRouteDto>> GetCompositeRoutesAsync(string groupId) =>
        SendAsync<List<CompositeModelRouteDto>>(
            HttpMethod.Get,
            $"{ApiPrefix}/admin/groups/{Uri.EscapeDataString(groupId)}/composite-routes");

    public Task<CompositeModelRouteDto> CreateCompositeRouteAsync(string groupId, CompositeModelRouteInput input) =>
        SendAsync<CompositeModelRouteDto>(
            HttpMethod.Post,
            $"{ApiPrefix}/admin/groups/{Uri.EscapeDataString(groupId)}/composite-routes",
            input);

    public Task<CompositeModelRouteDto> UpdateCompositeRouteAsync(string groupId, long routeId, CompositeModelRouteInput input) =>
        SendAsync<CompositeModelRouteDto>(
            HttpMethod.Put,
            $"{ApiPrefix}/admin/groups/{Uri.EscapeDataString(groupId)}/composite-routes/{routeId}",
            input);

    public Task DeleteCompositeRouteAsync(string groupId, long routeId) =>
        SendAsync(
            HttpMethod.Delete,
            $"{ApiPrefix}/admin/groups/{Uri.EscapeDataString(groupId)}/composite-routes/{routeId}");

    public Task<CompositeRouteDecisionDto> PreviewCompositeRouteAsync(string groupId, string model, string endpoint) =>
        SendAsync<CompositeRouteDecisionDto>(
            HttpMethod.Post,
            $"{ApiPrefix}/admin/groups/{Uri.EscapeDataString(groupId)}/composite-routes/preview",
            new { model = model.Trim(), endpoint });

    public async Task<List<ChannelDto>> GetChannelsAsync()
    {
        var page = await SendAsync<PagedEnvelope<GoChannel>>(HttpMethod.Get, $"{ApiPrefix}/admin/channels?page=1&page_size=1000");
        return page.Items.Select(ChannelDto.From).ToList();
    }

    public async Task<ChannelDto> CreateChannelAsync(ChannelInput input)
    {
        var payload = ParseObject(input.AdvancedJson, "渠道高级配置") ?? new(StringComparer.OrdinalIgnoreCase);
        ApplyChannelInput(payload, input, includeStatus: false);
        var channel = await SendAsync<GoChannel>(HttpMethod.Post, $"{ApiPrefix}/admin/channels", payload);
        return ChannelDto.From(channel);
    }

    public async Task<ChannelDto> UpdateChannelAsync(string id, ChannelInput input, bool active)
    {
        var payload = ParseObject(input.AdvancedJson, "渠道高级配置") ?? new(StringComparer.OrdinalIgnoreCase);
        ApplyChannelInput(payload, input, includeStatus: true, active);
        var channel = await SendAsync<GoChannel>(HttpMethod.Put, $"{ApiPrefix}/admin/channels/{Uri.EscapeDataString(id)}", payload);
        return ChannelDto.From(channel);
    }

    public async Task<ChannelDto> GetChannelAsync(string id)
    {
        var channel = await SendAsync<GoChannel>(HttpMethod.Get, $"{ApiPrefix}/admin/channels/{Uri.EscapeDataString(id)}");
        return ChannelDto.From(channel);
    }

    public Task DeleteChannelAsync(string id) => SendAsync(HttpMethod.Delete, $"{ApiPrefix}/admin/channels/{Uri.EscapeDataString(id)}");

    public async Task<List<ProxyDto>> GetProxiesAsync()
    {
        var page = await SendAsync<PagedEnvelope<ProxyDto>>(HttpMethod.Get, $"{ApiPrefix}/admin/proxies?page=1&page_size=1000");
        return page.Items;
    }

    public async Task<ProxyDto> CreateProxyAsync(ProxyInput input)
    {
        var proxy = await SendAsync<ProxyDto>(HttpMethod.Post, $"{ApiPrefix}/admin/proxies", input);
        return proxy;
    }

    public Task<ProxyDto> UpdateProxyAsync(string id, ProxyInput input) =>
        SendAsync<ProxyDto>(HttpMethod.Put, $"{ApiPrefix}/admin/proxies/{Uri.EscapeDataString(id)}", input);

    public Task DeleteProxyAsync(string id) => SendAsync(HttpMethod.Delete, $"{ApiPrefix}/admin/proxies/{Uri.EscapeDataString(id)}");

    public Task<AccountTestResultDto> TestProxyAsync(string id) =>
        SendAsync<AccountTestResultDto>(HttpMethod.Post, $"{ApiPrefix}/admin/proxies/{Uri.EscapeDataString(id)}/test");

    public async Task<List<AnnouncementDto>> GetAnnouncementsAsync()
    {
        var page = await SendAsync<PagedEnvelope<AnnouncementDto>>(HttpMethod.Get, $"{ApiPrefix}/admin/announcements?page=1&page_size=1000");
        return page.Items;
    }

    public async Task<AnnouncementDto> CreateAnnouncementAsync(AnnouncementInput input)
    {
        var payload = new Dictionary<string, object?>
        {
            ["title"] = input.Title.Trim(), ["content"] = input.Content,
            ["status"] = input.Status, ["notify_mode"] = input.NotifyMode,
            ["targeting"] = ParseObject(input.TargetingJson, "公告目标规则") ?? new()
        };
        if (input.StartsAt.HasValue) payload["starts_at"] = input.StartsAt.Value.ToUnixTimeMilliseconds();
        if (input.EndsAt.HasValue) payload["ends_at"] = input.EndsAt.Value.ToUnixTimeMilliseconds();
        return await SendAsync<AnnouncementDto>(HttpMethod.Post, $"{ApiPrefix}/admin/announcements", payload);
    }

    public async Task<AnnouncementDto> UpdateAnnouncementAsync(string id, AnnouncementInput input)
    {
        var payload = new Dictionary<string, object?>
        {
            ["title"] = input.Title.Trim(), ["content"] = input.Content,
            ["status"] = input.Status, ["notify_mode"] = input.NotifyMode,
            ["targeting"] = ParseObject(input.TargetingJson, "公告目标规则") ?? new()
        };
        if (input.StartsAt.HasValue) payload["starts_at"] = input.StartsAt.Value.ToUnixTimeMilliseconds();
        if (input.EndsAt.HasValue) payload["ends_at"] = input.EndsAt.Value.ToUnixTimeMilliseconds();
        return await SendAsync<AnnouncementDto>(HttpMethod.Put, $"{ApiPrefix}/admin/announcements/{Uri.EscapeDataString(id)}", payload);
    }

    public Task DeleteAnnouncementAsync(string id) => SendAsync(HttpMethod.Delete, $"{ApiPrefix}/admin/announcements/{Uri.EscapeDataString(id)}");

    public Task<PagedEnvelope<AuditLogDto>> GetAuditLogsAsync(
        int page = 1,
        int pageSize = 20,
        string? search = null,
        string? actorEmail = null,
        string? action = null,
        string? clientIp = null,
        string? method = null,
        string? authMethod = null,
        string? success = null,
        string? startTime = null,
        string? endTime = null,
        long? actorUserId = null)
    {
        var query = new List<string>
        {
            $"page={Math.Max(1, page)}",
            $"page_size={Math.Clamp(pageSize, 1, 200)}"
        };
        AddAuditLogQuery(query, "q", search);
        AddAuditLogQuery(query, "actor_email", actorEmail);
        AddAuditLogQuery(query, "action", action);
        AddAuditLogQuery(query, "client_ip", clientIp);
        AddAuditLogQuery(query, "method", method);
        AddAuditLogQuery(query, "auth_method", authMethod);
        AddAuditLogQuery(query, "success", success);
        AddAuditLogQuery(query, "start_time", startTime);
        AddAuditLogQuery(query, "end_time", endTime);
        if (actorUserId.HasValue && actorUserId.Value > 0) query.Add($"actor_user_id={actorUserId.Value}");
        return SendAsync<PagedEnvelope<AuditLogDto>>(HttpMethod.Get, $"{ApiPrefix}/admin/audit-logs?{string.Join("&", query)}");
    }

    public Task<AuditLogDto> GetAuditLogAsync(long id) =>
        SendAsync<AuditLogDto>(HttpMethod.Get, $"{ApiPrefix}/admin/audit-logs/{id}");

    public Task<AuditLogClearResultDto> ClearAuditLogsAsync(string totpCode) =>
        SendAsync<AuditLogClearResultDto>(HttpMethod.Post, $"{ApiPrefix}/admin/audit-logs/clear", new
        {
            totp_code = totpCode.Trim()
        });

    private static void AddAuditLogQuery(List<string> query, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) query.Add($"{key}={Uri.EscapeDataString(value.Trim())}");
    }

    public Task<JsonElement> GetAdminSettingsAsync() =>
        SendAsync<JsonElement>(HttpMethod.Get, $"{ApiPrefix}/admin/settings");

    public Task<AdminSettingsDto> GetAdminSettingsTypedAsync() =>
        SendAsync<AdminSettingsDto>(HttpMethod.Get, $"{ApiPrefix}/admin/settings");

    public Task<JsonElement> UpdateAdminSettingsAsync(JsonElement settings) =>
        SendAsync<JsonElement>(HttpMethod.Put, $"{ApiPrefix}/admin/settings", settings);

    public Task<AdminSettingsDto> UpdateAdminSettingsTypedAsync(object settings) =>
        SendAsync<AdminSettingsDto>(HttpMethod.Put, $"{ApiPrefix}/admin/settings", settings);

    public Task<AdminApiKeyStatusDto> GetAdminApiKeyStatusAsync() =>
        SendAsync<AdminApiKeyStatusDto>(HttpMethod.Get, $"{ApiPrefix}/admin/settings/admin-api-key");

    public Task<AdminApiKeyGeneratedDto> RegenerateAdminApiKeyAsync() =>
        SendAsync<AdminApiKeyGeneratedDto>(HttpMethod.Post, $"{ApiPrefix}/admin/settings/admin-api-key/regenerate");

    public Task DeleteAdminApiKeyAsync() =>
        SendAsync(HttpMethod.Delete, $"{ApiPrefix}/admin/settings/admin-api-key");

    public Task<OverloadCooldownSettingsDto> GetOverloadCooldownSettingsAsync() =>
        SendAsync<OverloadCooldownSettingsDto>(HttpMethod.Get, $"{ApiPrefix}/admin/settings/overload-cooldown");

    public Task<OverloadCooldownSettingsDto> UpdateOverloadCooldownSettingsAsync(OverloadCooldownSettingsDto value) =>
        SendAsync<OverloadCooldownSettingsDto>(HttpMethod.Put, $"{ApiPrefix}/admin/settings/overload-cooldown", new
        {
            enabled = value.Enabled,
            cooldown_minutes = value.CooldownMinutes
        });

    public Task<RateLimit429CooldownSettingsDto> GetRateLimit429CooldownSettingsAsync() =>
        SendAsync<RateLimit429CooldownSettingsDto>(HttpMethod.Get, $"{ApiPrefix}/admin/settings/rate-limit-429-cooldown");

    public Task<RateLimit429CooldownSettingsDto> UpdateRateLimit429CooldownSettingsAsync(RateLimit429CooldownSettingsDto value) =>
        SendAsync<RateLimit429CooldownSettingsDto>(HttpMethod.Put, $"{ApiPrefix}/admin/settings/rate-limit-429-cooldown", new
        {
            enabled = value.Enabled,
            cooldown_seconds = value.CooldownSeconds
        });

    public Task<PanelRateLimitSettingsDto> GetPanelRateLimitSettingsAsync() =>
        SendAsync<PanelRateLimitSettingsDto>(HttpMethod.Get, $"{ApiPrefix}/admin/settings/panel-rate-limit");

    public Task<PanelRateLimitSettingsDto> UpdatePanelRateLimitSettingsAsync(PanelRateLimitSettingsDto value) =>
        SendAsync<PanelRateLimitSettingsDto>(HttpMethod.Put, $"{ApiPrefix}/admin/settings/panel-rate-limit", new
        {
            enabled = value.Enabled,
            user_rpm = value.UserRpm,
            heavy_rpm = value.HeavyRpm,
            public_ip_rpm = value.PublicIpRpm,
            exempt_admin = value.ExemptAdmin
        });

    public Task<StreamTimeoutSettingsDto> GetStreamTimeoutSettingsAsync() =>
        SendAsync<StreamTimeoutSettingsDto>(HttpMethod.Get, $"{ApiPrefix}/admin/settings/stream-timeout");

    public Task<StreamTimeoutSettingsDto> UpdateStreamTimeoutSettingsAsync(StreamTimeoutSettingsDto value) =>
        SendAsync<StreamTimeoutSettingsDto>(HttpMethod.Put, $"{ApiPrefix}/admin/settings/stream-timeout", new
        {
            enabled = value.Enabled,
            action = value.Action,
            temp_unsched_minutes = value.TempUnschedMinutes,
            threshold_count = value.ThresholdCount,
            threshold_window_minutes = value.ThresholdWindowMinutes
        });

    public Task<RectifierSettingsDto> GetRectifierSettingsAsync() =>
        SendAsync<RectifierSettingsDto>(HttpMethod.Get, $"{ApiPrefix}/admin/settings/rectifier");

    public Task<RectifierSettingsDto> UpdateRectifierSettingsAsync(RectifierSettingsDto value) =>
        SendAsync<RectifierSettingsDto>(HttpMethod.Put, $"{ApiPrefix}/admin/settings/rectifier", new
        {
            enabled = value.Enabled,
            thinking_signature_enabled = value.ThinkingSignatureEnabled,
            thinking_budget_enabled = value.ThinkingBudgetEnabled,
            apikey_signature_enabled = value.ApiKeySignatureEnabled,
            apikey_signature_patterns = value.ApiKeySignaturePatterns
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray()
        });

    public Task<BetaPolicySettingsDto> GetBetaPolicySettingsAsync() =>
        SendAsync<BetaPolicySettingsDto>(HttpMethod.Get, $"{ApiPrefix}/admin/settings/beta-policy");

    public Task<BetaPolicySettingsDto> UpdateBetaPolicySettingsAsync(BetaPolicySettingsDto value) =>
        SendAsync<BetaPolicySettingsDto>(HttpMethod.Put, $"{ApiPrefix}/admin/settings/beta-policy", value);

    public Task<WebSearchEmulationConfigDto> GetWebSearchEmulationConfigAsync() =>
        SendAsync<WebSearchEmulationConfigDto>(HttpMethod.Get, $"{ApiPrefix}/admin/settings/web-search-emulation");

    public Task<WebSearchEmulationConfigDto> UpdateWebSearchEmulationConfigAsync(WebSearchEmulationConfigDto value) =>
        SendAsync<WebSearchEmulationConfigDto>(HttpMethod.Put, $"{ApiPrefix}/admin/settings/web-search-emulation", value);

    public Task<WebSearchTestResultDto> TestWebSearchEmulationAsync(string query) =>
        SendAsync<WebSearchTestResultDto>(HttpMethod.Post, $"{ApiPrefix}/admin/settings/web-search-emulation/test", new { query = query.Trim() });

    public Task ResetWebSearchUsageAsync(string providerType) =>
        SendAsync(HttpMethod.Post, $"{ApiPrefix}/admin/settings/web-search-emulation/reset-usage", new { provider_type = providerType.Trim() });

    public Task<ApiMessageDto> TestSmtpConnectionAsync(SmtpTestRequestDto value) =>
        SendAsync<ApiMessageDto>(HttpMethod.Post, $"{ApiPrefix}/admin/settings/test-smtp", value);

    public Task<ApiMessageDto> SendTestEmailAsync(SendTestEmailRequestDto value) =>
        SendAsync<ApiMessageDto>(HttpMethod.Post, $"{ApiPrefix}/admin/settings/send-test-email", value);

    public Task<EmailTemplateListDto> GetEmailTemplatesAsync() =>
        SendAsync<EmailTemplateListDto>(HttpMethod.Get, $"{ApiPrefix}/admin/settings/email-templates");

    public Task<EmailTemplateDetailDto> GetEmailTemplateAsync(string eventName, string locale) =>
        SendAsync<EmailTemplateDetailDto>(HttpMethod.Get, $"{ApiPrefix}/admin/settings/email-templates/{Uri.EscapeDataString(eventName)}/{Uri.EscapeDataString(locale)}");

    public Task<EmailTemplateDetailDto> UpdateEmailTemplateAsync(string eventName, string locale, string subject, string html) =>
        SendAsync<EmailTemplateDetailDto>(HttpMethod.Put, $"{ApiPrefix}/admin/settings/email-templates/{Uri.EscapeDataString(eventName)}/{Uri.EscapeDataString(locale)}", new { subject, html });

    public Task<EmailTemplateDetailDto> RestoreOfficialEmailTemplateAsync(string eventName, string locale) =>
        SendAsync<EmailTemplateDetailDto>(HttpMethod.Post, $"{ApiPrefix}/admin/settings/email-templates/{Uri.EscapeDataString(eventName)}/{Uri.EscapeDataString(locale)}/restore-official");

    public Task<EmailTemplatePreviewDto> PreviewEmailTemplateAsync(string eventName, string locale, string subject, string html) =>
        SendAsync<EmailTemplatePreviewDto>(HttpMethod.Post, $"{ApiPrefix}/admin/settings/email-template-preview", new { @event = eventName, locale, subject, html });

    public Task<UpstreamBillingProbeSettingsDto> GetUpstreamBillingProbeSettingsAsync() =>
        SendAsync<UpstreamBillingProbeSettingsDto>(HttpMethod.Get, $"{ApiPrefix}/admin/accounts/upstream-billing-probe/settings");

    public Task<UpstreamBillingProbeSettingsDto> UpdateUpstreamBillingProbeSettingsAsync(UpstreamBillingProbeSettingsDto value) =>
        SendAsync<UpstreamBillingProbeSettingsDto>(HttpMethod.Put, $"{ApiPrefix}/admin/accounts/upstream-billing-probe/settings", value);

    public Task<OllamaCloudUsageSettingsDto> GetOllamaCloudUsageSettingsAsync() =>
        SendAsync<OllamaCloudUsageSettingsDto>(HttpMethod.Get, $"{ApiPrefix}/admin/accounts/ollama-cloud-usage/settings");

    public Task<OllamaCloudUsageSettingsDto> UpdateOllamaCloudUsageSettingsAsync(OllamaCloudUsageSettingsDto value) =>
        SendAsync<OllamaCloudUsageSettingsDto>(HttpMethod.Put, $"{ApiPrefix}/admin/accounts/ollama-cloud-usage/settings", value);

    public Task<JsonElement> GetOpsOverviewAsync(string timeRange = "1h") =>
        SendAsync<JsonElement>(HttpMethod.Get, $"{ApiPrefix}/admin/ops/dashboard/overview?time_range={Uri.EscapeDataString(timeRange)}");

    public Task<JsonElement> GetRiskConfigAsync() =>
        SendAsync<JsonElement>(HttpMethod.Get, $"{ApiPrefix}/admin/risk-control/config");

    public Task<JsonElement> UpdateRiskConfigAsync(JsonElement config) =>
        SendAsync<JsonElement>(HttpMethod.Put, $"{ApiPrefix}/admin/risk-control/config", config);

    public Task<SystemVersionDto> GetSystemVersionAsync() =>
        SendAsync<SystemVersionDto>(HttpMethod.Get, $"{ApiPrefix}/admin/system/version");

    public async Task<List<JsonElement>> GetAdminSubscriptionsRawAsync()
    {
        var page = await SendAsync<PagedEnvelope<JsonElement>>(HttpMethod.Get, $"{ApiPrefix}/admin/subscriptions?page=1&page_size=1000");
        return page.Items;
    }

    public Task<PagedEnvelope<SubscriptionDto>> GetAdminSubscriptionsAsync(
        int page = 1,
        int pageSize = 20,
        long? userId = null,
        long? groupId = null,
        string? status = null,
        string? platform = null,
        string sortBy = "created_at",
        string sortOrder = "desc")
    {
        var query = new List<string>
        {
            $"page={Math.Max(1, page)}",
            $"page_size={Math.Clamp(pageSize, 1, 100)}",
            $"sort_by={Uri.EscapeDataString(sortBy)}",
            $"sort_order={Uri.EscapeDataString(sortOrder)}"
        };
        if (userId is > 0) query.Add($"user_id={userId.Value}");
        if (groupId is > 0) query.Add($"group_id={groupId.Value}");
        if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
        if (!string.IsNullOrWhiteSpace(platform)) query.Add($"platform={Uri.EscapeDataString(platform)}");
        return SendAsync<PagedEnvelope<SubscriptionDto>>(
            HttpMethod.Get,
            $"{ApiPrefix}/admin/subscriptions?{string.Join("&", query)}");
    }

    public Task<SubscriptionDto> GetAdminSubscriptionAsync(long id) =>
        SendAsync<SubscriptionDto>(HttpMethod.Get, $"{ApiPrefix}/admin/subscriptions/{id}");

    public Task<SubscriptionProgressDto> GetAdminSubscriptionProgressAsync(long id) =>
        SendAsync<SubscriptionProgressDto>(HttpMethod.Get, $"{ApiPrefix}/admin/subscriptions/{id}/progress");

    public Task<SubscriptionDto> AssignAdminSubscriptionAsync(SubscriptionAssignInput input) =>
        SendAsync<SubscriptionDto>(HttpMethod.Post, $"{ApiPrefix}/admin/subscriptions/assign", new
        {
            user_id = input.UserId,
            group_id = input.GroupId,
            validity_days = input.ValidityDays,
            notes = input.Notes.Trim()
        });

    public Task<BulkAssignSubscriptionResultDto> BulkAssignAdminSubscriptionsAsync(
        IEnumerable<long> userIds,
        long groupId,
        int validityDays = 30,
        string? notes = null) =>
        SendAsync<BulkAssignSubscriptionResultDto>(HttpMethod.Post, $"{ApiPrefix}/admin/subscriptions/bulk-assign", new
        {
            user_ids = userIds.Distinct().ToArray(),
            group_id = groupId,
            validity_days = validityDays,
            notes = notes?.Trim() ?? string.Empty
        });

    public Task<SubscriptionDto> AdjustAdminSubscriptionAsync(long id, int days) =>
        SendAsync<SubscriptionDto>(
            HttpMethod.Post,
            $"{ApiPrefix}/admin/subscriptions/{id}/extend",
            new { days },
            headers: new Dictionary<string, string>
            {
                ["Idempotency-Key"] = $"subscription-adjust-{id}-{Guid.NewGuid():N}"
            });

    public Task<SubscriptionDto> ResetAdminSubscriptionQuotaAsync(
        long id,
        bool daily = true,
        bool weekly = true,
        bool monthly = true) =>
        SendAsync<SubscriptionDto>(HttpMethod.Post, $"{ApiPrefix}/admin/subscriptions/{id}/reset-quota", new
        {
            daily,
            weekly,
            monthly
        });

    public Task RevokeAdminSubscriptionAsync(long id) =>
        SendAsync(HttpMethod.Post, $"{ApiPrefix}/admin/subscriptions/{id}/revoke");

    public Task<SubscriptionDto> RestoreAdminSubscriptionAsync(long id) =>
        SendAsync<SubscriptionDto>(HttpMethod.Post, $"{ApiPrefix}/admin/subscriptions/{id}/restore");

    public async Task<List<JsonElement>> GetAdminRedeemCodesRawAsync()
    {
        var page = await SendAsync<PagedEnvelope<JsonElement>>(HttpMethod.Get, $"{ApiPrefix}/admin/redeem-codes?page=1&page_size=1000");
        return page.Items;
    }

    public Task<JsonElement> GenerateRedeemCodesAsync(object payload) =>
        SendAsync<JsonElement>(HttpMethod.Post, $"{ApiPrefix}/admin/redeem-codes/generate", payload);

    public Task DeleteRedeemCodeAsync(string id) =>
        SendAsync(HttpMethod.Delete, $"{ApiPrefix}/admin/redeem-codes/{Uri.EscapeDataString(id)}");

    public Task<JsonElement> ExpireRedeemCodeAsync(string id) =>
        SendAsync<JsonElement>(HttpMethod.Post, $"{ApiPrefix}/admin/redeem-codes/{Uri.EscapeDataString(id)}/expire");

    public Task<List<UserAnnouncementDto>> GetUserAnnouncementsAsync(bool unreadOnly = false) =>
        SendAsync<List<UserAnnouncementDto>>(HttpMethod.Get, $"{ApiPrefix}/announcements{(unreadOnly ? "?unread_only=1" : string.Empty)}");

    public Task MarkAnnouncementReadAsync(string id) =>
        SendAsync(HttpMethod.Post, $"{ApiPrefix}/announcements/{Uri.EscapeDataString(id)}/read");

    public Task<List<UserAvailableChannelDto>> GetAvailableChannelsAsync() =>
        SendAsync<List<UserAvailableChannelDto>>(HttpMethod.Get, $"{ApiPrefix}/channels/available");

    public Task<Dictionary<long, double>> GetUserGroupRatesAsync() =>
        SendAsync<Dictionary<long, double>>(HttpMethod.Get, $"{ApiPrefix}/groups/rates");

    public Task<RedeemCodeDto> RedeemAsync(RedeemInput input) =>
        SendAsync<RedeemCodeDto>(HttpMethod.Post, $"{ApiPrefix}/redeem", new { code = input.Code.Trim() });

    public Task<List<RedeemCodeDto>> GetRedeemHistoryAsync() =>
        SendAsync<List<RedeemCodeDto>>(HttpMethod.Get, $"{ApiPrefix}/redeem/history");

    public Task<JsonElement> GetMySubscriptionsRawAsync() =>
        SendAsync<JsonElement>(HttpMethod.Get, $"{ApiPrefix}/subscriptions");

    public Task<List<SubscriptionDto>> GetMySubscriptionsAsync() =>
        SendAsync<List<SubscriptionDto>>(HttpMethod.Get, $"{ApiPrefix}/subscriptions");

    public Task<JsonElement> GetActiveSubscriptionsRawAsync() =>
        SendAsync<JsonElement>(HttpMethod.Get, $"{ApiPrefix}/subscriptions/active");

    public Task<JsonElement> GetSubscriptionProgressRawAsync() =>
        SendAsync<JsonElement>(HttpMethod.Get, $"{ApiPrefix}/subscriptions/progress");

    public Task<JsonElement> GetSubscriptionSummaryRawAsync() =>
        SendAsync<JsonElement>(HttpMethod.Get, $"{ApiPrefix}/subscriptions/summary");

    public Task<UserMonitorListResponseDto> GetChannelMonitorsAsync() =>
        SendAsync<UserMonitorListResponseDto>(HttpMethod.Get, $"{ApiPrefix}/channel-monitors");

    public Task<UserMonitorDetailDto> GetChannelMonitorStatusAsync(long id) =>
        SendAsync<UserMonitorDetailDto>(HttpMethod.Get, $"{ApiPrefix}/channel-monitors/{id}/status");

    public Task<ChannelMonitorV2DimensionsDto> GetUserChannelMonitorV2DimensionsAsync(ChannelMonitorV2FilterDto filter) =>
        SendAsync<ChannelMonitorV2DimensionsDto>(HttpMethod.Get, BuildChannelMonitorV2Url("dimensions", filter, admin: false));

    public Task<ChannelMonitorV2SnapshotDto> GetUserChannelMonitorV2SnapshotAsync(ChannelMonitorV2FilterDto filter) =>
        SendAsync<ChannelMonitorV2SnapshotDto>(HttpMethod.Get, BuildChannelMonitorV2Url("snapshot", filter, admin: false));

    public Task<ChannelMonitorV2MatrixDto> GetUserChannelMonitorV2MatrixAsync(ChannelMonitorV2FilterDto filter, string groupBy = "platform_group") =>
        SendAsync<ChannelMonitorV2MatrixDto>(HttpMethod.Get, BuildChannelMonitorV2Url("matrix", filter, [("group_by", groupBy)], admin: false));

    public Task<ChannelMonitorV2ListDto<ChannelMonitorV2ModelRowDto>> GetUserChannelMonitorV2ModelsAsync(ChannelMonitorV2FilterDto filter) =>
        SendAsync<ChannelMonitorV2ListDto<ChannelMonitorV2ModelRowDto>>(HttpMethod.Get, BuildChannelMonitorV2Url("models", filter, admin: false));

    public Task<ChannelMonitorV2ListDto<ChannelMonitorV2ErrorRowDto>> GetUserChannelMonitorV2ErrorsAsync(ChannelMonitorV2FilterDto filter) =>
        SendAsync<ChannelMonitorV2ListDto<ChannelMonitorV2ErrorRowDto>>(HttpMethod.Get, BuildChannelMonitorV2Url("errors", filter, admin: false));

    public Task<ChannelMonitorV2ListDto<ChannelMonitorV2UserRowDto>> GetUserChannelMonitorV2UsersAsync(ChannelMonitorV2FilterDto filter) =>
        SendAsync<ChannelMonitorV2ListDto<ChannelMonitorV2UserRowDto>>(HttpMethod.Get, BuildChannelMonitorV2Url("users", filter, admin: false));

    public Task<UserAffiliateDetailDto> GetAffiliateDetailAsync() =>
        SendAsync<UserAffiliateDetailDto>(HttpMethod.Get, $"{ApiPrefix}/user/aff");

    public Task<AffiliateTransferResponseDto> TransferAffiliateQuotaAsync() =>
        SendAsync<AffiliateTransferResponseDto>(HttpMethod.Post, $"{ApiPrefix}/user/aff/transfer");

    public Task<JsonElement> GetMyPlatformQuotasRawAsync() =>
        SendAsync<JsonElement>(HttpMethod.Get, $"{ApiPrefix}/user/platform-quotas");

    public Task<UserPlatformQuotaResponseDto> GetMyPlatformQuotasAsync() =>
        SendAsync<UserPlatformQuotaResponseDto>(HttpMethod.Get, $"{ApiPrefix}/user/platform-quotas");

    public Task<JsonElement> GetApiKeyDailyUsageRawAsync(string keyId, DateTimeOffset? start = null, DateTimeOffset? end = null) =>
        SendAsync<JsonElement>(HttpMethod.Get, $"{ApiPrefix}/user/api-keys/{Uri.EscapeDataString(keyId)}/usage/daily{BuildDateQuery(start, end)}");

    public Task<JsonElement> GetAdminOpsSnapshotRawAsync(string timeRange = "1h") =>
        SendAsync<JsonElement>(HttpMethod.Get, $"{ApiPrefix}/admin/ops/dashboard/snapshot-v2?time_range={Uri.EscapeDataString(timeRange)}");

    public Task<OpsDashboardSnapshotDto> GetAdminOpsSnapshotAsync(string timeRange = "1h", string? platform = null, long? groupId = null, string? mode = null, DateTimeOffset? start = null, DateTimeOffset? end = null)
    {
        return SendAsync<OpsDashboardSnapshotDto>(HttpMethod.Get, $"{ApiPrefix}/admin/ops/dashboard/snapshot-v2?{BuildOpsQuery(timeRange, platform, groupId, mode, start, end)}");
    }

    public Task<OpsDashboardOverviewDto> GetAdminOpsOverviewAsync(string timeRange = "1h", string? platform = null, long? groupId = null, string? mode = null, DateTimeOffset? start = null, DateTimeOffset? end = null) =>
        SendAsync<OpsDashboardOverviewDto>(HttpMethod.Get, $"{ApiPrefix}/admin/ops/dashboard/overview?{BuildOpsQuery(timeRange, platform, groupId, mode, start, end)}");

    public Task<OpsThroughputTrendDto> GetAdminOpsThroughputTrendAsync(string timeRange = "1h", string? platform = null, long? groupId = null, string? mode = null, DateTimeOffset? start = null, DateTimeOffset? end = null) =>
        SendAsync<OpsThroughputTrendDto>(HttpMethod.Get, $"{ApiPrefix}/admin/ops/dashboard/throughput-trend?{BuildOpsQuery(timeRange, platform, groupId, mode, start, end)}");

    public Task<OpsErrorTrendDto> GetAdminOpsErrorTrendAsync(string timeRange = "1h", string? platform = null, long? groupId = null, string? mode = null, DateTimeOffset? start = null, DateTimeOffset? end = null) =>
        SendAsync<OpsErrorTrendDto>(HttpMethod.Get, $"{ApiPrefix}/admin/ops/dashboard/error-trend?{BuildOpsQuery(timeRange, platform, groupId, mode, start, end)}");

    public Task<OpsLatencyHistogramDto> GetAdminOpsLatencyHistogramAsync(string timeRange = "1h", string? platform = null, long? groupId = null, string? mode = null, DateTimeOffset? start = null, DateTimeOffset? end = null) =>
        SendAsync<OpsLatencyHistogramDto>(HttpMethod.Get, $"{ApiPrefix}/admin/ops/dashboard/latency-histogram?{BuildOpsQuery(timeRange, platform, groupId, mode, start, end)}");

    public Task<OpsErrorDistributionDto> GetAdminOpsErrorDistributionAsync(string timeRange = "1h", string? platform = null, long? groupId = null, string? mode = null, DateTimeOffset? start = null, DateTimeOffset? end = null) =>
        SendAsync<OpsErrorDistributionDto>(HttpMethod.Get, $"{ApiPrefix}/admin/ops/dashboard/error-distribution?{BuildOpsQuery(timeRange, platform, groupId, mode, start, end)}");

    public Task<OpsConcurrencyStatsDto> GetAdminOpsConcurrencyAsync(string? platform = null, long? groupId = null) =>
        SendAsync<OpsConcurrencyStatsDto>(HttpMethod.Get, $"{ApiPrefix}/admin/ops/concurrency?{BuildOpsRealtimeFilter(platform, groupId)}");

    public Task<OpsUserConcurrencyStatsDto> GetAdminOpsUserConcurrencyAsync() =>
        SendAsync<OpsUserConcurrencyStatsDto>(HttpMethod.Get, $"{ApiPrefix}/admin/ops/user-concurrency");

    public Task<OpsAccountAvailabilityStatsDto> GetAdminOpsAccountAvailabilityAsync(string? platform = null, long? groupId = null) =>
        SendAsync<OpsAccountAvailabilityStatsDto>(HttpMethod.Get, $"{ApiPrefix}/admin/ops/account-availability?{BuildOpsRealtimeFilter(platform, groupId)}");

    public Task<OpsRealtimeTrafficResponseDto> GetAdminOpsRealtimeTrafficAsync(string window = "1min", string? platform = null, long? groupId = null)
    {
        var query = BuildOpsRealtimeFilter(platform, groupId);
        return SendAsync<OpsRealtimeTrafficResponseDto>(HttpMethod.Get, $"{ApiPrefix}/admin/ops/realtime-traffic?window={Uri.EscapeDataString(window)}{(query.Length > 0 ? $"&{query}" : string.Empty)}");
    }

    public Task<OpsOpenAiTokenStatsDto> GetAdminOpsOpenAiTokenStatsAsync(string timeRange = "1h", string? platform = null, long? groupId = null, int page = 1, int pageSize = 20, int? topN = null)
    {
        var query = new List<string>
        {
            $"time_range={Uri.EscapeDataString(timeRange)}"
        };
        if (topN.HasValue)
        {
            query.Add($"top_n={Math.Clamp(topN.Value, 1, 100)}");
        }
        else
        {
            query.Add($"page={Math.Max(1, page)}");
            query.Add($"page_size={Math.Clamp(pageSize, 1, 100)}");
        }
        if (!string.IsNullOrWhiteSpace(platform)) query.Add($"platform={Uri.EscapeDataString(platform.Trim())}");
        if (groupId.HasValue) query.Add($"group_id={groupId.Value}");
        return SendAsync<OpsOpenAiTokenStatsDto>(HttpMethod.Get, $"{ApiPrefix}/admin/ops/dashboard/openai-token-stats?{string.Join("&", query)}");
    }

    public Task<List<OpsAlertRuleDto>> GetAdminOpsAlertRulesAsync() =>
        SendAsync<List<OpsAlertRuleDto>>(HttpMethod.Get, $"{ApiPrefix}/admin/ops/alert-rules");

    public Task<OpsAlertRuleDto> CreateAdminOpsAlertRuleAsync(OpsAlertRuleDto value) =>
        SendAsync<OpsAlertRuleDto>(HttpMethod.Post, $"{ApiPrefix}/admin/ops/alert-rules", value);

    public Task<OpsAlertRuleDto> UpdateAdminOpsAlertRuleAsync(long id, OpsAlertRuleDto value) =>
        SendAsync<OpsAlertRuleDto>(HttpMethod.Put, $"{ApiPrefix}/admin/ops/alert-rules/{id}", value);

    public Task<JsonElement> DeleteAdminOpsAlertRuleAsync(long id) =>
        SendAsync<JsonElement>(HttpMethod.Delete, $"{ApiPrefix}/admin/ops/alert-rules/{id}");

    public Task<List<OpsAlertEventDto>> GetAdminOpsAlertEventsAsync(int limit = 20, string? status = null, string? severity = null) =>
        GetAdminOpsAlertEventsAsync(new OpsAlertEventsQueryDto { Limit = limit, Status = status ?? string.Empty, Severity = severity ?? string.Empty });

    public Task<List<OpsAlertEventDto>> GetAdminOpsAlertEventsAsync(OpsAlertEventsQueryDto filter)
    {
        var query = new List<string> { $"limit={Math.Clamp(filter.Limit, 1, 200)}" };
        AddOpsQuery(query, "status", filter.Status);
        AddOpsQuery(query, "severity", filter.Severity);
        AddOpsQuery(query, "email_sent", filter.EmailSent);
        AddOpsTimeQuery(query, filter.TimeRange, filter.StartTime, filter.EndTime);
        if (filter.BeforeFiredAt.HasValue && filter.BeforeId.HasValue)
        {
            AddOpsQuery(query, "before_fired_at", filter.BeforeFiredAt);
            AddOpsQuery(query, "before_id", filter.BeforeId);
        }
        AddOpsQuery(query, "platform", filter.Platform);
        AddOpsQuery(query, "group_id", filter.GroupId);
        return SendAsync<List<OpsAlertEventDto>>(HttpMethod.Get, $"{ApiPrefix}/admin/ops/alert-events?{string.Join("&", query)}");
    }

    public Task<OpsAlertEventDto> GetAdminOpsAlertEventAsync(long id) =>
        SendAsync<OpsAlertEventDto>(HttpMethod.Get, $"{ApiPrefix}/admin/ops/alert-events/{id}");

    public Task<JsonElement> CreateAdminOpsAlertSilenceAsync(OpsAlertSilenceRequestDto value) =>
        SendAsync<JsonElement>(HttpMethod.Post, $"{ApiPrefix}/admin/ops/alert-silences", value);

    public Task<JsonElement> UpdateAdminOpsAlertEventStatusAsync(long id, string status = "manual_resolved") =>
        SendAsync<JsonElement>(HttpMethod.Put, $"{ApiPrefix}/admin/ops/alert-events/{id}/status", new { status });

    public Task<OpsAdvancedSettingsDto> GetAdminOpsAdvancedSettingsAsync() =>
        SendAsync<OpsAdvancedSettingsDto>(HttpMethod.Get, $"{ApiPrefix}/admin/ops/advanced-settings");

    public Task<OpsAdvancedSettingsDto> UpdateAdminOpsAdvancedSettingsAsync(OpsAdvancedSettingsDto value) =>
        SendAsync<OpsAdvancedSettingsDto>(HttpMethod.Put, $"{ApiPrefix}/admin/ops/advanced-settings", value);

    public Task<OpsMetricThresholdsDto> GetAdminOpsMetricThresholdsAsync() =>
        SendAsync<OpsMetricThresholdsDto>(HttpMethod.Get, $"{ApiPrefix}/admin/ops/settings/metric-thresholds");

    public Task<JsonElement> UpdateAdminOpsMetricThresholdsAsync(OpsMetricThresholdsDto value) =>
        SendAsync<JsonElement>(HttpMethod.Put, $"{ApiPrefix}/admin/ops/settings/metric-thresholds", value);

    public Task<OpsRuntimeAlertSettingsDto> GetAdminOpsRuntimeAlertSettingsAsync() =>
        SendAsync<OpsRuntimeAlertSettingsDto>(HttpMethod.Get, $"{ApiPrefix}/admin/ops/runtime/alert");

    public Task<OpsRuntimeAlertSettingsDto> UpdateAdminOpsRuntimeAlertSettingsAsync(OpsRuntimeAlertSettingsDto value) =>
        SendAsync<OpsRuntimeAlertSettingsDto>(HttpMethod.Put, $"{ApiPrefix}/admin/ops/runtime/alert", value);

    public Task<OpsEmailNotificationConfigDto> GetAdminOpsEmailNotificationConfigAsync() =>
        SendAsync<OpsEmailNotificationConfigDto>(HttpMethod.Get, $"{ApiPrefix}/admin/ops/email-notification/config");

    public Task<OpsEmailNotificationConfigDto> UpdateAdminOpsEmailNotificationConfigAsync(OpsEmailNotificationConfigDto value) =>
        SendAsync<OpsEmailNotificationConfigDto>(HttpMethod.Put, $"{ApiPrefix}/admin/ops/email-notification/config", value);

    public Task<OpsRuntimeLogConfigDto> GetAdminOpsRuntimeLogConfigAsync() =>
        SendAsync<OpsRuntimeLogConfigDto>(HttpMethod.Get, $"{ApiPrefix}/admin/ops/runtime/logging");

    public Task<OpsRuntimeLogConfigDto> UpdateAdminOpsRuntimeLogConfigAsync(OpsRuntimeLogConfigDto value) =>
        SendAsync<OpsRuntimeLogConfigDto>(HttpMethod.Put, $"{ApiPrefix}/admin/ops/runtime/logging", value);

    public Task<OpsRuntimeLogConfigDto> ResetAdminOpsRuntimeLogConfigAsync() =>
        SendAsync<OpsRuntimeLogConfigDto>(HttpMethod.Post, $"{ApiPrefix}/admin/ops/runtime/logging/reset");

    public Task<PagedEnvelope<OpsSystemLogDto>> GetAdminOpsSystemLogsAsync(int page = 1, int pageSize = 20, string timeRange = "1h", string? level = null, string? component = null, string? platform = null, string? search = null) =>
        GetAdminOpsSystemLogsAsync(new OpsSystemLogQueryDto
        {
            Page = page,
            PageSize = pageSize,
            TimeRange = timeRange,
            Level = level ?? string.Empty,
            Component = component ?? string.Empty,
            Platform = platform ?? string.Empty,
            Search = search ?? string.Empty
        });

    public Task<PagedEnvelope<OpsSystemLogDto>> GetAdminOpsSystemLogsAsync(OpsSystemLogQueryDto filter)
    {
        var query = new List<string>
        {
            $"page={Math.Max(1, filter.Page)}", $"page_size={Math.Clamp(filter.PageSize, 1, 100)}"
        };
        AddOpsTimeQuery(query, filter.TimeRange, filter.StartTime, filter.EndTime);
        AddOpsQuery(query, "host", filter.Host);
        AddOpsQuery(query, "level", filter.Level);
        AddOpsQuery(query, "component", filter.Component);
        AddOpsQuery(query, "request_id", filter.RequestId);
        AddOpsQuery(query, "client_request_id", filter.ClientRequestId);
        AddOpsQuery(query, "user_id", filter.UserId);
        AddOpsQuery(query, "api_key_id", filter.ApiKeyId);
        AddOpsQuery(query, "account_id", filter.AccountId);
        AddOpsQuery(query, "platform", filter.Platform);
        AddOpsQuery(query, "model", filter.Model);
        AddOpsQuery(query, "q", filter.Search);
        return SendAsync<PagedEnvelope<OpsSystemLogDto>>(HttpMethod.Get, $"{ApiPrefix}/admin/ops/system-logs?{string.Join("&", query)}");
    }

    public Task<OpsSystemLogSinkHealthDto> GetAdminOpsSystemLogHealthAsync() =>
        SendAsync<OpsSystemLogSinkHealthDto>(HttpMethod.Get, $"{ApiPrefix}/admin/ops/system-logs/health");

    public Task<JsonElement> CleanupAdminOpsSystemLogsAsync(object filter) =>
        SendAsync<JsonElement>(HttpMethod.Post, $"{ApiPrefix}/admin/ops/system-logs/cleanup", filter);

    public Task<PagedEnvelope<OpsRequestDetailDto>> GetAdminOpsRequestDetailsAsync(int page = 1, int pageSize = 20, string timeRange = "1h", string kind = "all", string? platform = null, long? groupId = null, string? search = null, string sort = "created_at_desc") =>
        GetAdminOpsRequestDetailsAsync(new OpsRequestDetailsQueryDto
        {
            Page = page,
            PageSize = pageSize,
            TimeRange = timeRange,
            Kind = kind,
            Platform = platform ?? string.Empty,
            GroupId = groupId,
            Search = search ?? string.Empty,
            Sort = sort
        });

    public Task<PagedEnvelope<OpsRequestDetailDto>> GetAdminOpsRequestDetailsAsync(OpsRequestDetailsQueryDto filter)
    {
        var query = new List<string>
        {
            $"page={Math.Max(1, filter.Page)}", $"page_size={Math.Clamp(filter.PageSize, 1, 100)}",
            $"kind={Uri.EscapeDataString(filter.Kind)}", $"sort={Uri.EscapeDataString(filter.Sort)}"
        };
        AddOpsTimeQuery(query, filter.TimeRange, filter.StartTime, filter.EndTime);
        AddOpsQuery(query, "platform", filter.Platform);
        AddOpsQuery(query, "group_id", filter.GroupId);
        AddOpsQuery(query, "user_id", filter.UserId);
        AddOpsQuery(query, "api_key_id", filter.ApiKeyId);
        AddOpsQuery(query, "account_id", filter.AccountId);
        AddOpsQuery(query, "model", filter.Model);
        AddOpsQuery(query, "request_id", filter.RequestId);
        AddOpsQuery(query, "q", filter.Search);
        AddOpsQuery(query, "min_duration_ms", filter.MinDurationMs);
        AddOpsQuery(query, "max_duration_ms", filter.MaxDurationMs);
        return SendAsync<PagedEnvelope<OpsRequestDetailDto>>(HttpMethod.Get, $"{ApiPrefix}/admin/ops/requests?{string.Join("&", query)}");
    }

    public Task<PagedEnvelope<OpsErrorLogDto>> GetAdminOpsErrorsAsync(string kind, int page = 1, int pageSize = 20, string timeRange = "1h", string? platform = null, long? groupId = null, string? search = null, string view = "errors") =>
        GetAdminOpsErrorsAsync(kind, new OpsErrorListQueryDto
        {
            Page = page,
            PageSize = pageSize,
            TimeRange = timeRange,
            Platform = platform ?? string.Empty,
            GroupId = groupId,
            Search = search ?? string.Empty,
            View = view
        });

    public Task<PagedEnvelope<OpsErrorLogDto>> GetAdminOpsErrorsAsync(string kind, OpsErrorListQueryDto filter)
    {
        var resource = string.Equals(kind, "upstream", StringComparison.OrdinalIgnoreCase) ? "upstream-errors" : "request-errors";
        return SendAsync<PagedEnvelope<OpsErrorLogDto>>(HttpMethod.Get,
            $"{ApiPrefix}/admin/ops/{resource}?{BuildAdminOpsErrorQuery(filter)}");
    }

    public Task<PagedEnvelope<OpsErrorLogDto>> GetAdminOpsErrorLogsAsync(OpsErrorListQueryDto filter) =>
        SendAsync<PagedEnvelope<OpsErrorLogDto>>(HttpMethod.Get,
            $"{ApiPrefix}/admin/ops/errors?{BuildAdminOpsErrorQuery(filter)}");

    public Task<OpsErrorDetailDto> GetAdminOpsErrorLogDetailAsync(long id) =>
        SendAsync<OpsErrorDetailDto>(HttpMethod.Get, $"{ApiPrefix}/admin/ops/errors/{id}");

    public Task<OpsErrorDetailDto> GetAdminOpsErrorDetailAsync(string kind, long id)
    {
        var resource = string.Equals(kind, "upstream", StringComparison.OrdinalIgnoreCase) ? "upstream-errors" : "request-errors";
        return SendAsync<OpsErrorDetailDto>(HttpMethod.Get, $"{ApiPrefix}/admin/ops/{resource}/{id}");
    }

    public Task<PagedEnvelope<OpsErrorDetailDto>> GetAdminOpsCorrelatedUpstreamErrorsAsync(long requestErrorId, int page = 1, int pageSize = 100, bool includeDetail = true)
    {
        var query = $"page={Math.Max(1, page)}&page_size={Math.Clamp(pageSize, 1, 500)}{(includeDetail ? "&include_detail=1" : string.Empty)}";
        return SendAsync<PagedEnvelope<OpsErrorDetailDto>>(HttpMethod.Get, $"{ApiPrefix}/admin/ops/request-errors/{requestErrorId}/upstream-errors?{query}");
    }

    public Task<JsonElement> ResolveAdminOpsErrorAsync(string kind, long id, bool resolved) =>
        SendAsync<JsonElement>(HttpMethod.Put, $"{ApiPrefix}/admin/ops/{(string.Equals(kind, "upstream", StringComparison.OrdinalIgnoreCase) ? "upstream-errors" : "request-errors")}/{id}/resolve", new { resolved });

    private static string BuildAdminOpsErrorQuery(OpsErrorListQueryDto filter)
    {
        var query = new List<string>
        {
            $"page={Math.Max(1, filter.Page)}",
            $"page_size={Math.Clamp(filter.PageSize, 1, 500)}",
            $"view={Uri.EscapeDataString(string.IsNullOrWhiteSpace(filter.View) ? "errors" : filter.View)}"
        };
        AddOpsTimeQuery(query, filter.TimeRange, filter.StartTime, filter.EndTime);
        AddOpsQuery(query, "platform", filter.Platform);
        AddOpsQuery(query, "group_id", filter.GroupId);
        AddOpsQuery(query, "account_id", filter.AccountId);
        AddOpsQuery(query, "user_id", filter.UserId);
        AddOpsQuery(query, "api_key_id", filter.ApiKeyId);
        AddOpsQuery(query, "model", filter.Model);
        AddOpsQuery(query, "phase", filter.Phase);
        AddOpsQuery(query, "category", filter.Category);
        AddOpsQuery(query, "error_owner", filter.ErrorOwner);
        AddOpsQuery(query, "error_source", filter.ErrorSource);
        AddOpsQuery(query, "resolved", filter.Resolved);
        AddOpsQuery(query, "q", filter.Search);
        AddOpsQuery(query, "status_codes", filter.StatusCodes);
        if (filter.StatusCodesOther) query.Add("status_codes_other=1");
        AddOpsQuery(query, "sort_by", filter.SortBy);
        AddOpsQuery(query, "sort_order", filter.SortOrder);
        return string.Join("&", query);
    }
    private static void AddOpsTimeQuery(List<string> query, string? timeRange, DateTimeOffset? start, DateTimeOffset? end)
    {
        if (string.Equals(timeRange, "custom", StringComparison.OrdinalIgnoreCase))
        {
            if (start.HasValue && end.HasValue)
            {
                AddOpsQuery(query, "start_time", start);
                AddOpsQuery(query, "end_time", end);
            }
            else
            {
                AddOpsQuery(query, "time_range", "1h");
            }
            return;
        }
        if (!string.IsNullOrWhiteSpace(timeRange))
        {
            AddOpsQuery(query, "time_range", timeRange);
        }
    }

    private static void AddOpsQuery(List<string> query, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) query.Add($"{key}={Uri.EscapeDataString(value.Trim())}");
    }

    private static void AddOpsQuery(List<string> query, string key, long? value)
    {
        if (value.HasValue) query.Add($"{key}={value.Value}");
    }

    private static void AddOpsQuery(List<string> query, string key, int? value)
    {
        if (value.HasValue) query.Add($"{key}={value.Value}");
    }

    private static void AddOpsQuery(List<string> query, string key, bool? value)
    {
        if (value.HasValue) query.Add($"{key}={value.Value.ToString().ToLowerInvariant()}");
    }

    private static void AddOpsQuery(List<string> query, string key, DateTimeOffset? value)
    {
        if (value.HasValue) query.Add($"{key}={Uri.EscapeDataString(value.Value.ToUniversalTime().ToString("O"))}");
    }

    private static string BuildOpsQuery(string timeRange, string? platform, long? groupId, string? mode, DateTimeOffset? start, DateTimeOffset? end)
    {
        var query = new List<string>();
        if (string.Equals(timeRange, "custom", StringComparison.OrdinalIgnoreCase) && start.HasValue && end.HasValue)
        {
            query.Add($"start_time={Uri.EscapeDataString(start.Value.ToUniversalTime().ToString("O"))}");
            query.Add($"end_time={Uri.EscapeDataString(end.Value.ToUniversalTime().ToString("O"))}");
        }
        else query.Add($"time_range={Uri.EscapeDataString(timeRange)}");
        if (!string.IsNullOrWhiteSpace(platform)) query.Add($"platform={Uri.EscapeDataString(platform.Trim())}");
        if (groupId.HasValue) query.Add($"group_id={groupId.Value}");
        if (!string.IsNullOrWhiteSpace(mode)) query.Add($"mode={Uri.EscapeDataString(mode.Trim())}");
        return string.Join("&", query);
    }

    private static string BuildOpsRealtimeFilter(string? platform, long? groupId)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(platform)) query.Add($"platform={Uri.EscapeDataString(platform.Trim())}");
        if (groupId.HasValue) query.Add($"group_id={groupId.Value}");
        return string.Join("&", query);
    }

    private static string OptionalQueryPart(string key, string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : $"&{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value.Trim())}";

    public Task<JsonElement> GetAdminRiskStatusRawAsync() =>
        SendAsync<JsonElement>(HttpMethod.Get, $"{ApiPrefix}/admin/risk-control/status");

    public Task<JsonElement> GetAdminRiskLogsRawAsync() =>
        SendAsync<JsonElement>(HttpMethod.Get, $"{ApiPrefix}/admin/risk-control/logs?page=1&page_size=1000");

    public Task<RiskControlConfigDto> GetRiskConfigTypedAsync() =>
        SendAsync<RiskControlConfigDto>(HttpMethod.Get, $"{ApiPrefix}/admin/risk-control/config");

    public Task<RiskControlConfigDto> UpdateRiskConfigTypedAsync(object config) =>
        SendAsync<RiskControlConfigDto>(HttpMethod.Put, $"{ApiPrefix}/admin/risk-control/config", config);

    public Task<RiskControlStatusDto> GetRiskStatusAsync() =>
        SendAsync<RiskControlStatusDto>(HttpMethod.Get, $"{ApiPrefix}/admin/risk-control/status");

    public Task<RiskApiKeyTestResponseDto> TestRiskApiKeysAsync(object payload) =>
        SendAsync<RiskApiKeyTestResponseDto>(HttpMethod.Post, $"{ApiPrefix}/admin/risk-control/api-keys/test", payload);

    public Task<PagedEnvelope<RiskControlLogDto>> GetRiskLogsAsync(
        int page = 1,
        int pageSize = 50,
        string? search = null,
        string? result = null,
        long? groupId = null,
        string? endpoint = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null)
    {
        var query = new List<string>
        {
            $"page={Math.Max(1, page)}",
            $"page_size={Math.Clamp(pageSize, 1, 1000)}"
        };
        if (!string.IsNullOrWhiteSpace(result)) query.Add($"result={Uri.EscapeDataString(result.Trim())}");
        if (groupId.HasValue) query.Add($"group_id={groupId.Value}");
        if (!string.IsNullOrWhiteSpace(endpoint)) query.Add($"endpoint={Uri.EscapeDataString(endpoint.Trim())}");
        if (!string.IsNullOrWhiteSpace(search)) query.Add($"search={Uri.EscapeDataString(search.Trim())}");
        if (from.HasValue) query.Add($"from={Uri.EscapeDataString(from.Value.ToString("O"))}");
        if (to.HasValue) query.Add($"to={Uri.EscapeDataString(to.Value.ToString("O"))}");
        return SendAsync<PagedEnvelope<RiskControlLogDto>>(HttpMethod.Get, $"{ApiPrefix}/admin/risk-control/logs?{string.Join("&", query)}");
    }

    public Task<RiskUnbanResponseDto> UnbanRiskUserAsync(long userId) =>
        SendAsync<RiskUnbanResponseDto>(HttpMethod.Post, $"{ApiPrefix}/admin/risk-control/users/{userId}/unban");

    public Task<RiskDeleteHashResponseDto> DeleteRiskHashAsync(string hash) =>
        SendAsync<RiskDeleteHashResponseDto>(HttpMethod.Delete, $"{ApiPrefix}/admin/risk-control/hashes", new { input_hash = hash });

    public Task<RiskClearHashesResponseDto> ClearRiskHashesAsync() =>
        SendAsync<RiskClearHashesResponseDto>(HttpMethod.Delete, $"{ApiPrefix}/admin/risk-control/hashes/all");

    public Task<JsonElement> GetAdminBackupConfigRawAsync() =>
        SendAsync<JsonElement>(HttpMethod.Get, $"{ApiPrefix}/admin/backups/s3-config");

    public Task<JsonElement> GetAdminBackupScheduleRawAsync() =>
        SendAsync<JsonElement>(HttpMethod.Get, $"{ApiPrefix}/admin/backups/schedule");

    public Task<JsonElement> GetAdminBackupsRawAsync() =>
        SendAsync<JsonElement>(HttpMethod.Get, $"{ApiPrefix}/admin/backups?page=1&page_size=1000");

    public Task<JsonElement> GetAdminDataManagementConfigRawAsync() =>
        SendAsync<JsonElement>(HttpMethod.Get, $"{ApiPrefix}/admin/data-management/config");

    public Task<JsonElement> GetAdminDataManagementHealthRawAsync() =>
        SendAsync<JsonElement>(HttpMethod.Get, $"{ApiPrefix}/admin/data-management/agent/health");

    public Task<DataManagementConfigDto> GetAdminDataManagementConfigAsync() =>
        SendAsync<DataManagementConfigDto>(HttpMethod.Get, $"{ApiPrefix}/admin/data-management/config");

    public Task<DataManagementConfigDto> UpdateAdminDataManagementConfigAsync(object value) =>
        SendAsync<DataManagementConfigDto>(HttpMethod.Put, $"{ApiPrefix}/admin/data-management/config", value);

    public Task<DataManagementHealthDto> GetAdminDataManagementHealthAsync() =>
        SendAsync<DataManagementHealthDto>(HttpMethod.Get, $"{ApiPrefix}/admin/data-management/agent/health");

    public Task<List<DataManagementSourceProfileDto>> GetDataManagementSourceProfilesAsync(string sourceType) =>
        SendAsync<List<DataManagementSourceProfileDto>>(HttpMethod.Get, $"{ApiPrefix}/admin/data-management/sources/{Uri.EscapeDataString(sourceType)}/profiles", envelopeKeys: ["items"]);

    public Task<DataManagementSourceProfileDto> CreateDataManagementSourceProfileAsync(string sourceType, object value) =>
        SendAsync<DataManagementSourceProfileDto>(HttpMethod.Post, $"{ApiPrefix}/admin/data-management/sources/{Uri.EscapeDataString(sourceType)}/profiles", value);

    public Task<DataManagementSourceProfileDto> UpdateDataManagementSourceProfileAsync(string sourceType, string profileId, object value) =>
        SendAsync<DataManagementSourceProfileDto>(HttpMethod.Put, $"{ApiPrefix}/admin/data-management/sources/{Uri.EscapeDataString(sourceType)}/profiles/{Uri.EscapeDataString(profileId)}", value);

    public Task DeleteDataManagementSourceProfileAsync(string sourceType, string profileId) =>
        SendAsync(HttpMethod.Delete, $"{ApiPrefix}/admin/data-management/sources/{Uri.EscapeDataString(sourceType)}/profiles/{Uri.EscapeDataString(profileId)}");

    public Task<DataManagementSourceProfileDto> ActivateDataManagementSourceProfileAsync(string sourceType, string profileId) =>
        SendAsync<DataManagementSourceProfileDto>(HttpMethod.Post, $"{ApiPrefix}/admin/data-management/sources/{Uri.EscapeDataString(sourceType)}/profiles/{Uri.EscapeDataString(profileId)}/activate");

    public Task<S3TestResultDto> TestDataManagementS3Async(object value) =>
        SendAsync<S3TestResultDto>(HttpMethod.Post, $"{ApiPrefix}/admin/data-management/s3/test", value);

    public Task<List<DataManagementS3ProfileDto>> GetDataManagementS3ProfilesAsync() =>
        SendAsync<List<DataManagementS3ProfileDto>>(HttpMethod.Get, $"{ApiPrefix}/admin/data-management/s3/profiles", envelopeKeys: ["items"]);

    public Task<DataManagementS3ProfileDto> CreateDataManagementS3ProfileAsync(object value) =>
        SendAsync<DataManagementS3ProfileDto>(HttpMethod.Post, $"{ApiPrefix}/admin/data-management/s3/profiles", value);

    public Task<DataManagementS3ProfileDto> UpdateDataManagementS3ProfileAsync(string profileId, object value) =>
        SendAsync<DataManagementS3ProfileDto>(HttpMethod.Put, $"{ApiPrefix}/admin/data-management/s3/profiles/{Uri.EscapeDataString(profileId)}", value);

    public Task DeleteDataManagementS3ProfileAsync(string profileId) =>
        SendAsync(HttpMethod.Delete, $"{ApiPrefix}/admin/data-management/s3/profiles/{Uri.EscapeDataString(profileId)}");

    public Task<DataManagementS3ProfileDto> ActivateDataManagementS3ProfileAsync(string profileId) =>
        SendAsync<DataManagementS3ProfileDto>(HttpMethod.Post, $"{ApiPrefix}/admin/data-management/s3/profiles/{Uri.EscapeDataString(profileId)}/activate");

    public Task<List<DataManagementBackupJobDto>> GetDataManagementBackupJobsAsync() =>
        SendAsync<List<DataManagementBackupJobDto>>(HttpMethod.Get, $"{ApiPrefix}/admin/data-management/backups", envelopeKeys: ["items"]);

    public Task<DataManagementBackupJobDto> CreateDataManagementBackupJobAsync(object value) =>
        SendAsync<DataManagementBackupJobDto>(HttpMethod.Post, $"{ApiPrefix}/admin/data-management/backups", value);

    public Task<DataManagementBackupJobDto> GetDataManagementBackupJobAsync(string jobId) =>
        SendAsync<DataManagementBackupJobDto>(HttpMethod.Get, $"{ApiPrefix}/admin/data-management/backups/{Uri.EscapeDataString(jobId)}");

    public Task<JsonElement> GetAdminPromptAuditConfigRawAsync() =>
        SendAsync<JsonElement>(HttpMethod.Get, $"{ApiPrefix}/admin/prompt-audit/config");

    public Task<JsonElement> UpdateAdminPromptAuditConfigAsync(JsonElement value) =>
        SendAsync<JsonElement>(HttpMethod.Put, $"{ApiPrefix}/admin/prompt-audit/config", value);

    public Task<JsonElement> GetAdminErrorPassthroughRawAsync() =>
        SendAsync<JsonElement>(HttpMethod.Get, $"{ApiPrefix}/admin/error-passthrough-rules");

    public Task<JsonElement> GetAdminTlsProfilesRawAsync() =>
        SendAsync<JsonElement>(HttpMethod.Get, $"{ApiPrefix}/admin/tls-fingerprint-profiles");

    public Task<JsonElement> GetAdminChannelMonitorsRawAsync() =>
        SendAsync<JsonElement>(HttpMethod.Get, $"{ApiPrefix}/admin/channel-monitors?page=1&page_size=1000");

    public Task<PagedEnvelope<ChannelMonitorDto>> GetAdminChannelMonitorsAsync(int page = 1, int pageSize = 100, string? search = null, string? provider = null, bool? enabled = null) =>
        SendAsync<PagedEnvelope<ChannelMonitorDto>>(HttpMethod.Get, BuildChannelMonitorListUrl(page, pageSize, search, provider, enabled));

    public Task<ChannelMonitorDto> GetAdminChannelMonitorAsync(long id) =>
        SendAsync<ChannelMonitorDto>(HttpMethod.Get, $"{ApiPrefix}/admin/channel-monitors/{id}");

    public Task<ChannelMonitorDto> CreateAdminChannelMonitorAsync(object payload) =>
        SendAsync<ChannelMonitorDto>(HttpMethod.Post, $"{ApiPrefix}/admin/channel-monitors", payload);

    public Task<ChannelMonitorDto> UpdateAdminChannelMonitorAsync(long id, object payload) =>
        SendAsync<ChannelMonitorDto>(HttpMethod.Put, $"{ApiPrefix}/admin/channel-monitors/{id}", payload);

    public Task DeleteAdminChannelMonitorAsync(long id) =>
        SendAsync(HttpMethod.Delete, $"{ApiPrefix}/admin/channel-monitors/{id}");

    public Task<ChannelMonitorDto> DuplicateAdminChannelMonitorAsync(long id) =>
        SendAsync<ChannelMonitorDto>(HttpMethod.Post, $"{ApiPrefix}/admin/channel-monitors/{id}/duplicate");

    public Task<ChannelMonitorRunResultDto> RunAdminChannelMonitorTypedAsync(long id) =>
        SendAsync<ChannelMonitorRunResultDto>(HttpMethod.Post, $"{ApiPrefix}/admin/channel-monitors/{id}/run");

    public Task<ChannelMonitorHistoryDto> GetAdminChannelMonitorHistoryAsync(long id, string? model = null, int limit = 100) =>
        SendAsync<ChannelMonitorHistoryDto>(HttpMethod.Get, $"{ApiPrefix}/admin/channel-monitors/{id}/history?limit={Math.Clamp(limit, 1, 1000)}{(string.IsNullOrWhiteSpace(model) ? string.Empty : $"&model={Uri.EscapeDataString(model)}")}");

    public Task<List<ChannelMonitorTemplateDto>> GetChannelMonitorTemplatesAsync(string? provider = null, string? apiMode = null) =>
        SendAsync<List<ChannelMonitorTemplateDto>>(HttpMethod.Get, $"{ApiPrefix}/admin/channel-monitor-templates?{BuildOptionalQuery(("provider", provider), ("api_mode", apiMode))}", envelopeKeys: ["items"]);

    public Task<List<ChannelMonitorAssociatedDto>> GetChannelMonitorTemplateMonitorsAsync(long id) =>
        SendAsync<List<ChannelMonitorAssociatedDto>>(HttpMethod.Get, $"{ApiPrefix}/admin/channel-monitor-templates/{id}/monitors", envelopeKeys: ["items"]);

    public Task<ChannelMonitorTemplateDto> CreateChannelMonitorTemplateAsync(object payload) =>
        SendAsync<ChannelMonitorTemplateDto>(HttpMethod.Post, $"{ApiPrefix}/admin/channel-monitor-templates", payload);

    public Task<ChannelMonitorTemplateDto> UpdateChannelMonitorTemplateAsync(long id, object payload) =>
        SendAsync<ChannelMonitorTemplateDto>(HttpMethod.Put, $"{ApiPrefix}/admin/channel-monitor-templates/{id}", payload);

    public Task DeleteChannelMonitorTemplateAsync(long id) =>
        SendAsync(HttpMethod.Delete, $"{ApiPrefix}/admin/channel-monitor-templates/{id}");

    public Task<JsonElement> ApplyChannelMonitorTemplateAsync(long id, IEnumerable<long> monitorIds) =>
        SendAsync<JsonElement>(HttpMethod.Post, $"{ApiPrefix}/admin/channel-monitor-templates/{id}/apply", new { monitor_ids = monitorIds.ToArray() });

    public Task<JsonElement> RunAdminChannelMonitorAsync(string id) =>
        SendAsync<JsonElement>(HttpMethod.Post, $"{ApiPrefix}/admin/channel-monitors/{Uri.EscapeDataString(id)}/run");

    public Task<ChannelMonitorV2ConfigDto> GetChannelMonitorV2ConfigAsync() =>
        SendAsync<ChannelMonitorV2ConfigDto>(HttpMethod.Get, $"{ApiPrefix}/admin/channel-monitor-v2/config");

    public Task<ChannelMonitorV2ConfigDto> UpdateChannelMonitorV2ConfigAsync(ChannelMonitorV2ConfigDto config) =>
        SendAsync<ChannelMonitorV2ConfigDto>(HttpMethod.Put, $"{ApiPrefix}/admin/channel-monitor-v2/config", config);

    public Task<ChannelMonitorV2DimensionsDto> GetChannelMonitorV2DimensionsAsync(ChannelMonitorV2FilterDto filter) =>
        SendAsync<ChannelMonitorV2DimensionsDto>(HttpMethod.Get, BuildChannelMonitorV2Url("dimensions", filter));

    public Task<ChannelMonitorV2SnapshotDto> GetChannelMonitorV2SnapshotAsync(ChannelMonitorV2FilterDto filter) =>
        SendAsync<ChannelMonitorV2SnapshotDto>(HttpMethod.Get, BuildChannelMonitorV2Url("snapshot", filter));

    public Task<ChannelMonitorV2MatrixDto> GetChannelMonitorV2MatrixAsync(ChannelMonitorV2FilterDto filter, string groupBy = "platform_group") =>
        SendAsync<ChannelMonitorV2MatrixDto>(HttpMethod.Get, BuildChannelMonitorV2Url("matrix", filter, [("group_by", groupBy)]));

    public Task<ChannelMonitorV2ListDto<ChannelMonitorV2ModelRowDto>> GetChannelMonitorV2ModelsAsync(ChannelMonitorV2FilterDto filter) =>
        SendAsync<ChannelMonitorV2ListDto<ChannelMonitorV2ModelRowDto>>(HttpMethod.Get, BuildChannelMonitorV2Url("models", filter));

    public Task<ChannelMonitorV2ListDto<ChannelMonitorV2ErrorRowDto>> GetChannelMonitorV2ErrorsAsync(ChannelMonitorV2FilterDto filter) =>
        SendAsync<ChannelMonitorV2ListDto<ChannelMonitorV2ErrorRowDto>>(HttpMethod.Get, BuildChannelMonitorV2Url("errors", filter));

    public Task<ChannelMonitorV2ListDto<ChannelMonitorV2UserRowDto>> GetChannelMonitorV2UsersAsync(ChannelMonitorV2FilterDto filter) =>
        SendAsync<ChannelMonitorV2ListDto<ChannelMonitorV2UserRowDto>>(HttpMethod.Get, BuildChannelMonitorV2Url("users", filter));

    private static string BuildChannelMonitorV2Url(string resource, ChannelMonitorV2FilterDto filter, IEnumerable<(string Key, string Value)>? extras = null, bool admin = true)
    {
        var query = new List<string> { $"range={Uri.EscapeDataString(filter.Range)}" };
        foreach (var platform in filter.Platforms.Distinct(StringComparer.OrdinalIgnoreCase)) query.Add($"platform={Uri.EscapeDataString(platform)}");
        foreach (var group in filter.GroupIds.Distinct()) query.Add($"group_id={group}");
        foreach (var model in filter.Models.Distinct(StringComparer.OrdinalIgnoreCase)) query.Add($"model={Uri.EscapeDataString(model)}");
        if (extras is not null) foreach (var (key, value) in extras) query.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
        var scope = admin ? "/admin" : string.Empty;
        return $"{ApiPrefix}{scope}/channel-monitor-v2/{resource}?{string.Join("&", query)}";
    }

    private static string BuildChannelMonitorListUrl(int page, int pageSize, string? search, string? provider, bool? enabled)
    {
        var query = new List<string> { $"page={Math.Max(1, page)}", $"page_size={Math.Clamp(pageSize, 1, 100)}" };
        if (!string.IsNullOrWhiteSpace(search)) query.Add($"search={Uri.EscapeDataString(search.Trim())}");
        if (!string.IsNullOrWhiteSpace(provider)) query.Add($"provider={Uri.EscapeDataString(provider.Trim())}");
        if (enabled.HasValue) query.Add($"enabled={(enabled.Value ? "true" : "false")}");
        return $"{ApiPrefix}/admin/channel-monitors?{string.Join("&", query)}";
    }

    private static string BuildOptionalQuery(params (string Key, string? Value)[] values) =>
        string.Join("&", values.Where(x => !string.IsNullOrWhiteSpace(x.Value)).Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value!.Trim())}"));

    public Task<JsonElement> GetAdminSystemCheckUpdatesRawAsync(bool force = false) =>
        SendAsync<JsonElement>(HttpMethod.Get, $"{ApiPrefix}/admin/system/check-updates{(force ? "?force=true" : string.Empty)}");

    // 高风险管理资源使用类型化方法，页面不会再要求管理员直接编辑整段 JSON。
    public Task<BackupS3ConfigDto> GetBackupS3ConfigAsync() =>
        SendAsync<BackupS3ConfigDto>(HttpMethod.Get, $"{ApiPrefix}/admin/backups/s3-config");

    public Task<BackupS3ConfigDto> UpdateBackupS3ConfigAsync(BackupS3ConfigDto value) =>
        SendAsync<BackupS3ConfigDto>(HttpMethod.Put, $"{ApiPrefix}/admin/backups/s3-config", value);

    public Task<S3TestResultDto> TestBackupS3Async(BackupS3ConfigDto value) =>
        SendAsync<S3TestResultDto>(HttpMethod.Post, $"{ApiPrefix}/admin/backups/s3-config/test", value);

    public Task<ImageStorageConfigResponseDto> GetImageStorageConfigAsync() =>
        SendAsync<ImageStorageConfigResponseDto>(HttpMethod.Get, $"{ApiPrefix}/admin/backups/image-storage");

    public Task<ImageStorageConfigDto> UpdateImageStorageConfigAsync(ImageStorageConfigDto value) =>
        SendAsync<ImageStorageConfigDto>(HttpMethod.Put, $"{ApiPrefix}/admin/backups/image-storage", value);

    public Task<S3TestResultDto> TestImageStorageAsync(ImageStorageConfigDto value) =>
        SendAsync<S3TestResultDto>(HttpMethod.Post, $"{ApiPrefix}/admin/backups/image-storage/test", value);

    public Task<BackupScheduleDto> GetBackupScheduleAsync() =>
        SendAsync<BackupScheduleDto>(HttpMethod.Get, $"{ApiPrefix}/admin/backups/schedule");

    public Task<BackupScheduleDto> UpdateBackupScheduleAsync(BackupScheduleDto value) =>
        SendAsync<BackupScheduleDto>(HttpMethod.Put, $"{ApiPrefix}/admin/backups/schedule", value);

    public Task<BackupRecordDto> CreateBackupAsync(int? expireDays = 14) =>
        SendAsync<BackupRecordDto>(HttpMethod.Post, $"{ApiPrefix}/admin/backups", new { expire_days = expireDays });

    public Task<BackupListDto> GetBackupsAsync() =>
        SendAsync<BackupListDto>(HttpMethod.Get, $"{ApiPrefix}/admin/backups");

    public Task<BackupRecordDto> GetBackupAsync(string id) =>
        SendAsync<BackupRecordDto>(HttpMethod.Get, $"{ApiPrefix}/admin/backups/{Uri.EscapeDataString(id)}");

    public Task DeleteBackupAsync(string id) =>
        SendAsync(HttpMethod.Delete, $"{ApiPrefix}/admin/backups/{Uri.EscapeDataString(id)}");

    public Task<BackupDownloadDto> GetBackupDownloadAsync(string id) =>
        SendAsync<BackupDownloadDto>(HttpMethod.Get, $"{ApiPrefix}/admin/backups/{Uri.EscapeDataString(id)}/download-url");

    public Task<BackupRecordDto> RestoreBackupAsync(string id, string password) =>
        SendAsync<BackupRecordDto>(HttpMethod.Post, $"{ApiPrefix}/admin/backups/{Uri.EscapeDataString(id)}/restore", new { password });

    public Task<List<ErrorPassthroughRuleDto>> GetErrorPassthroughRulesAsync() =>
        SendAsync<List<ErrorPassthroughRuleDto>>(HttpMethod.Get, $"{ApiPrefix}/admin/error-passthrough-rules");

    public Task<ErrorPassthroughRuleDto> CreateErrorPassthroughRuleAsync(object payload) =>
        SendAsync<ErrorPassthroughRuleDto>(HttpMethod.Post, $"{ApiPrefix}/admin/error-passthrough-rules", payload);

    public Task<ErrorPassthroughRuleDto> UpdateErrorPassthroughRuleAsync(long id, object payload) =>
        SendAsync<ErrorPassthroughRuleDto>(HttpMethod.Put, $"{ApiPrefix}/admin/error-passthrough-rules/{id}", payload);

    public Task DeleteErrorPassthroughRuleAsync(long id) =>
        SendAsync(HttpMethod.Delete, $"{ApiPrefix}/admin/error-passthrough-rules/{id}");

    public Task<List<TlsFingerprintProfileDto>> GetTlsFingerprintProfilesAsync() =>
        SendAsync<List<TlsFingerprintProfileDto>>(HttpMethod.Get, $"{ApiPrefix}/admin/tls-fingerprint-profiles");

    public Task<TlsFingerprintProfileDto> CreateTlsFingerprintProfileAsync(object payload) =>
        SendAsync<TlsFingerprintProfileDto>(HttpMethod.Post, $"{ApiPrefix}/admin/tls-fingerprint-profiles", payload);

    public Task<TlsFingerprintProfileDto> UpdateTlsFingerprintProfileAsync(long id, object payload) =>
        SendAsync<TlsFingerprintProfileDto>(HttpMethod.Put, $"{ApiPrefix}/admin/tls-fingerprint-profiles/{id}", payload);

    public Task DeleteTlsFingerprintProfileAsync(long id) =>
        SendAsync(HttpMethod.Delete, $"{ApiPrefix}/admin/tls-fingerprint-profiles/{id}");

    public Task<PromptAuditConfigDto> GetPromptAuditConfigAsync() =>
        SendAsync<PromptAuditConfigDto>(HttpMethod.Get, $"{ApiPrefix}/admin/prompt-audit/config");

    public Task<PromptAuditConfigDto> UpdatePromptAuditConfigAsync(object payload) =>
        SendAsync<PromptAuditConfigDto>(HttpMethod.Put, $"{ApiPrefix}/admin/prompt-audit/config", payload);

    public Task<PromptAuditRuntimeDto> GetPromptAuditRuntimeAsync() =>
        SendAsync<PromptAuditRuntimeDto>(HttpMethod.Get, $"{ApiPrefix}/admin/prompt-audit/runtime");

    public Task<PromptAuditProbeResultDto> ProbePromptAuditEndpointAsync(PromptAuditEndpointDto endpoint) =>
        SendAsync<PromptAuditProbeResultDto>(HttpMethod.Post, $"{ApiPrefix}/admin/prompt-audit/endpoints/probe", new
        {
            endpoint = new
            {
                id = endpoint.Id,
                name = endpoint.Name,
                protocol = "openai_compatible",
                base_url = endpoint.BaseUrl,
                model = endpoint.Model,
                token = string.IsNullOrWhiteSpace(endpoint.Token) ? null : endpoint.Token,
                timeout_ms = endpoint.TimeoutMs,
                input_limit = endpoint.InputLimit,
                enabled = endpoint.Enabled
            }
        });

    public Task<List<PromptAuditGroupDto>> GetPromptAuditGroupsAsync() =>
        SendAsync<List<PromptAuditGroupDto>>(HttpMethod.Get, $"{ApiPrefix}/admin/groups/all?include_inactive=true");

    public Task<PagedEnvelope<PromptAuditEventDto>> GetPromptAuditEventsAsync(int page = 1, int pageSize = 20, PromptAuditEventFiltersDto? filters = null)
    {
        filters ??= new();
        var query = new List<string> { $"page={Math.Max(1, page)}", $"page_size={Math.Clamp(pageSize, 1, 100)}" };
        AddPromptAuditQuery(query, "decision", filters.Decision);
        AddPromptAuditQuery(query, "risk_level", filters.RiskLevel);
        AddPromptAuditQuery(query, "endpoint", filters.Endpoint);
        AddPromptAuditQuery(query, "group_id", filters.GroupId);
        AddPromptAuditQuery(query, "user_id", filters.UserId);
        AddPromptAuditQuery(query, "api_key_id", filters.ApiKeyId);
        AddPromptAuditQuery(query, "request_id", filters.RequestId);
        AddPromptAuditQuery(query, "prompt_hash", filters.PromptHash);
        AddPromptAuditQuery(query, "keyword", filters.Keyword);
        AddPromptAuditQuery(query, "start_at", filters.StartAt);
        AddPromptAuditQuery(query, "end_at", filters.EndAt);
        return SendAsync<PagedEnvelope<PromptAuditEventDto>>(HttpMethod.Get, $"{ApiPrefix}/admin/prompt-audit/events?{string.Join("&", query)}");
    }

    public Task<PromptAuditEventDto> GetPromptAuditEventAsync(long id) =>
        SendAsync<PromptAuditEventDto>(HttpMethod.Get, $"{ApiPrefix}/admin/prompt-audit/events/{id}");

    public Task<PromptAuditDeleteResultDto> DeletePromptAuditEventAsync(long id) =>
        SendAsync<PromptAuditDeleteResultDto>(HttpMethod.Delete, $"{ApiPrefix}/admin/prompt-audit/events/{id}");

    public Task<PromptAuditDeleteResultDto> BatchDeletePromptAuditEventsAsync(IEnumerable<long> ids) =>
        SendAsync<PromptAuditDeleteResultDto>(HttpMethod.Post, $"{ApiPrefix}/admin/prompt-audit/events/batch-delete", new { ids = ids.ToArray() });

    public Task<PromptAuditDeletePreviewDto> PreviewPromptAuditDeleteAsync(PromptAuditEventFiltersDto filters) =>
        SendAsync<PromptAuditDeletePreviewDto>(HttpMethod.Post, $"{ApiPrefix}/admin/prompt-audit/events/delete-preview", BuildPromptAuditFilterPayload(filters));

    public Task<PromptAuditDeleteResultDto> DeletePromptAuditEventsByFilterAsync(PromptAuditEventFiltersDto filters, PromptAuditDeletePreviewDto preview) =>
        SendAsync<PromptAuditDeleteResultDto>(HttpMethod.Post, $"{ApiPrefix}/admin/prompt-audit/events/delete-by-filter", new
        {
            filter = BuildPromptAuditFilterPayload(filters),
            snapshot_max_id = preview.SnapshotMaxId,
            filter_hash = preview.FilterHash,
            confirmation_token = preview.ConfirmationToken,
            confirm = true
        });

    private static Dictionary<string, object> BuildPromptAuditFilterPayload(PromptAuditEventFiltersDto filters)
    {
        var payload = new Dictionary<string, object>(StringComparer.Ordinal);
        AddPromptAuditFilterValue(payload, "decision", filters.Decision);
        AddPromptAuditFilterValue(payload, "risk_level", filters.RiskLevel);
        AddPromptAuditFilterValue(payload, "endpoint", filters.Endpoint);
        AddPromptAuditFilterId(payload, "group_id", filters.GroupId);
        AddPromptAuditFilterId(payload, "user_id", filters.UserId);
        AddPromptAuditFilterId(payload, "api_key_id", filters.ApiKeyId);
        AddPromptAuditFilterValue(payload, "request_id", filters.RequestId);
        AddPromptAuditFilterValue(payload, "prompt_hash", filters.PromptHash);
        AddPromptAuditFilterValue(payload, "keyword", filters.Keyword);
        AddPromptAuditFilterValue(payload, "start_at", filters.StartAt);
        AddPromptAuditFilterValue(payload, "end_at", filters.EndAt);
        return payload;
    }

    private static void AddPromptAuditFilterValue(Dictionary<string, object> payload, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) payload[key] = value.Trim();
    }

    private static void AddPromptAuditFilterId(Dictionary<string, object> payload, string key, string? value)
    {
        if (long.TryParse(value, out var id) && id > 0) payload[key] = id;
    }

    private static void AddPromptAuditQuery(List<string> query, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) query.Add($"{key}={Uri.EscapeDataString(value.Trim())}");
    }
    public Task<RequestAuditPolicyDto> GetRequestAuditPolicyAsync() =>
        SendAsync<RequestAuditPolicyDto>(HttpMethod.Get, $"{ApiPrefix}/admin/request-audit/policy");

    public Task<RequestAuditPolicyDto> UpdateRequestAuditPolicyAsync(object payload) =>
        SendAsync<RequestAuditPolicyDto>(HttpMethod.Put, $"{ApiPrefix}/admin/request-audit/policy", payload);

    public Task<RequestAuditRuntimeDto> GetRequestAuditRuntimeAsync() =>
        SendAsync<RequestAuditRuntimeDto>(HttpMethod.Get, $"{ApiPrefix}/admin/request-audit/runtime");

    public Task<PagedEnvelope<RequestAuditRecordDto>> GetRequestAuditRecordsAsync(int page, int pageSize, RequestAuditFilterDto? filters = null)
    {
        filters ??= new();
        var query = new List<string> { $"page={Math.Max(1, page)}", $"page_size={Math.Clamp(pageSize, 1, 100)}" };
        AddPromptAuditQuery(query, "user_id", filters.UserId);
        AddPromptAuditQuery(query, "api_key_id", filters.ApiKeyId);
        AddPromptAuditQuery(query, "group_id", filters.GroupId);
        AddPromptAuditQuery(query, "status_code", filters.StatusCode);
        AddPromptAuditQuery(query, "request_id", filters.RequestId);
        AddPromptAuditQuery(query, "model", filters.Model);
        AddPromptAuditQuery(query, "q", filters.Query);
        AddPromptAuditQuery(query, "start_at", filters.StartAt);
        AddPromptAuditQuery(query, "end_at", filters.EndAt);
        return SendAsync<PagedEnvelope<RequestAuditRecordDto>>(HttpMethod.Get, $"{ApiPrefix}/admin/request-audit/records?{string.Join("&", query)}");
    }

    public Task<RequestAuditRecordDto> GetRequestAuditRecordAsync(long id) =>
        SendAsync<RequestAuditRecordDto>(HttpMethod.Get, $"{ApiPrefix}/admin/request-audit/records/{id}");

    public Task<RequestAuditContentDto> GetRequestAuditContentAsync(long id) =>
        SendAsync<RequestAuditContentDto>(HttpMethod.Get, $"{ApiPrefix}/admin/request-audit/records/{id}/content");

    public Task<SystemUpdateInfoDto> CheckSystemUpdatesAsync(bool force = false) =>
        SendAsync<SystemUpdateInfoDto>(HttpMethod.Get, $"{ApiPrefix}/admin/system/check-updates{(force ? "?force=true" : string.Empty)}");

    public Task<SystemActionResultDto> PerformSystemUpdateAsync() =>
        SendAsync<SystemActionResultDto>(HttpMethod.Post, $"{ApiPrefix}/admin/system/update");

    public Task<SystemActionResultDto> RollbackSystemAsync(string? version = null) =>
        SendAsync<SystemActionResultDto>(HttpMethod.Post, $"{ApiPrefix}/admin/system/rollback",
            string.IsNullOrWhiteSpace(version) ? null : new { version });

    public Task<RollbackVersionsDto> GetRollbackVersionsAsync() =>
        SendAsync<RollbackVersionsDto>(HttpMethod.Get, $"{ApiPrefix}/admin/system/rollback-versions");

    public Task<JsonElement> RestartSystemAsync() =>
        SendAsync<JsonElement>(HttpMethod.Post, $"{ApiPrefix}/admin/system/restart");

    public Task<JsonElement> GetAdminScheduledTestPlansRawAsync(string accountId) =>
        SendAsync<JsonElement>(HttpMethod.Get, $"{ApiPrefix}/admin/accounts/{Uri.EscapeDataString(accountId)}/scheduled-test-plans");

    public Task<PagedEnvelope<PromoCodeDto>> GetAdminPromoCodesAsync(
        int page = 1,
        int pageSize = 100,
        string? status = null,
        string? search = null,
        string sortBy = "created_at",
        string sortOrder = "desc")
    {
        var query = new List<string>
        {
            $"page={Math.Max(1, page)}",
            $"page_size={Math.Clamp(pageSize, 1, 1000)}",
            $"sort_by={Uri.EscapeDataString(string.IsNullOrWhiteSpace(sortBy) ? "created_at" : sortBy)}",
            $"sort_order={Uri.EscapeDataString(string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc")}"
        };
        if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status.Trim())}");
        if (!string.IsNullOrWhiteSpace(search)) query.Add($"search={Uri.EscapeDataString(search.Trim())}");
        return SendAsync<PagedEnvelope<PromoCodeDto>>(HttpMethod.Get, $"{ApiPrefix}/admin/promo-codes?{string.Join("&", query)}");
    }

    public Task<PromoCodeDto> GetAdminPromoCodeAsync(long id) =>
        SendAsync<PromoCodeDto>(HttpMethod.Get, $"{ApiPrefix}/admin/promo-codes/{id}");

    public Task<PromoCodeDto> CreateAdminPromoCodeAsync(object payload) =>
        SendAsync<PromoCodeDto>(HttpMethod.Post, $"{ApiPrefix}/admin/promo-codes", payload);

    public Task<PromoCodeDto> UpdateAdminPromoCodeAsync(long id, object payload) =>
        SendAsync<PromoCodeDto>(HttpMethod.Put, $"{ApiPrefix}/admin/promo-codes/{id}", payload);

    public Task DeleteAdminPromoCodeAsync(long id) =>
        SendAsync(HttpMethod.Delete, $"{ApiPrefix}/admin/promo-codes/{id}");

    public Task<PagedEnvelope<PromoCodeUsageDto>> GetAdminPromoCodeUsagesAsync(long id, int page = 1, int pageSize = 20) =>
        SendAsync<PagedEnvelope<PromoCodeUsageDto>>(HttpMethod.Get,
            $"{ApiPrefix}/admin/promo-codes/{id}/usages?page={Math.Max(1, page)}&page_size={Math.Clamp(pageSize, 1, 1000)}");

    // Compatibility helper for older page code; the typed API above is preferred.
    public async Task<JsonElement> GetAdminPromoCodesRawAsync() =>
        JsonSerializer.SerializeToElement(await GetAdminPromoCodesAsync(1, 1000));

    public Task<JsonElement> GetAdminAffiliatesRawAsync(string kind) =>
        SendAsync<JsonElement>(HttpMethod.Get, $"{ApiPrefix}/admin/affiliates/{Uri.EscapeDataString(kind)}?page=1&page_size=1000");

    public Task<PagedEnvelope<AffiliateAdminEntryDto>> GetAffiliateAdminUsersAsync(int page = 1, int pageSize = 50, string? search = null) =>
        SendAsync<PagedEnvelope<AffiliateAdminEntryDto>>(HttpMethod.Get, $"{ApiPrefix}/admin/affiliates/users?page={Math.Max(1, page)}&page_size={Math.Clamp(pageSize, 1, 100)}&search={Uri.EscapeDataString(search ?? string.Empty)}");

    public Task<List<AffiliateUserLookupDto>> LookupAffiliateUsersAsync(string query) =>
        SendAsync<List<AffiliateUserLookupDto>>(HttpMethod.Get, $"{ApiPrefix}/admin/affiliates/users/lookup?q={Uri.EscapeDataString(query.Trim())}");

    public Task<AffiliateUserOverviewDto> GetAffiliateUserOverviewAsync(long userId) =>
        SendAsync<AffiliateUserOverviewDto>(HttpMethod.Get, $"{ApiPrefix}/admin/affiliates/users/{userId}/overview");

    public Task<JsonElement> BatchSetAffiliateRateAsync(IEnumerable<long> userIds, double? ratePercent, bool clear = false) =>
        SendAsync<JsonElement>(HttpMethod.Post, $"{ApiPrefix}/admin/affiliates/users/batch-rate", new { user_ids = userIds.ToArray(), aff_rebate_rate_percent = ratePercent, clear });

    public Task<JsonElement> UpdateAffiliateUserAsync(long userId, object payload) =>
        SendAsync<JsonElement>(HttpMethod.Put, $"{ApiPrefix}/admin/affiliates/users/{userId}", payload);

    public Task<JsonElement> ClearAffiliateUserAsync(long userId) =>
        SendAsync<JsonElement>(HttpMethod.Delete, $"{ApiPrefix}/admin/affiliates/users/{userId}");

    public Task<PagedEnvelope<AffiliateInviteRecordDto>> GetAffiliateInvitesAsync(int page = 1, int pageSize = 50, string? search = null) =>
        SendAsync<PagedEnvelope<AffiliateInviteRecordDto>>(HttpMethod.Get, BuildAffiliateRecordUrl("invites", page, pageSize, search));

    public Task<PagedEnvelope<AffiliateRebateRecordDto>> GetAffiliateRebatesAsync(int page = 1, int pageSize = 50, string? search = null) =>
        SendAsync<PagedEnvelope<AffiliateRebateRecordDto>>(HttpMethod.Get, BuildAffiliateRecordUrl("rebates", page, pageSize, search));

    public Task<PagedEnvelope<AffiliateTransferRecordDto>> GetAffiliateTransfersAsync(int page = 1, int pageSize = 50, string? search = null) =>
        SendAsync<PagedEnvelope<AffiliateTransferRecordDto>>(HttpMethod.Get, BuildAffiliateRecordUrl("transfers", page, pageSize, search));

    private static string BuildAffiliateRecordUrl(string kind, int page, int pageSize, string? search) =>
        $"{ApiPrefix}/admin/affiliates/{kind}?page={Math.Max(1, page)}&page_size={Math.Clamp(pageSize, 1, 100)}&search={Uri.EscapeDataString(search ?? string.Empty)}";

    /// <summary>为非核心面板功能保留官方 Go API 的完整 JSON 字段。</summary>
    public Task<JsonElement> RequestJsonAsync(HttpMethod method, string relativeUrl, object? body = null) =>
        SendAsync<JsonElement>(method, relativeUrl.StartsWith(ApiPrefix, StringComparison.OrdinalIgnoreCase)
            ? relativeUrl
            : $"{ApiPrefix}/{relativeUrl.TrimStart('/')}", body);

    public Task<JsonElement> GetSetupStatusAsync() =>
        SendAsync<JsonElement>(HttpMethod.Get, "/setup/status");

    public Task<SetupConnectionResultDto> TestSetupDatabaseAsync(SetupDatabaseInput input) =>
        SendAsync<SetupConnectionResultDto>(HttpMethod.Post, "/setup/test-db", input);

    public Task<SetupConnectionResultDto> TestSetupRedisAsync(SetupRedisInput input) =>
        SendAsync<SetupConnectionResultDto>(HttpMethod.Post, "/setup/test-redis", input);

    public Task<SetupInstallResultDto> InstallSetupAsync(SetupInstallInput input) =>
        SendAsync<SetupInstallResultDto>(HttpMethod.Post, "/setup/install", input);

    public Task<GatewayUsageResponse> GetGatewayUsageAsync(string apiKey, DateTimeOffset? start = null, DateTimeOffset? end = null, int days = 30)
    {
        var query = new List<string> { $"days={Math.Clamp(days, 1, 90)}" };
        if (start.HasValue) query.Add($"start_date={Uri.EscapeDataString(start.Value.ToString("yyyy-MM-dd"))}");
        if (end.HasValue) query.Add($"end_date={Uri.EscapeDataString(end.Value.ToString("yyyy-MM-dd"))}");
        var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/usage?{string.Join("&", query)}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey.Trim());
        return SendDirectAsync<GatewayUsageResponse>(request);
    }

    public Task<ModelPlazaResponse> GetModelPlazaAsync() =>
        SendAsync<ModelPlazaResponse>(HttpMethod.Get, $"{ApiPrefix}/model-plaza");

    public async Task<PublicSettingsDto> GetPublicSettingsAsync() =>
        await SendAsync<PublicSettingsDto>(HttpMethod.Get, $"{ApiPrefix}/settings/public");

    public async Task<List<string>> GetCustomPageSlugsAsync()
    {
        var value = await SendAsync<JsonElement>(HttpMethod.Get, $"{ApiPrefix}/pages");
        return ReadJsonArray(value)
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString() ?? string.Empty)
            .Where(x => x.Length > 0)
            .ToList();
    }

    public async Task<string> GetCustomPageMarkdownAsync(string slug)
    {
        using var response = await SendCoreAsync(HttpMethod.Get, $"{ApiPrefix}/pages/{Uri.EscapeDataString(slug)}", null);
        return await response.Content.ReadAsStringAsync();
    }

    public Task<ApiKeyDailyUsageResponse> GetApiKeyDailyUsageAsync(string keyId, int days = 30, string? timezone = null) =>
        SendAsync<ApiKeyDailyUsageResponse>(HttpMethod.Get,
            $"{ApiPrefix}/user/api-keys/{Uri.EscapeDataString(keyId)}/usage/daily?days={Math.Clamp(days, 1, 90)}"
            + (string.IsNullOrWhiteSpace(timezone) ? string.Empty : $"&timezone={Uri.EscapeDataString(timezone)}"));

    public Task<BatchImageJobDto> SubmitBatchImageAsync(string apiKey, BatchImageSubmitRequest input, string? idempotencyKey = null)
    {
        var request = CreateGatewayRequest(HttpMethod.Post, "/v1/images/batches", apiKey, input);
        if (!string.IsNullOrWhiteSpace(idempotencyKey)) request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        return SendDirectAsync<BatchImageJobDto>(request);
    }

    public Task<BatchImageJobDto> GetBatchImageJobAsync(string apiKey, string batchId) =>
        SendDirectAsync<BatchImageJobDto>(CreateGatewayRequest(HttpMethod.Get,
            $"/v1/images/batches/{Uri.EscapeDataString(batchId)}", apiKey));

    public Task<BatchImageListResponse> GetBatchImageJobsAsync(
        string apiKey,
        int limit = 50,
        string? status = null,
        string? cursor = null,
        string? taskName = null,
        string? downloaded = null,
        string? from = null,
        string? to = null)
    {
        var query = new List<string> { $"limit={Math.Clamp(limit, 1, 100)}" };
        if (!string.IsNullOrWhiteSpace(cursor)) query.Add($"cursor={Uri.EscapeDataString(cursor)}");
        if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
        if (!string.IsNullOrWhiteSpace(taskName)) query.Add($"task_name={Uri.EscapeDataString(taskName.Trim())}");
        if (!string.IsNullOrWhiteSpace(downloaded)) query.Add($"downloaded={Uri.EscapeDataString(downloaded)}");
        if (!string.IsNullOrWhiteSpace(from)) query.Add($"from={Uri.EscapeDataString(from)}");
        if (!string.IsNullOrWhiteSpace(to)) query.Add($"to={Uri.EscapeDataString(to)}");
        return SendDirectAsync<BatchImageListResponse>(CreateGatewayRequest(HttpMethod.Get,
            $"/v1/images/batches?{string.Join('&', query)}", apiKey));
    }

    public Task<BatchImageModelsResponse> GetBatchImageModelsAsync(string apiKey) =>
        SendDirectAsync<BatchImageModelsResponse>(CreateGatewayRequest(HttpMethod.Get, "/v1/images/batches/models", apiKey));

    public Task<BatchImageItemsResponse> GetBatchImageItemsAsync(string apiKey, string batchId, string? status = null) =>
        SendDirectAsync<BatchImageItemsResponse>(CreateGatewayRequest(HttpMethod.Get,
            $"/v1/images/batches/{Uri.EscapeDataString(batchId)}/items?limit=500"
            + (string.IsNullOrWhiteSpace(status) ? string.Empty : $"&status={Uri.EscapeDataString(status)}"), apiKey));

    public Task<GatewayDownload> GetBatchImageItemContentAsync(string apiKey, string batchId, string customId, int imageIndex = 0) =>
        DownloadGatewayAsync(CreateGatewayRequest(HttpMethod.Get,
            $"/v1/images/batches/{Uri.EscapeDataString(batchId)}/items/{Uri.EscapeDataString(customId)}/content?image_index={Math.Max(0, imageIndex)}",
            apiKey), $"{customId}-{Math.Max(0, imageIndex) + 1}.png");

    public Task<BatchImageJobDto> CancelBatchImageAsync(string apiKey, string batchId) =>
        SendDirectAsync<BatchImageJobDto>(CreateGatewayRequest(HttpMethod.Post,
            $"/v1/images/batches/{Uri.EscapeDataString(batchId)}/cancel", apiKey));

    public async Task DeleteBatchImageAsync(string apiKey, string batchId)
    {
        using var response = await SendDirectResponseAsync(CreateGatewayRequest(HttpMethod.Delete,
            $"/v1/images/batches/{Uri.EscapeDataString(batchId)}", apiKey));
    }

    public Task<GatewayDownload> DownloadBatchImageAsync(string apiKey, string batchId) =>
        DownloadGatewayAsync(CreateGatewayRequest(HttpMethod.Get,
            $"/v1/images/batches/{Uri.EscapeDataString(batchId)}/download", apiKey), $"{batchId}.zip");

    private HttpRequestMessage CreateGatewayRequest(HttpMethod method, string url, string apiKey, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey.Trim());
        if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
        return request;
    }

    private async Task<HttpResponseMessage> SendDirectResponseAsync(HttpRequestMessage request)
    {
        using (request)
        {
            var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await ReadErrorAsync(response);
                var message = error.Message;
                response.Dispose();
                throw new ApiException(message, response.StatusCode) { Code = error.Code, Metadata = error.Metadata };
            }
            return response;
        }
    }

    private async Task<GatewayDownload> DownloadGatewayAsync(HttpRequestMessage request, string fallbackName)
    {
        using (request)
        using (var response = await http.SendAsync(request))
        {
            if (!response.IsSuccessStatusCode)
            {
                var error = await ReadErrorAsync(response);
                throw new ApiException(error.Message, response.StatusCode) { Code = error.Code, Metadata = error.Metadata };
            }
            var bytes = await response.Content.ReadAsByteArrayAsync();
            var name = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                ?? fallbackName;
            return new GatewayDownload(bytes, response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream", name);
        }
    }

    private async Task<T> SendDirectAsync<T>(HttpRequestMessage request)
    {
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var error = await ReadErrorAsync(response);
            throw new ApiException(error.Message, response.StatusCode) { Code = error.Code, Metadata = error.Metadata };
        }
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var value = document.RootElement.Deserialize<T>(JsonOptions);
        return value ?? throw new ApiException("服务器返回的数据格式不正确。", response.StatusCode);
    }

    private static string BuildDateQuery(DateTimeOffset? start, DateTimeOffset? end)
    {
        var values = new List<string>();
        if (start.HasValue) values.Add($"start_at={Uri.EscapeDataString(start.Value.ToString("O"))}");
        if (end.HasValue) values.Add($"end_at={Uri.EscapeDataString(end.Value.ToString("O"))}");
        return values.Count == 0 ? string.Empty : $"?{string.Join("&", values)}";
    }

    private static List<JsonElement> ReadJsonArray(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Array) return value.EnumerateArray().Select(x => x.Clone()).ToList();
        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            return items.EnumerateArray().Select(x => x.Clone()).ToList();
        return [];
    }

    private static List<AccountTestEventDto> ParseAccountTestEvents(string payload)
    {
        var events = new List<AccountTestEventDto>();
        foreach (var rawLine in payload.Split('\n'))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) continue;
            var json = line[5..].Trim();
            if (json.Length == 0 || string.Equals(json, "[DONE]", StringComparison.Ordinal)) continue;
            try
            {
                var item = JsonSerializer.Deserialize<AccountTestEventDto>(json, JsonOptions);
                if (item is not null) events.Add(item);
            }
            catch (JsonException)
            {
                // Ignore malformed keep-alive/event lines and require a terminal event below.
            }
        }
        return events;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    /// <summary>
    /// Go 后端只接受 API Key 账号的上游计费探测配置，且不接受在创建请求中
    /// 发送 rate-sync 字段。将条件判断集中在客户端，避免不同页面产生不一致的请求体。
    /// </summary>
    private static void AddUpstreamBillingFields(Dictionary<string, object?> payload, AccountInput input)
    {
        if (!IsUpstreamBillingProbeAccount(input.Platform, input.Type))
        {
            return;
        }

        payload["upstream_billing_probe_enabled"] = input.ProbeEnabled;
        if (input.RateSyncEnabled)
        {
            payload["upstream_billing_rate_sync_enabled"] = true;
        }
    }

    public static bool IsUpstreamBillingProbeAccount(string? platform, string? type) =>
        string.Equals(type, "apikey", StringComparison.OrdinalIgnoreCase)
        && platform is "openai" or "anthropic" or "gemini" or "antigravity" or "grok";

    /// <summary>把各 Go OAuth service 的 token DTO 转成可持久化的 credentials。</summary>
    public static Dictionary<string, object?> BuildOAuthCredentials(string platform, IReadOnlyDictionary<string, JsonElement> token)
    {
        var credentials = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        CopyIfPresent(token, credentials, "access_token");
        CopyIfPresent(token, credentials, "refresh_token");
        CopyIfPresent(token, credentials, "token_type");
        CopyIfPresent(token, credentials, "scope");
        CopyIfPresent(token, credentials, "expires_in");
        CopyIfPresent(token, credentials, "expires_at");
        CopyIfPresent(token, credentials, "email");
        CopyIfPresent(token, credentials, "email_address");
        CopyIfPresent(token, credentials, "id_token");
        CopyIfPresent(token, credentials, "client_id");
        CopyIfPresent(token, credentials, "project_id");
        CopyIfPresent(token, credentials, "tier_id");
        CopyIfPresent(token, credentials, "oauth_type");
        CopyIfPresent(token, credentials, "org_uuid");
        CopyIfPresent(token, credentials, "account_uuid");
        CopyIfPresent(token, credentials, "chatgpt_account_id");
        CopyIfPresent(token, credentials, "chatgpt_user_id");
        CopyIfPresent(token, credentials, "organization_id");
        CopyIfPresent(token, credentials, "plan_type");
        CopyIfPresent(token, credentials, "subscription_expires_at");
        CopyIfPresent(token, credentials, "subscription_tier");
        CopyIfPresent(token, credentials, "entitlement_status");
        CopyIfPresent(token, credentials, "sub");
        if (platform.Equals("grok", StringComparison.OrdinalIgnoreCase)
            && !credentials.ContainsKey("base_url"))
        {
            credentials["base_url"] = "https://api.x.ai";
        }

        // Gemini/Antigravity services return provider-specific metadata under extra.
        if (token.TryGetValue("extra", out var extra) && extra.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in extra.EnumerateObject())
            {
                credentials[property.Name] = property.Value.Clone();
            }
        }

        return credentials;
    }

    private static void CopyIfPresent(IReadOnlyDictionary<string, JsonElement> source, Dictionary<string, object?> target, string key)
    {
        if (source.TryGetValue(key, out var value) && value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            target[key] = value.Clone();
        }
    }

    public Task<OfficialOAuthStartDto> StartOpenAIOAuthAsync() =>
        SendAsync<OfficialOAuthStartDto>(HttpMethod.Post, $"{ApiPrefix}/admin/openai/generate-auth-url", new { });

    public Task<OfficialOAuthStartDto> StartAnthropicOAuthAsync(long? proxyId = null) =>
        SendAsync<OfficialOAuthStartDto>(HttpMethod.Post, $"{ApiPrefix}/admin/accounts/generate-auth-url",
            new AnthropicOAuthStartInput { ProxyId = proxyId });

    /// <summary>
    /// Starts the GitHub Copilot device authorization flow. The device token
    /// remains on the Go server; the browser only receives the user code and
    /// verification URL.
    /// </summary>
    public Task<CopilotOAuthFlowDto> StartCopilotOAuthAsync(string name) =>
        StartCopilotOAuthAsync(new CopilotOAuthStartRequest { Name = name.Trim() });

    public Task<CopilotOAuthFlowDto> StartCopilotOAuthAsync(CopilotOAuthStartRequest request) =>
        SendAsync<CopilotOAuthFlowDto>(HttpMethod.Post, $"{ApiPrefix}/admin/openai/copilot/flows", request);

    /// <summary>Polls a server-side GitHub Copilot device authorization flow.</summary>
    public Task<CopilotOAuthFlowDto> PollCopilotOAuthAsync(string flowId) =>
        SendAsync<CopilotOAuthFlowDto>(HttpMethod.Post,
            $"{ApiPrefix}/admin/openai/copilot/flows/{Uri.EscapeDataString(flowId)}/poll");
    public Task CancelCopilotOAuthAsync(string flowId) =>
        SendAsync(HttpMethod.Delete, $"{ApiPrefix}/admin/openai/copilot/flows/{Uri.EscapeDataString(flowId)}");


    public async Task<AccountDto> CreateCopilotAccountAsync(CopilotManualCreateRequest request)
    {
        var account = await SendAsync<GoAccount>(
            HttpMethod.Post,
            $"{ApiPrefix}/admin/openai/copilot/accounts",
            request);
        return AccountDto.From(account);
    }

    public Task<CopilotBillingPatValidationDto> ValidateCopilotBillingPatAsync(CopilotBillingPatValidationRequest request) =>
        SendAsync<CopilotBillingPatValidationDto>(
            HttpMethod.Post,
            $"{ApiPrefix}/admin/accounts/copilot-billing-pat/validate",
            request);

    public Task<OfficialOAuthStartDto> StartGeminiOAuthAsync(string oauthType = "code_assist", string? projectId = null, string? tierId = null) =>
        SendAsync<OfficialOAuthStartDto>(HttpMethod.Post, $"{ApiPrefix}/admin/gemini/oauth/auth-url", new { oauth_type = oauthType, project_id = projectId, tier_id = tierId });

    public Task<OfficialOAuthStartDto> StartAntigravityOAuthAsync() =>
        SendAsync<OfficialOAuthStartDto>(HttpMethod.Post, $"{ApiPrefix}/admin/antigravity/oauth/auth-url", new { });

    public Task<OfficialOAuthStartDto> StartGrokOAuthAsync(string? redirectUri = null) =>
        SendAsync<OfficialOAuthStartDto>(HttpMethod.Post, $"{ApiPrefix}/admin/grok/oauth/auth-url", new { redirect_uri = redirectUri });

    public Task<Dictionary<string, JsonElement>> ExchangeOpenAIOAuthAsync(OAuthExchangeInput input) =>
        SendAsync<Dictionary<string, JsonElement>>(HttpMethod.Post, $"{ApiPrefix}/admin/openai/exchange-code", input);

    public Task<Dictionary<string, JsonElement>> ExchangeAnthropicOAuthAsync(OAuthExchangeInput input, long? proxyId = null) =>
        SendAsync<Dictionary<string, JsonElement>>(HttpMethod.Post, $"{ApiPrefix}/admin/accounts/exchange-code",
            new AnthropicOAuthExchangeInput
            {
                SessionId = input.SessionId,
                Code = input.Code,
                ProxyId = proxyId
            });

    public Task<Dictionary<string, JsonElement>> ExchangeGeminiOAuthAsync(OAuthExchangeInput input, string oauthType = "code_assist", string? tierId = null) =>
        SendAsync<Dictionary<string, JsonElement>>(HttpMethod.Post, $"{ApiPrefix}/admin/gemini/oauth/exchange-code", new
        {
            session_id = input.SessionId,
            code = input.Code,
            state = input.State,
            oauth_type = oauthType,
            tier_id = tierId
        });

    public Task<Dictionary<string, JsonElement>> ExchangeAntigravityOAuthAsync(OAuthExchangeInput input) =>
        SendAsync<Dictionary<string, JsonElement>>(HttpMethod.Post, $"{ApiPrefix}/admin/antigravity/oauth/exchange-code", input);

    public Task<Dictionary<string, JsonElement>> ExchangeGrokOAuthAsync(OAuthExchangeInput input) =>
        SendAsync<Dictionary<string, JsonElement>>(HttpMethod.Post, $"{ApiPrefix}/admin/grok/oauth/exchange-code", input);

    public async Task<AccountDto> CreateAccountFromOAuthAsync(string platform, OAuthExchangeInput input, string name, int concurrency, int priority, List<long> groupIds, AccountInput? settings = null)
    {
        var route = platform switch
        {
            "openai" => "openai/create-from-oauth",
            "grok" => "grok/oauth/create-from-oauth",
            _ => throw new ApiException("该平台不支持直接 OAuth 创建。")
        };
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["session_id"] = input.SessionId, ["code"] = input.Code, ["state"] = input.State,
            ["name"] = name.Trim(), ["concurrency"] = concurrency,
            ["priority"] = priority, ["group_ids"] = groupIds
        };
        if (!string.IsNullOrWhiteSpace(input.RedirectUri)) payload["redirect_uri"] = input.RedirectUri;
        if (settings is not null)
        {
            payload["proxy_id"] = settings.ProxyId;
            payload["load_factor"] = settings.LoadFactor;
            payload["rate_multiplier"] = settings.RateMultiplier;
            payload["expires_at"] = settings.ExpiresAt;
            payload["auto_pause_on_expired"] = settings.AutoPauseOnExpired;
            var credentialExtras = BuildModelRestrictionCredentials(settings, includeEmpty: false);
            if (credentialExtras is not null) payload["credential_extras"] = credentialExtras;
        }
        var account = await SendAsync<GoAccount>(HttpMethod.Post, $"{ApiPrefix}/admin/{route}", payload);
        return AccountDto.From(account);
    }

    private static Dictionary<string, object?>? BuildCredentials(AccountInput input, bool requireCredentials)
    {
        ValidateCopilotEditBillingIdentity(input);
        var isCopilotEdit = input.IsEditing && input.IsCopilotProfile;
        var credentials = isCopilotEdit
            ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            : ParseObject(input.CredentialsJson, "账号凭据") ?? new(StringComparer.OrdinalIgnoreCase);
        if (!isCopilotEdit && !string.IsNullOrWhiteSpace(input.ApiKey)) credentials["api_key"] = input.ApiKey.Trim();
        if (!isCopilotEdit && !string.IsNullOrWhiteSpace(input.AccessToken)) credentials["access_token"] = input.AccessToken.Trim();
        if (!isCopilotEdit && !string.IsNullOrWhiteSpace(input.RefreshToken)) credentials["refresh_token"] = input.RefreshToken.Trim();
        if (!isCopilotEdit && !string.IsNullOrWhiteSpace(input.BaseUrl)) credentials["base_url"] = input.BaseUrl.Trim();
        if (input.IsCopilotProfile)
        {
            if (!input.IsEditing) credentials["oauth_profile"] = AccountProviderIdentity.GitHubCopilotProfile;
            credentials["billing_username"] = input.BillingUsername.Trim();
            if (!string.IsNullOrWhiteSpace(input.BillingPat))
                credentials["billing_pat"] = input.BillingPat.Trim();
        }
        if (input.Platform is "kimi" or "zhipu" or "deepseek")
        {
            credentials["account_mode"] = input.AccountMode;
            credentials["api_protocol"] = input.ApiProtocol;
            if (string.Equals(input.ApiProtocol, "adaptive", StringComparison.OrdinalIgnoreCase))
            {
                var protocolBaseUrls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(input.AdaptiveChatCompletionsBaseUrl))
                    protocolBaseUrls["chat_completions"] = input.AdaptiveChatCompletionsBaseUrl.Trim();
                if (!string.IsNullOrWhiteSpace(input.AdaptiveAnthropicBaseUrl))
                    protocolBaseUrls["anthropic"] = input.AdaptiveAnthropicBaseUrl.Trim();
                if (input.Platform == "deepseek" && !string.IsNullOrWhiteSpace(input.AdaptiveResponsesBaseUrl))
                    protocolBaseUrls["responses"] = input.AdaptiveResponsesBaseUrl.Trim();
                if (protocolBaseUrls.Count > 0) credentials["api_base_urls"] = protocolBaseUrls;
                if (protocolBaseUrls.TryGetValue("chat_completions", out var chatBaseUrl))
                    credentials["base_url"] = chatBaseUrl;
            }
        }
        var modelRestrictions = BuildModelRestrictionCredentials(input, includeEmpty: input.IsEditing);
        if (modelRestrictions is not null)
        {
            foreach (var (key, value) in modelRestrictions) credentials[key] = value;
        }
        if (requireCredentials && credentials.Count == 0)
        {
            throw new ApiException("账号凭据不能为空。", HttpStatusCode.BadRequest);
        }
        return credentials.Count == 0 ? null : credentials;
    }

    private static void ValidateCopilotEditBillingIdentity(AccountInput input)
    {
        if (!input.IsEditing || !input.IsCopilotProfile) return;
        var billingPatWillRemain = input.HasBillingPat || !string.IsNullOrWhiteSpace(input.BillingPat);
        if (billingPatWillRemain && string.IsNullOrWhiteSpace(input.BillingUsername))
            throw new ApiException("已配置或输入 Billing PAT 时必须填写 GitHub Billing 用户名。", HttpStatusCode.BadRequest);
    }

    private static void AddCopilotBillingExtra(Dictionary<string, object?> extra, AccountInput input)
    {
        if (!input.IsCopilotProfile) return;
        extra["billing_credit_limit"] = input.BillingCreditLimit;
        extra["billing_safety_margin"] = input.BillingSafetyMargin;
        if (input.BillingAutoPauseDisabled) extra["billing_auto_pause_disabled"] = true;
        else extra.Remove("billing_auto_pause_disabled");
    }

    private static Dictionary<string, object?>? BuildModelRestrictionCredentials(AccountInput input, bool includeEmpty)
    {
        var validationError = AccountModelRestrictions.Validate(input);
        if (validationError is not null)
            throw new ApiException(validationError, HttpStatusCode.BadRequest);
        return AccountModelRestrictions.BuildCredentialPatch(input, includeEmpty);
    }

    private static Dictionary<string, object?>? ParseObject(string? json, string label)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(json, JsonOptions);
            return parsed ?? new(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException ex)
        {
            throw new ApiException($"{label}必须是有效的 JSON 对象。", HttpStatusCode.BadRequest, ex);
        }
    }

    private static void ApplyChannelInput(Dictionary<string, object?> payload, ChannelInput input, bool includeStatus, bool active = true)
    {
        var timePricingError = ChannelTimePricingRules.ValidateModelPricingJson(input.ModelPricingJson);
        if (timePricingError is not null)
            throw new ApiException(timePricingError, HttpStatusCode.BadRequest);
        payload["name"] = input.Name.Trim(); payload["description"] = input.Description.Trim();
        payload["group_ids"] = input.GroupIds; payload["restrict_models"] = input.RestrictModels;
        payload["billing_model_source"] = input.BillingModelSource;
        payload["features"] = input.Features.Trim();
        ApplyJsonOrRemove(payload, "features_config", input.FeaturesConfigJson, "渠道 features_config");
        ApplyJsonOrRemove(payload, "model_pricing", input.ModelPricingJson, "渠道 model_pricing");
        ApplyJsonOrRemove(payload, "model_mapping", input.ModelMappingJson, "渠道 model_mapping");
        payload["apply_pricing_to_account_stats"] = input.ApplyPricingToAccountStats;
        ApplyJsonOrRemove(payload, "account_stats_pricing_rules", input.AccountStatsPricingRulesJson, "渠道统计定价规则");
        if (includeStatus) payload["status"] = active ? "active" : "disabled";
    }

    private static void ApplyJsonIfPresent(Dictionary<string, object?> payload, string key, string? json, string label)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        try { payload[key] = JsonSerializer.Deserialize<object>(json, JsonOptions); }
        catch (JsonException ex) { throw new ApiException($"{label}必须是有效 JSON。", HttpStatusCode.BadRequest, ex); }
    }

    private static void ApplyJsonOrRemove(Dictionary<string, object?> payload, string key, string? json, string label)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            payload.Remove(key);
            return;
        }
        ApplyJsonIfPresent(payload, key, json, label);
    }

    public Task<PagedResult<UsageRecordDto>> GetUsageAsync(int page, int pageSize) =>
        GetUsageAsync(new AdminUsageQuery { Page = page, PageSize = pageSize });

    public async Task<PagedResult<UsageRecordDto>> GetUsageAsync(AdminUsageQuery filter)
    {
        var raw = await SendAsync<PagedEnvelope<GoUsageLog>>(HttpMethod.Get,
            $"{ApiPrefix}/admin/usage?{BuildAdminUsageQuery(filter)}");
        return PagedResult<UsageRecordDto>.From(raw);
    }

    private static string BuildAdminUsageQuery(AdminUsageQuery value)
    {
        var query = new List<string>
        {
            $"page={Math.Max(1, value.Page)}",
            $"page_size={Math.Clamp(value.PageSize, 1, 100)}",
            $"sort_by={Uri.EscapeDataString(string.IsNullOrWhiteSpace(value.SortBy) ? "created_at" : value.SortBy)}",
            $"sort_order={(string.Equals(value.SortOrder, "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc")}"
        };
        AddUserUsageQuery(query, "start_date", value.StartDate);
        AddUserUsageQuery(query, "end_date", value.EndDate);
        AddUserUsageQuery(query, "timezone", value.Timezone);
        AddUserUsageQuery(query, "model", value.Model);
        AddUserUsageQuery(query, "request_type", value.RequestType);
        AddUserUsageQuery(query, "billing_mode", value.BillingMode);
        if (value.UserId is > 0) query.Add($"user_id={value.UserId.Value}");
        if (value.ApiKeyId is > 0) query.Add($"api_key_id={value.ApiKeyId.Value}");
        if (value.AccountId is > 0) query.Add($"account_id={value.AccountId.Value}");
        if (value.GroupId is > 0) query.Add($"group_id={value.GroupId.Value}");
        if (value.BillingType.HasValue) query.Add($"billing_type={value.BillingType.Value}");
        return string.Join("&", query);
    }

    public async Task<PagedResult<UsageRecordDto>> GetMyUsageAsync(int page, int pageSize)
    {
        var raw = await SendAsync<PagedEnvelope<GoUsageLog>>(HttpMethod.Get, $"{ApiPrefix}/usage?page={page}&page_size={pageSize}");
        return PagedResult<UsageRecordDto>.From(raw);
    }

    public Task<List<GoGroup>> GetMyAvailableGroupsAsync() =>
        SendAsync<List<GoGroup>>(HttpMethod.Get, $"{ApiPrefix}/groups/available");

    public Task<PagedEnvelope<GoUsageLog>> GetMyUsageLogsAsync(UserUsageQuery query) =>
        SendAsync<PagedEnvelope<GoUsageLog>>(HttpMethod.Get,
            $"{ApiPrefix}/usage?{BuildUserUsageQuery(query, includePagination: true)}");

    public Task<UserUsageStatsDto> GetMyUsageStatsAsync(UserUsageQuery query) =>
        SendAsync<UserUsageStatsDto>(HttpMethod.Get,
            $"{ApiPrefix}/usage/stats?{BuildUserUsageQuery(query, includePagination: false)}");

    public Task<UserUsageModelsResponseDto> GetMyUsageModelsAsync(UserUsageQuery query) =>
        SendAsync<UserUsageModelsResponseDto>(HttpMethod.Get,
            $"{ApiPrefix}/usage/dashboard/models?{BuildUserUsageQuery(query, includePagination: false)}&model_source=requested");

    public Task<UserUsageSnapshotDto> GetMyUsageSnapshotAsync(UserUsageQuery query, string granularity) =>
        SendAsync<UserUsageSnapshotDto>(HttpMethod.Get,
            $"{ApiPrefix}/usage/dashboard/snapshot-v2?{BuildUserUsageQuery(query, includePagination: false)}"
            + $"&granularity={(string.Equals(granularity, "hour", StringComparison.OrdinalIgnoreCase) ? "hour" : "day")}"
            + "&include_trend=true&include_model_stats=false&include_group_stats=true");

    public Task<PagedEnvelope<UserErrorRequestDto>> GetMyErrorRequestsAsync(UserErrorRequestQuery query) =>
        SendAsync<PagedEnvelope<UserErrorRequestDto>>(HttpMethod.Get,
            $"{ApiPrefix}/usage/errors?{BuildUserErrorQuery(query)}");

    public Task<UserErrorRequestDetailDto> GetMyErrorRequestDetailAsync(long id) =>
        SendAsync<UserErrorRequestDetailDto>(HttpMethod.Get, $"{ApiPrefix}/usage/errors/{id}");

    private static string BuildUserUsageQuery(UserUsageQuery value, bool includePagination)
    {
        var query = new List<string>();
        if (includePagination)
        {
            query.Add($"page={Math.Max(1, value.Page)}");
            query.Add($"page_size={Math.Clamp(value.PageSize, 1, 100)}");
            query.Add($"sort_by={Uri.EscapeDataString(string.IsNullOrWhiteSpace(value.SortBy) ? "created_at" : value.SortBy)}");
            query.Add($"sort_order={(string.Equals(value.SortOrder, "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc")}");
        }
        AddUserUsageQuery(query, "start_date", value.StartDate);
        AddUserUsageQuery(query, "end_date", value.EndDate);
        AddUserUsageQuery(query, "timezone", value.Timezone);
        AddUserUsageQuery(query, "model", value.Model);
        AddUserUsageQuery(query, "request_type", value.RequestType);
        AddUserUsageQuery(query, "billing_mode", value.BillingMode);
        if (value.ApiKeyId.HasValue) query.Add($"api_key_id={value.ApiKeyId.Value}");
        if (value.GroupId.HasValue) query.Add($"group_id={value.GroupId.Value}");
        if (value.BillingType.HasValue) query.Add($"billing_type={value.BillingType.Value}");
        return string.Join("&", query);
    }

    private static string BuildUserErrorQuery(UserErrorRequestQuery value)
    {
        var query = new List<string>
        {
            $"page={Math.Max(1, value.Page)}",
            $"page_size={Math.Clamp(value.PageSize, 1, 100)}",
            $"sort_by={Uri.EscapeDataString(string.IsNullOrWhiteSpace(value.SortBy) ? "created_at" : value.SortBy)}",
            $"sort_order={(string.Equals(value.SortOrder, "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc")}"
        };
        AddUserUsageQuery(query, "start_date", value.StartDate);
        AddUserUsageQuery(query, "end_date", value.EndDate);
        AddUserUsageQuery(query, "timezone", value.Timezone);
        AddUserUsageQuery(query, "model", value.Model);
        AddUserUsageQuery(query, "category", value.Category);
        if (value.ApiKeyId.HasValue) query.Add($"api_key_id={value.ApiKeyId.Value}");
        if (value.StatusCode.HasValue) query.Add($"status_code={value.StatusCode.Value}");
        return string.Join("&", query);
    }

    private static void AddUserUsageQuery(List<string> query, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) query.Add($"{key}={Uri.EscapeDataString(value.Trim())}");
    }

    private async Task SendAsync(
        HttpMethod method,
        string url,
        object? body = null,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        using var response = await SendCoreAsync(method, url, body, headers: headers);
        if (response.Content.Headers.ContentLength is > 0)
        {
            _ = await response.Content.ReadAsStringAsync();
        }
    }

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string url,
        object? body = null,
        string[]? envelopeKeys = null,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        using var response = await SendCoreAsync(method, url, body, headers: headers);
        if (response.Content.Headers.ContentLength == 0)
        {
            throw new ApiException("服务器未返回所需数据。", response.StatusCode);
        }

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);
            var payload = Unwrap(document.RootElement, envelopeKeys);
            var result = payload.Deserialize<T>(JsonOptions);
            return result ?? throw new ApiException("服务器返回的数据格式不正确。", response.StatusCode);
        }
        catch (ApiException)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or HttpRequestException or IOException)
        {
            throw new ApiException("服务器返回的数据格式不正确。", response.StatusCode, ex);
        }
    }

    private async Task<HttpResponseMessage> SendCoreAsync(
        HttpMethod method,
        string url,
        object? body,
        bool allowRefresh = true,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        var accessToken = await GetTokenAsync(AccessTokenKey);
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        }
        if (headers is not null)
        {
            foreach (var (name, value) in headers)
            {
                request.Headers.TryAddWithoutValidation(name, value);
            }
        }

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException("无法连接到服务，请检查网络后重试。", null, ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new ApiException("请求超时，请稍后重试。", null, ex);
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized
            && allowRefresh
            && !string.Equals(url, $"{ApiPrefix}/auth/login", StringComparison.Ordinal)
            && !string.Equals(url, $"{ApiPrefix}/auth/register", StringComparison.Ordinal)
            && !string.Equals(url, $"{ApiPrefix}/auth/refresh", StringComparison.Ordinal))
        {
            response.Dispose();
            if (await RefreshTokensAsync())
            {
                return await SendCoreAsync(method, url, body, false, headers);
            }
            Unauthorized?.Invoke();
            throw new ApiException("登录已失效，请重新登录。", HttpStatusCode.Unauthorized);
        }

        var statusCode = response.StatusCode;
        var errorPayload = await ReadErrorAsync(response);
        response.Dispose();
        throw new ApiException(errorPayload.Message, statusCode) { Code = errorPayload.Code, Metadata = errorPayload.Metadata };
    }

    private static JsonElement Unwrap(JsonElement root, string[]? envelopeKeys)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return root;
        }

        var keys = new List<string> { "data", "result" };
        if (envelopeKeys is not null)
        {
            keys.AddRange(envelopeKeys);
        }

        foreach (var key in keys)
        {
            if (root.TryGetProperty(key, out var nested) && nested.ValueKind is not JsonValueKind.Null)
            {
                // Paginated/list endpoints commonly return data:{items:[...]};
                // honor an explicitly requested item envelope after unwrapping data.
                if (envelopeKeys is not null)
                {
                    foreach (var itemKey in envelopeKeys)
                    {
                        if (nested.ValueKind == JsonValueKind.Object
                            && nested.TryGetProperty(itemKey, out var item)
                            && item.ValueKind is not JsonValueKind.Null)
                        {
                            return item;
                        }
                    }
                }
                return nested;
            }
        }

        return root;
    }

    private static async Task<ApiErrorPayload> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(text))
            {
                return new(DefaultError(response.StatusCode), null, null);
            }

            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            string? code = null;
            if (root.TryGetProperty("code", out var codeElement) && codeElement.ValueKind == JsonValueKind.String)
            {
                code = codeElement.GetString();
            }
            Dictionary<string, string>? metadata = null;
            if (root.TryGetProperty("metadata", out var metadataElement) && metadataElement.ValueKind == JsonValueKind.Object)
            {
                metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in metadataElement.EnumerateObject()) metadata[property.Name] = property.Value.ToString();
            }
            foreach (var name in new[] { "message", "error", "detail" })
            {
                if (!root.TryGetProperty(name, out var value))
                {
                    continue;
                }

                if (value.ValueKind == JsonValueKind.String)
                {
                    return new(value.GetString() ?? DefaultError(response.StatusCode), code, metadata);
                }

                if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("message", out var nested))
                {
                    return new(nested.GetString() ?? DefaultError(response.StatusCode), code, metadata);
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            // Fall back to a concise status-specific message.
        }

        return new(DefaultError(response.StatusCode), null, null);
    }

    private static string DefaultError(HttpStatusCode status) => status switch
    {
        HttpStatusCode.BadRequest => "提交的数据不正确，请检查后重试。",
        HttpStatusCode.Unauthorized => "登录已失效，请重新登录。",
        HttpStatusCode.Forbidden => "当前账户无权执行此操作。",
        HttpStatusCode.NotFound => "请求的数据不存在或已被删除。",
        HttpStatusCode.Conflict => "数据发生冲突，请刷新后重试。",
        _ => "服务暂时不可用，请稍后重试。"
    };

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new UnixMillisecondsDateTimeOffsetConverter());
        return options;
    }
}

internal sealed class UnixMillisecondsDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var milliseconds))
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
        }
        if (reader.TokenType == JsonTokenType.String && DateTimeOffset.TryParse(reader.GetString(), out var value))
        {
            return value;
        }
        throw new JsonException("Expected a Unix millisecond timestamp or ISO date.");
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.ToUnixTimeMilliseconds());
}

internal sealed record ApiErrorPayload(string Message, string? Code, Dictionary<string, string>? Metadata);

public sealed class ApiException(string message, HttpStatusCode? statusCode = null, Exception? innerException = null)
    : Exception(message, innerException)
{
    public HttpStatusCode? StatusCode { get; } = statusCode;
    public string? Code { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

public sealed record GatewayDownload(byte[] Content, string ContentType, string FileName);
