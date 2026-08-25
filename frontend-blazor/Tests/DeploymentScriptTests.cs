using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class DeploymentScriptTests
{
    [Fact]
    public void CandidateDeploymentEnforcesDataStoreIsolation()
    {
        var script = ReadSource("deploy", "production", "deploy-candidate.sh");
        var caddy = ReadSource("deploy", "production", "production.Candidate.Caddyfile.example");

        Assert.Contains("production_config=${PRODUCTION_CONFIG_FILE:-/etc/paragateway/production-config.yaml}", script, StringComparison.Ordinal);
        Assert.Contains("candidate REDIS_DB must be", script, StringComparison.Ordinal);
        Assert.Contains("unset DATABASE_HOST DATABASE_DBNAME REDIS_DB", script, StringComparison.Ordinal);
        Assert.Contains("candidate DB target equals production", script, StringComparison.Ordinal);
        Assert.Contains("candidate REDIS_DB equals production", script, StringComparison.Ordinal);
        Assert.Contains("install -o root -g sub2api -m 640 \"$production_config\"", script, StringComparison.Ordinal);
        Assert.Contains("DATA_DIR=/var/lib/paragateway-backend-%s", script, StringComparison.Ordinal);
        Assert.Contains("EnvironmentFile=/etc/paragateway/%s/data.env", script, StringComparison.Ordinal);
        Assert.Contains("ReadWritePaths=\\nReadWritePaths=/var/lib/paragateway-backend-%s", script, StringComparison.Ordinal);
        Assert.Contains("sudo -u sub2api /usr/bin/caddy validate", script, StringComparison.Ordinal);
        Assert.Contains("systemctl start \"paragateway-backend@$release.service\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("systemctl enable --now", script, StringComparison.Ordinal);
        Assert.Contains("reverse_proxy 127.0.0.1:8284 {", caddy, StringComparison.Ordinal);
        Assert.Contains("flush_interval -1", caddy, StringComparison.Ordinal);
    }

    [Fact]
    public void FrontendArchiveRequiresAndTemporarilyInjectsDevExpressLicense()
    {
        var script = ReadSource("deploy", "production", "build-frontend-archive.ps1");

        Assert.Contains("DevExpress_License", script, StringComparison.Ordinal);
        Assert.Contains("DEVEXPRESS_LICENSE_FILE", script, StringComparison.Ordinal);
        Assert.Contains("DevExpress\\DevExpress_License.txt", script, StringComparison.Ordinal);
        Assert.Contains("DevExpress 许可证为空", script, StringComparison.Ordinal);
        Assert.Contains("DX1000|DX1001|DX1002|For evaluation purposes only", script, StringComparison.Ordinal);
        Assert.Contains("DevExpress 许可证未被构建接受", script, StringComparison.Ordinal);
        Assert.Contains("dotnet clean", script, StringComparison.Ordinal);
        Assert.Contains("Remove-Item -LiteralPath $publishLog", script, StringComparison.Ordinal);
        Assert.Contains("前端归档生成失败", script, StringComparison.Ordinal);
        Assert.Contains("SetEnvironmentVariable(\"DevExpress_License\", $licenseValue, \"Process\")", script, StringComparison.Ordinal);
        Assert.Contains("finally", script, StringComparison.Ordinal);
        Assert.Contains("SetEnvironmentVariable(\"DevExpress_License\", $restoreLicenseValue, \"Process\")", script, StringComparison.Ordinal);
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
