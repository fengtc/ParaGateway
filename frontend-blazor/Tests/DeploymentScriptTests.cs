using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class DeploymentScriptTests
{
    [Fact]
    public void CandidateDeploymentEnforcesDataStoreIsolation()
    {
        var script = ReadSource("deploy", "production", "deploy-candidate.sh");

        Assert.Contains("production_config=${PRODUCTION_CONFIG_FILE:-/etc/paragateway/production-config.yaml}", script, StringComparison.Ordinal);
        Assert.Contains("candidate REDIS_DB must be", script, StringComparison.Ordinal);
        Assert.Contains("unset DATABASE_HOST DATABASE_DBNAME REDIS_DB", script, StringComparison.Ordinal);
        Assert.Contains("candidate DB target equals production", script, StringComparison.Ordinal);
        Assert.Contains("candidate REDIS_DB equals production", script, StringComparison.Ordinal);
        Assert.Contains("install -o root -g sub2api -m 640 \"$production_config\"", script, StringComparison.Ordinal);
        Assert.Contains("DATA_DIR=/var/lib/paragateway-backend-%s", script, StringComparison.Ordinal);
        Assert.Contains("EnvironmentFile=/etc/paragateway/%s/data.env", script, StringComparison.Ordinal);
        Assert.Contains("ReadWritePaths=\\nReadWritePaths=/var/lib/paragateway-backend-%s", script, StringComparison.Ordinal);
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
