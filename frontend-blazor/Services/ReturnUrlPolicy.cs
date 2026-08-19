namespace ParaGateway.Frontend.Services;

public static class ReturnUrlPolicy
{
    private const int MaxReturnUrlLength = 2048;

    public static string GetSafeLocalPath(string? candidate, string baseUri, string fallback = "/") =>
        TryGetSafeLocalPath(candidate, baseUri, out var safePath) ? safePath : fallback;

    public static bool TryGetSafeLocalPath(string? candidate, string baseUri, out string safePath)
    {
        safePath = string.Empty;
        if (string.IsNullOrWhiteSpace(candidate)
            || candidate.Length > MaxReturnUrlLength
            || !Uri.TryCreate(baseUri, UriKind.Absolute, out var applicationBase)
            || applicationBase.Scheme is not ("http" or "https"))
        {
            return false;
        }

        var decoded = candidate;
        for (var i = 0; i <= candidate.Length; i++)
        {
            if (!HasSafePathPrefix(decoded) || ContainsUnsafeCharacters(decoded))
            {
                return false;
            }

            string next;
            try
            {
                next = Uri.UnescapeDataString(decoded);
            }
            catch (UriFormatException)
            {
                return false;
            }

            if (string.Equals(next, decoded, StringComparison.Ordinal))
            {
                break;
            }

            decoded = next;
            if (i == candidate.Length)
            {
                return false;
            }
        }

        if (!Uri.TryCreate(applicationBase, candidate, out var resolved)
            || !HasSameOrigin(applicationBase, resolved)
            || resolved.UserInfo.Length > 0)
        {
            return false;
        }

        safePath = candidate;
        return true;
    }

    private static bool HasSafePathPrefix(string value) =>
        value.Length > 0
        && value[0] == '/'
        && (value.Length == 1 || value[1] is not ('/' or '\\'));

    private static bool ContainsUnsafeCharacters(string value) =>
        value.Any(character => character == '\\' || char.IsControl(character));

    private static bool HasSameOrigin(Uri applicationBase, Uri target) =>
        string.Equals(applicationBase.Scheme, target.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(applicationBase.IdnHost, target.IdnHost, StringComparison.OrdinalIgnoreCase)
        && applicationBase.Port == target.Port;
}
