using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class SecurityHeadersTests
{
    [Fact]
    public void StaticHeadersUseRestrictedBlazorWebAssemblyPolicy()
    {
        var lines = File.ReadAllLines(FindHeadersFile())
            .Select(line => line.Trim())
            .ToArray();

        Assert.Contains("Strict-Transport-Security: max-age=31536000", lines);

        var contentSecurityPolicy = Assert.Single(
            lines,
            line => line.StartsWith("Content-Security-Policy:", StringComparison.Ordinal));
        var directives = ParseDirectives(contentSecurityPolicy["Content-Security-Policy:".Length..]);

        Assert.Equal(["'self'"], directives["default-src"]);
        Assert.Equal(["'self'"], directives["base-uri"]);
        Assert.Equal(["'none'"], directives["object-src"]);
        Assert.Equal(["'none'"], directives["frame-ancestors"]);
        Assert.Equal(["'self'"], directives["form-action"]);
        Assert.Contains("'self'", directives["font-src"]);
        Assert.Contains("'self'", directives["connect-src"]);
        Assert.Contains("data:", directives["img-src"]);
        Assert.Contains("'unsafe-inline'", directives["style-src"]);

        var scriptSources = directives["script-src"];
        Assert.Contains("'self'", scriptSources);
        Assert.Contains("'unsafe-inline'", scriptSources);
        Assert.Contains("'wasm-unsafe-eval'", scriptSources);
        Assert.Contains("https://challenges.cloudflare.com", scriptSources);
        Assert.Contains("https://turing.captcha.qcloud.com", scriptSources);
        Assert.Contains("https://ca.turing.captcha.qcloud.com", scriptSources);
        Assert.Contains("https://o.alicdn.com", scriptSources);
        Assert.DoesNotContain("'unsafe-eval'", scriptSources);

        var allSources = directives.Values.SelectMany(values => values).ToArray();
        Assert.DoesNotContain("*", allSources);
        Assert.DoesNotContain("ws:", allSources);
        Assert.DoesNotContain("wss:", allSources);
        Assert.DoesNotContain("https://*.stripe.com", allSources);
        Assert.DoesNotContain("https://checkout.airwallex.com", allSources);
    }

    private static string FindHeadersFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "wwwroot", "_headers");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate the Blazor static asset _headers file.");
    }

    private static IReadOnlyDictionary<string, string[]> ParseDirectives(string policy)
    {
        return policy
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(directive => directive.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToDictionary(parts => parts[0], parts => parts[1..], StringComparer.Ordinal);
    }
}
