using System.Text.Json;

namespace ParaGateway.Frontend.Models;

public sealed class ModelMappingInput
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
}

public sealed record ModelMappingPreset(string Label, string From, string To);

public static class AccountModelRestrictions
{
    private static readonly IReadOnlyDictionary<string, string[]> ModelsByPlatform =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["openai"] =
            [
                "gpt-5.2", "gpt-5.2-2025-12-11", "gpt-5.2-chat-latest",
                "gpt-5.2-pro", "gpt-5.2-pro-2025-12-11",
                "gpt-5.6", "gpt-5.6-sol", "gpt-5.6-terra", "gpt-5.6-luna",
                "gpt-5.5", "gpt-5.4", "gpt-5.4-mini", "gpt-5.4-2026-03-05",
                "gpt-5.3-codex-spark", "codex-auto-review",
                "gpt-4o-audio-preview", "gpt-4o-realtime-preview",
                "gpt-image-1", "gpt-image-1.5", "gpt-image-2"
            ],
            ["anthropic"] =
            [
                "claude-3-5-sonnet-20241022", "claude-3-5-sonnet-20240620",
                "claude-3-5-haiku-20241022", "claude-3-7-sonnet-20250219",
                "claude-sonnet-4-20250514", "claude-opus-4-20250514",
                "claude-opus-4-1-20250805", "claude-sonnet-4-5-20250929",
                "claude-haiku-4-5-20251001", "claude-opus-4-5-20251101",
                "claude-opus-4-6", "claude-opus-4-7", "claude-opus-4-8",
                "claude-opus-5", "claude-sonnet-4-6", "claude-sonnet-5", "claude-fable-5"
            ],
            ["gemini"] =
            [
                "gemini-3.1-flash-image", "gemini-2.5-flash-image", "gemini-2.0-flash",
                "gemini-2.5-flash", "gemini-2.5-pro", "gemini-3.5-flash",
                "gemini-3-flash-preview", "gemini-3-pro-preview"
            ],
            ["antigravity"] =
            [
                "claude-fable-5", "claude-opus-4-6", "claude-opus-4-6-thinking",
                "claude-opus-4-7", "claude-opus-4-8", "claude-opus-4-5-thinking",
                "claude-sonnet-4-6", "claude-sonnet-4-5", "claude-sonnet-4-5-thinking",
                "gemini-3.1-flash-image", "gemini-2.5-flash-image", "gemini-2.5-flash",
                "gemini-2.5-flash-lite", "gemini-2.5-flash-thinking", "gemini-2.5-pro",
                "gemini-3-flash", "gemini-3-pro-high", "gemini-3-pro-low",
                "gemini-3.1-pro", "gemini-3.1-pro-high", "gemini-3.1-pro-low",
                "gemini-3-pro-image", "gpt-oss-120b-medium", "tab_flash_lite_preview"
            ],
            ["grok"] =
            [
                "grok-4.6", "grok-4.5", "grok-4.3", "grok-build-0.1",
                "grok-composer-2.5-fast", "grok-4.20-0309-reasoning",
                "grok-4.20-0309-non-reasoning", "grok-4.20-multi-agent-0309",
                "grok-4.20-multi-agent", "grok-4.20-multi-agent-latest",
                "grok-4.3-latest", "grok-latest", "grok-4.6-latest", "grok-4.5-latest",
                "grok-build-latest", "composer-2.5", "grok-4.20-reasoning",
                "grok-4.20-non-reasoning", "grok-imagine", "grok-imagine-image-quality",
                "grok-imagine-image", "grok-imagine-video", "grok-imagine-video-1.5-preview",
                "grok-imagine-video-1.5"
            ],
            ["kimi"] = ["moonshot-v1-8k", "moonshot-v1-32k", "moonshot-v1-128k", "kimi-latest"],
            ["zhipu"] =
            [
                "glm-4", "glm-4v", "glm-4-plus", "glm-4-0520", "glm-4-air", "glm-4-airx",
                "glm-4-long", "glm-4-flash", "glm-4v-plus", "glm-4.5", "glm-4.6",
                "glm-3-turbo", "glm-4-alltools", "chatglm_turbo", "chatglm_pro",
                "chatglm_std", "chatglm_lite", "cogview-3", "cogvideo"
            ],
            ["deepseek"] =
            [
                "deepseek-chat", "deepseek-coder", "deepseek-reasoner", "deepseek-v3",
                "deepseek-v3-0324", "deepseek-r1", "deepseek-r1-0528",
                "deepseek-r1-distill-qwen-32b", "deepseek-r1-distill-qwen-14b",
                "deepseek-r1-distill-qwen-7b", "deepseek-r1-distill-llama-70b",
                "deepseek-r1-distill-llama-8b"
            ]
        };

    private static readonly IReadOnlyDictionary<string, ModelMappingPreset[]> PresetsByPlatform =
        new Dictionary<string, ModelMappingPreset[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["openai"] =
            [
                new("GPT-5.6", "gpt-5.6", "gpt-5.6"),
                new("GPT-5.6 Sol", "gpt-5.6-sol", "gpt-5.6-sol"),
                new("GPT-5.5", "gpt-5.5", "gpt-5.5"),
                new("GPT-5.4", "gpt-5.4", "gpt-5.4"),
                new("Haiku -> 5.4", "claude-haiku-4-5-20251001", "gpt-5.4"),
                new("Opus -> 5.4", "claude-opus-4-6", "gpt-5.4"),
                new("Sonnet -> 5.4", "claude-sonnet-4-6", "gpt-5.4")
            ],
            ["anthropic"] =
            [
                new("Sonnet 5", "claude-sonnet-5", "claude-sonnet-5"),
                new("Sonnet 4.6", "claude-sonnet-4-6", "claude-sonnet-4-6"),
                new("Opus 4.8", "claude-opus-4-8", "claude-opus-4-8"),
                new("Opus -> Sonnet", "claude-opus-4-6", "claude-sonnet-4-6")
            ],
            ["gemini"] =
            [
                new("2.5 Flash", "gemini-2.5-flash", "gemini-2.5-flash"),
                new("2.5 Pro", "gemini-2.5-pro", "gemini-2.5-pro"),
                new("3.5 Flash", "gemini-3.5-flash", "gemini-3.5-flash")
            ],
            ["grok"] =
            [
                new("Grok 4.6", "grok-4.6", "grok-4.6"),
                new("Grok 4.5", "grok-4.5", "grok-4.5"),
                new("Imagine Image", "grok-imagine", "grok-imagine-image-quality"),
                new("Imagine Video", "grok-imagine-video", "grok-imagine-video-1.5")
            ],
            ["antigravity"] =
            [
                new("Claude -> Sonnet", "claude-*", "claude-sonnet-4-5"),
                new("Sonnet -> 4.6", "claude-sonnet-*", "claude-sonnet-4-6"),
                new("Opus -> Thinking", "claude-opus-*", "claude-opus-4-6-thinking"),
                new("Gemini 3 -> Flash", "gemini-3*", "gemini-3-flash")
            ]
        };

    public static bool ShouldShow(string? platform, string? type)
    {
        platform = platform?.Trim().ToLowerInvariant();
        type = type?.Trim().ToLowerInvariant();
        if (platform is null or "copilot") return false;
        if (platform == "antigravity") return type is "oauth" or "apikey";
        if (type is "apikey" or "bedrock" or "service_account") return true;
        return type == "oauth" && platform is "openai" or "grok";
    }

    public static bool SupportsWhitelist(string? platform) =>
        !string.Equals(platform?.Trim(), "antigravity", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<string> ModelsFor(string? platform, string? type = null)
    {
        var key = string.Equals(type, "bedrock", StringComparison.OrdinalIgnoreCase)
            ? "anthropic"
            : platform?.Trim().ToLowerInvariant() ?? string.Empty;
        return ModelsByPlatform.TryGetValue(key, out var models) ? models : Array.Empty<string>();
    }

    public static IReadOnlyList<ModelMappingPreset> PresetsFor(string? platform, string? type = null)
    {
        var key = string.Equals(type, "bedrock", StringComparison.OrdinalIgnoreCase)
            ? "anthropic"
            : platform?.Trim().ToLowerInvariant() ?? string.Empty;
        return PresetsByPlatform.TryGetValue(key, out var presets) ? presets : Array.Empty<ModelMappingPreset>();
    }

    public static void Load(AccountInput input, IReadOnlyDictionary<string, JsonElement>? credentials)
    {
        input.AllowedModels.Clear();
        input.ModelMappings.Clear();
        input.ModelRestrictionMode = SupportsWhitelist(input.Platform) ? "whitelist" : "mapping";
        if (credentials is null || !credentials.TryGetValue("model_mapping", out var raw)
            || raw.ValueKind != JsonValueKind.Object) return;

        foreach (var property in raw.EnumerateObject())
        {
            var from = property.Name.Trim();
            var to = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString()?.Trim() : null;
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to)) continue;
            if (string.Equals(from, to, StringComparison.Ordinal)) input.AllowedModels.Add(from);
            else input.ModelMappings.Add(new ModelMappingInput { From = from, To = to });
        }

        if (input.ModelMappings.Count > 0 && input.AllowedModels.Count == 0)
            input.ModelRestrictionMode = "mapping";
    }

    public static string? Validate(AccountInput input)
    {
        if (!ShouldShow(input.Platform, input.Type)) return null;
        var includeWhitelist = input.IsEditing || input.ModelRestrictionMode == "whitelist";
        var includeMappings = input.IsEditing || input.ModelRestrictionMode == "mapping";
        if (includeWhitelist)
        {
            foreach (var model in input.AllowedModels.Select(value => value.Trim()).Where(value => value.Length > 0))
            {
                if (model.Length > 240) return $"模型名称过长：{model[..Math.Min(40, model.Length)]}";
                if (model.Contains('*')) return $"模型白名单不支持通配符：{model}";
            }
        }

        if (!includeMappings) return null;
        foreach (var mapping in input.ModelMappings)
        {
            var from = mapping.From.Trim();
            var to = mapping.To.Trim();
            if (from.Length == 0 && to.Length == 0) continue;
            if (from.Length == 0 || to.Length == 0) return "模型映射的请求模型和实际模型都必须填写。";
            if (from.Length > 240 || to.Length > 240) return "模型映射名称不能超过 240 个字符。";
            var star = from.IndexOf('*');
            if (star >= 0 && (star != from.Length - 1 || from.LastIndexOf('*') != star))
                return $"请求模型通配符只能放在末尾：{from}";
            if (to.Contains('*')) return $"实际模型不能包含通配符：{to}";
        }
        return null;
    }

    public static Dictionary<string, object?>? BuildCredentialPatch(AccountInput input, bool includeEmpty)
    {
        if (!ShouldShow(input.Platform, input.Type)) return null;
        var mapping = BuildMapping(input);
        if (mapping.Count == 0 && !includeEmpty) return null;
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["model_mapping"] = mapping
        };
    }

    private static Dictionary<string, string> BuildMapping(AccountInput input)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var includeWhitelist = input.IsEditing || input.ModelRestrictionMode == "whitelist";
        var includeMappings = input.IsEditing || input.ModelRestrictionMode == "mapping";
        if (includeWhitelist)
        {
            foreach (var model in input.AllowedModels.Select(value => value.Trim()).Where(value => value.Length > 0 && !value.Contains('*')).Distinct(StringComparer.Ordinal))
                result[model] = model;
        }
        if (includeMappings)
        {
            foreach (var mapping in input.ModelMappings)
            {
                var from = mapping.From.Trim();
                var to = mapping.To.Trim();
                if (from.Length > 0 && to.Length > 0) result[from] = to;
            }
        }
        return result;
    }
}
