using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using UserControl = System.Windows.Controls.UserControl;
using TextBox = System.Windows.Controls.TextBox;
using Button = System.Windows.Controls.Button;
using ProgressBar = System.Windows.Controls.ProgressBar;
using System.Text.RegularExpressions;
using System.Windows.Documents;
using Run = System.Windows.Documents.Run;
using Bold = System.Windows.Documents.Bold;
using InlineUIContainer = System.Windows.Documents.InlineUIContainer;
using Orientation = System.Windows.Controls.Orientation;
using Clipboard = System.Windows.Clipboard;
using ScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using Brushes = System.Windows.Media.Brushes;
using Brush = System.Windows.Media.Brush;
using FontFamily = System.Windows.Media.FontFamily;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Path = System.Windows.Shapes.Path;
using WpfEllipse = System.Windows.Shapes.Ellipse;
using WpfImage = System.Windows.Controls.Image;
using CadLine = Autodesk.AutoCAD.DatabaseServices.Line;
using CadPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace CadPerceptionPlugin
{
    public static class CadSvg
    {
        public const string Robot = "M12 2a2 2 0 0 1 2 2c0 .74-.4 1.39-1 1.73V7h1a7 7 0 0 1 7 7h1a1 1 0 0 1 1 1v3a1 1 0 0 1-1 1h-1v1a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-1H2a1 1 0 0 1-1-1v-3a1 1 0 0 1 1-1h1a7 7 0 0 1 7-7h1V5.73c-.6-.34-1-.99-1-1.73a2 2 0 0 1 2-2M7.5 13a1.5 1.5 0 1 0 0 3 1.5 1.5 0 0 0 0-3m9 0a1.5 1.5 0 1 0 0 3 1.5 1.5 0 0 0 0-3";
        public const string Phone = "M20.01 15.38c-1.23 0-2.42-.2-3.53-.56a.977.977 0 0 0-1.01.24l-2.2 2.2a15.057 15.057 0 0 1-6.59-6.59l2.2-2.21a.96.96 0 0 0 .25-1.01A11.36 11.36 0 0 1 8.57 3.9c0-.55-.45-1-1-1H4c-.55 0-1 .45-1 1 0 9.39 7.61 17 17 17 .55 0 1-.45 1-1v-3.52c0-.55-.45-1-.99-1z";
        public const string Settings = "M19.14 12.94c.04-.3.06-.61.06-.94 0-.32-.02-.64-.07-.94l2.03-1.58a.49.49 0 0 0 .12-.61l-1.92-3.32a.488.488 0 0 0-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54a.484.484 0 0 0-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L2.74 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.05.3-.09.63-.09.94s.02.64.07.94l-2.03 1.58a.49.49 0 0 0-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z";
        public const string Plus = "M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z";
        public const string ChevronUp = "M7.41 15.41L12 10.83l4.59 4.58L18 14l-6-6-6 6z";
        public const string Mic = "M12 14c1.66 0 3-1.34 3-3V5c0-1.66-1.34-3-3-3S9 3.34 9 5v6c0 1.66 1.34 3 3 3zm-1-9c0-.55.45-1 1-1s1 .45 1 1v6c0 .55-.45 1-1 1s-1-.45-1-1V5zm6 6c0 2.76-2.24 5-5 5s-5-2.24-5-5H5c0 3.53 2.61 6.43 6 6.92V21h2v-3.08c3.39-.49 6-3.39 6-6.92h-2z";
        public const string SendArrow = "M2.01 21L23 12 2.01 3 2 10l15 2-15 2z";
        public const string Image = "M21 19V5c0-1.1-.9-2-2-2H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2zM8.5 13.5l2.5 3.01L14.5 12l4.5 6H5l3.5-4.5z";
        public const string Close = "M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z";
        public const string Copy = "M16 1H4c-1.1 0-2 .9-2 2v14h2V3h12V1zm3 4H8c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h11c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2zm0 16H8V7h11v14z";
        public const string Undo = "M12.5 8c-2.65 0-5.05.99-6.9 2.6L2 7v9h9l-3.62-3.62c1.39-1.16 3.16-1.88 5.12-1.88 3.54 0 6.55 2.31 7.6 5.5l2.37-.78C21.08 11.03 17.15 8 12.5 8z";
        public const string Refresh = "M17.65 6.35C16.2 4.9 14.21 4 12 4c-4.42 0-7.99 3.58-7.99 8s3.57 8 7.99 8c3.73 0 6.84-2.55 7.73-6h-2.08c-.82 2.33-3.04 4-5.65 4-3.31 0-6-2.69-6-6s2.69-6 6-6c1.66 0 3.14.69 4.22 1.78L13 11h7V4l-2.35 2.35z";
        public const string List = "M3 13h2v-2H3v2zm0 4h2v-2H3v2zm0-8h2V7H3v2zm4 4h14v-2H7v2zm0 4h14v-2H7v2zM7 7v2h14V7H7z";
        public const string Play = "M8 5v14l11-7z";
        public const string Trash = "M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z";
        public const string Terminal = "M20 4H4a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V6a2 2 0 0 0-2-2zm0 14H4V8h16v10zM7 9.5 9.5 12 7 14.5 5.5 13l1-1-1-1L7 9.5zm4.5 5.5h6v2h-6z";
    }

    public class ChatMessageItem
    {
        public string Role { get; set; } = "user"; // "user" or "assistant"
        public string Content { get; set; } = "";
        public string? ImagePath { get; set; }
        public string? Reasoning { get; set; }
        public double DurationSec { get; set; }
        public string Timestamp { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        public int CachedTokens { get; set; } = 0;       // 模型侧前缀缓存命中的 tokens (Prompt Caching)
        public int TotalTokens { get; set; } = 0;        // 模型消耗的总 tokens
        public bool IsLocalCacheHit { get; set; } = false; // 是否命中本地端侧极速缓存
    }

    public class ChatSession
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = "新会话";
        public string UpdatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        public List<ChatMessageItem> Messages { get; set; } = new();
    }

    public class UsageBarSettings
    {
        public bool Enabled { get; set; } = true;
        public bool ShowSpeed { get; set; } = true;
        public bool ShowCost { get; set; } = true;
        public bool ShowTokens { get; set; } = true;
        public bool ShowContextBar { get; set; } = true;
    }

    public static class TokenPricingCalculator
    {
        public static double CalculateCost(string model, int totalTokens, int cachedTokens)
        {
            int outputTokens = Math.Max(0, (int)(totalTokens * 0.25));
            int inputTokens = Math.Max(0, totalTokens - outputTokens);
            int uncachedInput = Math.Max(0, inputTokens - cachedTokens);

            double priceIn = 1.0;
            double priceCache = 0.02;
            double priceOut = 4.0;

            // 2026-11-10：模型清单改为配置驱动后，价格按模型 ID 关键字匹配（不再是 muse 等已下线名称）
            if (model.Contains("kimi", StringComparison.OrdinalIgnoreCase))
            {
                priceIn = 4.0; priceCache = 0.8; priceOut = 16.0;
            }
            else if (model.Contains("gemini", StringComparison.OrdinalIgnoreCase))
            {
                priceIn = 1.5; priceCache = 0.15; priceOut = 6.0;
            }
            else if (model.Contains("gpt", StringComparison.OrdinalIgnoreCase))
            {
                priceIn = 2.5; priceCache = 0.25; priceOut = 10.0;
            }
            else if (model.Contains("vl", StringComparison.OrdinalIgnoreCase)
                     || model.Contains("qwen", StringComparison.OrdinalIgnoreCase))
            {
                priceIn = 1.2; priceCache = 0.12; priceOut = 3.6;
            }

            double cost = (uncachedInput * priceIn + cachedTokens * priceCache + outputTokens * priceOut) / 1000000.0;
            return Math.Round(cost, 5);
        }
    }

    /// <summary>
    /// CadCopilot 原生暗黑伴随式 AI 助理界面
    /// 100% 矢量 SVG，已原生接入真实大模型 API (DeepSeek-Flash/Kimi/Muse) 与 2px 极细圆角暗黑滚动条
    /// </summary>
    /// <summary>
    /// AI 生成命令/LISP 的静态安全审查器（2026-10-24，与网页版 LispVetter 同源规则）。
    /// 裁决：SAFE 直发 / DANGEROUS 确认闸 / INTERACTIVE 拒绝自动执行(卡命令队列) / BLOCK 拦截。
    /// </summary>
    public static class ActionVetter
    {
        public const string SAFE = "SAFE", DANGEROUS = "DANGEROUS", INTERACTIVE = "INTERACTIVE", BLOCK = "BLOCK";

        static readonly string[] BlockWords = { "QUIT", "EXIT", "SAVEAS", "NEW", "CLOSE", "SHUTDOWN", "LOGOUT", "RECOVER", "WIPEOUT" };
        static readonly string[] DangerWords = { "ERASE", "E", "DELETE", "DEL", "PURGE", "OVERKILL", "OOPS" };
        static readonly Regex CmdFormRe = new(@"\(\s*(?:command|vl-cmdf)\b[\s\S]*?\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex StrLitRe = new(@"""([^""]*)""", RegexOptions.Compiled);
        static readonly Regex CommentRe = new(@";[^\n]*", RegexOptions.Compiled);
        static readonly Regex QuotedRe = new(@"""(?:[^""\\]|\\.)*""", RegexOptions.Compiled);
        static readonly Regex EntdelRe = new(@"\(\s*entdel|vla-(?:delete|erase)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        // 前缀旁路封堵：-Purge/_-PURGE/_.-ERASE 等（2026-10-24 网页版对抗实证后移植）
        static readonly Regex DangerRe = new(@"(^|[\s_(""'])[-_.]{0,3}(?:ERASE|DELETE|DEL|PURGE|OVERKILL|OOPS)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex InteractRe = new(@"\b(?:getpoint|getstring|getint|getreal|getdist|getangle|getcorner|getkword|getorient|entsel|nentsel)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex SsgetRe = new(@"\bssget\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex SsgetPresetRe = new(@"ssget\s+[""']", RegexOptions.Compiled);
        static readonly Regex FileOpRe = new(@"vl-file-delete|\(\s*open\b[^)]*""w|startapp|\(\s*shell\b|\bwhile\s+T\b|\(\s*exit\s*\)|\(\s*quit\s*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex BareInteractiveRe = new(@"^(?:LINE|PLINE|CIRCLE|ARC|MOVE|COPY|ROTATE|SCALE|STRETCH|TRIM|EXTEND|OFFSET|INSERT|TEXT|MTEXT|DIM|HATCH|FILLET|CHAMFER|MIRROR|ARRAY|BREAK|JOIN|EXPLODE)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        static string StripPrefix(string s) => s.Trim().TrimStart('-', '_', '.').ToUpperInvariant();

        // -------------------------------------------------------------
        // 结构化动作白名单（2026-11-11）：这些 action 没有 command 字段，
        // 不能走 LISP 命令审查（否则一律被判「空命令」而拦截）。
        // 每个动作由插件在事务内以强类型执行，参数范围另有校验。
        // -------------------------------------------------------------
        static readonly HashSet<string> StructuredActions = new(StringComparer.OrdinalIgnoreCase)
        {
            "filter", "layerState", "measure", "normalizeLayer", "crossViewQuery", "exportText",
            "setLayer", "erase", "draw", "drawDimension", "transform", "insertBlock",
            "zoom", "stats", "listLayers", "listBlocks", "readBlock", "getSelection",
            "queryWindow", "getView", "hide", "unhide", "explode", "findText",
            "readDimensions", "getExtents", "flatten", "undo", "save", "regen", "purge"
        };

        /// <summary>
        /// 结构化动作的独立审查（2026-11-11）。不解析 LISP，只校验必填参数，
        /// 避免 filter/layerState 这类无 command 的动作被「空命令」误杀。
        /// </summary>
        public static bool IsStructured(string action) =>
            !string.IsNullOrWhiteSpace(action) && StructuredActions.Contains(action);

        public static (string Verdict, string Reason) VetStructured(string action, JsonElement root)
        {
            if (!StructuredActions.Contains(action)) return (BLOCK, $"未知动作 {action}");

            string Str(string n) => root.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String
                ? (v.GetString() ?? "") : "";
            bool HasArr(string n, bool requireNonEmpty)
            {
                if (!root.TryGetProperty(n, out var v) || v.ValueKind != JsonValueKind.Array) return !requireNonEmpty;
                if (!requireNonEmpty) return true;
                foreach (var e in v.EnumerateArray())
                    if (e.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(e.GetString())) return true;
                return false;
            }

            if (action.Equals("filter", StringComparison.OrdinalIgnoreCase))
            {
                string op = Str("op").Trim().ToLowerInvariant();
                if (op.Length == 0) op = "setlayer";
                if (op == "erase" && !HasArr("types", true))
                    return (BLOCK, "filter erase 必须给出 types（删什么类型），不能删全图");
                if (op.StartsWith("setlayer", StringComparison.Ordinal) && Str("layer").Trim().Length == 0)
                    return (BLOCK, "filter setLayer 必须给出 layer");
                if (op != "erase" && op != "setlayer" && op != "count")
                    return (BLOCK, $"filter 不支持 op={op}");
                return (SAFE, "");
            }

            if (action.Equals("layerState", StringComparison.OrdinalIgnoreCase))
            {
                string op = Str("op").Trim().ToLowerInvariant();
                var allowed = new[] { "on", "off", "freeze", "thaw", "lock", "unlock",
                                      "isolate", "highlight", "unhighlight", "setcurrent" };
                if (Array.IndexOf(allowed, op) < 0) return (BLOCK, $"layerState 不支持 op={op}");
                bool hasTarget = Str("layer").Trim().Length > 0 || HasArr("layers", true)
                                 || Str("nameContains").Trim().Length > 0
                                 || (root.TryGetProperty("allLayers", out var al) && al.ValueKind == JsonValueKind.True)
                                 || (root.TryGetProperty("selectionOnly", out var so) && so.ValueKind == JsonValueKind.True)
                                 || HasArr("handles", true);
                if (!hasTarget) return (BLOCK, "layerState 必须指定 layer / layers / nameContains / allLayers / 选区");
                return (SAFE, "");
            }

            if (action.Equals("normalizeLayer", StringComparison.OrdinalIgnoreCase))
            {
                bool hasMap = root.TryGetProperty("mapping", out var mp) && mp.ValueKind == JsonValueKind.Object
                              && mp.EnumerateObject().Any();
                if (!hasMap) return (BLOCK, "normalizeLayer 必须给出 mapping（别名层→规范层）");
                return (SAFE, "");
            }

            if (action.Equals("crossViewQuery", StringComparison.OrdinalIgnoreCase))
            {
                if (!root.TryGetProperty("x1", out _) || !root.TryGetProperty("x2", out _))
                    return (BLOCK, "crossViewQuery 需要 x1/x2（查询坐标带）");
                return (SAFE, "");
            }

            // 其余结构化动作：参数由插件内部校验，不在此处block
            return (SAFE, "");
        }

        public static (string Verdict, string Reason) Vet(string cmdRaw)
        {
            var cmd = (cmdRaw ?? "").Trim();
            if (cmd.Length == 0) return (BLOCK, "空命令");

            var reasons = new List<string>();
            var danger = new List<string>();

            int open = Regex.Matches(cmd, @"\(").Count;
            int close = Regex.Matches(cmd, @"\)").Count;
            if (open != close) reasons.Add($"括号不平衡({open}/{close})");

            // (command/vl-cmdf "...") 内的字符串字面量 + 裸命令首词 = 待执行动词
            var tokens = new List<string>();
            foreach (Match f in CmdFormRe.Matches(cmd))
                foreach (Match s in StrLitRe.Matches(f.Value))
                    tokens.Add(s.Groups[1].Value);
            if (!cmd.StartsWith("(")) tokens.Add(cmd.Split(' ', ',', '\t')[0]);

            foreach (var t in tokens)
            {
                var w = StripPrefix(t);
                if (BlockWords.Contains(w)) reasons.Add($"黑名单命令 {w}");
                if (DangerWords.Contains(w)) danger.Add(w);
            }

            // 全串兜底：去注释、剥离非执行字符串(避免 princ 提示文本误报)，再附加执行字面量
            string scan = CommentRe.Replace(cmd, " ");
            var execSet = new HashSet<string>(tokens, StringComparer.Ordinal);
            scan = QuotedRe.Replace(scan, m => execSet.Contains(m.Value.Trim('"')) ? m.Value.Trim('"') : " ");
            if (EntdelRe.IsMatch(scan)) danger.Add("entdel/vla-delete");
            if (DangerRe.IsMatch(scan)) danger.Add("danger");

            if (FileOpRe.IsMatch(cmd)) reasons.Add("文件写删/外部程序/死循环/退出类操作");

            string? interactive = null;
            var ia = InteractRe.Match(cmd);
            if (ia.Success) interactive = ia.Value;
            if (interactive == null && SsgetRe.IsMatch(cmd) && !SsgetPresetRe.IsMatch(cmd)) interactive = "ssget(无预选)";
            if (interactive == null && !cmd.StartsWith("("))
            {
                var parts = cmd.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1 && BareInteractiveRe.IsMatch(StripPrefix(parts[0]))) interactive = "裸交互命令未带全参";
            }

            if (reasons.Count > 0) return (BLOCK, string.Join("；", reasons));
            if (interactive != null) return (INTERACTIVE, $"含交互输入 {interactive}，会卡命令队列");
            if (danger.Count > 0) return (DANGEROUS, string.Join("；", danger.Distinct()));
            return (SAFE, "");
        }
    }

    public class AiAssistantView : UserControl
    {
        private static readonly Color CadBackground = Color.FromRgb(0x3B, 0x44, 0x53);   // 根底色 #3B4453
        private static readonly Color CadHeaderBg = Color.FromRgb(0x2E, 0x34, 0x40);     // 顶底栏色 #2E3440
        private static readonly Color CadInputBg = Color.FromRgb(0x27, 0x2F, 0x3A);      // 输入框卡片底色 #272F3A
        private static readonly Color CadBorder = Color.FromRgb(0x4E, 0x5A, 0x6E);       // 控件边框色 #4E5A6E
        private static readonly Color CadAccent = Color.FromRgb(0x06, 0x96, 0xD7);       // 主强调色 #0696D7
        private static readonly Color CadTextMain = Color.FromRgb(0xF5, 0xF5, 0xF5);     // 主文字色 #F5F5F5
        private static readonly Color CadTextDim = Color.FromRgb(0x96, 0x9A, 0xA0);      // 辅助灰字 #969AA0

        // 路径自适应（2026-11-10）：随实际用户目录解析，换机不再丢失会话
        private static readonly string SessionsFilePath = CadPaths.SessionsFile;
        private readonly List<ChatSession> _sessions = new();
        private ChatSession _currentSession = new();
        private Popup? _sessionsPopup;

        // 本地端侧语义与问答极速缓存 (Key: model|selection|prompt)
        private static readonly Dictionary<string, (string content, string reasoning, int cachedTokens, int totalTokens)> _localAiCache 
            = new(StringComparer.OrdinalIgnoreCase);

        // 用量悬浮条设置与控件 (参照 ZCode 标准规范)
        private readonly UsageBarSettings _usageSettings = new();
        private readonly Border _usageBar;
        private readonly TextBlock _speedText;
        private readonly TextBlock _costText;
        private readonly TextBlock _tokensText;
        private readonly StackPanel _ctxProgressContainer;
        private readonly ProgressBar _ctxProgressBar;
        private readonly TextBlock _ctxProgressText;

        private readonly TextBlock _selectionText;
        private string _lastSelectionSummary = "当前无选区";
        // 当前选区句柄（动作块协议给模型做定向操作的凭证；2026-10-24 对齐网页版）
        private System.Collections.Generic.List<string> _lastSelectionHandles = new();
        private readonly TextBox _inputBox;
        private readonly TextBlock _placeholder;
        private readonly Button _modelSelectBtn;
        private readonly TextBlock _modelLabel;
        private readonly StackPanel _messagesPanel;
        private readonly ScrollViewer _scrollViewer;
        private readonly Border _sendBtnBorder;
        private readonly Popup _modelPopup;

        // 图片附件
        private readonly Border _imagePreviewCard;
        private readonly WpfImage _imageThumbnail;
        private readonly TextBlock _imageNameText;
        private string? _pendingImagePath;

        // 当前模型（显示名）。清单与路由一律由 ai_presets.json 驱动（AiModelCatalog），
        // 2026-11-10 起不再硬编码：此前写死的 muse-spark-1.3 在当前 apigoto 账号下不存在，
        // 选中即 403「该 API Key 未开放模型」。
        private string _currentModel = AiModelCatalog.DefaultChat.Name;

        // 真实 HTTP Client
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        // 路径自适应（2026-11-10）：配置读写件随实际用户目录解析
        private static readonly string ConfigPath = CadPaths.PresetsFile;

        // -------------------------------------------------------------
        // 动作块直接执行（2026-10-24 与网页版对齐）：从模型回复提取 <<CAD_ACTION>>{json}<<CAD_ACTION_END>>，
        // 过 ActionVetter 审查→危险类弹确认→先 QSAVE→经 PluginMain 统一执行链下发。返回清理后的展示文本。
        // -------------------------------------------------------------
        private string ExtractAndRunAiAction(string content)
        {
            try
            {
                var raw = content ?? "";
                var m = Regex.Match(raw, @"<<CAD_ACTION>>([\s\S]*?)<<CAD_ACTION_END>>");
                bool hasStart = raw.Contains("<<CAD_ACTION>>");
                if (!m.Success && !hasStart) return raw;
                string cleaned = m.Success ? raw.Replace(m.Value, "").Trim()
                    : raw.Substring(0, raw.IndexOf("<<CAD_ACTION>>")).Trim();
                string actionJson = m.Success ? m.Groups[1].Value.Trim() : null;

                string action = "", command = "";
                bool parsed = false;
                JsonElement rootEl = default;
                bool hasRootEl = false;
                if (actionJson != null)
                {
                    try
                    {
                        using (var jdoc = JsonDocument.Parse(actionJson))
                        {
                            rootEl = jdoc.RootElement.Clone();
                            hasRootEl = true;
                            if (jdoc.RootElement.TryGetProperty("action", out var a)) action = a.GetString() ?? "";
                            if (jdoc.RootElement.TryGetProperty("command", out var c)) command = c.GetString() ?? "";
                        }
                        parsed = true;
                    }
                    catch { }
                }
                if (!parsed)
                {
                    // 截断/坏块抢救：只要 "command" 字符串值完整即可还原，照常走审查执行链（2026-10-25 事故：710字节截断整块报废）
                    var am = Regex.Match(raw, "\"action\"\\s*:\\s*\"([A-Za-z_]+)\"");
                    var cm = Regex.Match(raw, "\"command\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
                    if (am.Success && cm.Success)
                    {
                        action = am.Groups[1].Value;
                        command = cm.Groups[1].Value
                            .Replace("\\\\", "\u0001").Replace("\\\"", "\"")
                            .Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\r", "\r")
                            .Replace("\u0001", "\\");
                        actionJson = JsonSerializer.Serialize(new { action, command });
                        parsed = !string.IsNullOrWhiteSpace(command);
                    }
                }
                if (!parsed)
                {
                    AddAiDirectSuccessMessage("动作块不完整，未执行",
                        "模型输出的动作 JSON 被截断且无法安全还原。请把需求拆小（一次一个操作）重发。");
                    return cleaned;
                }

                var (verdict, reason) = (ActionVetter.IsStructured(action) && hasRootEl)
                    ? ActionVetter.VetStructured(action, rootEl)
                    : ActionVetter.Vet(command);
                if (verdict == ActionVetter.BLOCK)
                {
                    AddAiDirectSuccessMessage("⛔ 已拦截未执行", $"{reason}。动作: `{Trunc(actionJson ?? command, 120)}`");
                    return cleaned;
                }
                if (verdict == ActionVetter.INTERACTIVE)
                {
                    AddAiDirectSuccessMessage("⚠ 拒绝自动执行", $"{reason}。请按提示手动操作或换个说法：`{Trunc(command, 80)}`");
                    return cleaned;
                }
                if (verdict == ActionVetter.DANGEROUS)
                {
                    bool ok = false;
                    Dispatcher.Invoke(() => ok = ShowDangerConfirm(command));
                    if (!ok)
                    {
                        AddAiDirectSuccessMessage("已取消", "用户取消了危险操作，未做任何修改");
                        return cleaned;
                    }
                    // 用户口述安全策略：确认后先保存一次再执行，可 UNDO 回退
                    CadPerceptionApp.ExecuteAiAction(JsonSerializer.Serialize(new { action = "save" }));
                }

                // 同步执行并回读真实结果（2026-11-11）：
                // 此前这里是「发出去就报成功」，导致模型/界面声称「已删除 3 个」而实际一个没删。
                // 现在把动作的真实回执取回来，只显示验证过的事实。
                // runCommand 的回执只是「已发送到命令行」，不代表命令真的干成了事。
                // 2026-11-11：对疑似删除/修改类命令，执行前后各读一次实体统计，把真实差异写进回执。
                // 否则模型写一段 LISP 发出去，面板报「已执行」，图上一个没变——正是此前的核心问题。
                bool needsDiff = action.Equals("runCommand", StringComparison.OrdinalIgnoreCase)
                                 && Regex.IsMatch(actionJson ?? "", @"(?i)(entdel|\bERASE\b|\bPURGE\b|\bDELETE\b|vla-delete)");

                string beforeStats = needsDiff ? TrySnapshotEntityStats() : "";
                var (execOk, execMsg, execData) = CadPerceptionApp.ExecuteAiActionSync(actionJson);

                string diffNote = "";
                if (needsDiff)
                {
                    // SendStringToExecute 是异步的：等一小会儿再读，避免读到执行前的状态
                    Thread.Sleep(600);
                    string afterStats = TrySnapshotEntityStats();
                    if (!string.IsNullOrEmpty(beforeStats) && !string.IsNullOrEmpty(afterStats))
                    {
                        diffNote = beforeStats == afterStats
                            ? $"\n⚠ 图纸图元没有任何变化（执行前后均为 {afterStats}）——命令很可能未真正生效"
                            : $"\n图元统计：{beforeStats} → {afterStats}";
                    }
                }

                string summary;
                if (!execOk)
                {
                    summary = $"❌ 执行未成功：{(string.IsNullOrWhiteSpace(execMsg) ? "无回执（可能仍在队列中）" : execMsg)}";
                }
                else
                {
                    // 尽量把关键计数提出来，让用户看到「实际改了多少」
                    string count = "";
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(execData))
                        {
                            using var dj = JsonDocument.Parse(execData);
                            foreach (var k in new[] { "count", "erased", "modifiedCount" })
                                if (dj.RootElement.TryGetProperty(k, out var kv) && kv.ValueKind == JsonValueKind.Number)
                                { count = $" 实际 {k}={kv.GetInt32()}"; break; }
                        }
                    }
                    catch { }
                    summary = $"✅ 已执行并读回{count}（{action}）" +
                              (string.IsNullOrWhiteSpace(execMsg) ? "" : "\n" + execMsg) + diffNote;
                }

                AddAiDirectSuccessMessage(execOk ? "⚡ 已直接在 CAD 执行（结果已回读）" : "⚠ 执行结果异常", summary);
                return cleaned;
            }
            catch (Exception ex)
            {
                AddAiDirectSuccessMessage("动作解析失败", ex.Message);
                return content ?? "";
            }
        }

        private static string Trunc(string s, int n) => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s.Substring(0, n) + "…");

        /// <summary>读一次图元统计快照（形如「Line=5, Hatch=3」），失败返回空串。2026-11-11</summary>
        private static string TrySnapshotEntityStats()
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc?.Database == null) return "";
                var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                using var tr = doc.Database.TransactionManager.StartTransaction();
                var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId id in ms)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is Entity ent)
                    {
                        string n = ent.GetType().Name;
                        counts[n] = counts.TryGetValue(n, out int c) ? c + 1 : 1;
                    }
                }
                tr.Abort();
                if (counts.Count == 0) return "空图";
                return string.Join(", ", counts.OrderByDescending(kv => kv.Value)
                    .Select(kv => $"{kv.Key}={kv.Value}"));
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// 确定性兜底：把高置信的「动词 + 对象」语句直接翻译成结构化动作 JSON（2026-11-11）。
        /// 只在模型没给动作块时调用。宁可少匹配，不可错执行——因此只覆盖对象与动词都无歧义的组合。
        /// </summary>
        private static string TrySynthesizeAction(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt)) return "";

            // 对象词 -> filter 的 types
            var typeMap = new (string Word, string Type)[]
            {
                ("填充", "Hatch"), ("充填", "Hatch"), ("剖面线", "Hatch"),
                ("直线", "Line"), ("线条", "Line"),
                ("圆弧", "Arc"),
                ("多段线", "Polyline"), ("多义线", "Polyline"),
                ("圆", "Circle"),
                ("文字", "Text"), ("文本框", "MText"),
                ("块参照", "Insert"), ("图块", "Insert"),
            };
            var found = new List<string>();
            foreach (var (word, type) in typeMap)
                if (prompt.Contains(word) && !found.Contains(type)) found.Add(type);
            // 「非直线」这类排除式：把直线剔除，其余保留
            bool excludeLine = Regex.IsMatch(prompt, @"(非|不是|除.{0,3}了?)(直线|线条)");
            if (excludeLine) found.Remove("Line");

            // 删除类
            if (Regex.IsMatch(prompt, @"(删|清空|去掉|移除|擦除)") && found.Count > 0)
            {
                return JsonSerializer.Serialize(new { action = "filter", op = "erase", types = found.ToArray() });
            }

            // 数量统计
            if (Regex.IsMatch(prompt, @"(多少|几个|数量|统计)") && found.Count > 0 && !Regex.IsMatch(prompt, @"(删|移|改|换)"))
            {
                return JsonSerializer.Serialize(new { action = "filter", op = "count", types = found.ToArray() });
            }

            // 换图层：「X 换/移/放到 Y 图层」
            var layerM = Regex.Match(prompt, @"(?:移|放|换|改|归)(?:到|至|入)\s*【?([A-Za-z0-9_\-\u4e00-\u9fa5]+)】?\s*图层");
            if (layerM.Success && Regex.IsMatch(prompt, @"(移|放|换|改|归)") && (found.Count > 0 || prompt.Contains("选中") || prompt.Contains("选区")))
            {
                string target = layerM.Groups[1].Value.Trim();
                if (found.Count > 0)
                    return JsonSerializer.Serialize(new { action = "filter", op = "setLayer", types = found.ToArray(), layer = target });
                return JsonSerializer.Serialize(new { action = "filter", op = "setLayer", layer = target });
            }

            // 图层冻结/关闭/锁定
            string? layerOp = null;
            if (prompt.Contains("冻结")) layerOp = "freeze";
            else if (Regex.IsMatch(prompt, @"(关闭|隐藏|关掉)")) layerOp = "off";
            else if (prompt.Contains("上锁") || prompt.Contains("锁定")) layerOp = "lock";
            else if (Regex.IsMatch(prompt, @"(只显示|只留|隔离)")) layerOp = "isolate";
            else if (prompt.Contains("高亮")) layerOp = "highlight";

            if (layerOp != null && prompt.Contains("图层"))
            {
                var kwM = Regex.Match(prompt, @"([\u4e00-\u9fa5A-Za-z0-9_\-]{1,8})(?:图层)");
                if (kwM.Success)
                {
                    string kw = kwM.Groups[1].Value.Trim();
                    // 去掉动词性前缀，保留纯名词作模糊匹配词
                    kw = Regex.Replace(kw, @"^(所有|全部|把|将|请|帮我把|帮我将|这些|那些)", "");
                    if (kw.Length > 0)
                        return JsonSerializer.Serialize(new { action = "layerState", op = layerOp, nameContains = kw });
                }
            }

            // 面积周长测量：必须同时有「测量动词」+「测量对象」，且指向当前选区/选中图元。
            // 2026-11-11 收紧：原先只要出现「面积」和「标」就触发，导致一段含「面积…标」的
            // 普通文本（比如测试 Markdown 表格）被误判成测量指令并真的往图上写标注。
            bool measureVerb = Regex.IsMatch(prompt, @"(算|计算|测量|测一?下|统计).{0,6}(面积|周长)")
                            || Regex.IsMatch(prompt, @"(面积|周长).{0,6}(算|计算|测量|标)");
            bool measureTarget = prompt.Contains("选中") || prompt.Contains("选区") || prompt.Contains("所选")
                              || prompt.Contains("这些图") || prompt.Contains("框选");
            if (measureVerb && measureTarget || Regex.IsMatch(prompt, @"(标|写).{0,4}(面积|周长).{0,6}(到|在)?图"))
            {
                return JsonSerializer.Serialize(new { action = "measure" });
            }

            // 导出
            if (Regex.IsMatch(prompt, @"导出") && Regex.IsMatch(prompt, @"(txt|清单|表格|文件)"))
            {
                return JsonSerializer.Serialize(new { action = "exportText" });
            }

            return "";
        }

        /// <summary>
        /// 判断回复是否在「声称已完成」（2026-11-11）。用于识别没有动作块却宣称动手的回复。
        /// 只认明确的完成式动词，避免把解释性文字误判。
        /// </summary>
        private static bool LooksLikeClaimingDone(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return false;
            return Regex.IsMatch(content,
                @"(已(经)?(直接)?(执行|删除|删掉|清除|清理|完成|处理|修改|移动|归并|标注|导出|生成|保存))" +
                @"|(操作完成)|(已提交)|(任务完成)|(处理完毕)|(✅\s*已)");
        }

        // 危险操作二次确认窗（PRD 禁 MessageBox 打断工作流；破坏性操作例外需要模态确认，样式沿用深色系）
        private bool ShowDangerConfirm(string command)
        {
            bool result = false;
            var win = new Window
            {
                Title = "删除类操作需要确认",
                Width = 400,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x26, 0x33)),
                Foreground = new SolidColorBrush(CadTextMain),
                FontFamily = new FontFamily("Segoe UI Variable Text, Microsoft YaHei UI"),
                FontSize = 12,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow
            };
            var panel = new StackPanel { Margin = new Thickness(18) };
            panel.Children.Add(new TextBlock
            {
                Text = "⚠ 即将执行删除/清理类操作",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24)),
                Margin = new Thickness(0, 0, 0, 8)
            });
            panel.Children.Add(new TextBlock
            {
                Text = $"动作：{command}",
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
                Margin = new Thickness(0, 0, 0, 8)
            });
            panel.Children.Add(new TextBlock
            {
                Text = "确认后将先自动保存 (QSAVE) 再执行；出错可点面板顶栏撤销 (UNDO) 回退。",
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1)),
                Margin = new Thickness(0, 0, 0, 14)
            });
            var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
            var cancel = new Button { Content = "取消", Width = 84, Height = 30, Margin = new Thickness(0, 0, 10, 0) };
            var confirm = new Button
            {
                Content = "确认执行",
                Width = 96,
                Height = 30,
                Background = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold
            };
            cancel.Click += (s, e) => { result = false; win.Close(); };
            confirm.Click += (s, e) => { result = true; win.Close(); };
            btns.Children.Add(cancel);
            btns.Children.Add(confirm);
            panel.Children.Add(btns);
            win.Content = panel;
            win.ShowDialog();
            return result;
        }

        public AiAssistantView()
        {
            Background = new SolidColorBrush(CadBackground);
            Foreground = new SolidColorBrush(CadTextMain);
            FontFamily = new FontFamily("Segoe UI Variable Text, Microsoft YaHei UI, Segoe UI");
            FontSize = 12;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                      // 0: Header
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 1: Messages
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                      // 2: Selection Perception
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                      // 3: Token Usage Bar
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                      // 4: Image Attachment
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                      // 5: Bottom Input Card

            // -------------------------------------------------------------
            // 1. 顶栏 (Header)
            // -------------------------------------------------------------
            var headerBorder = new Border
            {
                Background = new SolidColorBrush(CadHeaderBg),
                Padding = new Thickness(10, 7, 10, 7),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x22, 0x29, 0x33)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            var headerDock = new DockPanel();
            var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var botIcon = CreateSvgPath(CadSvg.Robot, 14, 14, new SolidColorBrush(CadAccent));
            botIcon.Margin = new Thickness(0, 0, 6, 0);

            var titleText = new TextBlock
            {
                Text = "AI助理",
                Foreground = new SolidColorBrush(CadTextMain),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            titleStack.Children.Add(botIcon);
            titleStack.Children.Add(titleText);
            DockPanel.SetDock(titleStack, Dock.Left);
            headerDock.Children.Add(titleStack);

            var rightActionStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };

            var newChatBtn = CreateIconButton(CadSvg.Plus, "新建会话 (开启全新会话与上下文)", (s, e) => StartNewSession());
            Button historyBtn = null!;
            historyBtn = CreateIconButton(CadSvg.List, "会话历史列表 (查看/切换历史对话)", (s, e) => ShowSessionsPopup(historyBtn));
            var undoHeaderBtn = CreateIconButton(CadSvg.Undo, "一键撤回 CAD 修改 (Ctrl+Z / UNDO)", (s, e) => TriggerCadUndo());
            var cfgBtn = CreateIconButton(CadSvg.Settings, "图形化 AI 助理配置", (s, e) => OpenSettingsDialog());

            rightActionStack.Children.Add(newChatBtn);
            rightActionStack.Children.Add(historyBtn);
            rightActionStack.Children.Add(undoHeaderBtn);
            rightActionStack.Children.Add(cfgBtn);
            DockPanel.SetDock(rightActionStack, Dock.Right);
            headerDock.Children.Add(rightActionStack);

            headerBorder.Child = headerDock;
            Grid.SetRow(headerBorder, 0);
            rootGrid.Children.Add(headerBorder);

            // -------------------------------------------------------------
            // 2. 消息流 (配备 2px 极细圆角滚动条，彻底消灭原生白色粗长条)
            // -------------------------------------------------------------
            _messagesPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 4, 0, 8) };
            _scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(10, 8, 4, 4),
                Content = _messagesPanel
            };

            // 应用 CAD 2px 极细圆角滚动条模板
            ApplyUltraSlimScrollBarStyle(_scrollViewer);

            Grid.SetRow(_scrollViewer, 1);
            rootGrid.Children.Add(_scrollViewer);

            // -------------------------------------------------------------
            // 3. 选区感知微状态条 (Selection Perception)
            // -------------------------------------------------------------
            var selBar = new Border { Background = Brushes.Transparent, Padding = new Thickness(12, 2, 12, 6) };
            var selDock = new DockPanel();
            var dot = new WpfEllipse
            {
                Width = 6,
                Height = 6,
                Fill = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            _selectionText = new TextBlock
            {
                Text = "实时感知: 当前未选择图元 (在 CAD 中框选图元可直接操作)",
                Foreground = new SolidColorBrush(CadTextDim),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            selDock.Children.Add(dot);
            selDock.Children.Add(_selectionText);
            selBar.Child = selDock;
            Grid.SetRow(selBar, 2);
            rootGrid.Children.Add(selBar);

            // -------------------------------------------------------------
            // 2.5 AI Token 用量悬浮胶囊条 (ZCode 架构标准)
            // -------------------------------------------------------------
            _usageBar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x27, 0x2E, 0x3A)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3B, 0x47, 0x58)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(10, 0, 10, 6),
                Visibility = System.Windows.Visibility.Visible
            };

            var usageDock = new DockPanel();
            var usageLeftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            _speedText = new TextBlock
            {
                Text = "⚡ -- t/s",
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)),
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 8, 0),
                ToolTip = "实时 Token 生成速度 (Tokens Per Second)"
            };

            _costText = new TextBlock
            {
                Text = "¥0.0000",
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 8, 0),
                ToolTip = "当前会话累计 API 目录折算费用 (参考 ZCode prices.json 官方标准)"
            };

            _tokensText = new TextBlock
            {
                Text = "∑ 0 tok",
                FontSize = 10,
                Foreground = new SolidColorBrush(CadTextDim),
                Margin = new Thickness(0, 0, 8, 0),
                ToolTip = "当前会话累计消耗的总 Tokens"
            };

            _ctxProgressContainer = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _ctxProgressContainer.Children.Add(new TextBlock { Text = "1M: ", FontSize = 9.5, Foreground = new SolidColorBrush(CadTextDim) });
            _ctxProgressBar = new ProgressBar
            {
                Width = 40,
                Height = 4,
                Minimum = 0,
                Maximum = 1000000,
                Value = 0,
                Foreground = new SolidColorBrush(CadAccent),
                Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x26, 0x33)),
                BorderThickness = new Thickness(0),
                Margin = new Thickness(2, 0, 4, 0)
            };
            _ctxProgressText = new TextBlock { Text = "0%", FontSize = 9.5, Foreground = new SolidColorBrush(CadTextDim) };
            _ctxProgressContainer.Children.Add(_ctxProgressBar);
            _ctxProgressContainer.Children.Add(_ctxProgressText);

            usageLeftStack.Children.Add(_speedText);
            usageLeftStack.Children.Add(_costText);
            usageLeftStack.Children.Add(_tokensText);
            usageLeftStack.Children.Add(_ctxProgressContainer);
            DockPanel.SetDock(usageLeftStack, Dock.Left);
            usageDock.Children.Add(usageLeftStack);

            var settingsMiniBtn = CreateIconButton(CadSvg.Settings, "配置用量条显示项与独立开关", (s, e) => OpenSettingsDialog());
            settingsMiniBtn.Width = 20;
            settingsMiniBtn.Height = 20;
            settingsMiniBtn.Opacity = 0.7;
            DockPanel.SetDock(settingsMiniBtn, Dock.Right);
            usageDock.Children.Add(settingsMiniBtn);

            _usageBar.Child = usageDock;
            Grid.SetRow(_usageBar, 3);
            rootGrid.Children.Add(_usageBar);

            // -------------------------------------------------------------
            // 3.5 图片附件预览条
            // -------------------------------------------------------------
            _imagePreviewCard = new Border
            {
                Background = new SolidColorBrush(CadHeaderBg),
                BorderBrush = new SolidColorBrush(CadBorder),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6),
                Margin = new Thickness(10, 0, 10, 6),
                Visibility = System.Windows.Visibility.Collapsed
            };
            var imgDock = new DockPanel();
            _imageThumbnail = new WpfImage { Width = 40, Height = 40, Stretch = Stretch.UniformToFill, Margin = new Thickness(0, 0, 8, 0) };
            _imageNameText = new TextBlock
            {
                Text = "CAD当前视口截图",
                Foreground = new SolidColorBrush(CadTextMain),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            var closeImgBtn = CreateIconButton(CadSvg.Close, "移除图片", (s, e) => RemovePendingImage());
            DockPanel.SetDock(closeImgBtn, Dock.Right);
            imgDock.Children.Add(closeImgBtn);
            imgDock.Children.Add(_imageThumbnail);
            imgDock.Children.Add(_imageNameText);
            _imagePreviewCard.Child = imgDock;
            Grid.SetRow(_imagePreviewCard, 4);
            rootGrid.Children.Add(_imagePreviewCard);

            // -------------------------------------------------------------
            // 4. 底部输入卡片 (Bottom Input Card)
            // -------------------------------------------------------------
            var bottomContainer = new Border
            {
                Margin = new Thickness(10, 0, 10, 10),
                Background = new SolidColorBrush(CadInputBg),
                BorderBrush = new SolidColorBrush(CadBorder),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 8, 8, 8)
            };

            var inputCardGrid = new Grid();
            inputCardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            inputCardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 文本框 + Placeholder
            var textContainer = new Grid();
            _placeholder = new TextBlock
            {
                Text = "Ask anything, 输入需求或指令 (支持 Ctrl+V 粘贴截图)...",
                Foreground = new SolidColorBrush(CadTextDim),
                FontSize = 12,
                Margin = new Thickness(2, 2, 0, 0),
                IsHitTestVisible = false
            };

            _inputBox = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(CadTextMain),
                CaretBrush = new SolidColorBrush(CadTextMain),
                FontSize = 12,
                MinHeight = 36,
                MaxHeight = 110,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(0)
            };

            _inputBox.TextChanged += (s, e) =>
            {
                _placeholder.Visibility = string.IsNullOrEmpty(_inputBox.Text) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            };

            _inputBox.PreviewKeyDown += HandleInputKeyDown;

            textContainer.Children.Add(_placeholder);
            textContainer.Children.Add(_inputBox);
            Grid.SetRow(textContainer, 0);
            inputCardGrid.Children.Add(textContainer);

            // 工具条
            var toolbarGrid = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // 模型切换按钮
            _modelSelectBtn = new Button
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0, 2, 4, 2),
                Cursor = Cursors.Hand,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left
            };
            var modelBtnStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var plusSvg = CreateSvgPath(CadSvg.Plus, 11, 11, new SolidColorBrush(CadTextDim));
            plusSvg.Margin = new Thickness(0, 0, 4, 0);

            _modelLabel = new TextBlock
            {
                Text = _currentModel,
                Foreground = new SolidColorBrush(CadTextDim),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            var arrowSvg = CreateSvgPath(CadSvg.ChevronUp, 11, 11, new SolidColorBrush(CadTextDim));
            arrowSvg.Margin = new Thickness(3, 0, 0, 0);

            modelBtnStack.Children.Add(plusSvg);
            modelBtnStack.Children.Add(_modelLabel);
            modelBtnStack.Children.Add(arrowSvg);
            _modelSelectBtn.Content = modelBtnStack;

            _modelPopup = CreateAntigravityModelPopup();
            _modelSelectBtn.Click += (s, e) =>
            {
                _modelPopup.IsOpen = !_modelPopup.IsOpen;
            };

            Grid.SetColumn(_modelSelectBtn, 0);
            toolbarGrid.Children.Add(_modelSelectBtn);

            // 右下角按钮组
            var rightToolStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var imgBtn = CreateIconButton(CadSvg.Image, "附加当前 CAD 视口截图 / 本地图片", (s, e) => AttachViewportOrImage());
            imgBtn.Margin = new Thickness(0, 0, 4, 0);
            rightToolStack.Children.Add(imgBtn);

            var micSvg = CreateSvgPath(CadSvg.Mic, 13, 13, new SolidColorBrush(CadTextDim));
            micSvg.Margin = new Thickness(0, 0, 8, 0);
            rightToolStack.Children.Add(micSvg);

            _sendBtnBorder = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = new SolidColorBrush(CadAccent),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            var sendArrow = CreateSvgPath(CadSvg.SendArrow, 11, 11, Brushes.White);
            sendArrow.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            sendArrow.VerticalAlignment = VerticalAlignment.Center;
            _sendBtnBorder.Child = sendArrow;

            _sendBtnBorder.MouseLeftButtonDown += (s, e) => ExecuteCommand();
            _sendBtnBorder.MouseEnter += (s, e) => _sendBtnBorder.Background = new SolidColorBrush(Color.FromRgb(0x1B, 0xAC, 0xEE));
            _sendBtnBorder.MouseLeave += (s, e) => _sendBtnBorder.Background = new SolidColorBrush(CadAccent);

            rightToolStack.Children.Add(_sendBtnBorder);
            Grid.SetColumn(rightToolStack, 1);
            toolbarGrid.Children.Add(rightToolStack);

            Grid.SetRow(toolbarGrid, 1);
            inputCardGrid.Children.Add(toolbarGrid);

            bottomContainer.Child = inputCardGrid;
            Grid.SetRow(bottomContainer, 5);
            rootGrid.Children.Add(bottomContainer);

            Content = rootGrid;


            // 载入用量条独立开关与初始配置
            LoadUsageSettings();
            AiSettingsWindow.OnSettingsSaved += () => Dispatcher.Invoke(LoadUsageSettings);

            // 载入持久化历史会话并初始化界面
            LoadSessionsAndInit();

            // 版本戳：开面板即知运行的 DLL 新旧（2026-10-25 用户要求，杜绝"不知道新的是旧的"）
            try
            {
                string buildTime = System.IO.File.GetLastWriteTime(System.Reflection.Assembly.GetExecutingAssembly().Location).ToString("yyyy-MM-dd HH:mm");
                AddAiMessage($"[版本] CadCopilot build {buildTime}（即 cadsys\\NET\\CadPerceptionPlugin.dll 的文件时间）");
            }
            catch { }
        }

        // -------------------------------------------------------------
        // CAD 2px 极细圆角滚动条样式 (100% 消除原生 16px 粗白条与上下箭头)
        // -------------------------------------------------------------
        private static void ApplyUltraSlimScrollBarStyle(ScrollViewer sv)
        {
            try
            {
                string xaml = @"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
       xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
       TargetType='ScrollBar'>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='Width' Value='2'/>
    <Setter Property='MinWidth' Value='2'/>
    <Setter Property='Template'>
        <Setter.Value>
            <ControlTemplate TargetType='ScrollBar'>
                <Grid Width='2' Background='Transparent' SnapsToDevicePixels='True'>
                    <Track x:Name='PART_Track' IsDirectionReversed='True'>
                        <Track.Thumb>
                            <Thumb>
                                <Thumb.Template>
                                    <ControlTemplate TargetType='Thumb'>
                                        <Border Background='#556072' Width='2' MinWidth='2' MaxWidth='2' CornerRadius='1'/>
                                    </ControlTemplate>
                                </Thumb.Template>
                            </Thumb>
                        </Track.Thumb>
                    </Track>
                </Grid>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>";
                var style = (Style)System.Windows.Markup.XamlReader.Parse(xaml);
                sv.Resources.Add(typeof(ScrollBar), style);
            }
            catch { }
        }

        // -------------------------------------------------------------
        // 纯净 Antigravity Popup 模型选择器
        // -------------------------------------------------------------
        private Popup CreateAntigravityModelPopup()
        {
            var popup = new Popup
            {
                PlacementTarget = _modelSelectBtn,
                Placement = PlacementMode.Top,
                StaysOpen = false,
                AllowsTransparency = true
            };

            var container = new Border
            {
                Background = new SolidColorBrush(CadInputBg),
                BorderBrush = new SolidColorBrush(CadBorder),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6),
                Width = 260
            };

            var stack = new StackPanel();
            var header = new TextBlock
            {
                Text = "Model",
                Foreground = new SolidColorBrush(CadTextDim),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(8, 4, 8, 6)
            };
            stack.Children.Add(header);

            foreach (var item in AiModelCatalog.ChatModels)
            {
                var itemBorder = new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(8, 6, 8, 6),
                    Cursor = Cursors.Hand,
                    Background = Brushes.Transparent,
                    Margin = new Thickness(0, 1, 0, 1)
                };

                var itemGrid = new Grid();
                itemGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                itemGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var nameTb = new TextBlock
                {
                    Text = item.Name,
                    Foreground = new SolidColorBrush(CadTextMain),
                    FontSize = 12
                };
                var descTb = new TextBlock
                {
                    Text = item.CapabilityLine,
                    Foreground = new SolidColorBrush(CadTextDim),
                    FontSize = 10,
                    Margin = new Thickness(0, 2, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };

                Grid.SetRow(nameTb, 0);
                Grid.SetRow(descTb, 1);
                itemGrid.Children.Add(nameTb);
                itemGrid.Children.Add(descTb);
                itemBorder.Child = itemGrid;

                string mName = item.Name;
                itemBorder.MouseEnter += (s, e) => itemBorder.Background = new SolidColorBrush(Color.FromRgb(0x3B, 0x44, 0x53));
                itemBorder.MouseLeave += (s, e) => itemBorder.Background = Brushes.Transparent;
                itemBorder.MouseLeftButtonDown += (s, e) =>
                {
                    _currentModel = mName;
                    _modelLabel.Text = _currentModel;
                    popup.IsOpen = false;
                };

                stack.Children.Add(itemBorder);
            }

            container.Child = stack;
            popup.Child = container;
            return popup;
        }

        // -------------------------------------------------------------
        // 多模态图片支持
        // -------------------------------------------------------------
        private void HandleInputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                if (Clipboard.ContainsImage())
                {
                    var img = Clipboard.GetImage();
                    if (img != null)
                    {
                        SaveAndSetPendingImage(img, "剪贴板截图");
                        e.Handled = true;
                        return;
                    }
                }
            }

            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true;
                ExecuteCommand();
            }
        }

        private void AttachViewportOrImage()
        {
            try
            {
                string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CadPerception");
                Directory.CreateDirectory(tempDir);
                string screenPath = System.IO.Path.Combine(tempDir, $"viewport_{DateTime.Now:yyyyMMdd_HHmmss}.png");

                // 2026-11-10 修复：原实现只用 Graphics.CopyFromScreen，RDP 会话断开（或最小化）时
                // 桌面 DC 无效，必然抛「句柄无效」，视口识图整块失效。改为三级降级：
                //   1) CadViewportCapture.PrintWindow 抓 CAD 主窗体（会话断开也有效）
                //   2) CopyFromScreen（会话在线时最忠实）
                //   3) 抛错交给下面的提示气泡
                bool saved = CadViewportCapture.TryCapture(screenPath);
                if (!saved)
                {
                    using (var bmp = new System.Drawing.Bitmap(800, 600))
                    {
                        using (var g = System.Drawing.Graphics.FromImage(bmp))
                        {
                            g.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(800, 600));
                        }
                        bmp.Save(screenPath, System.Drawing.Imaging.ImageFormat.Png);
                    }
                }

                SetPendingImagePath(screenPath, "CAD 当前视口快照");
            }
            catch (Exception ex)
            {
                AddAiMessage("[提示] 视口快照生成失败: " + ex.Message);
            }
        }

        private void SaveAndSetPendingImage(BitmapSource img, string title)
        {
            string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CadPerception");
            Directory.CreateDirectory(tempDir);
            string filePath = System.IO.Path.Combine(tempDir, $"clip_{DateTime.Now:yyyyMMdd_HHmmss}.png");

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(img));
                encoder.Save(fileStream);
            }

            SetPendingImagePath(filePath, title);
        }

        private void SetPendingImagePath(string path, string title)
        {
            _pendingImagePath = path;
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(path);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();
                _imageThumbnail.Source = bi;
                _imageNameText.Text = title;
                _imagePreviewCard.Visibility = System.Windows.Visibility.Visible;
            }
            catch { }
        }

        private void RemovePendingImage()
        {
            _pendingImagePath = null;
            _imageThumbnail.Source = null;
            _imagePreviewCard.Visibility = System.Windows.Visibility.Collapsed;
        }

        private void OpenSettingsDialog()
        {
            try
            {
                var win = new AiSettingsWindow();
                win.ShowDialog();
            }
            catch (Exception ex)
            {
                AddAiMessage("[错误] 无法打开配置窗口: " + ex.Message);
            }
        }

        // -------------------------------------------------------------
        // Token 用量监控悬浮条设置与指标刷新 (ZCode 架构标准)
        // -------------------------------------------------------------
        private void LoadUsageSettings()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var node = JsonNode.Parse(File.ReadAllText(ConfigPath));
                    var usageNode = node?["usage_bar_settings"];
                    if (usageNode != null)
                    {
                        _usageSettings.Enabled = usageNode["enabled"]?.GetValue<bool>() ?? true;
                        _usageSettings.ShowSpeed = usageNode["show_speed"]?.GetValue<bool>() ?? true;
                        _usageSettings.ShowCost = usageNode["show_cost"]?.GetValue<bool>() ?? true;
                        _usageSettings.ShowTokens = usageNode["show_tokens"]?.GetValue<bool>() ?? true;
                        _usageSettings.ShowContextBar = usageNode["show_context_bar"]?.GetValue<bool>() ?? true;
                    }
                }
            }
            catch { }

            ApplyUsageSettings();
            UpdateUsageBarMetrics();
        }

        private void ApplyUsageSettings()
        {
            _usageBar.Visibility = _usageSettings.Enabled ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            _speedText.Visibility = _usageSettings.ShowSpeed ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            _costText.Visibility = _usageSettings.ShowCost ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            _tokensText.Visibility = _usageSettings.ShowTokens ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            _ctxProgressContainer.Visibility = _usageSettings.ShowContextBar ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        private void UpdateUsageBarMetrics(double latestDurationSec = 0, int latestTokens = 0, int latestCachedTokens = 0)
        {
            int sessionTotalTokens = 0;
            double sessionTotalCost = 0;

            foreach (var m in _currentSession.Messages)
            {
                if (m.Role == "assistant")
                {
                    sessionTotalTokens += m.TotalTokens;
                    sessionTotalCost += TokenPricingCalculator.CalculateCost(_currentModel, m.TotalTokens, m.CachedTokens);
                }
            }

            double tps = (latestDurationSec > 0 && latestTokens > 0) ? (latestTokens / latestDurationSec) : 0;
            if (tps > 0)
            {
                _speedText.Text = $"⚡ {tps:0.#} t/s";
            }

            _costText.Text = $"¥{sessionTotalCost:0.0000}";
            _tokensText.Text = $"∑ {sessionTotalTokens:N0} tok";

            _ctxProgressBar.Value = Math.Min(1000000, sessionTotalTokens);
            double pct = sessionTotalTokens * 100.0 / 1000000.0;
            _ctxProgressText.Text = $"{pct:0.0}%";

            ApplyUsageSettings();
        }

        // -------------------------------------------------------------
        // 会话持久化与长上下文管理 (ZCode / Codex 架构标准)
        // -------------------------------------------------------------
        private void LoadSessionsAndInit()
        {
            try
            {
                if (File.Exists(SessionsFilePath))
                {
                    string json = File.ReadAllText(SessionsFilePath);
                    var list = JsonSerializer.Deserialize<List<ChatSession>>(json);
                    if (list != null && list.Count > 0)
                    {
                        _sessions.Clear();
                        _sessions.AddRange(list);
                        SwitchToSession(_sessions[0], false);
                        return;
                    }
                }
            }
            catch { }

            StartNewSession();
        }

        private void SaveSessionsToDisk()
        {
            Task.Run(() =>
            {
                try
                {
                    lock (_sessions)
                    {
                        if (!_sessions.Contains(_currentSession))
                        {
                            _sessions.Insert(0, _currentSession);
                        }
                        var json = JsonSerializer.Serialize(_sessions, new JsonSerializerOptions { WriteIndented = true });
                        CadPaths.EnsureNetDir();
                        File.WriteAllText(SessionsFilePath, json);
                    }
                }
                catch (Exception ex)
                {
                    // 历史行为是静默 catch，会话丢失无从查证；改为落日志
                    Logger.LogException("SaveSessionsToDisk", ex);
                }
            });
        }

        private void StartNewSession()
        {
            if (_currentSession.Messages.Count > 0 && !_sessions.Contains(_currentSession))
            {
                _sessions.Insert(0, _currentSession);
            }

            _currentSession = new ChatSession();
            _sessions.Insert(0, _currentSession);
            _messagesPanel.Children.Clear();
            AddAiMessage("你好！我是 AutoCAD 原生伴随助理 CadCopilot。\n已开启全新会话，大模型与图纸实时感知通道已就绪，框选图元后直接在下方输入需求即可操作。");
            SaveSessionsToDisk();
            UpdateUsageBarMetrics();
            _scrollViewer.ScrollToTop();
        }

        private void SwitchToSession(ChatSession session, bool shouldSave = true)
        {
            if (shouldSave && _currentSession.Messages.Count > 0 && !_sessions.Contains(_currentSession))
            {
                _sessions.Insert(0, _currentSession);
            }

            _currentSession = session;
            if (_sessionsPopup != null) _sessionsPopup.IsOpen = false;
            _messagesPanel.Children.Clear();

            if (_currentSession.Messages.Count == 0)
            {
                AddAiMessage("你好！我是 AutoCAD 原生伴随助理 CadCopilot。\n大模型与图纸实时感知通道已就绪，框选图元后直接在下方输入需求即可操作。");
            }
            else
            {
                foreach (var msg in _currentSession.Messages)
                {
                    if (msg.Role == "user")
                    {
                        AddUserMessage(!string.IsNullOrEmpty(msg.ImagePath) ? $"{msg.Content}\n[已附加图纸视口图片]" : msg.Content);
                    }
                    else if (msg.Role == "assistant")
                    {
                        var card = new Border
                        {
                            Background = Brushes.Transparent,
                            Padding = new Thickness(0, 2, 4, 8),
                            Margin = new Thickness(0, 2, 0, 6),
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
                        };
                        card.Child = CreateAiResponseContent(msg.Reasoning ?? "", msg.Content, msg.DurationSec, "", null, msg.CachedTokens, msg.TotalTokens, msg.IsLocalCacheHit);
                        _messagesPanel.Children.Add(card);
                    }
                }
            }

            _scrollViewer.ScrollToEnd();
            UpdateUsageBarMetrics();
            if (shouldSave) SaveSessionsToDisk();
        }

        private void ShowSessionsPopup(FrameworkElement anchor)
        {
            if (_sessionsPopup == null)
            {
                _sessionsPopup = new Popup
                {
                    StaysOpen = false,
                    AllowsTransparency = true,
                    Placement = PlacementMode.Bottom,
                    PlacementTarget = anchor,
                    PopupAnimation = PopupAnimation.Fade
                };
            }

            var container = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x27, 0x2E, 0x3A)),
                BorderBrush = new SolidColorBrush(CadBorder),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 8, 8, 8),
                Width = 270,
                MaxHeight = 360
            };

            var mainStack = new StackPanel();

            // 标题栏：会话历史 (N) + 新建按钮
            var topDock = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var title = new TextBlock
            {
                Text = $"会话历史 ({_sessions.Count})",
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(CadTextDim),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(title, Dock.Left);
            topDock.Children.Add(title);

            var newBtnInPopup = new Border
            {
                Cursor = Cursors.Hand,
                Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x26, 0x33)),
                BorderBrush = new SolidColorBrush(CadAccent),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2)
            };
            var newTb = new TextBlock { Text = "+ 新建会话", FontSize = 10, Foreground = new SolidColorBrush(CadAccent) };
            newBtnInPopup.Child = newTb;
            newBtnInPopup.MouseLeftButtonDown += (s, e) =>
            {
                _sessionsPopup.IsOpen = false;
                StartNewSession();
            };
            DockPanel.SetDock(newBtnInPopup, Dock.Right);
            topDock.Children.Add(newBtnInPopup);
            mainStack.Children.Add(topDock);

            // 列表滚动区
            var sv = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = 280
            };
            ApplyUltraSlimScrollBarStyle(sv);

            var listStack = new StackPanel();

            if (_sessions.Count == 0 && _currentSession.Messages.Count > 0)
            {
                _sessions.Insert(0, _currentSession);
            }

            foreach (var session in _sessions.ToList())
            {
                bool isCurrent = session.Id == _currentSession.Id;
                var card = new Border
                {
                    Cursor = Cursors.Hand,
                    Background = isCurrent ? new SolidColorBrush(Color.FromRgb(0x31, 0x3B, 0x4A)) : Brushes.Transparent,
                    BorderBrush = isCurrent ? new SolidColorBrush(CadAccent) : Brushes.Transparent,
                    BorderThickness = new Thickness(isCurrent ? 1 : 0),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 6, 6, 6),
                    Margin = new Thickness(0, 0, 0, 4)
                };

                var itemDock = new DockPanel();

                var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var titleTb = new TextBlock
                {
                    Text = session.Title,
                    FontSize = 11.5,
                    Foreground = new SolidColorBrush(isCurrent ? Color.FromRgb(0x38, 0xBD, 0xF8) : CadTextMain),
                    FontWeight = isCurrent ? FontWeights.SemiBold : FontWeights.Normal,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 190
                };
                var timeTb = new TextBlock
                {
                    Text = $"{session.UpdatedAt} · {session.Messages.Count}条对话",
                    FontSize = 9.5,
                    Foreground = new SolidColorBrush(CadTextDim),
                    Margin = new Thickness(0, 2, 0, 0)
                };
                textStack.Children.Add(titleTb);
                textStack.Children.Add(timeTb);
                DockPanel.SetDock(textStack, Dock.Left);
                itemDock.Children.Add(textStack);

                // 删除按钮
                var delBtn = CreateIconButton(CadSvg.Trash, "删除该会话", (s, e) =>
                {
                    _sessions.Remove(session);
                    if (_currentSession.Id == session.Id)
                    {
                        StartNewSession();
                    }
                    SaveSessionsToDisk();
                    ShowSessionsPopup(anchor);
                });
                delBtn.Opacity = 0.6;
                DockPanel.SetDock(delBtn, Dock.Right);
                itemDock.Children.Add(delBtn);

                card.Child = itemDock;

                card.MouseEnter += (s, e) =>
                {
                    if (session.Id != _currentSession.Id)
                        card.Background = new SolidColorBrush(Color.FromRgb(0x2E, 0x37, 0x46));
                };
                card.MouseLeave += (s, e) =>
                {
                    if (session.Id != _currentSession.Id)
                        card.Background = Brushes.Transparent;
                };
                card.MouseLeftButtonDown += (s, e) =>
                {
                    if (e.OriginalSource is Visual v && VisualTreeHelper.GetParent(v) is Button) return;
                    SwitchToSession(session);
                };

                listStack.Children.Add(card);
            }

            if (_sessions.Count == 0)
            {
                listStack.Children.Add(new TextBlock
                {
                    Text = "暂无历史会话",
                    Foreground = new SolidColorBrush(CadTextDim),
                    FontSize = 11,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 16, 0, 16)
                });
            }

            sv.Content = listStack;
            mainStack.Children.Add(sv);
            container.Child = mainStack;
            _sessionsPopup.Child = container;
            _sessionsPopup.IsOpen = true;
        }

        // -------------------------------------------------------------
        // 执行用户指令：真正调用大模型 API + 执行 CAD 本地原子动作
        // -------------------------------------------------------------
        private async void ExecuteCommand()
        {
            string prompt = _inputBox.Text.Trim();
            if (string.IsNullOrEmpty(prompt) && string.IsNullOrEmpty(_pendingImagePath)) return;

            string attachedImg = _pendingImagePath ?? string.Empty;
            _inputBox.Text = string.Empty;
            RemovePendingImage();

            SubmitPrompt(prompt, attachedImg);
        }

        /// <summary>
        /// 可编程提交入口（2026-11-11）：把「用户输入框内容 + 回车/点发送」之后的所有处理
        /// 收拢到这里，供 ExecuteCommand 与外部驱动共用。
        /// 动机：RDP 会话无前台焦点时键盘事件被系统丢弃，无法自动化验收「面板内真实发送」。
        /// 有了这个入口，验收脚本可以像真人一样把话送进面板并读回回复，不必依赖物理键盘。
        /// </summary>
        public async void SubmitPrompt(string prompt, string attachedImg)
        {

            // 1. 添加用户气泡
            AddUserMessage(!string.IsNullOrEmpty(attachedImg) ? $"{prompt}\n[已附加图纸视口图片]" : prompt);

            // 记录用户消息进当前会话持久记忆
            _currentSession.Messages.Add(new ChatMessageItem
            {
                Role = "user",
                Content = prompt,
                ImagePath = attachedImg,
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            });
            if (_currentSession.Title == "新会话" || string.IsNullOrEmpty(_currentSession.Title))
            {
                string firstLine = prompt.Split('\n')[0].Trim();
                _currentSession.Title = firstLine.Length > 16 ? firstLine.Substring(0, 16) + "..." : firstLine;
            }
            _currentSession.UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            SaveSessionsToDisk();

            var doc = Application.DocumentManager.MdiActiveDocument;

            // 3. 直通 CAD 命令行 (/ 或 . 或 _ 开头的显式命令)
            if (prompt.StartsWith("/") || prompt.StartsWith(".") || prompt.StartsWith("_"))
            {
                string rawCmd = prompt.TrimStart('/', '.');
                if (!string.IsNullOrWhiteSpace(rawCmd) && doc != null)
                {
                    doc.SendStringToExecute(rawCmd + "\n", true, false, false);
                    AddAiDirectSuccessMessage("已直接发送至 CAD 命令行", $"命令已成功注入执行：`{rawCmd}`");
                    return;
                }
            }

            // 4. 意图快速分支（仅限**明确无歧义**的祈使句式；2026-11-10 收窄）
            // 历史问题：原先只要命中「填充」「墙体」「文字」「压扁」任一词就劫持到硬编码函数，
            // 用户说「把所有的填充删掉」会命中「填充+墙体」分支去转墙体，根本不删——表现即为答非所问。
            // 现在：只保留「动作词 + 对象词同时出现」的强特征，其余一律交给大模型走动作块。
            if (doc != null)
            {
                // A. 文字图层转移（必须同时出现「文字」与显式的「移到/放到/归类 + 图层」句式）
                if (prompt.Contains("文字") && Regex.IsMatch(prompt, @"(?:移|放|归|换)(?:到|至|入)\S{0,8}图层"))
                {
                    string targetLayer = "文字";
                    var matchLayer = Regex.Match(prompt, @"(?:至|到|放|归入|归类到|移到|换到)【?([a-zA-Z0-9_\-\u4e00-\u9fa5]+)】?图层");
                    if (matchLayer.Success && !string.IsNullOrWhiteSpace(matchLayer.Groups[1].Value))
                    {
                        targetLayer = matchLayer.Groups[1].Value.Trim();
                    }
                    ExecuteMoveTextToLayer(doc, targetLayer);
                    return;
                }

                // B. 选区深度解析：必须是明确的「分析/解析 + 选区/选中」询问式
                if (Regex.IsMatch(prompt, @"(?:解析|分析|看看|查一下|查下).{0,6}(?:选区|选中|所选)") &&
                    !Regex.IsMatch(prompt, @"(?:删|移|改|换|放到|清|填)"))
                {
                    ExecuteAnalyzeSelection(doc);
                    return;
                }

                // C. 填充转墙体：必须明确说「转/变/换」成墙体，光提「填充」不再命中
                if (prompt.Contains("填充") && prompt.Contains("墙体") &&
                    Regex.IsMatch(prompt, @"(?:转|变|换|改)(?:成|为)"))
                {
                    ExecuteSwitchHatchToWall(doc);
                    return;
                }

                // D. 压平 Z 轴
                if (prompt.Contains("压平") && !prompt.Contains("删"))
                {
                    ExecuteFlatten(doc);
                    return;
                }

                // E. 清理图纸：必须是「清理/清除」且明确指向块或图纸本身
                if ((prompt.Contains("清理图纸") || prompt.Contains("清除垃圾") ||
                     prompt.Equals("purge", StringComparison.OrdinalIgnoreCase)) &&
                    !prompt.Contains("填充") && !prompt.Contains("线"))
                {
                    ExecutePurgeDrawing(doc);
                    return;
                }
            }

            // 5. 真正调用大模型 (DeepSeek-Flash / Kimi / Muse) 进行智能解答
            await CallLanguageModelApiAsync(prompt, attachedImg);
        }

        // -------------------------------------------------------------
        // 真实大模型 API 异步调用与流式气泡渲染
        // -------------------------------------------------------------
        private async Task CallLanguageModelApiAsync(string prompt, string? imagePath)
        {
            var (thinkingCard, stopTimer) = AddAiThinkingCard();

            try
            {
                // 每个模型用自己的 baseUrl / apiKey / modelId（2026-11-10：改为配置驱动，
                // 原实现无条件用 apigoto 的 Key，导致硅基流动等其它网关的模型选了也用不了）
                var preset = AiModelCatalog.ResolveByName(_currentModel) ?? AiModelCatalog.DefaultChat;
                string apiKey = preset.ApiKey;
                string baseUrl = preset.BaseUrl;
                string modelId = preset.ModelId;
                bool modelSupportsVision = preset.SupportsVision;

                var activeDoc = Application.DocumentManager.MdiActiveDocument;

                // 准备高阶现场 System Prompt：动作块直接执行协议（2026-10-24 与网页版对齐）
                string handlesJson = _lastSelectionHandles.Count > 0
                    ? string.Join(",", _lastSelectionHandles.Take(80).Select(h => $"\"{h}\""))
                    : "";
                string sysPrompt = $"你是运行在 AutoCAD 2027 进程内部的原生伴随式执行智能体 CadCopilot。\n" +
                                   $"【直接执行协议】(最重要)：用户意图是明确的 CAD 操作时，必须在回复中返回一个动作块，UI 会立即真实执行：\n" +
                                   $"<<CAD_ACTION>>{{\"action\":\"runCommand\",\"command\":\"_.FILEDIA 1\"}}<<CAD_ACTION_END>>\n" +
                                   $"可用 action：runCommand(命令或单行LISP) / setLayer(handles+layer) / erase(handles) / transform(move|rotate|scale|copy|mirror) / insertBlock(name,x,y) / zoom(handles或窗口) / save / undo / stats / listLayers / listBlocks / flatten(handles)。\n" +
                                   $"【批量按类型处理】需要「删掉所有填充」「把选区里的非直线换到某图层」这类操作时，必须用 filter，不要手搓 LISP ssget：\n" +
                                   $"<<CAD_ACTION>>{{\"action\":\"filter\",\"op\":\"erase\",\"types\":[\"Hatch\"]}}<<CAD_ACTION_END>>\n" +
                                   $"filter 参数：op=erase|setLayer|count；types=[\"Hatch\",\"Line\",\"Arc\",\"Polyline\",\"Text\",\"Circle\",\"Insert\"]；\n" +
                                   $"范围优先级：handles（给了就只处理这些）> x1,y1,x2,y2（窗口内完全落内的图元）> 未给则用当前选区；op=setLayer 时必带 layer。\n" +
                                   $"【图层状态批量管理】layerState：op=on|off|freeze|thaw|lock|unlock|isolate|highlight|unhighlight|setcurrent；\n" +
                                   $"  目标图层用 layer（单个）或 layers（数组）；也可用选区反推（selectionOnly=true）或 allLayers=true。\n" +
                                   $"  用户说「所有墙体图层」这类模糊说法时，用 nameContains:\"墙体\" 按名匹配，不要先 listLayers 再猜。\n" +
                                   $"<<CAD_ACTION>>{{\"action\":\"layerState\",\"op\":\"freeze\",\"nameContains\":\"墙体\"}}<<CAD_ACTION_END>>\n" +
                                   $"<<CAD_ACTION>>{{\"action\":\"layerState\",\"op\":\"freeze\",\"layers\":[\"墙体\",\"家具\"]}}<<CAD_ACTION_END>>\n" +
                                   $"【面积周长测量并标注】measure：先框选再用；自动写 MText 到 AI标注 层（textHeight 可指定字高，offset 为偏移）。\n" +
                                   $"<<CAD_ACTION>>{{\"action\":\"measure\",\"textHeight\":120}}<<CAD_ACTION_END>>\n" +
                                   $"【图层规范化】normalizeLayer：必须给 mapping（{{别名层:规范层}}），不臆造规范口径。\n" +
                                   $"<<CAD_ACTION>>{{\"action\":\"normalizeLayer\",\"mapping\":{{\"WALL\":\"墙体\",\"QiangTi\":\"墙体\"}}}}<<CAD_ACTION_END>>\n" +
                                   $"【跨视图尺寸联查】crossViewQuery：op=x|y + x1,y1,x2,y2 指定坐标带（factor 为容差带宽，默认 50），返回该带内所有图元与全部尺寸标注。\n" +
                                   $"【导出清单】exportText：把选区（或全图）图元清单写成 txt，file 可指定路径，缺省落 cadsys\\NET\\exports。\n" +
                                   $"规则：1) 能直接执行就必须给动作块，禁止\"我写好了LSP你去运行\"式答复；动作块外用一两句话说明你做了什么。" +
                                   $"【选动作的优先级——严格遵守】\n" +
                                   $"  ① 删除/改层/计数某类图元 → 必须用 filter（绝对不要写 entdel / ssget / vla-delete 的 LISP）\n" +
                                   $"  ② 图层开关冻结锁定隔离高亮 → 必须用 layerState\n" +
                                   $"  ③ 量面积周长标注 → 必须用 measure\n" +
                                   $"  ④ 图层归并 → 必须用 normalizeLayer\n" +
                                   $"  ⑤ 导出清单 → 必须用 exportText\n" +
                                   $"  ⑥ 只有上面都不适用的绘图/编辑动作，才用 runCommand 写 LISP\n" +
                                   $"  每次回复最多一个动作块，优先选最具体的结构化动作，不要用 runCommand 兜所有事。" +
                                   $"【绝对禁止】不要只回复「已删除」「已处理」「已发送」这类文字而同时不给动作块——那等于什么都没做，面板会判定为未执行并提示用户。" +
                                   $"如果你不确定用哪个动作，就选最接近的那个并给全参数，不要用文字代替动作块。\n" +
                                   $"2) 复杂几何（自包含 (defun c:XXX ...)）才用 runCommand。" +
                                   $"3) 删除/清理类不必询问用户——UI 安全闸会自动高亮确认并先保存。" +
                                   $"4) 需要选区而无选区时先让用户框选或用 (ssget \"_I\")。\n" +
                                   $"【当前图纸状态】文档名={activeDoc?.Name ?? "未打开"}, 选区={_lastSelectionSummary}" +
                                   (handlesJson.Length > 0 ? $", 句柄(前80)=[{handlesJson}]" : ", 句柄=无") + "。\n" +
                                   $"回答要求清晰利落、专业准确。";

                var messages = new List<object>
                {
                    new { role = "system", content = sysPrompt }
                };

                // 注入单会话全量历史上下文 (支持 1M 超长上下文与多轮连续记忆，不留死角)
                bool hasImage = false;
                foreach (var msgItem in _currentSession.Messages)
                {
                    if (msgItem.Role == "user")
                    {
                        if (!string.IsNullOrEmpty(msgItem.ImagePath) && File.Exists(msgItem.ImagePath))
                        {
                            try
                            {
                                byte[] bytes = File.ReadAllBytes(msgItem.ImagePath);
                                string b64 = Convert.ToBase64String(bytes);
                                messages.Add(new
                                {
                                    role = "user",
                                    content = new object[]
                                    {
                                        new { type = "text", text = msgItem.Content },
                                        new { type = "image_url", image_url = new { url = $"data:image/png;base64,{b64}" } }
                                    }
                                });
                                // 带图路由：当前模型支持视觉就沿用；否则切到配置里的 active_vision_model
                                if (!modelSupportsVision)
                                {
                                    var vision = AiModelCatalog.ActiveVision;
                                    if (vision != null)
                                    {
                                        modelId = vision.ModelId;
                                        apiKey = vision.ApiKey;
                                        baseUrl = vision.BaseUrl;
                                        modelSupportsVision = true;
                                    }
                                    else
                                    {
                                        Logger.Log("[AI] 带图请求但配置里没有支持视觉的模型，按当前模型发送");
                                    }
                                }
                                hasImage = true;
                            }
                            catch
                            {
                                messages.Add(new { role = "user", content = msgItem.Content });
                            }
                        }
                        else
                        {
                            messages.Add(new { role = "user", content = msgItem.Content });
                        }
                    }
                    else if (msgItem.Role == "assistant")
                    {
                        if (!string.IsNullOrEmpty(msgItem.Content))
                        {
                            messages.Add(new { role = "assistant", content = msgItem.Content });
                        }
                    }
                }

                // 带图请求路由留痕（排障时能确认走的是哪条模型链）
                if (hasImage) Logger.Log($"[AI] 带图请求 -> {modelId}");

                // 1. 本地端侧极速语义缓存检查 (相同选区与提示词瞬间直出)
                // 2026-11-11 修正：缓存命中原本会直接 return，把「模型没给动作块」的那次坏结果
                // 永久缓存下来，用户重发同一句话永远得不到执行。现在：命中缓存时，
                // 若回复里没有动作且语句本身是高置信可执行指令，仍走确定性兜底真正执行。
                string cacheKey = $"{modelId}|{_lastSelectionSummary}|{prompt.Trim()}";
                if (string.IsNullOrEmpty(imagePath) && _localAiCache.TryGetValue(cacheKey, out var localCached))
                {
                    string cachedContent = Regex.Replace(localCached.content ?? "", @"<<CAD_ACTION>>[\s\S]*?<<CAD_ACTION_END>>", "").Trim();
                    if (string.IsNullOrEmpty(cachedContent)) cachedContent = localCached.content ?? "";

                    bool cachedHadAction = Regex.IsMatch(localCached.content ?? "",
                        @"<<CAD_ACTION>>[\s\S]*?<<CAD_ACTION_END>>");
                    string fbAction = cachedHadAction ? "" : TrySynthesizeAction(prompt);
                    if (!string.IsNullOrEmpty(fbAction))
                    {
                        // 缓存里是「只有话没有动作」的坏结果：补执行，避免重发永远无效
                        Logger.Log($"[AI] 缓存命中但无动作块，改走确定性兜底: {fbAction}");
                        var (cOk, cMsg, cData) = CadPerceptionApp.ExecuteAiActionSync(fbAction);
                        string cCount = "";
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(cData))
                            {
                                using var cdj = JsonDocument.Parse(cData);
                                foreach (var k in new[] { "count", "erased" })
                                    if (cdj.RootElement.TryGetProperty(k, out var kv) && kv.ValueKind == JsonValueKind.Number)
                                    { cCount = $" 实际 {k}={kv.GetInt32()}"; break; }
                            }
                        }
                        catch { }
                        AddAiDirectSuccessMessage(
                            cOk ? "⚡ 已按规则直接执行（结果已回读）" : "⚠ 执行失败",
                            (cOk ? $"✅ 已执行并读回{cCount}" : $"❌ {cMsg}") + "\n兜底动作：" + fbAction);
                        cachedContent += "\n\n（本条由本地规则直接执行，未经模型生成动作块）";
                    }

                    stopTimer();
                    Dispatcher.Invoke(() =>
                    {
                        thinkingCard.Child = CreateAiResponseContent(localCached.reasoning, cachedContent, 0.05, prompt, imagePath, localCached.cachedTokens, localCached.totalTokens, isLocalCacheHit: true);
                        _scrollViewer.ScrollToEnd();
                    });

                    _currentSession.Messages.Add(new ChatMessageItem
                    {
                        Role = "assistant",
                        Content = cachedContent,
                        Reasoning = localCached.reasoning,
                        DurationSec = 0.05,
                        CachedTokens = localCached.cachedTokens,
                        TotalTokens = localCached.totalTokens,
                        IsLocalCacheHit = true,
                        Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    });
                    _currentSession.UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    SaveSessionsToDisk();
                    Dispatcher.Invoke(() => UpdateUsageBarMetrics(0.05, localCached.totalTokens, localCached.cachedTokens));
                    return;
                }

                var reqBody = new
                {
                    model = modelId,
                    messages
                };

                var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/chat/completions")
                {
                    Content = new StringContent(JsonSerializer.Serialize(reqBody), Encoding.UTF8, "application/json")
                };
                request.Headers.Add("Authorization", $"Bearer {apiKey}");

                var sw = Stopwatch.StartNew();
                var resp = await _httpClient.SendAsync(request);
                string respJson = await resp.Content.ReadAsStringAsync();
                sw.Stop();
                stopTimer();
                double durationSec = Math.Max(0.1, Math.Round(sw.Elapsed.TotalSeconds, 1));

                if (!resp.IsSuccessStatusCode)
                {
                    UpdateAiMessageText(thinkingCard, $"[请求异常 {resp.StatusCode}]\n{respJson}");
                    return;
                }

                using var jdoc = JsonDocument.Parse(respJson);
                var root = jdoc.RootElement;
                string content = string.Empty;
                string reasoning = string.Empty;

                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var msg = choices[0].GetProperty("message");
                    if (msg.TryGetProperty("content", out var cProp)) content = cProp.GetString() ?? "";
                    if (msg.TryGetProperty("reasoning_content", out var rProp)) reasoning = rProp.GetString() ?? "";
                }

                // 2. 提取大模型上下文缓存 (Prompt Cache) 命中统计
                int cachedTokens = 0;
                int totalTokens = 0;
                if (root.TryGetProperty("usage", out var usageProp))
                {
                    if (usageProp.TryGetProperty("total_tokens", out var tt)) totalTokens = tt.GetInt32();
                    if (usageProp.TryGetProperty("prompt_cache_hit_tokens", out var pch)) cachedTokens = pch.GetInt32();
                    else if (usageProp.TryGetProperty("prompt_tokens_details", out var ptd) && ptd.TryGetProperty("cached_tokens", out var ct))
                    {
                        cachedTokens = ct.GetInt32();
                    }
                }

                // 写入本地极速缓存（缓存存原始含动作块文本，命中时仅剥离显示不重复执行）
                if (!string.IsNullOrEmpty(content) && string.IsNullOrEmpty(imagePath))
                {
                    _localAiCache[cacheKey] = (content, reasoning, cachedTokens, totalTokens);
                }

                // 动作块直接执行（2026-10-24 对齐网页版）：审查→危险确认→先保存→统一执行链
                // 2026-11-11 新增「空口完成」拦截：模型常常只回一段「已删除/已处理」文字，
                // 却完全没有给动作块，图纸一点没变。用户看到的却是成功。这里强制标注真相。
                bool hadActionBlock = Regex.IsMatch(content ?? "", @"<<CAD_ACTION>>[\s\S]*?<<CAD_ACTION_END>>");

                // 2026-11-11 确定性兜底：模型时好时坏（实测 3 次只有 1 次给动作块），
                // 光靠提示词治不住。对「明确动词 + 明确对象」的高置信语句，模型若没给动作块，
                // 就由本地规则合成一个结构化动作下发——保证「说删填充就一定删填充」。
                if (!hadActionBlock)
                {
                    string fallbackAction = TrySynthesizeAction(prompt);
                    if (!string.IsNullOrEmpty(fallbackAction))
                    {
                        Logger.Log($"[AI] 模型未给动作块，启用确定性兜底: {fallbackAction}");
                        var (fbOk, fbMsg, fbData) = CadPerceptionApp.ExecuteAiActionSync(fallbackAction);
                        string fbCount = "";
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(fbData))
                            {
                                using var fdj = JsonDocument.Parse(fbData);
                                foreach (var k in new[] { "count", "erased" })
                                    if (fdj.RootElement.TryGetProperty(k, out var kv) && kv.ValueKind == JsonValueKind.Number)
                                    { fbCount = $" 实际 {k}={kv.GetInt32()}"; break; }
                            }
                        }
                        catch { }
                        AddAiDirectSuccessMessage(
                            fbOk ? "⚡ 模型未给动作，已按规则直接执行（结果已回读）" : "⚠ 规则兜底执行失败",
                            (fbOk ? $"✅ 已执行并读回{fbCount}" : $"❌ {fbMsg}") + "\n兜底动作：" + fallbackAction);
                        content = (content ?? "") + "\n\n（本次由本地规则直接执行，未经模型生成动作块）";
                    }
                }
                content = ExtractAndRunAiAction(content);
                if (!hadActionBlock && LooksLikeClaimingDone(content))
                {
                    AddAiDirectSuccessMessage("⚠ 模型声称已完成，但未下发任何动作",
                        "本次回复里没有动作块，图纸没有任何改动。请重发一次，或把需求说得更具体（例如「用 filter 删除所有 Hatch」）。");
                    Logger.Log("[AI] 空口完成拦截：回复声称已执行但无动作块");
                }

                // 直接安全更新 thinkingCard.Child，绝不修改 Children 集合避免 Visual Parent 碰撞异常
                Dispatcher.Invoke(() =>
                {
                    thinkingCard.Child = CreateAiResponseContent(reasoning, content, durationSec, prompt, imagePath, cachedTokens, totalTokens, isLocalCacheHit: false);
                    _scrollViewer.ScrollToEnd();
                });

                // 核心持久记忆：将本次助手回复追加到当前会话中并落盘持久化
                _currentSession.Messages.Add(new ChatMessageItem
                {
                    Role = "assistant",
                    Content = content,
                    Reasoning = reasoning,
                    DurationSec = durationSec,
                    CachedTokens = cachedTokens,
                    TotalTokens = totalTokens,
                    IsLocalCacheHit = false,
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });
                _currentSession.UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                SaveSessionsToDisk();
                Dispatcher.Invoke(() => UpdateUsageBarMetrics(durationSec, totalTokens, cachedTokens));

                // 广播面板事件
                CadPerceptionApp.Broadcast(new
                {
                    @event = "aiAssistantResponse",
                    prompt,
                    model = modelId,
                    reply = content,
                    cachedTokens,
                    totalTokens
                });
            }
            catch (Exception ex)
            {
                stopTimer();
                UpdateAiMessageText(thinkingCard, $"[大模型调用异常]\n{ex.Message}");
            }
        }

        // -------------------------------------------------------------
        // CAD 本地事务操作        // CAD 本地事务操作
        // -------------------------------------------------------------
        private void ExecuteSwitchHatchToWall(Document doc)
        {
            var ed = doc.Editor;
            var sel = ed.SelectImplied();
            if (sel.Status != PromptStatus.OK || sel.Value == null || sel.Value.Count == 0)
            {
                AddAiMessage("[提示] 请先在 CAD 图纸中框选包含填充的图元，然后再发送此指令。");
                return;
            }

            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                try
                {
                    string targetLayer = "墙体";
                    var lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForWrite);
                    if (!lt.Has(targetLayer))
                    {
                        var ltr = new LayerTableRecord
                        {
                            Name = targetLayer,
                            Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, 3)
                        };
                        lt.Add(ltr);
                        tr.AddNewlyCreatedDBObject(ltr, true);
                    }

                    int count = 0;
                    foreach (ObjectId id in sel.Value.GetObjectIds())
                    {
                        if (id.ObjectClass.DxfName.Equals("HATCH", StringComparison.OrdinalIgnoreCase))
                        {
                            if (tr.GetObject(id, OpenMode.ForWrite) is Entity ent)
                            {
                                ent.Layer = targetLayer;
                                count++;
                            }
                        }
                    }

                    tr.Commit();
                    AddAiMessage($"Thought for 1s >\n[已完成] 成功将选区中的 {count} 个填充图元转移至【{targetLayer}】图层。\n支持直接按 Ctrl+Z 一步撤销。");
                    ed.Regen();
                }
                catch (Exception ex)
                {
                    tr.Abort();
                    AddAiMessage($"[错误] 执行失败: {ex.Message}");
                }
            }
        }

        private void ExecuteFlatten(Document doc)
        {
            var ed = doc.Editor;
            var sel = ed.SelectImplied();
            if (sel.Status != PromptStatus.OK || sel.Value == null || sel.Value.Count == 0)
            {
                AddAiMessage("[提示] 请先在 CAD 图纸中框选要压平的图元。");
                return;
            }

            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                try
                {
                    int count = 0;
                    foreach (ObjectId id in sel.Value.GetObjectIds())
                    {
                        if (tr.GetObject(id, OpenMode.ForWrite) is Entity ent)
                        {
                            if (ent is CadLine line)
                            {
                                line.StartPoint = new Autodesk.AutoCAD.Geometry.Point3d(line.StartPoint.X, line.StartPoint.Y, 0);
                                line.EndPoint = new Autodesk.AutoCAD.Geometry.Point3d(line.EndPoint.X, line.EndPoint.Y, 0);
                                count++;
                            }
                            else if (ent is CadPolyline pl)
                            {
                                pl.Elevation = 0;
                                count++;
                            }
                        }
                    }
                    tr.Commit();
                    AddAiMessage($"Thought for 1s >\n[已完成] 成功压平 {count} 个图元的 Z 轴标高至 Z=0。\n支持按 Ctrl+Z 撤销。");
                    ed.Regen();
                }
                catch (Exception ex)
                {
                    tr.Abort();
                    AddAiMessage($"[错误] 压平失败: {ex.Message}");
                }
            }
        }

        private void ExecuteMoveTextToLayer(Document doc, string targetLayer)
        {
            var ed = doc.Editor;
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                try
                {
                    // 确保目标图层存在
                    var lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForWrite);
                    if (!lt.Has(targetLayer))
                    {
                        var ltr = new LayerTableRecord
                        {
                            Name = targetLayer,
                            Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, 7) // 白色/标准色
                        };
                        lt.Add(ltr);
                        tr.AddNewlyCreatedDBObject(ltr, true);
                    }

                    int count = 0;
                    var sel = ed.SelectImplied();
                    bool hasSelection = sel.Status == PromptStatus.OK && sel.Value != null && sel.Value.Count > 0;

                    if (hasSelection)
                    {
                        foreach (ObjectId id in sel.Value.GetObjectIds())
                        {
                            string dxf = id.ObjectClass.DxfName;
                            if (dxf.Equals("TEXT", StringComparison.OrdinalIgnoreCase) || dxf.Equals("MTEXT", StringComparison.OrdinalIgnoreCase))
                            {
                                if (tr.GetObject(id, OpenMode.ForWrite) is Entity ent)
                                {
                                    ent.Layer = targetLayer;
                                    count++;
                                }
                            }
                        }
                    }
                    else
                    {
                        // 若未框选，则遍历当前空间全量实体
                        var btr = (BlockTableRecord)tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForRead);
                        foreach (ObjectId id in btr)
                        {
                            string dxf = id.ObjectClass.DxfName;
                            if (dxf.Equals("TEXT", StringComparison.OrdinalIgnoreCase) || dxf.Equals("MTEXT", StringComparison.OrdinalIgnoreCase))
                            {
                                if (tr.GetObject(id, OpenMode.ForWrite) is Entity ent)
                                {
                                    ent.Layer = targetLayer;
                                    count++;
                                }
                            }
                        }
                    }

                    tr.Commit();
                    ed.Regen();

                    string scope = hasSelection ? "当前选区" : "当前空间全局";
                    AddAiDirectSuccessMessage("已直接在 CAD 中执行完成！",
                        $"成功将【{scope}】中的 **{count}** 个文字实体（TEXT / MTEXT）转移至【**{targetLayer}**】图层。\n\n" +
                        $"• 目标图层：`{targetLayer}`\n" +
                        $"• 影响图元：`{count}` 个\n" +
                        $"• 撤销支持：已写入事务历史，如需还原可随时按 **Ctrl+Z** 或下方撤回按钮。");
                }
                catch (Exception ex)
                {
                    tr.Abort();
                    AddAiMessage($"[执行异常] 文字图层转移失败: {ex.Message}");
                }
            }
        }

        private void ExecuteAnalyzeSelection(Document doc)
        {
            var ed = doc.Editor;
            var sel = ed.SelectImplied();
            if (sel.Status != PromptStatus.OK || sel.Value == null || sel.Value.Count == 0)
            {
                AddAiMessage("### 🔍 当前选区感知\n当前在 AutoCAD 视口中**暂未选择图元**。\n\n> 💡 提示：您可以在图纸中用鼠标框选目标图元，我将自动实时解析其图层、类型、几何参数并为您提供一键操作！");
                return;
            }

            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                try
                {
                    var typeCounts = new Dictionary<string, int>();
                    var layerCounts = new Dictionary<string, int>();
                    int total = sel.Value.Count;
                    var textSamples = new List<string>();

                    foreach (ObjectId id in sel.Value.GetObjectIds())
                    {
                        if (tr.GetObject(id, OpenMode.ForRead) is Entity ent)
                        {
                            string dxf = ent.GetRXClass().DxfName;
                            typeCounts[dxf] = typeCounts.GetValueOrDefault(dxf, 0) + 1;
                            layerCounts[ent.Layer] = layerCounts.GetValueOrDefault(ent.Layer, 0) + 1;

                            if (ent is DBText dbText && textSamples.Count < 5)
                            {
                                if (!string.IsNullOrWhiteSpace(dbText.TextString)) textSamples.Add(dbText.TextString.Trim());
                            }
                            else if (ent is MText mText && textSamples.Count < 5)
                            {
                                if (!string.IsNullOrWhiteSpace(mText.Contents)) textSamples.Add(mText.Contents.Trim());
                            }
                        }
                    }

                    var sb = new StringBuilder();
                    sb.AppendLine($"### 🎯 选区实时解析报告 (共 {total} 个图元)");
                    sb.AppendLine();
                    sb.AppendLine("**图元类型分布：**");
                    foreach (var kv in typeCounts.OrderByDescending(k => k.Value))
                    {
                        sb.AppendLine($"- `{kv.Key}`: **{kv.Value}** 个");
                    }
                    sb.AppendLine();
                    sb.AppendLine("**所在图层分布：**");
                    foreach (var kv in layerCounts.OrderByDescending(k => k.Value))
                    {
                        sb.AppendLine($"- 图层 `{kv.Key}`: **{kv.Value}** 个图元");
                    }

                    if (textSamples.Count > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine("**文字内容采样：**");
                        foreach (var sample in textSamples)
                        {
                            sb.AppendLine($"- `\"{sample}\"`");
                        }
                    }

                    sb.AppendLine();
                    sb.AppendLine("> 💡 您可以直接告诉我下一步指令（例如：“把所有文字放到文字图层”、“把填充移到墙体图层”），我将直接在 CAD 中执行！");

                    AddAiDirectSuccessMessage("选区实时深度解析", sb.ToString());
                }
                catch (Exception ex)
                {
                    AddAiMessage($"[解析异常]: {ex.Message}");
                }
            }
        }

        private void ExecutePurgeDrawing(Document doc)
        {
            try
            {
                doc.SendStringToExecute("-PURGE\nALL\n*\nN\n", true, false, false);
                AddAiDirectSuccessMessage("已直接向 CAD 发送清理命令！", "已向当前文档注入 `-PURGE ALL * N`，后台静默清理图纸中未使用的块、图层、线型等冗余数据。");
            }
            catch (Exception ex)
            {
                AddAiMessage($"[清理异常]: {ex.Message}");
            }
        }

        private void ExecuteCodeInCad(string code, string lang, TextBlock? tipBlock = null)
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null)
                {
                    if (tipBlock != null)
                    {
                        tipBlock.Text = "[无活动CAD图纸]";
                        tipBlock.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                        tipBlock.Visibility = System.Windows.Visibility.Visible;
                    }
                    return;
                }

                string cleanCode = code.Trim();
                bool isLisp = lang.Equals("LISP", StringComparison.OrdinalIgnoreCase) ||
                              lang.Equals("LSP", StringComparison.OrdinalIgnoreCase) ||
                              lang.Equals("AUTOLISP", StringComparison.OrdinalIgnoreCase) ||
                              cleanCode.StartsWith("(") ||
                              cleanCode.Contains("(defun");

                if (isLisp)
                {
                    string tempDir = CadPaths.NetDir;
                    CadPaths.EnsureNetDir();
                    string tempFile = System.IO.Path.Combine(tempDir, "ai_temp_exec.lsp");

                    // 写入临时 LISP 文件 (UTF-8 无 BOM)
                    File.WriteAllText(tempFile, cleanCode, new UTF8Encoding(false));

                    // 检测是否含有命令定义 (defun c:XXX
                    var match = Regex.Match(cleanCode, @"\(defun\s+c:([a-zA-Z0-9_\-]+)", RegexOptions.IgnoreCase);
                    string execCmd;
                    if (match.Success)
                    {
                        string cmdName = match.Groups[1].Value;
                        execCmd = $"(load \"{tempFile.Replace('\\', '/')}\")\n{cmdName}\n";
                    }
                    else
                    {
                        execCmd = $"(load \"{tempFile.Replace('\\', '/')}\")\n";
                    }

                    doc.SendStringToExecute(execCmd, true, false, false);

                    if (tipBlock != null)
                    {
                        tipBlock.Text = match.Success ? $"[已注入并运行: {match.Groups[1].Value}]" : "[已注入LISP执行]";
                        tipBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                        tipBlock.Visibility = System.Windows.Visibility.Visible;
                        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
                        t.Tick += (ts, te) => { tipBlock.Visibility = System.Windows.Visibility.Collapsed; t.Stop(); };
                        t.Start();
                    }
                }
                else
                {
                    string cmdToRun = cleanCode.Replace("\r\n", "\n");
                    if (!cmdToRun.EndsWith("\n")) cmdToRun += "\n";
                    doc.SendStringToExecute(cmdToRun, true, false, false);

                    if (tipBlock != null)
                    {
                        tipBlock.Text = "[已发送CAD命令]";
                        tipBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                        tipBlock.Visibility = System.Windows.Visibility.Visible;
                        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
                        t.Tick += (ts, te) => { tipBlock.Visibility = System.Windows.Visibility.Collapsed; t.Stop(); };
                        t.Start();
                    }
                }
            }
            catch (Exception ex)
            {
                if (tipBlock != null)
                {
                    tipBlock.Text = $"[执行异常: {ex.Message}]";
                    tipBlock.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                    tipBlock.Visibility = System.Windows.Visibility.Visible;
                }
            }
        }

        private void AddAiDirectSuccessMessage(string title, string detailsMarkdown)
        {
            Dispatcher.Invoke(() =>
            {
                string fullContent = string.IsNullOrEmpty(title) ? detailsMarkdown : $"### ✅ {title}\n\n{detailsMarkdown}";
                var bubble = AddAiMessage(fullContent);

                // 记入会话持久记忆
                _currentSession.Messages.Add(new ChatMessageItem
                {
                    Role = "assistant",
                    Content = fullContent,
                    DurationSec = 0.01,
                    IsLocalCacheHit = true,
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });
                _currentSession.UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                SaveSessionsToDisk();
            });
        }

        /// <summary>
        /// 读回面板状态（2026-11-11）：供验收脚本确认「消息已进入面板、且拿到了回复」，
        /// 不必依赖截图或物理键盘。
        /// </summary>
        public string GetTranscript()
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append("{\"title\":").Append(JsonSerializer.Serialize(_currentSession.Title ?? ""));
                sb.Append(",\"model\":").Append(JsonSerializer.Serialize(_currentModel ?? ""));
                sb.Append(",\"messages\":[");
                bool first = true;
                foreach (var m in _currentSession.Messages)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append('{')
                      .Append("\"role\":").Append(JsonSerializer.Serialize(m.Role ?? ""))
                      .Append(",\"content\":").Append(JsonSerializer.Serialize(m.Content ?? ""))
                      .Append(",\"tokens\":").Append(m.TotalTokens)
                      .Append('}');
                }
                sb.Append("]}");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return "{\"error\":" + JsonSerializer.Serialize(ex.Message) + "}";
            }
        }

        public void UpdateSelection(Document doc)
        {
            if (doc == null) return;
            try
            {
                var ed = doc.Editor;
                var sel = ed.SelectImplied();
                if (sel.Status == PromptStatus.OK && sel.Value != null && sel.Value.Count > 0)
                {
                    var counts = new Dictionary<string, int>();
                    _lastSelectionHandles = new System.Collections.Generic.List<string>();
                    foreach (ObjectId id in sel.Value.GetObjectIds())
                    {
                        string tp = id.ObjectClass.DxfName;
                        counts[tp] = counts.GetValueOrDefault(tp, 0) + 1;
                        if (_lastSelectionHandles.Count < 200) _lastSelectionHandles.Add(id.Handle.ToString());
                    }
                    string summary = string.Join(", ", counts.Select(kv => $"{kv.Key} x{kv.Value}"));
                    _lastSelectionSummary = $"已选中 {sel.Value.Count} 个图元 ({summary})";
                    _selectionText.Text = $"实时感知: {_lastSelectionSummary}";
                    _selectionText.Foreground = new SolidColorBrush(CadTextMain);
                }
                else
                {
                    _lastSelectionSummary = "当前无选区";
                    _lastSelectionHandles = new System.Collections.Generic.List<string>();
                    _selectionText.Text = "实时感知: 当前未选择图元 (在 CAD 中框选图元可直接操作)";
                    _selectionText.Foreground = new SolidColorBrush(CadTextDim);
                }
            }
            catch (Exception ex)
            {
                _selectionText.Text = $"选区感知异常: {ex.Message}";
            }
        }

        // -------------------------------------------------------------
        // UI 辅助方法
        // -------------------------------------------------------------
        private static Path CreateSvgPath(string pathData, double width, double height, Brush fillBrush)
        {
            return new Path
            {
                Data = Geometry.Parse(pathData),
                Fill = fillBrush,
                Width = width,
                Height = height,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private Button CreateIconButton(string pathData, string tooltip, RoutedEventHandler onClick)
        {
            var btn = new Button
            {
                Width = 28,
                Height = 28,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
                Margin = new Thickness(2, 0, 0, 0)
            };

            var border = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(4),
                Background = Brushes.Transparent
            };

            var path = CreateSvgPath(pathData, 14, 14, new SolidColorBrush(CadTextDim));
            path.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            path.VerticalAlignment = VerticalAlignment.Center;

            border.Child = path;
            btn.Content = border;

            btn.MouseEnter += (s, e) =>
            {
                border.Background = new SolidColorBrush(Color.FromRgb(0x45, 0x4F, 0x61));
                path.Fill = new SolidColorBrush(CadTextMain);
            };
            btn.MouseLeave += (s, e) =>
            {
                border.Background = Brushes.Transparent;
                path.Fill = new SolidColorBrush(CadTextDim);
            };

            btn.Click += onClick;
            return btn;
        }

        private void AddUserMessage(string text)
        {
            // 微信式右侧气泡（2026-11-11 重构）：头像是圆角方块 + 右对齐气泡 + 尖角
            _messagesPanel.Children.Add(BuildChatRow(
                isUser: true,
                buildBody: _ => BuildPlainTextBlock(text, CadTextMain, 12, 18)));
            _scrollViewer.ScrollToEnd();
        }

        // -------------------------------------------------------------
        // 微信式会话气泡骨架（2026-11-11 重构）
        // 结构：[头像] [气泡] —— 用户在右（头像在气泡右侧），AI 在左。
        // 气泡用 Path 画一个小尖角指向头像，视觉上连成一体。
        // -------------------------------------------------------------
        private FrameworkElement BuildChatRow(bool isUser, Func<StackPanel, FrameworkElement> buildBody)
        {
            var row = new Grid { Margin = new Thickness(0, 6, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                     // 头像
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 气泡
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                     // 右侧头像位（用户）

            var bubbleBody = new StackPanel();
            var bodyEl = buildBody(bubbleBody);

            var bubble = new Border
            {
                Background = new SolidColorBrush(isUser
                    ? Color.FromRgb(0x0E, 0x6E, 0x4E)      // 微信绿（深色主题下的对话绿）
                    : Color.FromRgb(0x2A, 0x32, 0x3E)),    // 对方灰蓝
                BorderBrush = new SolidColorBrush(isUser
                    ? Color.FromRgb(0x12, 0x8A, 0x62)
                    : Color.FromRgb(0x3C, 0x47, 0x57)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 7, 10, 7),
                MaxWidth = 260,
                HorizontalAlignment = isUser ? System.Windows.HorizontalAlignment.Right
                                             : System.Windows.HorizontalAlignment.Left,
                Child = bodyEl
            };

            var avatar = BuildAvatar(isUser);

            if (isUser)
            {
                Grid.SetColumn(bubble, 1);
                bubble.Margin = new Thickness(12, 0, 4, 0);
                row.Children.Add(bubble);
                Grid.SetColumn(avatar, 2);
                row.Children.Add(avatar);
            }
            else
            {
                Grid.SetColumn(avatar, 0);
                avatar.Margin = new Thickness(0, 0, 6, 0);
                row.Children.Add(avatar);
                Grid.SetColumn(bubble, 1);
                bubble.Margin = new Thickness(4, 0, 12, 0);
                row.Children.Add(bubble);
            }

            return row;
        }

        /// <summary>圆角方块头像：用户「我」，AI 用机器人图标。</summary>
        private FrameworkElement BuildAvatar(bool isUser)
        {
            var bd = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(isUser
                    ? Color.FromRgb(0x1F, 0x6F, 0x54)
                    : Color.FromRgb(0x2E, 0x3A, 0x4A)),
                BorderBrush = new SolidColorBrush(isUser
                    ? Color.FromRgb(0x27, 0x8A, 0x69)
                    : Color.FromRgb(0x40, 0x4E, 0x61)),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 0, 0)
            };

            if (isUser)
            {
                bd.Child = new TextBlock
                {
                    Text = "我",
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xF5, 0xEF)),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            else
            {
                var icon = CreateSvgPath(CadSvg.Robot, 16, 16, new SolidColorBrush(CadAccent));
                icon.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
                icon.VerticalAlignment = VerticalAlignment.Center;
                bd.Child = icon;
            }
            return bd;
        }

        private static TextBlock BuildPlainTextBlock(string text, Color color, double fontSize, double lineHeight)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(color),
                FontSize = fontSize,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = lineHeight
            };
        }

        private (Border card, Action stopTimer) AddAiThinkingCard()
        {
            var card = new Border
            {
                Background = Brushes.Transparent,
                Padding = new Thickness(0, 2, 4, 8),
                Margin = new Thickness(0, 2, 0, 6),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
            };
            var tb = new TextBlock
            {
                Text = "思考中 (0.0s)...",
                Foreground = new SolidColorBrush(CadTextDim),
                FontSize = 12
            };
            card.Child = tb;
            _messagesPanel.Children.Add(card);
            _scrollViewer.ScrollToEnd();

            var sw = Stopwatch.StartNew();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            timer.Tick += (s, e) =>
            {
                tb.Text = $"思考中 ({sw.Elapsed.TotalSeconds:0.0}s)...";
            };
            timer.Start();

            Action stop = () =>
            {
                timer.Stop();
                sw.Stop();
            };

            return (card, stop);
        }

        private Border AddAiMessage(string text)
        {
            // 微信式左侧气泡（2026-11-11 重构）：改为返回气泡 Border，
            // 便于 UpdateAiMessageText 等调用方继续改写内容。
            Border? bubble = null;
            var body = new StackPanel();
            RenderMarkdownBlocks(body, text);

            var row = BuildChatRow(isUser: false, buildBody: _ => body);
            // 从行里取回气泡容器
            foreach (var child in ((Grid)row).Children)
            {
                if (child is Border b) { bubble = b; break; }
            }

            var card = bubble ?? new Border { Child = body };
            _messagesPanel.Children.Add(row);
            _scrollViewer.ScrollToEnd();
            return card;
        }

        private void UpdateAiMessageText(Border card, string newText)
        {
            Dispatcher.Invoke(() =>
            {
                var tb = new TextBox
                {
                    Text = newText,
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
                    FontSize = 12,
                    Padding = new Thickness(0),
                    SelectionBrush = new SolidColorBrush(CadAccent)
                };
                card.Child = tb;
                _scrollViewer.ScrollToEnd();
            });
        }

        private static void AppendFormattedInlines(TextBlock tb, string line, Color accentColor, Color mainTextColor)
        {
            if (string.IsNullOrEmpty(line)) return;

            // 正则匹配 **粗体** / *斜体* / `行内代码` / [文本](链接) / ~~删除线~~
            // 2026-11-11 扩充：原先只支持粗体+行内代码，模型输出常带斜体/链接，会被当普通文本露出星号。
            var regex = new Regex(@"(\*\*[^*]+\*\*|~~[^~]+~~|\*[^*\n]+\*|`[^`]+`|\[[^\]]+\]\([^)]+\))");
            var parts = regex.Split(line);

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;

                if (part.StartsWith("**") && part.EndsWith("**") && part.Length >= 4)
                {
                    string boldText = part.Substring(2, part.Length - 4);
                    var boldRun = new Run(boldText)
                    {
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xF9, 0xFA, 0xFB))
                    };
                    tb.Inlines.Add(new Bold(boldRun));
                }
                else if (part.StartsWith("~~") && part.EndsWith("~~") && part.Length >= 4)
                {
                    var delRun = new Run(part.Substring(2, part.Length - 4))
                    {
                        TextDecorations = TextDecorations.Strikethrough,
                        Foreground = new SolidColorBrush(CadTextDim)
                    };
                    tb.Inlines.Add(delRun);
                }
                else if (part.StartsWith("*") && part.EndsWith("*") && part.Length >= 2 && !part.StartsWith("**"))
                {
                    var italRun = new Run(part.Substring(1, part.Length - 2))
                    {
                        FontStyle = FontStyles.Italic,
                        Foreground = new SolidColorBrush(mainTextColor)
                    };
                    tb.Inlines.Add(italRun);
                }
                else if (part.StartsWith("[") && part.Contains("]("))
                {
                    // 链接：[文本](url) —— 面板内不做跳转，显示为带下划线的彩色文本 + 悬浮显示地址
                    int rb = part.IndexOf("](");
                    string label = part.Substring(1, rb - 1);
                    string url = part.Substring(rb + 2, part.Length - rb - 3);
                    var linkRun = new Run(label)
                    {
                        Foreground = new SolidColorBrush(CadAccent),
                        TextDecorations = TextDecorations.Underline
                    };
                    tb.Inlines.Add(linkRun);
                    tb.Inlines.Add(new Run(" (" + url + ")")
                    {
                        Foreground = new SolidColorBrush(CadTextDim),
                        FontSize = 10
                    });
                }
                else if (part.StartsWith("`") && part.EndsWith("`") && part.Length >= 2)
                {
                    string codeText = part.Substring(1, part.Length - 2);
                    var codeTag = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(0x27, 0x31, 0x40)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(0x3B, 0x47, 0x58)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(4, 0, 4, 1),
                        Margin = new Thickness(2, 0, 2, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Child = new TextBlock
                        {
                            Text = codeText,
                            FontFamily = new FontFamily("Consolas, Courier New"),
                            FontSize = 11,
                            Foreground = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8))
                        }
                    };
                    tb.Inlines.Add(new InlineUIContainer(codeTag));
                }
                else
                {
                    tb.Inlines.Add(new Run(part)
                    {
                        Foreground = new SolidColorBrush(mainTextColor)
                    });
                }
            }
        }

        private void RenderMarkdownBlocks(StackPanel container, string markdown)
        {
            if (string.IsNullOrEmpty(markdown)) return;

            string[] rawLines = markdown.Replace("\r\n", "\n").Split('\n');
            bool inCodeBlock = false;
            string codeLang = "CODE";
            var codeBuffer = new StringBuilder();

            for (int i = 0; i < rawLines.Length; i++)
            {
                string line = rawLines[i];
                string trimmed = line.Trim();

                // 1. 处理代码块 ```
                if (trimmed.StartsWith("```"))
                {
                    if (!inCodeBlock)
                    {
                        inCodeBlock = true;
                        string lang = trimmed.Substring(3).Trim();
                        codeLang = string.IsNullOrEmpty(lang) ? "CODE" : lang.ToUpper();
                        codeBuffer.Clear();
                    }
                    else
                    {
                        inCodeBlock = false;
                        string codeContent = codeBuffer.ToString().TrimEnd();

                        // 渲染独立代码块卡片
                        var codeCard = new Border
                        {
                            Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x25, 0x30)),
                            BorderBrush = new SolidColorBrush(CadBorder),
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(4),
                            Margin = new Thickness(0, 4, 0, 6)
                        };

                        var codeStack = new StackPanel();

                        var codeHeaderBorder = new Border
                        {
                            Background = new SolidColorBrush(Color.FromRgb(0x27, 0x2E, 0x3A)),
                            Padding = new Thickness(8, 3, 4, 3)
                        };
                        var codeHeader = new DockPanel();

                        var langTb = new TextBlock
                        {
                            Text = codeLang,
                            Foreground = new SolidColorBrush(CadTextDim),
                            FontSize = 10,
                            FontWeight = FontWeights.SemiBold,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        DockPanel.SetDock(langTb, Dock.Left);
                        codeHeader.Children.Add(langTb);

                        var executeTip = new TextBlock
                        {
                            Text = "[已发送执行]",
                            Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)),
                            FontSize = 9,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(0, 0, 4, 0),
                            Visibility = System.Windows.Visibility.Collapsed
                        };

                        var executeBtn = new Button
                        {
                            Cursor = Cursors.Hand,
                            Background = new SolidColorBrush(Color.FromArgb(0x33, 0x10, 0xB9, 0x81)),
                            BorderBrush = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)),
                            BorderThickness = new Thickness(1),
                            Padding = new Thickness(5, 1, 5, 1),
                            Margin = new Thickness(0, 0, 4, 0),
                            ToolTip = "一键将此代码块注入当前 AutoCAD 视口并立即执行",
                            Style = null
                        };
                        var execStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                        var playIcon = CreateSvgPath(CadSvg.Play, 9, 9, new SolidColorBrush(Color.FromRgb(0x34, 0xD3, 0x99)));
                        playIcon.Margin = new Thickness(0, 0, 3, 0);
                        execStack.Children.Add(playIcon);
                        var execTb = new TextBlock
                        {
                            Text = "发送执行",
                            FontSize = 9.5,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = new SolidColorBrush(Color.FromRgb(0xD1, 0xFA, 0xE5)),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        execStack.Children.Add(execTb);
                        executeBtn.Content = execStack;

                        executeBtn.Click += (s, e) =>
                        {
                            ExecuteCodeInCad(codeContent, codeLang, executeTip);
                        };

                        var copyCodeTip = new TextBlock
                        {
                            Text = "[已复制]",
                            Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)),
                            FontSize = 9,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(0, 0, 4, 0),
                            Visibility = System.Windows.Visibility.Collapsed
                        };

                        var copyCodeBtn = CreateIconButton(CadSvg.Copy, "复制代码块", (s, e) =>
                        {
                            try
                            {
                                Clipboard.SetText(codeContent);
                                copyCodeTip.Visibility = System.Windows.Visibility.Visible;
                                var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                                t.Tick += (ts, te) =>
                                {
                                    copyCodeTip.Visibility = System.Windows.Visibility.Collapsed;
                                    t.Stop();
                                };
                                t.Start();
                            }
                            catch { }
                        });

                        var headerRight = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
                        };
                        headerRight.Children.Add(executeTip);
                        headerRight.Children.Add(executeBtn);
                        headerRight.Children.Add(copyCodeTip);
                        headerRight.Children.Add(copyCodeBtn);
                        DockPanel.SetDock(headerRight, Dock.Right);
                        codeHeader.Children.Add(headerRight);
                        codeHeaderBorder.Child = codeHeader;
                        codeStack.Children.Add(codeHeaderBorder);

                        // 代码正文 (等宽文本框支持划选复制)
                        var codeTextBox = new TextBox
                        {
                            Text = codeContent,
                            IsReadOnly = true,
                            TextWrapping = TextWrapping.NoWrap,
                            Background = Brushes.Transparent,
                            BorderThickness = new Thickness(0),
                            FontFamily = new FontFamily("Consolas, Courier New"),
                            FontSize = 11,
                            Foreground = new SolidColorBrush(Color.FromRgb(0xCF, 0xD8, 0xDC)),
                            Padding = new Thickness(8, 6, 8, 6),
                            SelectionBrush = new SolidColorBrush(CadAccent)
                        };
                        codeStack.Children.Add(codeTextBox);

                        codeCard.Child = codeStack;
                        container.Children.Add(codeCard);
                    }
                    continue;
                }

                if (inCodeBlock)
                {
                    codeBuffer.AppendLine(line);
                    continue;
                }

                // 2. 标题行 (# / ## / ###)
                if (trimmed.StartsWith("#"))
                {
                    int lvl = 0;
                    while (lvl < trimmed.Length && trimmed[lvl] == '#') lvl++;
                    string titleText = trimmed.Substring(lvl).TrimStart();
                    double fontSize = lvl switch { 1 => 14.0, 2 => 13.0, _ => 12.0 };

                    var hTb = new TextBlock
                    {
                        FontSize = fontSize,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 6, 0, 2)
                    };
                    AppendFormattedInlines(hTb, titleText, CadAccent, Color.FromRgb(0xF9, 0xFA, 0xFB));
                    container.Children.Add(hTb);
                    continue;
                }

                // 3. 引用块 (> )
                if (trimmed.StartsWith("> "))
                {
                    var quoteBorder = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(0x27, 0x2F, 0x3A)),
                        BorderBrush = new SolidColorBrush(CadAccent),
                        BorderThickness = new Thickness(2, 0, 0, 0),
                        Padding = new Thickness(8, 4, 8, 4),
                        Margin = new Thickness(0, 2, 0, 4)
                    };
                    var qTb = new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        LineHeight = 17,
                        FontSize = 11.5
                    };
                    AppendFormattedInlines(qTb, trimmed.Substring(2), CadAccent, CadTextDim);
                    quoteBorder.Child = qTb;
                    container.Children.Add(quoteBorder);
                    continue;
                }

                // 4. 无序列表项 (- 或 * 开头)
                if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
                {
                    var listRow = new DockPanel { Margin = new Thickness(4, 2, 0, 2) };
                    var dot = new TextBlock
                    {
                        Text = "•",
                        Foreground = new SolidColorBrush(CadAccent),
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(2, 0, 6, 0)
                    };
                    DockPanel.SetDock(dot, Dock.Left);
                    listRow.Children.Add(dot);

                    var itemTb = new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        LineHeight = 18,
                        FontSize = 12
                    };
                    AppendFormattedInlines(itemTb, trimmed.Substring(2), CadAccent, CadTextMain);
                    listRow.Children.Add(itemTb);

                    container.Children.Add(listRow);
                    continue;
                }

                // 4b. 有序列表项 (1. / 2. ...) —— 2026-11-11 新增，模型常用编号列步骤
                var olM = Regex.Match(trimmed, @"^(\d{1,2})[.)]\s+(.*)$");
                if (olM.Success)
                {
                    var olRow = new DockPanel { Margin = new Thickness(4, 2, 0, 2) };
                    var num = new TextBlock
                    {
                        Text = olM.Groups[1].Value + ".",
                        Foreground = new SolidColorBrush(CadAccent),
                        FontSize = 12,
                        FontWeight = FontWeights.SemiBold,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(2, 0, 6, 0),
                        MinWidth = 16
                    };
                    DockPanel.SetDock(num, Dock.Left);
                    olRow.Children.Add(num);

                    var olTb = new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        LineHeight = 18,
                        FontSize = 12
                    };
                    AppendFormattedInlines(olTb, olM.Groups[2].Value, CadAccent, CadTextMain);
                    olRow.Children.Add(olTb);
                    container.Children.Add(olRow);
                    continue;
                }

                // 4c. 水平分割线 (--- / *** / ___)
                if (Regex.IsMatch(trimmed, @"^(?:-{3,}|\*{3,}|_{3,})$"))
                {
                    container.Children.Add(new Border
                    {
                        Height = 1,
                        Background = new SolidColorBrush(CadBorder),
                        Margin = new Thickness(0, 6, 0, 6),
                        Opacity = 0.6
                    });
                    continue;
                }

                // 4d. Markdown 表格（2026-11-11 新增）
                // 识别形如：
                //   | A | B |
                //   |---|---|
                //   | 1 | 2 |
                if (trimmed.StartsWith("|") && trimmed.EndsWith("|") &&
                    i + 1 < rawLines.Length && Regex.IsMatch(rawLines[i + 1].Trim(), @"^\|[\s:\-|]+\|$"))
                {
                    var headerCells = SplitTableRow(trimmed);
                    var rowsData = new List<List<string>>();
                    int j = i + 2;
                    while (j < rawLines.Length)
                    {
                        string rl = rawLines[j].Trim();
                        if (!rl.StartsWith("|") || !rl.EndsWith("|")) break;
                        rowsData.Add(SplitTableRow(rl));
                        j++;
                    }
                    i = j - 1;

                    container.Children.Add(BuildMarkdownTable(headerCells, rowsData));
                    continue;
                }

                // 5. 空行或普通段落
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    container.Children.Add(new FrameworkElement { Height = 4 });
                }
                else
                {
                    var pTb = new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        LineHeight = 18,
                        FontSize = 12,
                        Margin = new Thickness(0, 1, 0, 2)
                    };
                    AppendFormattedInlines(pTb, line, CadAccent, CadTextMain);
                    container.Children.Add(pTb);
                }
            }
        }

        /// <summary>把一行表格拆成单元格（去掉首尾竖线，trim）。</summary>
        private static List<string> SplitTableRow(string row)
        {
            string s = row.Trim();
            if (s.StartsWith("|")) s = s.Substring(1);
            if (s.EndsWith("|")) s = s.Substring(0, s.Length - 1);
            return s.Split('|').Select(c => c.Trim()).ToList();
        }

        /// <summary>渲染 Markdown 表格为 WPF Grid（表头加底色，单元格细边框）。2026-11-11 新增。</summary>
        private FrameworkElement BuildMarkdownTable(List<string> header, List<List<string>> rows)
        {
            int cols = Math.Max(header.Count, rows.Count == 0 ? 0 : rows.Max(r => r.Count));
            if (cols == 0) return new Border();

            var grid = new Grid { Margin = new Thickness(0, 4, 0, 6) };
            for (int c = 0; c < cols; c++)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            foreach (var _ in rows)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var borderBrush = new SolidColorBrush(CadBorder);

            Border MakeCell(string text, bool isHeader, int rowIdx)
            {
                var tb = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 11.5,
                    LineHeight = 16
                };
                AppendFormattedInlines(tb, text,
                    isHeader ? Color.FromRgb(0x38, 0xBD, 0xF8) : CadAccent,
                    isHeader ? Color.FromRgb(0xF9, 0xFA, 0xFB) : CadTextMain);
                if (isHeader) tb.FontWeight = FontWeights.SemiBold;

                return new Border
                {
                    Background = isHeader
                        ? new SolidColorBrush(Color.FromRgb(0x25, 0x2E, 0x3B))
                        : new SolidColorBrush(rowIdx % 2 == 0
                            ? Color.FromArgb(0x18, 0x3B, 0x44, 0x53)
                            : Colors.Transparent),
                    BorderBrush = borderBrush,
                    BorderThickness = new Thickness(isHeader ? 0 : 1, isHeader ? 0 : 1, 0, 1),
                    Padding = new Thickness(6, 4, 6, 4),
                    Child = tb
                };
            }

            for (int c = 0; c < cols; c++)
            {
                var cell = MakeCell(c < header.Count ? header[c] : "", true, 0);
                Grid.SetRow(cell, 0);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }

            for (int r = 0; r < rows.Count; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    var cell = MakeCell(c < rows[r].Count ? rows[r][c] : "", false, r);
                    Grid.SetRow(cell, r + 1);
                    Grid.SetColumn(cell, c);
                    grid.Children.Add(cell);
                }
            }

            // 表格外框
            return new Border
            {
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Child = grid,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Margin = new Thickness(0, 4, 0, 6)
            };
        }

        private void TriggerCadUndo(TextBlock? tipBlock = null)
        {
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    // 注入 CAD 核心单步撤销命令 (相当于 Ctrl+Z / UNDO 1)
                    doc.SendStringToExecute("_.U\n", true, false, false);
                    if (tipBlock != null)
                    {
                        tipBlock.Text = "[已撤回CAD]";
                        tipBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                        tipBlock.Visibility = System.Windows.Visibility.Visible;
                        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                        timer.Tick += (s, e) =>
                        {
                            tipBlock.Visibility = System.Windows.Visibility.Collapsed;
                            timer.Stop();
                        };
                        timer.Start();
                    }
                }
                else if (tipBlock != null)
                {
                    tipBlock.Text = "[无活动CAD图纸]";
                    tipBlock.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                    tipBlock.Visibility = System.Windows.Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                if (tipBlock != null)
                {
                    tipBlock.Text = $"[撤回异常: {ex.Message}]";
                    tipBlock.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                    tipBlock.Visibility = System.Windows.Visibility.Visible;
                }
            }
        }

        private UIElement CreateAiResponseContent(string reasoning, string content, double durationSec, string prompt, string? imagePath, int cachedTokens = 0, int totalTokens = 0, bool isLocalCacheHit = false)
        {
            var contentStack = new StackPanel();

            // 1. 如果有思考内容，添加可折叠折叠条
            if (!string.IsNullOrEmpty(reasoning))
            {
                var thoughtToggle = new Border
                {
                    Cursor = Cursors.Hand,
                    Background = Brushes.Transparent,
                    Padding = new Thickness(0, 2, 0, 4)
                };
                var toggleDock = new DockPanel();
                var toggleText = new TextBlock
                {
                    Text = $"Thought for {durationSec:0.#}s >",
                    Foreground = new SolidColorBrush(CadTextDim),
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(toggleText, Dock.Left);
                toggleDock.Children.Add(toggleText);

                // 呈现高科技缓存命中胶囊 (Prompt Caching / 本地极速缓存)
                if (isLocalCacheHit || cachedTokens > 0)
                {
                    var cacheBadge = new Border
                    {
                        Background = new SolidColorBrush(isLocalCacheHit ? Color.FromArgb(0x33, 0x10, 0xB9, 0x81) : Color.FromArgb(0x33, 0x06, 0x96, 0xD7)),
                        BorderBrush = new SolidColorBrush(isLocalCacheHit ? Color.FromRgb(0x10, 0xB9, 0x81) : Color.FromRgb(0x38, 0xBD, 0xF8)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(5, 1, 5, 2),
                        Margin = new Thickness(8, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        ToolTip = isLocalCacheHit 
                            ? "端侧语义极速缓存已命中 (0.05s)，零消耗 Tokens" 
                            : $"大模型前缀缓存 (Prompt Caching) 命中 {cachedTokens:N0} tokens，推理延迟降低 80%"
                    };
                    var badgeText = new TextBlock
                    {
                        Text = isLocalCacheHit ? "⚡ 本地极速缓存" : $"⚡ 缓存命中 {cachedTokens:N0} tokens",
                        FontSize = 9.5,
                        Foreground = new SolidColorBrush(isLocalCacheHit ? Color.FromRgb(0x34, 0xD3, 0x99) : Color.FromRgb(0x38, 0xBD, 0xF8)),
                        FontWeight = FontWeights.SemiBold
                    };
                    cacheBadge.Child = badgeText;
                    DockPanel.SetDock(cacheBadge, Dock.Left);
                    toggleDock.Children.Add(cacheBadge);
                }

                // 独立展示本条消息的 Token 速度与折算费用 (受独立开关控制)
                double singleTps = (durationSec > 0 && totalTokens > 0) ? (totalTokens / durationSec) : 0;
                double singleCost = TokenPricingCalculator.CalculateCost(_currentModel, totalTokens, cachedTokens);

                var messageMetrics = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                if (_usageSettings.ShowSpeed && singleTps > 0)
                {
                    messageMetrics.Children.Add(new TextBlock
                    {
                        Text = $"{singleTps:0.#} t/s",
                        FontSize = 9.5,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)),
                        Margin = new Thickness(0, 0, 8, 0),
                        ToolTip = "本条回复生成速度"
                    });
                }
                if (_usageSettings.ShowCost && singleCost > 0)
                {
                    messageMetrics.Children.Add(new TextBlock
                    {
                        Text = $"¥{singleCost:0.0000}",
                        FontSize = 9.5,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
                        Margin = new Thickness(0, 0, 4, 0),
                        ToolTip = "本条回复 API 折算费用"
                    });
                }
                DockPanel.SetDock(messageMetrics, Dock.Right);
                toggleDock.Children.Add(messageMetrics);

                thoughtToggle.Child = toggleDock;

                // 思考正文卡片 (默认折叠收起！)
                var thoughtContentBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0x27, 0x2F, 0x3A)),
                    BorderBrush = new SolidColorBrush(CadBorder),
                    BorderThickness = new Thickness(2, 0, 0, 0), // 左侧 2px 优雅引用线
                    Padding = new Thickness(8, 6, 8, 6),
                    Margin = new Thickness(0, 2, 0, 6),
                    Visibility = System.Windows.Visibility.Collapsed // 默认折叠收起！
                };
                var thoughtTb = new TextBox
                {
                    Text = reasoning.Trim(),
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = new SolidColorBrush(CadTextDim),
                    FontSize = 11,
                    Padding = new Thickness(0),
                    SelectionBrush = new SolidColorBrush(CadAccent)
                };
                thoughtContentBorder.Child = thoughtTb;

                bool isExpanded = false;
                thoughtToggle.MouseLeftButtonDown += (s, e) =>
                {
                    isExpanded = !isExpanded;
                    thoughtContentBorder.Visibility = isExpanded ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                    toggleText.Text = isExpanded ? $"Thought for {durationSec:0.#}s ∨" : $"Thought for {durationSec:0.#}s >";
                };

                contentStack.Children.Add(thoughtToggle);
                contentStack.Children.Add(thoughtContentBorder);
            }
            else if (isLocalCacheHit || cachedTokens > 0 || (_usageSettings.ShowCost && totalTokens > 0))
            {
                var hitBar = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };

                if (isLocalCacheHit || cachedTokens > 0)
                {
                    var cacheBadge = new Border
                    {
                        Background = new SolidColorBrush(isLocalCacheHit ? Color.FromArgb(0x33, 0x10, 0xB9, 0x81) : Color.FromArgb(0x33, 0x06, 0x96, 0xD7)),
                        BorderBrush = new SolidColorBrush(isLocalCacheHit ? Color.FromRgb(0x10, 0xB9, 0x81) : Color.FromRgb(0x38, 0xBD, 0xF8)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(5, 1, 5, 2),
                        ToolTip = isLocalCacheHit ? "端侧语义极速缓存已命中 (0.05s)，零消耗 Tokens" : $"大模型前缀缓存 (Prompt Caching) 命中 {cachedTokens:N0} tokens，推理延迟降低 80%"
                    };
                    cacheBadge.Child = new TextBlock
                    {
                        Text = isLocalCacheHit ? "⚡ 本地极速缓存命中 (0.05s)" : $"⚡ Prompt 缓存命中 {cachedTokens:N0} tokens",
                        FontSize = 9.5,
                        Foreground = new SolidColorBrush(isLocalCacheHit ? Color.FromRgb(0x34, 0xD3, 0x99) : Color.FromRgb(0x38, 0xBD, 0xF8)),
                        FontWeight = FontWeights.SemiBold
                    };
                    DockPanel.SetDock(cacheBadge, Dock.Left);
                    hitBar.Children.Add(cacheBadge);
                }

                double singleTps = (durationSec > 0 && totalTokens > 0) ? (totalTokens / durationSec) : 0;
                double singleCost = TokenPricingCalculator.CalculateCost(_currentModel, totalTokens, cachedTokens);

                var messageMetrics = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                if (_usageSettings.ShowSpeed && singleTps > 0)
                {
                    messageMetrics.Children.Add(new TextBlock
                    {
                        Text = $"{singleTps:0.#} t/s",
                        FontSize = 9.5,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)),
                        Margin = new Thickness(0, 0, 8, 0),
                        ToolTip = "本条回复生成速度"
                    });
                }
                if (_usageSettings.ShowCost && singleCost > 0)
                {
                    messageMetrics.Children.Add(new TextBlock
                    {
                        Text = $"¥{singleCost:0.0000}",
                        FontSize = 9.5,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
                        Margin = new Thickness(0, 0, 4, 0),
                        ToolTip = "本条回复 API 折算费用"
                    });
                }
                DockPanel.SetDock(messageMetrics, Dock.Right);
                hitBar.Children.Add(messageMetrics);

                contentStack.Children.Add(hitBar);
            }

            // 2. 独立正文输出 (原生 Markdown 解析渲染：粗体、行内代码胶囊、列表圆点、高亮代码块卡片)
            var markdownContainer = new StackPanel { Margin = new Thickness(0, 2, 0, 4) };
            RenderMarkdownBlocks(markdownContainer, string.IsNullOrWhiteSpace(content) ? "(无文本回复)" : content.Trim());
            contentStack.Children.Add(markdownContainer);

            // 3. 底部操作栏: [状态提示] + [一键撤回 CAD (Ctrl+Z)] + [重新生成本次回答] + [一键复制回答]
            var actionDock = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
            var rightBtnStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };

            var actionTip = new TextBlock
            {
                Text = "",
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                Visibility = System.Windows.Visibility.Collapsed
            };

            // 按钮 1: 一键撤回 CAD 动作 (Ctrl+Z)
            var undoBtn = CreateIconButton(CadSvg.Undo, "一键撤回 CAD 修改 (Ctrl+Z / UNDO)", (s, e) =>
            {
                TriggerCadUndo(actionTip);
            });

            // 按钮 2: 重新生成本次回答
            var regenBtn = CreateIconButton(CadSvg.Refresh, "重新生成本次回答", (s, e) =>
            {
                if (!string.IsNullOrEmpty(prompt))
                {
                    _ = CallLanguageModelApiAsync(prompt, imagePath);
                }
            });

            // 按钮 3: 一键复制回答内容
            var copyBtn = CreateIconButton(CadSvg.Copy, "一键复制回答内容", (s, e) =>
            {
                try
                {
                    Clipboard.SetText(content.Trim());
                    actionTip.Text = "[已复制]";
                    actionTip.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                    actionTip.Visibility = System.Windows.Visibility.Visible;
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                    timer.Tick += (ts, te) =>
                    {
                        actionTip.Visibility = System.Windows.Visibility.Collapsed;
                        timer.Stop();
                    };
                    timer.Start();
                }
                catch { }
            });

            rightBtnStack.Children.Add(actionTip);
            rightBtnStack.Children.Add(undoBtn);
            rightBtnStack.Children.Add(regenBtn);
            rightBtnStack.Children.Add(copyBtn);
            actionDock.Children.Add(rightBtnStack);
            contentStack.Children.Add(actionDock);

            // 微信式左侧气泡（2026-11-11 重构）：AI 回复整体包进带头像的气泡，
            // 正文仍走 RenderMarkdownBlocks（标题/粗体/行内代码/代码块/引用/列表）。
            return BuildChatRow(isUser: false, buildBody: _ => contentStack);
        }
    }
}
