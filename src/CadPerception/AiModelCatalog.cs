using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace CadPerceptionPlugin
{
    /// <summary>
    /// 单个 AI 预设（对应 ai_presets.json 的 presets[] 一项）。
    /// 面板此前把模型清单硬编码在源码里（DeepSeek-Flash / Kimi-K3 / Muse-Spark-1.3），
    /// 其中 muse-spark-1.3 在当前 apigoto 账号下根本不存在（网关只开放 4 个模型），
    /// 导致「选了就 403」。现在清单一律从配置文件读取，换模型不改代码。
    /// 2026-11-10 新增 tierflow 套餐：与 apigoto 并存，按 provider 区分。
    /// </summary>
    internal sealed class AiModelPreset
    {
        public string Name { get; set; } = "";          // 面板显示名
        public string Id { get; set; } = "";            // 配置里的预设 id
        public string ModelId { get; set; } = "";       // 请求体里的 model 字段
        public string BaseUrl { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public string Desc { get; set; } = "";
        public string Provider { get; set; } = "";      // 渠道标签（apigoto / tierflow / ...）
        public bool SupportsVision { get; set; }
        public bool IsEmbedding { get; set; }
        public bool IsText { get; set; } = true;

        /// <summary>面板弹窗里的一行能力说明（不直接搬配置文件的长描述）。</summary>
        public string CapabilityLine
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(Provider)) parts.Add(Provider);
                if (IsText) parts.Add("对话");
                if (SupportsVision) parts.Add("识图");
                if (parts.Count == 0) parts.Add("未标注能力");
                return string.Join(" / ", parts) + "  ·  " + ModelId;
            }
        }
    }

    /// <summary>
    /// 读取 ai_presets.json 生成模型清单与路由。文件缺失或解析失败时退回内置默认，
    /// 保证面板永远有可用的对话模型，不会因为配置损坏变成空列表。
    /// </summary>
    internal static class AiModelCatalog
    {
        // 配置文件彻底不可用时的兜底（与出厂默认一致）
        private const string FallbackBaseUrl = "https://api.openai.com/v1";
        private const string FallbackApiKey = "";
        private const string FallbackModelId = "gpt-4o-mini";
        private const string FallbackName = "Fallback (configure AICFG)";

        private static List<AiModelPreset>? _chatCache;
        private static List<AiModelPreset>? _allCache;
        private static DateTime _stamp;

        /// <summary>可对话模型（排除向量模型）。配置文件变更后自动重载。</summary>
        public static List<AiModelPreset> ChatModels => Load().Where(m => !m.IsEmbedding).ToList();

        /// <summary>面板启动时的默认模型（取 active_language_model）。</summary>
        public static AiModelPreset DefaultChat
        {
            get
            {
                var all = Load();
                string activeId = ReadScalar("active_language_model");
                var hit = all.FirstOrDefault(m => !m.IsEmbedding && m.Id == activeId)
                          ?? all.FirstOrDefault(m => !m.IsEmbedding);
                return hit ?? FallbackPreset();
            }
        }

        /// <summary>识图模型（取 active_vision_model，必须支持视觉）。</summary>
        public static AiModelPreset? ActiveVision
        {
            get
            {
                var all = Load().Where(m => !m.IsEmbedding).ToList();
                string activeId = ReadScalar("active_vision_model");
                var hit = all.FirstOrDefault(m => m.Id == activeId && m.SupportsVision);
                return hit ?? all.FirstOrDefault(m => m.SupportsVision);
            }
        }

        public static AiModelPreset? ResolveByName(string? displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName)) return null;
            var all = Load();
            return all.FirstOrDefault(m => string.Equals(m.Name, displayName, StringComparison.OrdinalIgnoreCase))
                ?? all.FirstOrDefault(m => string.Equals(m.ModelId, displayName, StringComparison.OrdinalIgnoreCase))
                ?? all.FirstOrDefault(m => string.Equals(m.Id, displayName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>配置里 lsp_directory / library_memory_root（可被环境变量覆盖）。</summary>
        public static string LspDirectory =>
            Environment.GetEnvironmentVariable("CAD_LSP_DIR") is string e && e.Length > 0
                ? e : (ReadScalar("lsp_directory") ?? CadPaths.LspDir);

        public static string LibraryMemoryRoot =>
            Environment.GetEnvironmentVariable("CAD_MEMORY_ROOT") is string e && e.Length > 0
                ? e : (ReadScalar("library_memory_root") ?? Path.Combine(CadPaths.CadsysRoot, "library"));

        private static AiModelPreset FallbackPreset() => new()
        {
            Name = FallbackName,
            Id = "fallback-deepseek-flash",
            ModelId = FallbackModelId,
            BaseUrl = FallbackBaseUrl,
            ApiKey = FallbackApiKey,
            Desc = "内置兜底（配置文件不可用）",
            SupportsVision = true
        };

        private static List<AiModelPreset> Load()
        {
            string path = CadPaths.PresetsFile;
            DateTime stamp;
            try { stamp = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue; }
            catch { stamp = DateTime.MinValue; }

            if (_allCache != null && stamp == _stamp) return _allCache;

            var list = new List<AiModelPreset>();
            try
            {
                if (File.Exists(path))
                {
                    var node = JsonNode.Parse(File.ReadAllText(path));
                    if (node?["presets"] is JsonArray arr)
                    {
                        foreach (var p in arr)
                        {
                            if (p == null) continue;
                            bool isEmb = p["isEmbeddingModel"]?.GetValue<bool>() ?? false;
                            var item = new AiModelPreset
                            {
                                Id = p["id"]?.ToString() ?? "",
                                Name = p["displayName"]?.ToString()
                                       ?? p["name"]?.ToString()
                                       ?? p["model"]?.ToString()
                                       ?? p["id"]?.ToString() ?? "",
                                ModelId = p["model"]?.ToString() ?? "",
                                BaseUrl = p["baseUrl"]?.ToString() ?? FallbackBaseUrl,
                                ApiKey = p["apiKey"]?.ToString() ?? "",
                                Desc = p["description"]?.ToString() ?? "",
                                Provider = p["provider"]?.ToString() ?? ProviderFromUrl(p["baseUrl"]?.ToString() ?? ""),
                                IsEmbedding = isEmb,
                                IsText = p["isTextModel"]?.GetValue<bool>() ?? true,
                                SupportsVision = (p["supportVision"]?.GetValue<bool>() ?? false)
                                                 || (p["isImageModel"]?.GetValue<bool>() ?? false)
                            };
                            if (!string.IsNullOrEmpty(item.ModelId)) list.Add(item);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogException("AiModelCatalog.Load 解析配置失败", ex);
            }

            if (list.Count == 0 || !list.Any(m => !m.IsEmbedding))
            {
                Logger.Log($"[AI-MODELS] 配置无可用对话模型（path={path}），使用内置兜底");
                list.Add(FallbackPreset());
            }
            else
            {
                Logger.Log($"[AI-MODELS] 载入 {list.Count} 个预设（对话 {list.Count(m => !m.IsEmbedding)} 个）");
            }

            _allCache = list;
            _chatCache = list.Where(m => !m.IsEmbedding).ToList();
            _stamp = stamp;
            return list;
        }

        private static string? ReadScalar(string key)
        {
            try
            {
                string path = CadPaths.PresetsFile;
                if (!File.Exists(path)) return null;
                var node = JsonNode.Parse(File.ReadAllText(path));
                return node?[key]?.ToString();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>没写 provider 时按 baseUrl 推断渠道标签，便于面板里区分套餐来源。</summary>
        private static string ProviderFromUrl(string baseUrl)
        {
            try
            {
                var host = new Uri(baseUrl).Host.ToLowerInvariant();
                if (host.Contains("tierflow")) return "tierflow";
                if (host.Contains("apigoto")) return "apigoto";
                if (host.Contains("siliconflow")) return "siliconflow";
                return host;
            }
            catch
            {
                return "";
            }
        }
    }
}