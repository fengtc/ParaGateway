using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class DeploymentScriptTests
{
    [Fact]
    public void CandidateDeploymentEnforcesDataStoreIsolation()
    {
        var script = ReadSource("deploy", "production", "deploy-candidate.sh");
        var caddy = ReadSource("deploy", "production", "production.Candidate.Caddyfile.example");
        var backendUnit = ReadSource("deploy", "production", "paragateway-backend@.service");
        var verify = ReadSource("deploy", "production", "verify-candidate.sh");

        Assert.Contains("production_config=${PRODUCTION_CONFIG_FILE:-/etc/paragateway/production-config.yaml}", script, StringComparison.Ordinal);
        Assert.Contains("candidate REDIS_DB must be", script, StringComparison.Ordinal);
        Assert.Contains("unset DATABASE_HOST DATABASE_DBNAME REDIS_DB", script, StringComparison.Ordinal);
        Assert.Contains("candidate DB target equals production", script, StringComparison.Ordinal);
        Assert.Contains("candidate REDIS_DB equals production", script, StringComparison.Ordinal);
        Assert.Contains("PRODUCTION_COMMIT", script, StringComparison.Ordinal);
        Assert.Contains("backend migrations differ from production", script, StringComparison.Ordinal);
        Assert.Contains("ALLOW_CANDIDATE_MIGRATIONS", script, StringComparison.Ordinal);
        Assert.Contains("candidate-migrations-only", script, StringComparison.Ordinal);
        Assert.Contains("candidate port $port is already in use", script, StringComparison.Ordinal);
        Assert.Contains("DEVEXPRESS_PACKAGES_DIR", script, StringComparison.Ordinal);
        Assert.Contains("DEVEXPRESS_LICENSE_FILE", script, StringComparison.Ordinal);
        Assert.Contains(".NET 10 SDK is required", script, StringComparison.Ordinal);
        Assert.Contains("dotnet restore", script, StringComparison.Ordinal);
        Assert.Contains("dotnet publish", script, StringComparison.Ordinal);
        Assert.Contains("DevExpress_License=", script, StringComparison.Ordinal);
        Assert.Contains("frontend WASM does not contain the DevExpress license marker", script, StringComparison.Ordinal);
        Assert.DoesNotContain("FRONTEND_ARCHIVE", script, StringComparison.Ordinal);
        Assert.Contains("release-commit.txt", script, StringComparison.Ordinal);
        Assert.Contains("merge-base --is-ancestor", script, StringComparison.Ordinal);
        Assert.Contains("commit is not published on origin/main", script, StringComparison.Ordinal);
        Assert.Contains("server source worktree is not clean", script, StringComparison.Ordinal);
        Assert.Contains("release name already exists", script, StringComparison.Ordinal);
        Assert.Contains("another ParaGateway release operation is running", script, StringComparison.Ordinal);
        Assert.Contains("release has stale systemd drop-ins", script, StringComparison.Ordinal);
        Assert.Contains("EnvironmentFiles --value paragateway-backend.service", script, StringComparison.Ordinal);
        Assert.Contains("FragmentPath --value paragateway-backend.service", script, StringComparison.Ordinal);
        Assert.Contains("PRODUCTION_ENV_FILE is not used by the active production backend", script, StringComparison.Ordinal);
        Assert.Contains("PRODUCTION_CONFIG_FILE is not used by the active production backend", script, StringComparison.Ordinal);
        Assert.Contains("$release/candidate.env", script, StringComparison.Ordinal);
        Assert.Contains("$release/production.env", script, StringComparison.Ordinal);
        Assert.Contains("$release/production-env-source", script, StringComparison.Ordinal);
        Assert.Contains("$release/production-config-source", script, StringComparison.Ordinal);
        Assert.Contains("install -o root -g sub2api -m 640 \"$production_config_real\"", script, StringComparison.Ordinal);
        Assert.Contains("DATA_DIR=/var/lib/paragateway-backend-%s", script, StringComparison.Ordinal);
        Assert.Contains("EnvironmentFile=/etc/paragateway/%s/data.env", script, StringComparison.Ordinal);
        Assert.Contains("ReadWritePaths=\\nReadWritePaths=/var/lib/paragateway-backend-%s", script, StringComparison.Ordinal);
        Assert.Contains("sudo -u sub2api /usr/bin/caddy validate", script, StringComparison.Ordinal);
        Assert.Contains("systemctl start \"paragateway-backend@$release.service\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("systemctl enable --now", script, StringComparison.Ordinal);
        Assert.Contains("reverse_proxy 127.0.0.1:8284 {", caddy, StringComparison.Ordinal);
        Assert.Contains("trusted_proxies 127.0.0.1/32", caddy, StringComparison.Ordinal);
        Assert.Contains("header_up Host {upstream_hostport}", caddy, StringComparison.Ordinal);
        Assert.Contains("/responses /responses/*", caddy, StringComparison.Ordinal);
        Assert.Contains("/realtime /realtime/*", caddy, StringComparison.Ordinal);
        Assert.Contains("precompressed br gzip", caddy, StringComparison.Ordinal);
        Assert.Contains("flush_interval -1", caddy, StringComparison.Ordinal);
        Assert.Contains("EnvironmentFile=/etc/paragateway/%i/candidate.env", backendUnit, StringComparison.Ordinal);
        Assert.Contains("Environment=SERVER_TRUSTED_PROXIES=127.0.0.1/32,::1/128", backendUnit, StringComparison.Ordinal);
        Assert.Contains("trusted_proxies 127.0.0.1/32", verify, StringComparison.Ordinal);
        Assert.Contains("SERVER_TRUSTED_PROXIES=127.0.0.1/32,::1/128", verify, StringComparison.Ordinal);
        Assert.Contains("production-env-source", verify, StringComparison.Ordinal);
        Assert.Contains("production-config-source", verify, StringComparison.Ordinal);
        Assert.Contains("backend route $path fell through to the SPA", verify, StringComparison.Ordinal);
        Assert.Contains("candidate frontend commit identity mismatch", verify, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionPromotionUsesDedicatedArtifactsAndSnapshotRollback()
    {
        var caddy = ReadSource("deploy", "production", "production.Caddyfile.example");
        var backendUnit = ReadSource("deploy", "production", "paragateway-backend.production.service.example");
        var gatewayUnit = ReadSource("deploy", "production", "paragateway-gateway.production.service.example");
        var deploy = ReadSource("deploy", "production", "deploy-candidate.sh");
        var promote = ReadSource("deploy", "production", "promote.sh");
        var rollback = ReadSource("deploy", "production", "rollback.sh");

        foreach (var script in new[] { deploy, promote, rollback })
        {
            Assert.Contains("exec 9>/run/lock/paragateway-release.lock", script, StringComparison.Ordinal);
            Assert.Contains("flock -n 9", script, StringComparison.Ordinal);
        }

        Assert.Contains(":8182", caddy, StringComparison.Ordinal);
        Assert.Contains("reverse_proxy 127.0.0.1:8184 {", caddy, StringComparison.Ordinal);
        Assert.Contains("trusted_proxies 127.0.0.1/32", caddy, StringComparison.Ordinal);
        Assert.Contains("/responses /responses/*", caddy, StringComparison.Ordinal);
        Assert.Contains("/api/event_logging/batch", caddy, StringComparison.Ordinal);
        Assert.Contains("EnvironmentFile=/etc/paragateway/%RELEASE%/production.env", backendUnit, StringComparison.Ordinal);
        Assert.Contains("LoadCredential=config.yaml:", backendUnit, StringComparison.Ordinal);
        Assert.Contains("Environment=SERVER_PORT=8184", backendUnit, StringComparison.Ordinal);
        Assert.Contains("Environment=SERVER_TRUSTED_PROXIES=127.0.0.1/32,::1/128", backendUnit, StringComparison.Ordinal);
        Assert.Contains("Caddyfile.production", gatewayUnit, StringComparison.Ordinal);
        Assert.Contains("production.Caddyfile.example", deploy, StringComparison.Ordinal);
        Assert.Contains("paragateway-backend.production.service.example", deploy, StringComparison.Ordinal);
        Assert.Contains("paragateway-gateway.production.service.example", deploy, StringComparison.Ordinal);
        Assert.Contains("rollback-snapshots/pre-$release", promote, StringComparison.Ordinal);
        Assert.Contains("previous-production-snapshot", promote, StringComparison.Ordinal);
        Assert.Contains("promotion was already attempted for this release", promote, StringComparison.Ordinal);
        Assert.Contains("release contains candidate-only migrations", promote, StringComparison.Ordinal);
        Assert.Contains("another ParaGateway release operation is running", promote, StringComparison.Ordinal);
        AssertAppearsBefore(promote, "[ ! -e \"$pointer\" ]", "printf '%s\\n' \"$snapshot\" > \"/etc/paragateway/$release/previous-production-snapshot\"");
        Assert.Contains("verify-candidate.sh", promote, StringComparison.Ordinal);
        Assert.Contains("production environment changed after candidate deployment", promote, StringComparison.Ordinal);
        Assert.Contains("production config changed after candidate deployment", promote, StringComparison.Ordinal);
        Assert.Contains("PRODUCTION_CONFIG_FILE", promote, StringComparison.Ordinal);
        Assert.Contains("EnvironmentFiles --value paragateway-backend.service", promote, StringComparison.Ordinal);
        Assert.Contains("FragmentPath --value paragateway-backend.service", promote, StringComparison.Ordinal);
        Assert.Contains("recorded_production_env", promote, StringComparison.Ordinal);
        Assert.Contains("recorded_production_config", promote, StringComparison.Ordinal);
        Assert.Contains("PRODUCTION_ENV_FILE is not used by the active production backend", promote, StringComparison.Ordinal);
        Assert.Contains("PRODUCTION_CONFIG_FILE is not used by the active production backend", promote, StringComparison.Ordinal);
        Assert.Contains("unsupported systemd drop-ins", promote, StringComparison.Ordinal);
        Assert.Contains("trap restore_previous_production ERR", promote, StringComparison.Ordinal);
        Assert.Contains("promotion failed; restoring previous production units", promote, StringComparison.Ordinal);
        Assert.Contains("for attempt in {1..30}", promote, StringComparison.Ordinal);
        Assert.Contains("http://127.0.0.1:8184/health", promote, StringComparison.Ordinal);
        Assert.Contains("http://127.0.0.1:8182/health", promote, StringComparison.Ordinal);
        Assert.Contains("production frontend commit identity mismatch", promote, StringComparison.Ordinal);
        Assert.Contains("http://127.0.0.1:8182/release-commit.txt", promote, StringComparison.Ordinal);
        Assert.Contains("/etc/paragateway/$release/paragateway-backend.service", promote, StringComparison.Ordinal);
        Assert.DoesNotContain("sed 's/8284/8184", promote, StringComparison.Ordinal);
        Assert.DoesNotContain("systemctl stop paragateway-gateway.service", promote, StringComparison.Ordinal);
        Assert.Contains("previous-production-snapshot", rollback, StringComparison.Ordinal);
        Assert.Contains("invalid rollback snapshot path", rollback, StringComparison.Ordinal);
        Assert.Contains("$snapshot/paragateway-backend.service", rollback, StringComparison.Ordinal);
        Assert.Contains("another ParaGateway release operation is running", rollback, StringComparison.Ordinal);
        Assert.Contains("pre-rollback-$release", rollback, StringComparison.Ordinal);
        Assert.Contains("trap restore_pre_rollback_production ERR", rollback, StringComparison.Ordinal);
        Assert.Contains("rollback failed; restoring the units active before rollback", rollback, StringComparison.Ordinal);
        AssertAppearsBefore(rollback, "trap restore_pre_rollback_production ERR", "install -o root -g root -m 644 \"$snapshot/paragateway-backend.service\"");
        Assert.DoesNotContain("paragateway-backend@.service >", rollback, StringComparison.Ordinal);
        Assert.DoesNotContain("systemctl stop paragateway-gateway.service", rollback, StringComparison.Ordinal);
    }

    [Fact]
    public void FrontendArchiveRequiresAndTemporarilyInjectsDevExpressLicense()
    {
        var script = ReadSource("deploy", "production", "build-frontend-archive.ps1");

        Assert.Contains("DevExpress_License", script, StringComparison.Ordinal);
        Assert.Contains("DEVEXPRESS_LICENSE_FILE", script, StringComparison.Ordinal);
        Assert.Contains("DevExpress\\DevExpress_License.txt", script, StringComparison.Ordinal);
        Assert.Contains("DevExpress 许可证为空", script, StringComparison.Ordinal);
        Assert.Contains("DX1000|DX1001|DX1002|DX1003|For evaluation purposes only", script, StringComparison.Ordinal);
        Assert.Contains("DevExpress 许可证未被构建接受", script, StringComparison.Ordinal);
        Assert.Contains("if (Test-Path $archive) { Remove-Item -LiteralPath $archive -Force }", script, StringComparison.Ordinal);
        Assert.Contains("$appWasm.Count -ne 1", script, StringComparison.Ordinal);
        Assert.Contains("[regex]::Matches($appWasmText, \"LCPv1!\").Count -ne 1", script, StringComparison.Ordinal);
        Assert.Contains("DevExpress 许可证属性", script, StringComparison.Ordinal);
        Assert.Contains("release-commit.txt", script, StringComparison.Ordinal);
        Assert.Contains("WriteAllText", script, StringComparison.Ordinal);
        Assert.Contains("dotnet clean", script, StringComparison.Ordinal);
        Assert.Contains("Remove-Item -LiteralPath $publishLog", script, StringComparison.Ordinal);
        Assert.Contains("前端归档生成失败", script, StringComparison.Ordinal);
        Assert.Contains("SetEnvironmentVariable(\"DevExpress_License\", $licenseValue, \"Process\")", script, StringComparison.Ordinal);
        Assert.Contains("finally", script, StringComparison.Ordinal);
        Assert.Contains("SetEnvironmentVariable(\"DevExpress_License\", $restoreLicenseValue, \"Process\")", script, StringComparison.Ordinal);
        Assert.Contains("status --porcelain --untracked-files=all", script, StringComparison.Ordinal);
        Assert.Contains("Git 工作区存在未提交内容", script, StringComparison.Ordinal);
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

    private static void AssertAppearsBefore(string source, string first, string second)
    {
        var firstIndex = source.IndexOf(first, StringComparison.Ordinal);
        var secondIndex = source.IndexOf(second, StringComparison.Ordinal);
        Assert.True(firstIndex >= 0, $"Could not find expected marker: {first}");
        Assert.True(secondIndex >= 0, $"Could not find expected marker: {second}");
        Assert.True(firstIndex < secondIndex, $"Expected '{first}' to appear before '{second}'.");
    }
}
