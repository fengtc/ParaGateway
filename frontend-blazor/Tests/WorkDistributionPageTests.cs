namespace ParaGateway.Frontend.Tests;

using Xunit;

public sealed class WorkDistributionPageTests
{
    [Fact]
    public void PageContainsRequiredFiltersAndHorizontalStackedChart()
    {
        var root = FindRepositoryRoot();
        var page = File.ReadAllText(Path.Combine(root, "frontend-blazor", "Pages", "AdminWorkDistribution.razor"));
        foreach (var text in new[] { "人员", "部门", "岗位角色", "开始时间", "结束时间", "datetime-local", "step=\"1\"", "DateTime.Today.ToString(\"yyyy-MM-dd'T'00:00:00\"", "DateTime.Today.AddDays(1).ToString(\"yyyy-MM-dd'T'00:00:00\"", "工作相关占比", "编码", "文档", "分析/运维", "不确定", "样本数", "平均置信度", "stacked-chart" })
            Assert.Contains(text, page, StringComparison.Ordinal);
        foreach (var excluded in new[] { "申诉", "纠正分类", "分类明细", "MinSampleSize", "MinCohortSize" })
            Assert.DoesNotContain(excluded, page, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "frontend-blazor"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException();
    }
}
