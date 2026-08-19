using System.Text.Json;
using ParaGateway.Frontend.Models;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class BatchImagePageTests
{
    [Fact]
    public void BatchImagePageCoversOfficialUserWorkflow()
    {
        var page = ReadSource("Pages", "BatchImage.razor");
        var client = ReadSource("Services", "ApiClient.cs");
        var models = ReadSource("Models", "Dtos.cs");

        Assert.Contains("Api.GetMyApiKeysAsync", page, StringComparison.Ordinal);
        Assert.Contains("GroupAllowsBatchImageGeneration", page, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"batch-key\"", page, StringComparison.Ordinal);
        Assert.Contains("taskNameFilter", page, StringComparison.Ordinal);
        Assert.Contains("apiKeyFilter", page, StringComparison.Ordinal);
        Assert.Contains("downloadedFilter", page, StringComparison.Ordinal);
        Assert.Contains("DownloadSelectedJobsAsync", page, StringComparison.Ordinal);
        Assert.Contains("DeleteSelectedJobsAsync", page, StringComparison.Ordinal);
        Assert.Contains("RetryFailedAsync", page, StringComparison.Ordinal);
        Assert.Contains("ParentBatchId", page, StringComparison.Ordinal);
        Assert.Contains("GetBatchImageItemContentAsync", page, StringComparison.Ordinal);
        Assert.Contains("ReadReferenceImagesAsync", page, StringComparison.Ordinal);
        Assert.Contains("MaxOutputsPerJob = 200", page, StringComparison.Ordinal);
        Assert.Contains("StartPolling", page, StringComparison.Ordinal);
        Assert.Contains("AgentInstruction", page, StringComparison.Ordinal);

        Assert.Contains("GetBatchImageJobAsync", client, StringComparison.Ordinal);
        Assert.Contains("task_name=", client, StringComparison.Ordinal);
        Assert.Contains("downloaded=", client, StringComparison.Ordinal);
        Assert.Contains("image_index=", client, StringComparison.Ordinal);
        Assert.Contains("public sealed class BatchImageReferenceImageDto", models, StringComparison.Ordinal);
        Assert.Contains("allow_batch_image_generation", models, StringComparison.Ordinal);
        Assert.Contains("hold_amount", models, StringComparison.Ordinal);
        Assert.Contains("parent_batch_id", models, StringComparison.Ordinal);
    }

    [Fact]
    public void BatchImageRequestSerializesRetryAndReferenceImageFields()
    {
        var request = new BatchImageSubmitRequest
        {
            Model = "gemini-2.5-flash-image",
            TaskName = "retry",
            ParentBatchId = "batch-root",
            Items =
            [
                new BatchImageSubmitItem
                {
                    CustomId = "img_001_retry",
                    Prompt = "prompt",
                    OutputCount = 2,
                    ReferenceImages =
                    [
                        new BatchImageReferenceImageDto
                        {
                            Id = "reference.png",
                            Type = "reference",
                            MimeType = "image/png",
                            Data = "AA=="
                        }
                    ]
                }
            ]
        };

        var json = JsonSerializer.Serialize(request);

        Assert.Contains("\"parent_batch_id\":\"batch-root\"", json, StringComparison.Ordinal);
        Assert.Contains("\"output_count\":2", json, StringComparison.Ordinal);
        Assert.Contains("\"reference_images\"", json, StringComparison.Ordinal);
        Assert.Contains("\"mime_type\":\"image/png\"", json, StringComparison.Ordinal);
    }

    private static string ReadSource(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(path)) return File.ReadAllText(path);
            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {Path.Combine(parts)}");
    }
}
