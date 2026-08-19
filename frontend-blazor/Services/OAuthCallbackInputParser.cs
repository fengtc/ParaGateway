namespace ParaGateway.Frontend.Services;

public sealed record OAuthCallbackInputValues(string Code, string State, string SessionId);

public static class OAuthCallbackInputParser
{
    public static OAuthCallbackInputValues Normalize(
        string? callbackUrl,
        string? code,
        string? state,
        string? sessionId)
    {
        var normalizedCode = code?.Trim() ?? string.Empty;
        var normalizedState = state?.Trim() ?? string.Empty;
        var normalizedSessionId = sessionId?.Trim() ?? string.Empty;

        var callbackInput = !string.IsNullOrWhiteSpace(callbackUrl)
            ? callbackUrl.Trim()
            : LooksLikeCallbackInput(normalizedCode) ? normalizedCode : null;

        if (callbackInput is not null)
        {
            var query = ParseCallbackInput(callbackInput);
            if (!query.TryGetValue("code", out var callbackCode) || string.IsNullOrWhiteSpace(callbackCode))
            {
                throw new FormatException("回调信息中没有找到授权码 code，请粘贴包含 code 参数的完整回调 URL。");
            }

            normalizedCode = callbackCode.Trim();
            if (query.TryGetValue("state", out var callbackState) && !string.IsNullOrWhiteSpace(callbackState))
            {
                normalizedState = callbackState.Trim();
            }
            if (query.TryGetValue("session_id", out var callbackSessionId) && !string.IsNullOrWhiteSpace(callbackSessionId))
            {
                normalizedSessionId = callbackSessionId.Trim();
            }
        }

        if (LooksLikeCallbackInput(normalizedCode))
        {
            throw new FormatException("授权码 code 仍是 URL 或查询串，未能提取有效 code，请重新粘贴完整回调 URL。");
        }

        return new OAuthCallbackInputValues(normalizedCode, normalizedState, normalizedSessionId);
    }

    private static Dictionary<string, string> ParseCallbackInput(string value)
    {
        var queryString = ExtractQueryString(value.Trim());
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var pair in queryString.TrimStart('?', '#').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);
                var key = Decode(parts[0]);
                var parsedValue = parts.Length == 1 ? string.Empty : Decode(parts[1]);
                if (!string.IsNullOrWhiteSpace(key)) values[key] = parsedValue;
            }
        }
        catch (UriFormatException ex)
        {
            throw new FormatException("回调 URL 的参数编码不正确，请重新复制完整回调地址。", ex);
        }

        return values;
    }

    private static string ExtractQueryString(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            if (!string.IsNullOrWhiteSpace(uri.Query)) return uri.Query;
            if (!string.IsNullOrWhiteSpace(uri.Fragment) && uri.Fragment.Contains('=')) return uri.Fragment;
            throw new FormatException("完整回调 URL 中没有查询参数，请重新复制包含 code 和 state 的地址。");
        }

        var queryIndex = value.IndexOf('?');
        if (queryIndex >= 0)
        {
            return value[(queryIndex + 1)..].Split('#', 2)[0];
        }

        if (value.StartsWith('?') || value.StartsWith('#') || value.StartsWith("code=", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        throw new FormatException("完整回调 URL 格式不正确，请粘贴浏览器显示的完整 localhost 回调地址。");
    }

    private static bool LooksLikeCallbackInput(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var candidate = value.Trim();
        return candidate.Contains("://", StringComparison.Ordinal)
            || candidate.StartsWith('?')
            || candidate.StartsWith('#')
            || candidate.StartsWith('/')
            || candidate.StartsWith("code=", StringComparison.OrdinalIgnoreCase)
            || candidate.Contains("?code=", StringComparison.OrdinalIgnoreCase)
            || candidate.Contains("&code=", StringComparison.OrdinalIgnoreCase);
    }

    private static string Decode(string value) => Uri.UnescapeDataString(value.Replace('+', ' '));
}
