using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

// UseWindowsForms 引入的隐式 using 会与 Autodesk 的 Application 冲突，这里显式别名
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CoreApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: ExtensionApplication(typeof(CadPerceptionPlugin.CadPerceptionApp))]
[assembly: CommandClass(typeof(CadPerceptionPlugin.CadPerceptionApp))]

namespace CadPerceptionPlugin
{
    public static class Logger
    {
        private static readonly object LockObj = new();
        // 日志路径随当前用户的 CAD_SYS_ROOT 解析
        private static readonly string LogPath = CadPaths.DebugLogFile;

        static Logger()
        {
            CadPaths.EnsureNetDir();
        }

        public static void Log(string message)
        {
            try
            {
                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
                lock (LockObj)
                {
                    File.AppendAllText(LogPath, line + Environment.NewLine);
                }
            }
            catch { }
        }

        public static void LogException(string context, System.Exception ex)
        {
            try
            {
                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERROR] {context}:\n" +
                              $"{ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}";
                if (ex.InnerException != null)
                {
                    line += $"\nInner: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}";
                }
                lock (LockObj)
                {
                    File.AppendAllText(LogPath, line + Environment.NewLine);
                }
            }
            catch { }
        }
    }

    public class CadPerceptionApp : IExtensionApplication
    {
        // 下行动作一律排队到 CAD 主线程执行：后台线程直接 LockDocument 会与主线程抢文档锁，
        // 主线程占锁时即死锁卡死 CAD（2026-10-04 事故根因），必须走主线程定时器队列
        private static System.Windows.Forms.Timer? _actionTimer;
        private static readonly System.Collections.Concurrent.ConcurrentQueue<Action> _actionQueue = new();

        // 下行通道黑名单：拒绝高危命令（只挡会丢会话/文件的，其余交给网页端确认开关）
        private static readonly HashSet<string> BlockedCommands = new(StringComparer.OrdinalIgnoreCase)
        {
            "QUIT", "EXIT", "SAVEAS", "NEW", "CLOSE", "SHUTDOWN", "LOGOUT", "RECOVER"
        };

        private static Autodesk.AutoCAD.Windows.PaletteSet? _paletteSet;
        private static AiAssistantView? _aiView;

        // 原生隐藏句柄集合：用于快速 unhide 恢复
        private static readonly HashSet<string> _nativeHiddenHandles = new(StringComparer.OrdinalIgnoreCase);

        public void Initialize()
        {
            try
            {
                Logger.Log("=== CadPerception AI Initialize Started ===");

                // 全局未处理异常兜底
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    if (e.ExceptionObject is System.Exception ex)
                        Logger.LogException("Unhandled AppDomain Exception", ex);
                };

                TaskScheduler.UnobservedTaskException += (s, e) =>
                {
                    Logger.LogException("Unobserved Task Exception", e.Exception);
                    e.SetObserved();
                };

                // 下行（AI 动作）队列：WinForms Timer 走 CAD 主线程消息循环，120ms 一拍
                _actionTimer = new System.Windows.Forms.Timer { Interval = 120 };
                _actionTimer.Tick += (s, e) => DrainActionQueue();
                _actionTimer.Start();

                var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
                ed?.WriteMessage("\n=======================================================");
                ed?.WriteMessage("\n[AI] AutoCAD AI Assistant 已加载");
                ed?.WriteMessage("\n[AI] 输入 AI 打开面板，输入 AICFG 查看当前模型配置");
                ed?.WriteMessage("\n=======================================================\n");

                string buildStamp = "";
                try { buildStamp = System.IO.File.GetLastWriteTime(System.Reflection.Assembly.GetExecutingAssembly().Location).ToString("yyyy-MM-dd HH:mm"); } catch { }
                Logger.Log($"=== CadPerception AI Initialize Completed (build {buildStamp}) ===");
            }
            catch (System.Exception ex)
            {
                Logger.LogException("Initialize Exception", ex);
            }
        }

        public void Terminate()
        {
            try
            {
                Logger.Log("=== CadPerception AI Terminate ===");
                _actionTimer?.Stop();
            }
            catch (System.Exception ex)
            {
                Logger.LogException("Terminate Exception", ex);
            }
        }

        internal static bool TryReadLiveView(out double cx, out double cy, out double w, out double h, out double twist)
        {
            cx = cy = w = h = twist = 0;
            try
            {
                Document? doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return false;

                double aspect = 1.0;
                try
                {
                    using ViewTableRecord v = doc.Editor.GetCurrentView();
                    if (v.Height > 1e-9) aspect = v.Width / v.Height;
                }
                catch { }

                try
                {
                    object? ctrObj = Application.GetSystemVariable("VIEWCTR");
                    object? sizeObj = Application.GetSystemVariable("VIEWSIZE");
                    double vsize = Convert.ToDouble(sizeObj);
                    Point3d ctr = ctrObj is Point3d p ? p : new Point3d(0, 0, 0);
                    if (ctrObj is double[] arr && arr.Length >= 2) ctr = new Point3d(arr[0], arr[1], 0);
                    try { twist = Convert.ToDouble(Application.GetSystemVariable("VIEWTWIST")); } catch { }
                    cx = ctr.X; cy = ctr.Y;
                    h = vsize; w = vsize * aspect;
                    return h > 1e-9;
                }
                catch
                {
                    using ViewTableRecord v = doc.Editor.GetCurrentView();
                    cx = v.CenterPoint.X; cy = v.CenterPoint.Y;
                    w = v.Width; h = v.Height; twist = v.ViewTwist;
                    return h > 1e-9;
                }
            }
            catch { return false; }
        }

        [CommandMethod("AI")]
        public void AICommand()
        {
            try
            {
                if (_paletteSet == null)
                {
                    _paletteSet = new Autodesk.AutoCAD.Windows.PaletteSet(
                        "AI助理",
                        new Guid("C8D234A1-82CF-4BC1-9C4F-018C99F0AA88")
                    )
                    {
                        Size = new System.Drawing.Size(360, 600),
                        MinimumSize = new System.Drawing.Size(300, 420),
                        Style = Autodesk.AutoCAD.Windows.PaletteSetStyles.ShowCloseButton |
                                Autodesk.AutoCAD.Windows.PaletteSetStyles.ShowAutoHideButton |
                                Autodesk.AutoCAD.Windows.PaletteSetStyles.ShowPropertiesMenu,
                        Dock = Autodesk.AutoCAD.Windows.DockSides.Right,
                        KeepFocus = true
                    };

                    _aiView = new AiAssistantView();
                    _paletteSet.AddVisual("AI助理", _aiView);
                }

                _paletteSet.Visible = !_paletteSet.Visible;
                if (_paletteSet.Visible)
                {
                    _paletteSet.Activate(0);
                    // 2026-11-11：防止此前被用户拖成 0 宽度后再次打开仍是隐形条，
                    // 显式恢复一个可用尺寸（不影响用户之后手动拖动）。
                    try
                    {
                        var sz = _paletteSet.Size;
                        if (sz.Width < 200 || sz.Height < 300)
                            _paletteSet.Size = new System.Drawing.Size(360, 600);
                    }
                    catch { }

                    var doc = Application.DocumentManager.MdiActiveDocument;
                    if (doc != null)
                    {
                        _aiView?.UpdateSelection(doc);
                    }
                }

                Broadcast(new
                {
                    @event = "aiAssistantWakeup",
                    visible = _paletteSet.Visible,
                    message = "CadCopilot 原生伴随式 AI 助理面板已切换",
                    time = DateTime.Now.ToString("HH:mm:ss.fff")
                });
            }
            catch (System.Exception ex)
            {
                Logger.LogException("AI Command PaletteSet Exception", ex);
            }
        }

        [CommandMethod("AICFG")]
        public void AICfgCommand()
        {
            try
            {
                var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
                ed?.WriteMessage("\n========== [CadCopilot AI 配置与预设] ==========");
                ed?.WriteMessage("\n[向量模型] 硅基流动: Qwen/Qwen3-VL-Embedding-8B (4096维)");
                ed?.WriteMessage("\n[调度模型] Apigoto: deepseek-flash (高速 Tool Calling / 动态手搓)");
                ed?.WriteMessage("\n[推理模型] Apigoto: kimi-k3 (长上下文 / 复杂结构推理)");
                ed?.WriteMessage("\n[识图模型] Apigoto: deepseek-flash（原生多模态识图，2026-11-10 实测通过；muse-spark-1.3 需网关开分组）");
                ed?.WriteMessage($"\n[LSP 直通] {CadPaths.LspDir}");
                ed?.WriteMessage($"\n[配置文件] {CadPaths.PresetsFile}（实际运行读写件；仓内 dll/CadPerception/ai_presets.json 仅为模板）");
                ed?.WriteMessage("\n================================================\n");
            }
            catch (System.Exception ex)
            {
                Logger.LogException("AICFG Command Exception", ex);
            }
        }

        private static object? SerializeEntity(Entity ent, bool isLocked)
        {
            if (ent is Line line)
            {
                return new
                {
                    handle = line.Handle.ToString(),
                    type = "Line",
                    layer = line.Layer,
                    visible = ent.Visible,
                    isLocked = isLocked,
                    startPoint = new { x = line.StartPoint.X, y = line.StartPoint.Y, z = line.StartPoint.Z },
                    endPoint = new { x = line.EndPoint.X, y = line.EndPoint.Y, z = line.EndPoint.Z },
                    length2D = Math.Sqrt(Math.Pow(line.EndPoint.X - line.StartPoint.X, 2) + Math.Pow(line.EndPoint.Y - line.StartPoint.Y, 2))
                };
            }
            if (ent is Circle circle)
            {
                return new
                {
                    handle = circle.Handle.ToString(),
                    type = "Circle",
                    layer = circle.Layer,
                    visible = ent.Visible,
                    isLocked = isLocked,
                    center = new { x = circle.Center.X, y = circle.Center.Y },
                    radius = circle.Radius
                };
            }
            if (ent is Arc arc) // ARC 命令生成的圆弧
            {
                // 弧长手动计算（2027 API 无 Arc.GetLength）：扫过角 = 终角 - 起角，非正补 2π
                double sweep = arc.EndAngle - arc.StartAngle;
                if (sweep <= 0) sweep += 2 * Math.PI;

                return new
                {
                    handle = arc.Handle.ToString(),
                    type = "Arc",
                    layer = arc.Layer,
                    visible = ent.Visible,
                    isLocked = isLocked,
                    center = new { x = arc.Center.X, y = arc.Center.Y },
                    radius = arc.Radius,
                    startAngle = arc.StartAngle,
                    endAngle = arc.EndAngle,
                    startPoint = new { x = arc.StartPoint.X, y = arc.StartPoint.Y },
                    endPoint = new { x = arc.EndPoint.X, y = arc.EndPoint.Y },
                    length2D = arc.Radius * sweep
                };
            }
            if (ent is Polyline pl) // LWPolyline：RECTANG / PLINE 命令生成的矩形与多段线
            {
                var verts = new List<object>();
                var bulges = new List<double>();
                for (int i = 0; i < pl.NumberOfVertices; i++)
                {
                    Point2d pt = pl.GetPoint2dAt(i);
                    verts.Add(new { x = pt.X, y = pt.Y });
                    bulges.Add(pl.GetBulgeAt(i));
                }

                // 周长按段累加：凸度非零段按圆弧长 r·|4·atan(b)|（2027 API 无 Arc.GetLength）
                double len2D = 0;
                for (int i = 0; i + 1 < pl.NumberOfVertices; i++)
                {
                    len2D += SegmentLength2D(pl.GetPoint2dAt(i), pl.GetPoint2dAt(i + 1), pl.GetBulgeAt(i));
                }
                if (pl.Closed && pl.NumberOfVertices > 2)
                {
                    len2D += SegmentLength2D(pl.GetPoint2dAt(pl.NumberOfVertices - 1), pl.GetPoint2dAt(0), pl.GetBulgeAt(pl.NumberOfVertices - 1));
                }

                return new
                {
                    handle = pl.Handle.ToString(),
                    type = "Polyline",
                    layer = pl.Layer,
                    visible = ent.Visible,
                    isLocked = isLocked,
                    vertices = verts,
                    bulges = bulges,
                    closed = pl.Closed,
                    length2D = len2D
                };
            }
            if (ent is DBText dt) // TEXT 单行文字
            {
                return new
                {
                    handle = dt.Handle.ToString(),
                    type = "Text",
                    layer = dt.Layer,
                    visible = ent.Visible,
                    isLocked = isLocked,
                    text = dt.TextString,
                    position = new { x = dt.Position.X, y = dt.Position.Y },
                    height = dt.Height,
                    rotation = dt.Rotation
                };
            }
            if (ent is MText mt) // MTEXT 多行文字
            {
                string raw = mt.Contents ?? "";
                return new
                {
                    handle = mt.Handle.ToString(),
                    type = "MText",
                    layer = mt.Layer,
                    visible = ent.Visible,
                    isLocked = isLocked,
                    text = raw,
                    plainText = StripMTextFormatting(raw),
                    position = new { x = mt.Location.X, y = mt.Location.Y },
                    height = mt.TextHeight,
                    rotation = mt.Rotation
                };
            }
            if (ent is Dimension dim) // 线性/对齐/角度/半径等标注，基类统一取值
            {
                return new
                {
                    handle = dim.Handle.ToString(),
                    type = "Dimension",
                    layer = dim.Layer,
                    visible = ent.Visible,
                    isLocked = isLocked,
                    measurement = dim.Measurement,
                    textOverride = string.IsNullOrEmpty(dim.DimensionText) ? null : dim.DimensionText,
                    dimStyle = dim.DimensionStyleName,
                    textPosition = new { x = dim.TextPosition.X, y = dim.TextPosition.Y }
                };
            }
            if (ent is BlockReference bref) // 块参照：块名+位姿（模型空间里的实例，内部几何用 readBlock 查）
            {
                return new
                {
                    handle = bref.Handle.ToString(),
                    type = "BlockReference",
                    layer = bref.Layer,
                    visible = ent.Visible,
                    isLocked = isLocked,
                    blockName = bref.Name,
                    position = new { x = bref.Position.X, y = bref.Position.Y },
                    scale = bref.ScaleFactors.X,
                    rotation = bref.Rotation * 180.0 / Math.PI
                };
            }
            return new
            {
                handle = ent.Handle.ToString(),
                type = ent.GetType().Name,
                layer = ent.Layer,
                isLocked = isLocked
            };
        }

        private object Diagnose2DLineIntersection(Line l1, Line l2, Transaction tr)
        {
            var layer1 = tr.GetObject(l1.LayerId, OpenMode.ForRead) as LayerTableRecord;
            var layer2 = tr.GetObject(l2.LayerId, OpenMode.ForRead) as LayerTableRecord;

            if ((layer1?.IsLocked ?? false) || (layer2?.IsLocked ?? false))
            {
                return new
                {
                    canBreak = false,
                    reason = "【图层锁定】所选线段所在图层被锁定，CAD 禁止修改。",
                    solution = "解锁图层"
                };
            }

            // 检查 Z 轴噪点
            bool zDirty = Math.Abs(l1.StartPoint.Z) > 1e-4 || Math.Abs(l1.EndPoint.Z) > 1e-4 ||
                          Math.Abs(l2.StartPoint.Z) > 1e-4 || Math.Abs(l2.EndPoint.Z) > 1e-4;
            double zDelta = Math.Abs(l1.StartPoint.Z - l2.StartPoint.Z);

            Point2d p1Start = new Point2d(l1.StartPoint.X, l1.StartPoint.Y);
            Point2d p1End = new Point2d(l1.EndPoint.X, l1.EndPoint.Y);
            Point2d p2Start = new Point2d(l2.StartPoint.X, l2.StartPoint.Y);
            Point2d p2End = new Point2d(l2.EndPoint.X, l2.EndPoint.Y);

            LineSegment2d seg1 = new LineSegment2d(p1Start, p1End);
            LineSegment2d seg2 = new LineSegment2d(p2Start, p2End);

            Point2d[]? interPts = seg1.IntersectWith(seg2);
            bool has2DIntersection = interPts != null && interPts.Length > 0;

            Line2d line1Inf = new Line2d(p1Start, p1End);
            Line2d line2Inf = new Line2d(p2Start, p2End);
            Point2d[]? extPts = line1Inf.IntersectWith(line2Inf);
            bool hasExtIntersection = extPts != null && extPts.Length > 0;

            if (zDirty && zDelta > 1e-4)
            {
                return new
                {
                    canBreak = false,
                    is2DIntersecting = has2DIntersection,
                    zDelta = zDelta,
                    reason = $"【2D相交但3D脱节】平面视图 (XY) 看起来相交，但两线 Z 轴高差为 {zDelta:F4}，CAD 原生打断命令在 3D 异面下会直接失效！",
                    solution = "建议执行一键压平 (Flatten Z=0)"
                };
            }

            if (has2DIntersection)
            {
                var pt = interPts![0];
                return new
                {
                    canBreak = true,
                    is2DIntersecting = true,
                    intersectionXY = new { x = pt.X, y = pt.Y },
                    reason = "平面上完全相交，几何拓扑正常，可直接打断。",
                    solution = "直接打断"
                };
            }

            if (hasExtIntersection)
            {
                var extPt = extPts![0];
                return new
                {
                    canBreak = false,
                    is2DIntersecting = false,
                    extendedIntersectionXY = new { x = extPt.X, y = extPt.Y },
                    reason = "【未搭接缝隙】平面延伸后有交点，但当前端点脱节，存在肉眼不易察觉的微小缺口。",
                    solution = "需先延伸 (Extend) 再打断"
                };
            }

            return new
            {
                canBreak = false,
                is2DIntersecting = false,
                reason = "两线段在平面上平行，无任何交点。",
                solution = "无法打断"
            };
        }

        // 多行文字格式码清理：\P→换行、\~→空格，剥离 \f..; \H2x; \L 等控制码与花括号
        private static string StripMTextFormatting(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            string t = s.Replace("\\P", "\n").Replace("\\~", " ");
            t = System.Text.RegularExpressions.Regex.Replace(t, @"\\[A-Za-z][^;\\]*;?", "");
            t = t.Replace("{", "").Replace("}", "");
            return t.Trim();
        }

        // 多段线段长：直线段=弦长；凸度段=半径×|扫过角|，扫过角=4·atan(|bulge|)
        private static double SegmentLength2D(Point2d a, Point2d b, double bulge)
        {
            double chord = a.GetDistanceTo(b);
            if (Math.Abs(bulge) < 1e-10 || chord < 1e-12) return chord;
            double r = chord * (1 + bulge * bulge) / (4 * Math.Abs(bulge));
            return r * 4 * Math.Atan(Math.Abs(bulge));
        }

        private object GetCurrentViewport2DBounds(Editor ed)
        {
            try
            {
                using (ViewTableRecord view = ed.GetCurrentView())
                {
                    double height = view.Height;
                    double width = view.Width;
                    Point2d center = view.CenterPoint;

                    return new
                    {
                        center = new { x = center.X, y = center.Y },
                        minXY = new { x = center.X - width / 2.0, y = center.Y - height / 2.0 },
                        maxXY = new { x = center.X + width / 2.0, y = center.Y + height / 2.0 },
                        width = width,
                        height = height
                    };
                }
            }
            catch
            {
                return new { status = "viewport_unavailable" };
            }
        }

        // WPF AI 面板直接复用 WS 下行执行链（2026-10-24 动作块协议对齐）：
        // 同一黑名单/主线程队列/aiActionResult 回执广播，网页与原生面板走完全相同的地板
        public static void ExecuteAiAction(string json) => HandleClientMessage(json);

        /// <summary>
        /// 同步执行并从回执里取真实结果（2026-11-11）。
        /// 动机：面板此前是「发出去就算完成」，结果里说「已删除 20 个」其实什么都没删——
        /// 因为真正执行在 UI 队列里，是否成功从未回读。这个方法把 Actions 的结果同步取回来，
        /// 让面板显示验证过的事实，而不是模型的自我声称。
        /// 死锁防护：若当前已在 UI 线程，Write.* 动作会直接就地执行（_syncInline=true），
        /// 不再入队等待——否则 UI 线程阻塞等队列，队列又需要 UI 线程 tick，必死锁。
        /// </summary>
        public static (bool Ok, string Message, string DataJson) ExecuteAiActionSync(string json)
        {
            bool ok = false;
            string msg = "";
            string dataJson = "";
            var done = new ManualResetEventSlim(false);

            void Capture(object payload)
            {
                try
                {
                    var t = payload.GetType();
                    var okProp = t.GetProperty("ok");
                    if (okProp != null) ok = (bool)(okProp.GetValue(payload) ?? false);
                    var mProp = t.GetProperty("message") ?? t.GetProperty("error");
                    if (mProp != null) msg = mProp.GetValue(payload)?.ToString() ?? "";
                    var dProp = t.GetProperty("data");
                    if (dProp != null)
                    {
                        var dd = dProp.GetValue(payload);
                        if (dd != null) dataJson = JsonSerializer.Serialize(dd);
                    }
                }
                catch { }
                finally { done.Set(); }
            }

            var prevSink = _syncReplySink;
            _syncReplySink = Capture;
            bool prevInline = _syncInline;
            _syncInline = true;      // 让 EnqueueAction 就地执行，避免 UI 线程自锁
            try
            {
                HandleClientMessage(json);
                if (!done.IsSet) done.Wait(10000);
            }
            finally
            {
                _syncInline = prevInline;
                _syncReplySink = prevSink;
            }

            return (ok, msg, dataJson);
        }

        // 同步回执接收器：ExecuteAiActionSync 期间由 SendReply 分流到它
        private static Action<object>? _syncReplySink;
        // 同步模式下 EnqueueAction 就地执行（2026-11-11，防 UI 线程自锁）
        private static bool _syncInline;

        // 处理网页下行指令：runCommand（执行命令字符串）/ ping
        private static void HandleClientMessage(string json)
        {
            try
            {
                using JsonDocument jdoc = JsonDocument.Parse(json);                var root = jdoc.RootElement;
                string? requestId = root.TryGetProperty("requestId", out var rid) && rid.ValueKind == JsonValueKind.String ? rid.GetString() : null;
                string expectedDocument = root.TryGetProperty("expectedDocument", out var edoc) && edoc.ValueKind == JsonValueKind.String ? edoc.GetString() ?? "" : "";
                Action<object> reply = data => SendReply(data, requestId);
                string action = root.TryGetProperty("action", out var actionEl) ? actionEl.GetString() ?? "" : "";

                if (action == "runCommand")
                {
                    string cmd = root.TryGetProperty("command", out var cmdEl) ? cmdEl.GetString() ?? "" : "";
                    cmd = cmd.Trim();
                    if (string.IsNullOrWhiteSpace(cmd))
                    {
                        reply(new { @event = "commandResult", ok = false, command = cmd, message = "空命令", time = DateTime.Now.ToString("HH:mm:ss.fff") });
                        return;
                    }

                    // 黑名单：第一个词（命令名）命中即拒绝
                    string firstWord = cmd.Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0];
                    if (BlockedCommands.Contains(firstWord))
                    {
                        Logger.Log($"[DOWNLINK] 拒绝黑名单命令: {cmd}");
                        reply(new { @event = "commandResult", ok = false, command = cmd, message = $"命令 {firstWord} 在黑名单中，已拒绝", time = DateTime.Now.ToString("HH:mm:ss.fff") });
                        return;
                    }

                    EnqueueAction(() =>
                    {
                    Document? doc = Application.DocumentManager.MdiActiveDocument;
                    if (doc == null)
                    {
                        reply(new { @event = "commandResult", ok = false, command = cmd, message = "无活动文档", time = DateTime.Now.ToString("HH:mm:ss.fff") });
                        return;
                    }

                    if (!MatchesDocument(doc, expectedDocument))
                    {
                        reply(new { @event = "commandResult", ok = false, command = cmd, message = "活动图纸不符合任务合同", code = "DOCUMENT_MISMATCH" });
                        return;
                    }
                    Logger.Log($"[DOWNLINK] 执行命令: {cmd}");
                    // SendStringToExecute 会排入 CAD 命令队列异步执行；结果通过 commandWillStart/Ended/Failed/Cancelled 事件流回传
                    doc.SendStringToExecute(cmd + "\n", true, false, false);
                    reply(new { @event = "commandResult", ok = true, command = cmd, message = "已发送到 CAD 命令行", time = DateTime.Now.ToString("HH:mm:ss.fff") });
                    }, reply);
                }
                else if (action == "ping")
                {
                    reply(new { @event = "pong", time = DateTime.Now.ToString("HH:mm:ss.fff") });
                }
                else if (action == "draw" || action == "erase" || action == "query")
                {
                    // 结构化下行（直改数据库，不经命令行）：注意 JsonElement 在 using jdoc 释放后不可用，先解析成 POCO
                    List<DrawSpec> drawItems = action == "draw" ? ParseDrawItems(root) : new List<DrawSpec>();
                    List<string> handles = new();
                    if (action == "erase" || action == "query")
                    {
                        if (root.TryGetProperty("handles", out var hArr) && hArr.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var h in hArr.EnumerateArray()) handles.Add(h.GetString() ?? "");
                        }
                    }
                    Logger.Log($"[DOWNLINK] {action}: items={drawItems.Count}, handles={handles.Count}");
                    EnqueueAction(() => RunStructuredAction(action, drawItems, handles, reply, expectedDocument), reply);
                }
                else if (action == "insertBlock" || action == "transform" || action == "queryWindow" ||
                         action == "listBlocks" || action == "listLayers" || action == "undo" || action == "save" || action == "stats" ||
                         action == "readBlock" || action == "getView" ||
                         action == "getSelection" || action == "hide" || action == "unhide" || action == "explode" ||
                         action == "findText" || action == "readDimensions" || action == "getExtents" || action == "flatten" || action == "setLayer" ||
                         action == "drawDimension" || action == "zoom" || action == "purge" || action == "regen" ||
                         action == "filter" || action == "layerState" || action == "measure" ||
                         action == "normalizeLayer" || action == "crossViewQuery" || action == "exportText" ||
                         action == "aiSubmit" || action == "aiTranscript")
                {
                    // 绘图 API v2：同样先解析 POCO，再排队到主线程执行
                    ActionSpec a = ParseActionSpec(root, action);
                    Logger.Log($"[DOWNLINK] {action}: {a.Op}{a.Name}");
                    EnqueueAction(() => RunExtendedAction(action, a, reply, expectedDocument), reply);
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogException("HandleClientMessage Exception", ex);
            }
        }

        // 下行动作入队（WS 接收线程只做入队，绝不碰文档锁）；队列上限防积压
        private static void EnqueueAction(Action run, Action<object> reply)
        {
            // 同步调用模式（面板回读）：就地执行，绝不入队，避免 UI 线程等自己（2026-11-11）
            if (_syncInline)
            {
                try { run(); }
                catch (System.Exception ex) { Logger.LogException("EnqueueAction(sync)", ex); }
                return;
            }

            if (_actionQueue.Count >= 64)
            {
                Logger.Log("[DOWNLINK] 动作队列已满(64)，丢弃新请求");
                reply(new { @event = "aiActionResult", action = "queueFull", ok = false, message = "动作队列已满，请稍后再发", time = DateTime.Now.ToString("HH:mm:ss.fff") });
                return;
            }
            _actionQueue.Enqueue(run);
        }

        // 主线程消费下行动作：命令进行中让路（放回队尾，防与活动命令重入），空闲时执行
        private static void DrainActionQueue()
        {
            int done = 0;
            while (done < 2 && _actionQueue.TryDequeue(out Action? run))
            {
                try
                {
                    Document? doc = Application.DocumentManager.MdiActiveDocument;
                    if (doc != null && !string.IsNullOrEmpty(doc.CommandInProgress))
                    {
                        _actionQueue.Enqueue(run);
                        return;
                    }
                    run();
                }
                catch (System.Exception ex)
                {
                    Logger.LogException("DrainActionQueue Exception", ex);
                }
                done++;
            }
        }

        // 结构化动作在 CAD 主线程空闲时执行（经动作队列）：draw 建实体 / erase 删实体 / query 回读
        private static void RunStructuredAction(string action, List<DrawSpec> drawItems, List<string> handles, Action<object> reply, string expectedDocument)
        {
            try
            {
                Document? doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null)
                {
                    reply(new { @event = "aiActionResult", action, ok = false, message = "无活动文档", time = DateTime.Now.ToString("HH:mm:ss.fff") });
                    return;
                }

                // DocumentLock 允许后台线程安全进入文档上下文（模态/定时器场景标准做法）
                if (!MatchesDocument(doc, expectedDocument))
                {
                    reply(new { @event = "aiActionResult", action, ok = false, code = "DOCUMENT_MISMATCH", message = "活动图纸不符合任务合同" });
                    return;
                }
                using (doc.LockDocument())
                {
                    var result = new Dictionary<string, object> { ["document"] = doc.Name };
                    Database db = doc.Database;
                    using Transaction tr = db.TransactionManager.StartTransaction();
                    try
                    {
                        if (action == "draw")
                        {
                            var made = new List<string>();
                            var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                            foreach (var d in drawItems)
                            {
                                Entity? e = d.Kind switch
                                {
                                    "line" => new Line(new Point3d(d.X1, d.Y1, 0), new Point3d(d.X2, d.Y2, 0)),
                                    "circle" => new Circle(new Point3d(d.Cx, d.Cy, 0), Vector3d.ZAxis, d.R),
                                    "arc" => new Arc(new Point3d(d.Cx, d.Cy, 0), d.R, d.StartAngle, d.EndAngle),
                                    "text" => new DBText { TextString = d.Text, Position = new Point3d(d.X, d.Y, 0), Height = d.Height },
                                    "polyline" => BuildPolyline(d),
                                    _ => null
                                };
                                if (e == null) continue;
                                EnsureLayer(tr, db, d.Layer);
                                e.Layer = d.Layer;
                                btr.AppendEntity(e);
                                tr.AddNewlyCreatedDBObject(e, true);
                                made.Add(e.Handle.ToString());
                            }
                            result["handles"] = made;
                            result["count"] = made.Count;
                        }
                        else if (action == "erase")
                        {
                            int n = 0;
                            var missing = new List<string>();
                            foreach (string h in handles)
                            {
                                if (!TryGetObjectId(db, h, out ObjectId id)) { missing.Add(h); continue; }
                                if (tr.GetObject(id, OpenMode.ForWrite) is Entity ent) { ent.Erase(); n++; }
                            }
                            result["erased"] = n;
                            result["missing"] = missing;
                        }
                        else if (action == "query")
                        {
                            var ents = new List<object>();
                            var missing = new List<string>();
                            foreach (string h in handles)
                            {
                                if (!TryGetObjectId(db, h, out ObjectId id)) { missing.Add(h); continue; }
                                if (tr.GetObject(id, OpenMode.ForRead) is Entity ent)
                                {
                                    var ltr = tr.GetObject(ent.LayerId, OpenMode.ForRead) as LayerTableRecord;
                                    ents.Add(SerializeEntity(ent, ltr?.IsLocked ?? false) ?? new { handle = h, type = "Unknown" });
                                }
                            }
                            result["entities"] = ents;
                            result["missing"] = missing;
                        }
                        tr.Commit();
                        reply(new { @event = "aiActionResult", action, ok = true, data = result, time = DateTime.Now.ToString("HH:mm:ss.fff") });
                    }
                    catch (System.Exception ex)
                    {
                        tr.Abort();
                        Logger.LogException($"RunStructuredAction({action}) Exception", ex);
                        reply(new { @event = "aiActionResult", action, ok = false, message = ex.Message, time = DateTime.Now.ToString("HH:mm:ss.fff") });
                    }
                } // end using LockDocument
            }
            catch (System.Exception ex)
            {
                Logger.LogException("RunStructuredAction outer Exception", ex);
                reply(new { @event = "aiActionResult", action, ok = false, message = ex.Message, time = DateTime.Now.ToString("HH:mm:ss.fff") });
            }
        }

        private static Polyline BuildPolyline(DrawSpec d)
        {
            var pl = new Polyline();
            foreach (Point2d p in d.Points) pl.AddVertexAt(pl.NumberOfVertices, p, 0, 0, 0);
            pl.Closed = d.Closed;
            return pl;
        }

        private static bool TryGetObjectId(Database db, string handleHex, out ObjectId id)
        {
            id = ObjectId.Null;
            if (string.IsNullOrWhiteSpace(handleHex)) return false;
            try
            {
                long hv = Convert.ToInt64(handleHex, 16);
                id = db.GetObjectId(false, new Handle(hv), 0);
                return id.IsValid;
            }
            catch
            {
                return false;
            }
        }

        private static void EnsureLayer(Transaction tr, Database db, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(name)) return;
            lt.UpgradeOpen();
            var ltr = new LayerTableRecord { Name = name };
            lt.Add(ltr);
            tr.AddNewlyCreatedDBObject(ltr, true);
        }

        private static List<DrawSpec> ParseDrawItems(JsonElement root)
        {
            var list = new List<DrawSpec>();
            if (!root.TryGetProperty("entities", out var arr) || arr.ValueKind != JsonValueKind.Array) return list;
            foreach (var it in arr.EnumerateArray())
            {
                var d = new DrawSpec();
                double Num(string n, double def = 0) => it.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : def;
                string Str(string n, string def) => it.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? def : def;
                d.Kind = Str("kind", "line");
                d.Layer = Str("layer", "0");
                d.X1 = Num("x1"); d.Y1 = Num("y1"); d.X2 = Num("x2"); d.Y2 = Num("y2");
                d.Cx = Num("cx"); d.Cy = Num("cy"); d.R = Num("r", 100);
                d.StartAngle = Num("startAngle"); d.EndAngle = Num("endAngle");
                d.X = Num("x"); d.Y = Num("y"); d.Height = Num("height", 100);
                d.Text = Str("text", "");
                d.Closed = it.TryGetProperty("closed", out var cb) && cb.ValueKind == JsonValueKind.True;
                if (it.TryGetProperty("points", out var pts) && pts.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in pts.EnumerateArray())
                    {
                        if (p.TryGetProperty("x", out var px) && p.TryGetProperty("y", out var py))
                        {
                            d.Points.Add(new Point2d(px.GetDouble(), py.GetDouble()));
                        }
                    }
                }
                list.Add(d);
            }
            return list;
        }

        private sealed class DrawSpec
        {
            public string Kind = "line";
            public string Layer = "0";
            public string Text = "";
            public double X1, Y1, X2, Y2, Cx, Cy, R = 100, StartAngle, EndAngle, X, Y, Height = 100;
            public bool Closed;
            public readonly List<Point2d> Points = new();
        }

        // 绘图 API v2 + 业务运维动作的统一参数载体
        private sealed class ActionSpec
        {
            public string Name = "";      // insertBlock 的块名
            public string Op = "";        // transform 的操作：move/rotate/scale/copy/mirror
            public string Mode = "window";// queryWindow 的模式：window/crossing / zoom 的模式
            public string Kind = "rotated"; // drawDimension 的类型：rotated / aligned
            public double X, Y, Scale = 1, Rotation; // insertBlock / drawDimension
            public double Dx, Dy, Angle, Factor = 1, Cx, Cy, X1, Y1, X2, Y2; // transform/queryWindow
            public double DimX, DimY;    // 标注线通过点
            public string Layer = "";
            public string Text = "";      // findText 的查找关键词 / 标注替代文字
            public int ColorIndex = 7;    // setLayer 的图层颜色
            public int Depth = 1;         // explode 的递归深度
            public bool Commit = true;     // explode 是否提交修改
            public bool SelectionOnly = false; // hide/getExtents/flatten 是否仅限当前选区
            // filter：按图元类型 + 可选窗口范围批量改层/删除（2026-11-10 新增）
            // 起因：用户口述「删掉所有填充」「选区里非直线改图层」时，模型只能手搓 LISP，
            // 而 LISP 的 ssget 过滤在脚本执行下不可靠（实测误删直线），必须内置为原子动作。
            public readonly List<string> Types = new();
            public string FilterOp = "";   // erase / setLayer
            public bool HasWindow = false;
            // 图层状态动作（2026-11-10）：layerState = on/off/freeze/thaw/lock/unlock/highlight/isolate/unhighlight
            public string LayerState = "";
            public readonly List<string> LayerNames = new();   // 多图层批量
            public bool AllLayers = false;
            // 测量标注（measure）：textHeight 字高、offset 文字偏移、layer 标注图层
            public double TextHeight = 0;
            public double Offset = 200;
            // 导出（exportText）：file 输出路径、format 输出格式
            public string FilePath = "";
            public string Format = "";
            public string NameContains = "";   // layerState 按图层名关键词模糊匹配
            // normalizeLayer 的 别名层 -> 规范层 映射
            public readonly Dictionary<string, string> LayerMapping = new(StringComparer.OrdinalIgnoreCase);
            public readonly List<string> Handles = new();
        }

        private static ActionSpec ParseActionSpec(JsonElement root, string action)
        {
            var a = new ActionSpec();
            double Num(string n, double def = 0) => root.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : def;
            string Str(string n, string def) => root.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? def : def;
            int IntVal(string n, int def = 0) => root.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : def;
            bool BoolVal(string n, bool def = false) => root.TryGetProperty(n, out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False) ? v.GetBoolean() : def;

            a.Name = Str("name", "");
            a.Op = Str("op", "");
            a.Mode = Str("mode", "window");
            a.Kind = Str("kind", "rotated");
            a.Layer = Str("layer", "");
            a.Text = Str("text", "");
            a.ColorIndex = IntVal("color", 7);
            a.Depth = IntVal("depth", 1);
            a.Commit = BoolVal("commit", true);
            a.SelectionOnly = BoolVal("selectionOnly", false);
            a.X = Num("x"); a.Y = Num("y"); a.Scale = Num("scale", 1); a.Rotation = Num("rotation");
            a.Dx = Num("dx"); a.Dy = Num("dy"); a.Angle = Num("angle"); a.Factor = Num("factor", 1);
            a.Cx = Num("cx"); a.Cy = Num("cy"); a.X1 = Num("x1"); a.Y1 = Num("y1"); a.X2 = Num("x2"); a.Y2 = Num("y2");
            a.DimX = Num("dimX"); a.DimY = Num("dimY");
            if (root.TryGetProperty("handles", out var hArr) && hArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var h in hArr.EnumerateArray()) a.Handles.Add(h.GetString() ?? "");
            }
            if (root.TryGetProperty("types", out var tArr) && tArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var t in tArr.EnumerateArray()) a.Types.Add(t.GetString() ?? "");
            }
            a.FilterOp = Str("op", a.FilterOp);
            // 2026-11-10：layerState 的模糊匹配词（"所有墙体图层" -> filter 字段），
            // 复用 filter 字段会与 filter 动作的 op 冲突，这里另开一个字段。
            a.NameContains = Str("nameContains", a.NameContains);
            a.HasWindow = root.TryGetProperty("x1", out _) && root.TryGetProperty("x2", out _);
            a.LayerState = Str("state", "");
            a.AllLayers = BoolVal("allLayers", false);
            if (root.TryGetProperty("layers", out var lArr) && lArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var l in lArr.EnumerateArray()) a.LayerNames.Add(l.GetString() ?? "");
            }
            a.TextHeight = Num("textHeight", 0);
            a.Offset = Num("offset", 200);
            a.FilePath = Str("file", "");
            a.Format = Str("format", "");
            if (root.TryGetProperty("mapping", out var mapEl) && mapEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var kv in mapEl.EnumerateObject())
                    if (kv.Value.ValueKind == JsonValueKind.String)
                        a.LayerMapping[kv.Name] = kv.Value.GetString() ?? "";
            }
            return a;
        }

        // 绘图 API v2：插块/枚举块/变换/范围查询/图层表/UNDO/保存/全图统计
        private static void RunExtendedAction(string action, ActionSpec a, Action<object> reply, string expectedDocument)
        {
            try
            {
                Document? doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null)
                {
                    reply(new { @event = "aiActionResult", action, ok = false, message = "无活动文档", time = DateTime.Now.ToString("HH:mm:ss.fff") });
                    return;
                }

                if (!MatchesDocument(doc, expectedDocument))
                {
                    reply(new { @event = "aiActionResult", action, ok = false, code = "DOCUMENT_MISMATCH", message = "活动图纸不符合任务合同" });
                    return;
                }

                // 走命令行的动作（安全且可审计，不进 DocumentLock 事务）：undo / save / zoom / regen / purge
                if (action == "undo" || action == "save" || action == "zoom" || action == "regen" || action == "purge")
                {
                    if (action == "undo")
                    {
                        doc.SendStringToExecute("_.U\n", true, false, false);
                        reply(new { @event = "aiActionResult", action, ok = true, message = "已发送 UNDO（回退一步）", time = DateTime.Now.ToString("HH:mm:ss.fff") });
                    }
                    else if (action == "save")
                    {
                        if (string.IsNullOrEmpty(doc.Database.Filename))
                        {
                            reply(new { @event = "aiActionResult", action, ok = false, message = "图纸未命名，跳过保存（避免弹对话框）", time = DateTime.Now.ToString("HH:mm:ss.fff") });
                        }
                        else
                        {
                            doc.SendStringToExecute("_.QSAVE\n", true, false, false);
                            reply(new { @event = "aiActionResult", action, ok = true, message = "已发送 QSAVE", time = DateTime.Now.ToString("HH:mm:ss.fff") });
                        }
                    }
                    else if (action == "regen")
                    {
                        doc.SendStringToExecute("_.REGEN\n", true, false, false);
                        reply(new { @event = "aiActionResult", action, ok = true, message = "已发送 REGEN（重生成视口）", time = DateTime.Now.ToString("HH:mm:ss.fff") });
                    }
                    else if (action == "purge")
                    {
                        doc.SendStringToExecute("_.-PURGE _ALL * _N\n", true, false, false); // pit45: dialog version wedges headless queue
                        reply(new { @event = "aiActionResult", action, ok = true, message = "已发送 PURGE ALL（清理全图无用块和图层）", time = DateTime.Now.ToString("HH:mm:ss.fff") });
                    }
                    else if (action == "zoom")
                    {
                        if (a.Mode == "extents")
                        {
                            doc.SendStringToExecute("_.ZOOM _E\n", true, false, false);
                            reply(new { @event = "aiActionResult", action, ok = true, message = "已发送 ZOOM E（全图缩放）", time = DateTime.Now.ToString("HH:mm:ss.fff") });
                        }
                        else if (a.Handles.Count > 0)
                        {
                            double minX = double.MaxValue, minY = double.MaxValue;
                            double maxX = double.MinValue, maxY = double.MinValue;
                            int valid = 0;
                            using (var ztr = doc.Database.TransactionManager.StartTransaction())
                            {
                                foreach (string h in a.Handles)
                                {
                                    if (TryGetObjectId(doc.Database, h, out ObjectId zid) && ztr.GetObject(zid, OpenMode.ForRead) is Entity zent)
                                    {
                                        try
                                        {
                                            var zext = zent.GeometricExtents;
                                            minX = Math.Min(minX, zext.MinPoint.X);
                                            minY = Math.Min(minY, zext.MinPoint.Y);
                                            maxX = Math.Max(maxX, zext.MaxPoint.X);
                                            maxY = Math.Max(maxY, zext.MaxPoint.Y);
                                            valid++;
                                        }
                                        catch { }
                                    }
                                }
                                ztr.Commit();
                            }
                            if (valid > 0)
                            {
                                double pad = Math.Max(50.0, Math.Max(maxX - minX, maxY - minY) * 0.1);
                                doc.SendStringToExecute($"_.ZOOM _W {minX - pad:F1},{minY - pad:F1} {maxX + pad:F1},{maxY + pad:F1}\n", true, false, false);
                                reply(new { @event = "aiActionResult", action, ok = true, message = $"已缩放到指定 {valid} 个图元范围", time = DateTime.Now.ToString("HH:mm:ss.fff") });
                            }
                            else
                            {
                                reply(new { @event = "aiActionResult", action, ok = false, message = "指定句柄无有效几何包围盒", time = DateTime.Now.ToString("HH:mm:ss.fff") });
                            }
                        }
                        else
                        {
                            doc.SendStringToExecute($"_.ZOOM _W {a.X1:F1},{a.Y1:F1} {a.X2:F1},{a.Y2:F1}\n", true, false, false);
                            reply(new { @event = "aiActionResult", action, ok = true, message = $"已缩放到窗口 ({a.X1:F1},{a.Y1:F1}) -> ({a.X2:F1},{a.Y2:F1})", time = DateTime.Now.ToString("HH:mm:ss.fff") });
                        }
                    }
                    return;
                }

                using (doc.LockDocument())
                {
                    Database db = doc.Database;
                    using Transaction tr = db.TransactionManager.StartTransaction();
                    try
                    {
                        var result = new Dictionary<string, object> { ["document"] = doc.Name };

                        if (action == "insertBlock")
                        {
                            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                            if (!bt.Has(a.Name))
                            {
                                var avail = new List<string>();
                                foreach (ObjectId bid in bt)
                                {
                                    if (tr.GetObject(bid, OpenMode.ForRead) is BlockTableRecord r && !r.IsLayout && !r.Name.StartsWith("*")) avail.Add(r.Name);
                                }
                                tr.Abort();
                                reply(new { @event = "aiActionResult", action, ok = false, message = $"块 {a.Name} 不存在。可用块: {string.Join(", ", avail.Take(50))}", time = DateTime.Now.ToString("HH:mm:ss.fff") });
                                return;
                            }
                            var ms = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                            var br = new BlockReference(new Point3d(a.X, a.Y, 0), bt[a.Name])
                            {
                                ScaleFactors = new Scale3d(a.Scale),
                                Rotation = a.Rotation * Math.PI / 180.0
                            };
                            if (!string.IsNullOrEmpty(a.Layer)) { EnsureLayer(tr, db, a.Layer); br.Layer = a.Layer; }
                            ms.AppendEntity(br);
                            tr.AddNewlyCreatedDBObject(br, true);
                            result["handle"] = br.Handle.ToString();
                            result["name"] = a.Name;
                        }
                        else if (action == "listBlocks")
                        {
                            var names = new List<string>();
                            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                            foreach (ObjectId bid in bt)
                            {
                                if (tr.GetObject(bid, OpenMode.ForRead) is BlockTableRecord r && !r.IsLayout && !r.Name.StartsWith("*")) names.Add(r.Name);
                            }
                            result["blocks"] = names;
                            result["count"] = names.Count;
                        }
                        else if (action == "readBlock") // 读块定义内部几何（学习模型库用）
                        {
                            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                            if (!bt.Has(a.Name))
                            {
                                tr.Abort();
                                reply(new { @event = "aiActionResult", action, ok = false, message = $"块 {a.Name} 不存在（可用 listBlocks 查全部块名）", time = DateTime.Now.ToString("HH:mm:ss.fff") });
                                return;
                            }
                            var btr = (BlockTableRecord)tr.GetObject(bt[a.Name], OpenMode.ForRead);
                            var ents = new List<object>();
                            foreach (ObjectId eid in btr)
                            {
                                if (ents.Count >= 300) break;
                                if (tr.GetObject(eid, OpenMode.ForRead) is Entity be)
                                    ents.Add(SerializeEntity(be, false) ?? new { handle = be.Handle.ToString(), type = be.GetType().Name, layer = be.Layer });
                            }
                            result["name"] = a.Name;
                            result["origin"] = new { x = btr.Origin.X, y = btr.Origin.Y };
                            result["entities"] = ents;
                            result["count"] = ents.Count;
                        }
                        else if (action == "listLayers")                        {
                            var layers = new List<object>();
                            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                            foreach (ObjectId lid in lt)
                            {
                                if (tr.GetObject(lid, OpenMode.ForRead) is LayerTableRecord l)
                                {
                                    layers.Add(new { name = l.Name, color = (int)l.Color.ColorIndex, off = l.IsOff, frozen = l.IsFrozen, locked = l.IsLocked });
                                }
                            }
                            result["layers"] = layers;
                            result["count"] = layers.Count;
                        }
                        else if (action == "transform")
                        {
                            int n = 0;
                            var moved = new List<string>();
                            var missing = new List<string>();
                            Matrix3d mat = a.Op switch
                            {
                                "move" => Matrix3d.Displacement(new Vector3d(a.Dx, a.Dy, 0)),
                                "rotate" => Matrix3d.Rotation(a.Angle * Math.PI / 180.0, Vector3d.ZAxis, new Point3d(a.Cx, a.Cy, 0)),
                                "scale" => Matrix3d.Scaling(a.Factor, new Point3d(a.Cx, a.Cy, 0)),
                                "mirror" => Matrix3d.Mirroring(new Line3d(new Point3d(a.X1, a.Y1, 0), new Point3d(a.X2, a.Y2, 0))),
                                _ => Matrix3d.Identity
                            };
                            if (a.Op != "copy" && mat == Matrix3d.Identity)
                            {
                                tr.Abort();
                                reply(new { @event = "aiActionResult", action, ok = false, message = $"未知 op: {a.Op}（支持 move/rotate/scale/copy/mirror）", time = DateTime.Now.ToString("HH:mm:ss.fff") });
                                return;
                            }
                            var ms = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                            foreach (string h in a.Handles)
                            {
                                if (!TryGetObjectId(db, h, out ObjectId id)) { missing.Add(h); continue; }
                                if (tr.GetObject(id, OpenMode.ForWrite) is not Entity ent) { missing.Add(h); continue; }
                                if (a.Op == "copy")
                                {
                                    var clone = (Entity)ent.Clone();
                                    clone.TransformBy(Matrix3d.Displacement(new Vector3d(a.Dx, a.Dy, 0)));
                                    ms.AppendEntity(clone);
                                    tr.AddNewlyCreatedDBObject(clone, true);
                                    moved.Add(clone.Handle.ToString());
                                }
                                else
                                {
                                    ent.TransformBy(mat);
                                    moved.Add(h);
                                }
                                n++;
                            }
                            result["count"] = n;
                            result["handles"] = moved;
                            result["missing"] = missing;
                        }
                        else if (action == "queryWindow")
                        {
                            double minX = Math.Min(a.X1, a.X2), maxX = Math.Max(a.X1, a.X2);
                            double minY = Math.Min(a.Y1, a.Y2), maxY = Math.Max(a.Y1, a.Y2);
                            var ents = new List<object>();
                            int scanned = 0;
                            const int cap = 50000;
                            bool truncated = false;
                            var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
                            foreach (ObjectId id in ms)
                            {
                                if (truncated) { scanned++; continue; }
                                if (tr.GetObject(id, OpenMode.ForRead) is not Entity ent) continue;
                                scanned++;
                                Extents3d ext;
                                try { ext = ent.GeometricExtents; } catch { continue; }
                                bool overlap = !(ext.MinPoint.X > maxX || ext.MaxPoint.X < minX || ext.MinPoint.Y > maxY || ext.MaxPoint.Y < minY);
                                bool inside = ext.MinPoint.X >= minX && ext.MaxPoint.X <= maxX && ext.MinPoint.Y >= minY && ext.MaxPoint.Y <= maxY;
                                bool hit = a.Mode == "crossing" ? overlap : inside;
                                if (!hit) continue;
                                if (ents.Count >= cap) { truncated = true; continue; }
                                var ltr = tr.GetObject(ent.LayerId, OpenMode.ForRead) as LayerTableRecord;
                                ents.Add(SerializeEntity(ent, ltr?.IsLocked ?? false) ?? new { handle = "?", type = ent.GetType().Name });
                            }
                            result["entities"] = ents;
                            result["count"] = ents.Count;
                            result["scanned"] = scanned;
                            result["truncated"] = truncated;
                        }
                        else if (action == "stats")
                        {
                            var counts = new Dictionary<string, int>();
                            int total = 0;
                            var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
                            foreach (ObjectId id in ms)
                            {
                                string cn = id.ObjectClass?.Name ?? "?";
                                counts[cn] = counts.GetValueOrDefault(cn) + 1;
                                total++;
                            }
                            int layerCount = 0;
                            var lt2 = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                            foreach (ObjectId _ in lt2) layerCount++;
                            int blockCount = 0;
                            var bt2 = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                            foreach (ObjectId bid in bt2)
                            {
                                if (tr.GetObject(bid, OpenMode.ForRead) is BlockTableRecord r && !r.IsLayout && !r.Name.StartsWith("*")) blockCount++;
                            }
                            result["entityCount"] = total;
                            result["typeCounts"] = counts;
                            result["layerCount"] = layerCount;
                            result["blockCount"] = blockCount;
                            result["filename"] = db.Filename;
                        }
                        else if (action == "getView") // 绘制前一刻实时拿视口（VIEWCTR 本体直读，防过期快照）
                        {
                            if (TryReadLiveView(out double vcx, out double vcy, out double vw, out double vh, out double tw))
                            {
                                result["center"] = new { x = vcx, y = vcy };
                                result["width"] = vw;
                                result["height"] = vh;
                                result["twist"] = tw;
                            }
                            else
                            {
                                tr.Abort();
                                reply(new { @event = "aiActionResult", action, ok = false, message = "无法读取当前视口", time = DateTime.Now.ToString("HH:mm:ss.fff") });
                                return;
                            }
                        }
                        else if (action == "getSelection") // 直读当前选区，0 LISP，0 延迟
                        {
                            var ed = doc.Editor;
                            var psr = ed.SelectImplied();
                            var handles = new List<string>();
                            var typeCounts = new Dictionary<string, int>();
                            var layerCounts = new Dictionary<string, int>();
                            var ents = new List<object>();
                            const int cap = 50000;

                            if (psr.Status == PromptStatus.OK && psr.Value != null)
                            {
                                foreach (SelectedObject so in psr.Value)
                                {
                                    if (so == null) continue;
                                    if (tr.GetObject(so.ObjectId, OpenMode.ForRead) is not Entity ent) continue;
                                    string h = ent.Handle.ToString();
                                    handles.Add(h);
                                    string tn = ent.GetType().Name.Replace("AcDb", "");
                                    typeCounts[tn] = typeCounts.GetValueOrDefault(tn) + 1;
                                    layerCounts[ent.Layer] = layerCounts.GetValueOrDefault(ent.Layer) + 1;

                                    if (ents.Count < cap)
                                    {
                                        var ltr = tr.GetObject(ent.LayerId, OpenMode.ForRead) as LayerTableRecord;
                                        ents.Add(SerializeEntity(ent, ltr?.IsLocked ?? false) ?? new { handle = h, type = tn, layer = ent.Layer });
                                    }
                                }
                            }
                            result["count"] = handles.Count;
                            result["handles"] = handles;
                            result["typeCounts"] = typeCounts;
                            result["layerCounts"] = layerCounts;
                            result["entities"] = ents;
                            result["truncated"] = handles.Count > cap;
                        }
                        else if (action == "hide") // 内存直改 Visibility 隐藏，支持图层过滤与选区限定，绝不调 UNISOLATE
                        {
                            var targetIds = new List<ObjectId>();
                            if (a.SelectionOnly || a.Handles.Count == 0)
                            {
                                var psr = doc.Editor.SelectImplied();
                                if (psr.Status == PromptStatus.OK && psr.Value != null)
                                {
                                    foreach (SelectedObject so in psr.Value)
                                    {
                                        if (so != null) targetIds.Add(so.ObjectId);
                                    }
                                }
                            }
                            else
                            {
                                foreach (string h in a.Handles)
                                {
                                    if (TryGetObjectId(db, h, out ObjectId id)) targetIds.Add(id);
                                }
                            }

                            var hidden = new List<string>();
                            foreach (ObjectId id in targetIds)
                            {
                                if (tr.GetObject(id, OpenMode.ForWrite) is Entity ent)
                                {
                                    if (!string.IsNullOrEmpty(a.Layer) && !string.Equals(ent.Layer, a.Layer, StringComparison.OrdinalIgnoreCase))
                                    {
                                        continue;
                                    }
                                    ent.Visible = false;
                                    ent.RecordGraphicsModified(true);
                                    string h = ent.Handle.ToString();
                                    hidden.Add(h);
                                    lock (_nativeHiddenHandles) { _nativeHiddenHandles.Add(h); }
                                }
                            }
                            result["count"] = hidden.Count;
                            result["handles"] = hidden;
                            result["layerFilter"] = a.Layer;
                            result["message"] = $"成功隐藏 {hidden.Count} 个图元（内存直改，未执行 UNISOLATE）";
                        }
                        else if (action == "unhide") // 恢复隐藏图元
                        {
                            var toRestore = new List<string>();
                            if (a.Handles.Count > 0)
                            {
                                toRestore.AddRange(a.Handles);
                            }
                            else
                            {
                                lock (_nativeHiddenHandles)
                                {
                                    toRestore.AddRange(_nativeHiddenHandles);
                                    _nativeHiddenHandles.Clear();
                                }
                            }

                            int restored = 0;
                            foreach (string h in toRestore)
                            {
                                if (TryGetObjectId(db, h, out ObjectId id))
                                {
                                    if (tr.GetObject(id, OpenMode.ForWrite) is Entity ent)
                                    {
                                        ent.Visible = true;
                                        ent.RecordGraphicsModified(true);
                                        restored++;
                                    }
                                }
                            }
                            if (a.Handles.Count == 0)
                            {
                                doc.SendStringToExecute("_.UNISOLATEOBJECTS\n", true, false, false);
                            }
                            result["count"] = restored;
                            result["message"] = $"已恢复 {restored} 个图元的显示";
                        }
                        else if (action == "explode") // 递归安全打散块，提取业务特征
                        {
                            var targetIds = new List<ObjectId>();
                            if (a.Handles.Count > 0)
                            {
                                foreach (string h in a.Handles)
                                {
                                    if (TryGetObjectId(db, h, out ObjectId id)) targetIds.Add(id);
                                }
                            }
                            else
                            {
                                var psr = doc.Editor.SelectImplied();
                                if (psr.Status == PromptStatus.OK && psr.Value != null)
                                {
                                    foreach (SelectedObject so in psr.Value)
                                    {
                                        if (so != null) targetIds.Add(so.ObjectId);
                                    }
                                }
                            }

                            var ms = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                            var leaves = new List<Entity>();
                            var originalHandles = new List<string>();

                            foreach (ObjectId id in targetIds)
                            {
                                if (tr.GetObject(id, OpenMode.ForWrite) is Entity ent)
                                {
                                    originalHandles.Add(ent.Handle.ToString());
                                    RecursiveExplode(ent, a.Depth, true, leaves);
                                    if (a.Commit && ent is BlockReference)
                                    {
                                        ent.Erase();
                                    }
                                }
                            }

                            var leafHandles = new List<string>();
                            var typeCounts = new Dictionary<string, int>();
                            var socket86 = new List<object>();
                            var equipRects = new List<object>();

                            foreach (Entity leaf in leaves)
                            {
                                string tn = leaf.GetType().Name.Replace("AcDb", "");
                                typeCounts[tn] = typeCounts.GetValueOrDefault(tn) + 1;

                                if (a.Commit)
                                {
                                    ms.AppendEntity(leaf);
                                    tr.AddNewlyCreatedDBObject(leaf, true);
                                    leafHandles.Add(leaf.Handle.ToString());
                                }

                                try
                                {
                                    Extents3d ext = leaf.GeometricExtents;
                                    double w = Math.Abs(ext.MaxPoint.X - ext.MinPoint.X);
                                    double h = Math.Abs(ext.MaxPoint.Y - ext.MinPoint.Y);
                                    if (leaf is Polyline && Math.Abs(w - 86) < 1.5 && Math.Abs(h - 86) < 1.5)
                                    {
                                        socket86.Add(new { x = ext.MinPoint.X, y = ext.MinPoint.Y, layer = leaf.Layer });
                                    }
                                    else if (w >= 300 && w <= 600 && h >= 300 && h <= 600)
                                    {
                                        equipRects.Add(new { w, h, x = ext.MinPoint.X, y = ext.MinPoint.Y, layer = leaf.Layer });
                                    }
                                }
                                catch { }
                            }

                            result["originalCount"] = originalHandles.Count;
                            result["leafCount"] = leaves.Count;
                            result["typeCounts"] = typeCounts;
                            result["leafHandles"] = leafHandles;
                            result["socket86Count"] = socket86.Count;
                            result["socket86"] = socket86;
                            result["equipRectsCount"] = equipRects.Count;
                            result["equipRects"] = equipRects;
                        }
                        else if (action == "findText")
                        {
                            var matches = new List<object>();
                            var targetIds = new List<ObjectId>();
                            if (a.Handles.Count > 0)
                            {
                                foreach (string h in a.Handles)
                                    if (TryGetObjectId(db, h, out ObjectId id)) targetIds.Add(id);
                            }
                            else
                            {
                                var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
                                foreach (ObjectId id in ms) targetIds.Add(id);
                            }

                            string filter = a.Text?.Trim() ?? "";
                            foreach (ObjectId id in targetIds)
                            {
                                if (matches.Count >= 20000) break;
                                if (tr.GetObject(id, OpenMode.ForRead) is not Entity ent) continue;
                                if (!string.IsNullOrEmpty(a.Layer) && !string.Equals(ent.Layer, a.Layer, StringComparison.OrdinalIgnoreCase)) continue;

                                if (ent is DBText dt)
                                {
                                    string str = dt.TextString ?? "";
                                    if (string.IsNullOrEmpty(filter) || str.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        matches.Add(new
                                        {
                                            handle = dt.Handle.ToString(),
                                            type = "Text",
                                            text = str,
                                            layer = dt.Layer,
                                            position = new { x = Math.Round(dt.Position.X, 2), y = Math.Round(dt.Position.Y, 2), z = Math.Round(dt.Position.Z, 2) },
                                            height = Math.Round(dt.Height, 2),
                                            rotation = Math.Round(dt.Rotation * 180.0 / Math.PI, 1)
                                        });
                                    }
                                }
                                else if (ent is MText mt)
                                {
                                    string raw = mt.Contents ?? "";
                                    string plain = StripMTextFormatting(raw);
                                    if (string.IsNullOrEmpty(filter) || plain.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 || raw.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        matches.Add(new
                                        {
                                            handle = mt.Handle.ToString(),
                                            type = "MText",
                                            text = plain,
                                            rawText = raw,
                                            layer = mt.Layer,
                                            position = new { x = Math.Round(mt.Location.X, 2), y = Math.Round(mt.Location.Y, 2), z = Math.Round(mt.Location.Z, 2) },
                                            height = Math.Round(mt.TextHeight, 2),
                                            rotation = Math.Round(mt.Rotation * 180.0 / Math.PI, 1)
                                        });
                                    }
                                }
                            }
                            result["count"] = matches.Count;
                            result["matches"] = matches;
                            result["query"] = filter;
                        }
                        else if (action == "readDimensions")
                        {
                            var dims = new List<object>();
                            var targetIds = new List<ObjectId>();
                            if (a.Handles.Count > 0)
                            {
                                foreach (string h in a.Handles)
                                    if (TryGetObjectId(db, h, out ObjectId id)) targetIds.Add(id);
                            }
                            else
                            {
                                var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
                                foreach (ObjectId id in ms) targetIds.Add(id);
                            }

                            foreach (ObjectId id in targetIds)
                            {
                                if (dims.Count >= 20000) break;
                                if (tr.GetObject(id, OpenMode.ForRead) is not Entity ent) continue;
                                if (!string.IsNullOrEmpty(a.Layer) && !string.Equals(ent.Layer, a.Layer, StringComparison.OrdinalIgnoreCase)) continue;

                                if (ent is RotatedDimension rd)
                                {
                                    dims.Add(new
                                    {
                                        handle = rd.Handle.ToString(),
                                        type = "RotatedDimension",
                                        layer = rd.Layer,
                                        measurement = Math.Round(rd.Measurement, 2),
                                        textOverride = string.IsNullOrEmpty(rd.DimensionText) ? null : rd.DimensionText,
                                        dimStyle = rd.DimensionStyleName,
                                        rotation = Math.Round(rd.Rotation * 180.0 / Math.PI, 1),
                                        pt1 = new { x = Math.Round(rd.XLine1Point.X, 2), y = Math.Round(rd.XLine1Point.Y, 2) },
                                        pt2 = new { x = Math.Round(rd.XLine2Point.X, 2), y = Math.Round(rd.XLine2Point.Y, 2) },
                                        dimLinePt = new { x = Math.Round(rd.DimLinePoint.X, 2), y = Math.Round(rd.DimLinePoint.Y, 2) }
                                    });
                                }
                                else if (ent is AlignedDimension ad)
                                {
                                    dims.Add(new
                                    {
                                        handle = ad.Handle.ToString(),
                                        type = "AlignedDimension",
                                        layer = ad.Layer,
                                        measurement = Math.Round(ad.Measurement, 2),
                                        textOverride = string.IsNullOrEmpty(ad.DimensionText) ? null : ad.DimensionText,
                                        dimStyle = ad.DimensionStyleName,
                                        pt1 = new { x = Math.Round(ad.XLine1Point.X, 2), y = Math.Round(ad.XLine1Point.Y, 2) },
                                        pt2 = new { x = Math.Round(ad.XLine2Point.X, 2), y = Math.Round(ad.XLine2Point.Y, 2) },
                                        dimLinePt = new { x = Math.Round(ad.DimLinePoint.X, 2), y = Math.Round(ad.DimLinePoint.Y, 2) }
                                    });
                                }
                                else if (ent is Dimension d)
                                {
                                    dims.Add(new
                                    {
                                        handle = d.Handle.ToString(),
                                        type = d.GetType().Name.Replace("AcDb", ""),
                                        layer = d.Layer,
                                        measurement = Math.Round(d.Measurement, 2),
                                        textOverride = string.IsNullOrEmpty(d.DimensionText) ? null : d.DimensionText,
                                        dimStyle = d.DimensionStyleName,
                                        textPosition = new { x = Math.Round(d.TextPosition.X, 2), y = Math.Round(d.TextPosition.Y, 2) }
                                    });
                                }
                            }
                            result["count"] = dims.Count;
                            result["dimensions"] = dims;
                        }
                        else if (action == "getExtents")
                        {
                            var targetIds = new List<ObjectId>();
                            if (a.Handles.Count > 0)
                            {
                                foreach (string h in a.Handles)
                                    if (TryGetObjectId(db, h, out ObjectId id)) targetIds.Add(id);
                            }
                            else if (a.SelectionOnly)
                            {
                                var psr = doc.Editor.SelectImplied();
                                if (psr.Status == PromptStatus.OK && psr.Value != null)
                                {
                                    foreach (SelectedObject so in psr.Value)
                                        if (so != null) targetIds.Add(so.ObjectId);
                                }
                            }
                            else
                            {
                                var psr = doc.Editor.SelectImplied();
                                if (psr.Status == PromptStatus.OK && psr.Value != null && psr.Value.Count > 0)
                                {
                                    foreach (SelectedObject so in psr.Value)
                                        if (so != null) targetIds.Add(so.ObjectId);
                                }
                                else
                                {
                                    var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
                                    foreach (ObjectId id in ms) targetIds.Add(id);
                                }
                            }

                            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
                            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
                            int validCount = 0;

                            foreach (ObjectId id in targetIds)
                            {
                                if (tr.GetObject(id, OpenMode.ForRead) is not Entity ent) continue;
                                if (!string.IsNullOrEmpty(a.Layer) && !string.Equals(ent.Layer, a.Layer, StringComparison.OrdinalIgnoreCase)) continue;

                                try
                                {
                                    Extents3d ext = ent.GeometricExtents;
                                    minX = Math.Min(minX, ext.MinPoint.X);
                                    minY = Math.Min(minY, ext.MinPoint.Y);
                                    minZ = Math.Min(minZ, ext.MinPoint.Z);
                                    maxX = Math.Max(maxX, ext.MaxPoint.X);
                                    maxY = Math.Max(maxY, ext.MaxPoint.Y);
                                    maxZ = Math.Max(maxZ, ext.MaxPoint.Z);
                                    validCount++;
                                }
                                catch { }
                            }

                            if (validCount > 0)
                            {
                                result["hasBounds"] = true;
                                result["min"] = new { x = Math.Round(minX, 2), y = Math.Round(minY, 2), z = Math.Round(minZ, 2) };
                                result["max"] = new { x = Math.Round(maxX, 2), y = Math.Round(maxY, 2), z = Math.Round(maxZ, 2) };
                                result["width"] = Math.Round(maxX - minX, 2);
                                result["height"] = Math.Round(maxY - minY, 2);
                                result["center"] = new { x = Math.Round((minX + maxX) / 2.0, 2), y = Math.Round((minY + maxY) / 2.0, 2) };
                                result["count"] = validCount;
                            }
                            else
                            {
                                result["hasBounds"] = false;
                                result["message"] = "未找到有效几何图元或所选图元无有效外包围盒";
                            }
                        }
                        else if (action == "flatten")
                        {
                            var targetIds = new List<ObjectId>();
                            if (a.Handles.Count > 0)
                            {
                                foreach (string h in a.Handles)
                                    if (TryGetObjectId(db, h, out ObjectId id)) targetIds.Add(id);
                            }
                            else
                            {
                                var psr = doc.Editor.SelectImplied();
                                if (psr.Status == PromptStatus.OK && psr.Value != null && psr.Value.Count > 0)
                                {
                                    foreach (SelectedObject so in psr.Value)
                                        if (so != null) targetIds.Add(so.ObjectId);
                                }
                                else if (a.Mode == "all")
                                {
                                    var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
                                    foreach (ObjectId id in ms) targetIds.Add(id);
                                }
                            }

                            int flatCount = 0;
                            foreach (ObjectId id in targetIds)
                            {
                                if (tr.GetObject(id, OpenMode.ForWrite) is not Entity ent) continue;
                                bool modified = false;

                                if (ent is Line line)
                                {
                                    if (Math.Abs(line.StartPoint.Z) > 1e-4 || Math.Abs(line.EndPoint.Z) > 1e-4)
                                    {
                                        line.StartPoint = new Point3d(line.StartPoint.X, line.StartPoint.Y, 0);
                                        line.EndPoint = new Point3d(line.EndPoint.X, line.EndPoint.Y, 0);
                                        modified = true;
                                    }
                                }
                                else if (ent is Polyline pl)
                                {
                                    if (Math.Abs(pl.Elevation) > 1e-4 || pl.Normal != Vector3d.ZAxis)
                                    {
                                        pl.Elevation = 0;
                                        pl.Normal = Vector3d.ZAxis;
                                        modified = true;
                                    }
                                }
                                else if (ent is Circle circle)
                                {
                                    if (Math.Abs(circle.Center.Z) > 1e-4 || circle.Normal != Vector3d.ZAxis)
                                    {
                                        circle.Center = new Point3d(circle.Center.X, circle.Center.Y, 0);
                                        circle.Normal = Vector3d.ZAxis;
                                        modified = true;
                                    }
                                }
                                else if (ent is Arc arc)
                                {
                                    if (Math.Abs(arc.Center.Z) > 1e-4 || arc.Normal != Vector3d.ZAxis)
                                    {
                                        arc.Center = new Point3d(arc.Center.X, arc.Center.Y, 0);
                                        arc.Normal = Vector3d.ZAxis;
                                        modified = true;
                                    }
                                }
                                else if (ent is DBText dt)
                                {
                                    if (Math.Abs(dt.Position.Z) > 1e-4)
                                    {
                                        dt.Position = new Point3d(dt.Position.X, dt.Position.Y, 0);
                                        modified = true;
                                    }
                                    if (Math.Abs(dt.AlignmentPoint.Z) > 1e-4)
                                    {
                                        dt.AlignmentPoint = new Point3d(dt.AlignmentPoint.X, dt.AlignmentPoint.Y, 0);
                                        modified = true;
                                    }
                                }
                                else if (ent is MText mt)
                                {
                                    if (Math.Abs(mt.Location.Z) > 1e-4)
                                    {
                                        mt.Location = new Point3d(mt.Location.X, mt.Location.Y, 0);
                                        modified = true;
                                    }
                                }
                                else if (ent is BlockReference bref)
                                {
                                    if (Math.Abs(bref.Position.Z) > 1e-4 || bref.Normal != Vector3d.ZAxis)
                                    {
                                        bref.Position = new Point3d(bref.Position.X, bref.Position.Y, 0);
                                        bref.Normal = Vector3d.ZAxis;
                                        modified = true;
                                    }
                                }

                                if (modified)
                                {
                                    ent.RecordGraphicsModified(true);
                                    flatCount++;
                                }
                            }

                            result["flattenedCount"] = flatCount;
                            result["scannedCount"] = targetIds.Count;
                            result["message"] = $"已将 {flatCount} 个图元的 Z 轴坐标压平至 Z=0 平面";
                        }
                        else if (action == "setLayer")
                        {
                            var targetIds = new List<ObjectId>();
                            if (a.Handles.Count > 0)
                            {
                                foreach (string h in a.Handles)
                                    if (TryGetObjectId(db, h, out ObjectId id)) targetIds.Add(id);
                            }
                            else
                            {
                                var psr = doc.Editor.SelectImplied();
                                if (psr.Status == PromptStatus.OK && psr.Value != null)
                                {
                                    foreach (SelectedObject so in psr.Value)
                                        if (so != null) targetIds.Add(so.ObjectId);
                                }
                            }

                            if (!string.IsNullOrEmpty(a.Layer))
                            {
                                EnsureLayer(tr, db, a.Layer);
                                if (a.ColorIndex > 0 && a.ColorIndex <= 256)
                                {
                                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                                    if (lt.Has(a.Layer))
                                    {
                                        var ltr = (LayerTableRecord)tr.GetObject(lt[a.Layer], OpenMode.ForWrite);
                                        ltr.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, (short)a.ColorIndex);
                                    }
                                }
                            }

                            int modifiedCount = 0;
                            foreach (ObjectId id in targetIds)
                            {
                                if (tr.GetObject(id, OpenMode.ForWrite) is not Entity ent) continue;
                                if (!string.IsNullOrEmpty(a.Layer))
                                {
                                    ent.Layer = a.Layer;
                                }
                                if (a.ColorIndex > 0 && a.ColorIndex <= 256)
                                {
                                    ent.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, (short)a.ColorIndex);
                                }
                                ent.RecordGraphicsModified(true);
                                modifiedCount++;
                            }

                            result["count"] = modifiedCount;
                            result["layer"] = a.Layer;
                            result["color"] = a.ColorIndex;
                            result["message"] = $"已将 {modifiedCount} 个图元移至图层 '{a.Layer}'";
                        }
                        else if (action == "filter")
                        {
                            // 按图元类型（+可选窗口范围 / 当前选区）批量改层或删除。
                            // 2026-11-10：内置此动作，替代模型手搓 LISP ssget 过滤（实测会误删）。
                            // op=setLayer 需 layer；op=erase 走删除。scope 优先级：handles > window > 当前选区 > 全图。
                            // 类型归一：支持 AcDbHatch / Hatch / HATCH / hatch / 填充 / 直线 等写法
                            // 2026-11-10：兼容复数与复数中文（填充物/hacthes/lines），模型常这么写
                            static string NormType(string s)
                            {
                                var t = (s ?? "").Trim();
                                if (t.Length == 0) return "";
                                if (t.StartsWith("AcDb", StringComparison.OrdinalIgnoreCase)) t = t.Substring(4);
                                // 去复数 s
                                if (t.Length > 3 && (t.EndsWith("s") || t.EndsWith("S")) &&
                                    !t.Equals("Text", StringComparison.OrdinalIgnoreCase) &&
                                    !t.Equals("MText", StringComparison.OrdinalIgnoreCase))
                                {
                                    string sing = t.Substring(0, t.Length - 1);
                                    if (sing.Equals("Hatche", StringComparison.OrdinalIgnoreCase)) t = "Hatch";
                                    else if (sing.Equals("Line", StringComparison.OrdinalIgnoreCase) ||
                                             sing.Equals("Arc", StringComparison.OrdinalIgnoreCase) ||
                                             sing.Equals("Circle", StringComparison.OrdinalIgnoreCase) ||
                                             sing.Equals("Polyline", StringComparison.OrdinalIgnoreCase))
                                        t = sing;
                                }
                                switch (t.ToLowerInvariant())
                                {
                                    case "填充": case "填充图案": case "hatch": return "hatch";
                                    case "直线": case "线": case "line": return "line";
                                    case "多段线": case "多义线": case "polyline": case "lwpolyline": return "polyline";
                                    case "圆弧": case "弧": case "arc": return "arc";
                                    case "圆": case "circle": return "circle";
                                    case "文字": case "单行文字": case "text": return "text";
                                    case "多行文字": case "mtext": return "mtext";
                                    case "块": case "块参照": case "insert": case "blockreference": return "insert";
                                    case "椭圆": case "ellipse": return "ellipse";
                                    case "样条": case "样条曲线": case "spline": return "spline";
                                    case "尺寸": case "标注": case "dimension": case "rotateddimension":
                                    case "aligneddimension": return "dimension";
                                    case "射线": case "ray": return "ray";
                                    case "构造线": case "xline": return "xline";
                                    case "点": case "point": return "point";
                                    default: return t.ToLowerInvariant();
                                }
                            }
                            var wantedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var t in a.Types)
                            {
                                var nt = NormType(t);
                                if (nt.Length > 0) wantedTypes.Add(nt);
                            }

                            // 未指定类型 = 全部图元（用于「把选中的图元换到某图层」这类不带类型的请求）。
                            // 有类型时才是「只动这一类」，避免误伤。
                            bool typeRestricted = wantedTypes.Count > 0;

                            var typeSet = wantedTypes;

                            string fop = (a.FilterOp ?? "").Trim();
                            if (fop.Length == 0) fop = "setLayer";
                            bool isErase = string.Equals(fop, "erase", StringComparison.OrdinalIgnoreCase);
                            if (!isErase && !string.Equals(fop, "setLayer", StringComparison.OrdinalIgnoreCase) && !string.Equals(fop, "count", StringComparison.OrdinalIgnoreCase))
                            {
                                result["error"] = $"不支持的 op={fop}（只允许 erase / setLayer / count）";
                                result["count"] = 0;
                            }
                            else
                            {
                                // 收集候选
                                var candidates = new List<ObjectId>();
                                string scopeUsed = "none";
                                void Collect(ObjectId id, string typeName)
                                {
                                    var nt = NormType(typeName);
                                    if (typeRestricted && !wantedTypes.Contains(nt)) return;
                                    candidates.Add(id);
                                }

                                if (a.Handles.Count > 0)
                                {
                                    foreach (string h in a.Handles)
                                    {
                                        if (!TryGetObjectId(db, h, out ObjectId id)) continue;
                                        string tn = "";
                                        if (tr.GetObject(id, OpenMode.ForRead) is Entity he) tn = he.GetType().Name;
                                        Collect(id, tn);
                                    }
                                }
                                else if (a.HasWindow)
                                {
                                    scopeUsed = "window";
                                    double x1 = Math.Min(a.X1, a.X2), x2 = Math.Max(a.X1, a.X2);
                                    double y1 = Math.Min(a.Y1, a.Y2), y2 = Math.Max(a.Y1, a.Y2);
                                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                                    foreach (ObjectId id in ms)
                                    {
                                        if (tr.GetObject(id, OpenMode.ForRead) is not Entity ent) continue;
                                        try
                                        {
                                            var ext = ent.GeometricExtents;
                                            // 完全落在窗口内才算命中（与框选内窗语义一致）
                                            if (ext.MinPoint.X >= x1 && ext.MaxPoint.X <= x2 &&
                                                ext.MinPoint.Y >= y1 && ext.MaxPoint.Y <= y2)
                                            {
                                                Collect(id, ent.GetType().Name);
                                            }
                                        }
                                        catch { }  // 无有效范围（如空块）跳过
                                    }
                                }
                                else
                                {
                                    // 无 handles、无窗口：优先当前选区；选区为空则全图扫描。
                                    // 2026-11-10 修复：原实现只读隐含选区，无选区时「删除所有填充」直接返回 0，
                                    // 这才是用户口述「删掉所有填充」失灵的真正原因。types 必填保证了全图扫描的边界。
                                    var psr = doc.Editor.SelectImplied();
                                    bool hasImplied = psr.Status == PromptStatus.OK && psr.Value != null && psr.Value.Count > 0;
                                    if (hasImplied)
                                    {
                                        scopeUsed = "selection";
                                        foreach (SelectedObject so in psr.Value)
                                        {
                                            if (so == null) continue;
                                            string tn = "";
                                            if (tr.GetObject(so.ObjectId, OpenMode.ForRead) is Entity se) tn = se.GetType().Name;
                                            Collect(so.ObjectId, tn);
                                        }
                                    }

                                    if (!hasImplied && typeRestricted)
                                    {
                                        scopeUsed = "all";
                                        var bt2 = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                                        var ms2 = (BlockTableRecord)tr.GetObject(bt2[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                                        foreach (ObjectId id in ms2)
                                        {
                                            if (tr.GetObject(id, OpenMode.ForRead) is not Entity ent) continue;
                                            Collect(id, ent.GetType().Name);
                                        }
                                    }
                                }

                                int touched = 0;
                                var touchedHandles = new List<string>();
                                bool isCount = string.Equals(fop, "count", StringComparison.OrdinalIgnoreCase);
                                if (isCount)
                                {
                                    // 只统计不动手（2026-11-10）：原先 count 落进 setLayer 分支，
                                    // 因缺 layer 直接报错返回 0，与「数一下有多少填充」的期望不符。
                                    foreach (ObjectId id in candidates)
                                    {
                                        try
                                        {
                                            if (tr.GetObject(id, OpenMode.ForRead) is not Entity ent) continue;
                                            touchedHandles.Add(ent.Handle.ToString());
                                            touched++;
                                        }
                                        catch { }
                                    }
                                }
                                else if (isErase)
                                {
                                    foreach (ObjectId id in candidates)
                                    {
                                        try
                                        {
                                            if (tr.GetObject(id, OpenMode.ForRead) is not Entity ent) continue;
                                            touchedHandles.Add(ent.Handle.ToString());
                                            ent.UpgradeOpen();
                                            ent.Erase();
                                            touched++;
                                        }
                                        catch { }
                                    }
                                }
                                else
                                {
                                    if (string.IsNullOrEmpty(a.Layer))
                                    {
                                        result["error"] = "op=setLayer 需要 layer 参数";
                                    }
                                    else
                                    {
                                        EnsureLayer(tr, db, a.Layer);
                                        foreach (ObjectId id in candidates)
                                        {
                                            try
                                            {
                                                if (tr.GetObject(id, OpenMode.ForWrite) is not Entity ent) continue;
                                                ent.Layer = a.Layer;
                                                ent.RecordGraphicsModified(true);
                                                touchedHandles.Add(ent.Handle.ToString());
                                                touched++;
                                            }
                                            catch { }
                                        }
                                    }
                                }

                                result["count"] = touched;
                                result["op"] = fop;
                                result["scope"] = scopeUsed;
                                if (scopeUsed == "none")
                                {
                                    result["message"] = "未指定类型且当前无选区，未改动任何图元；请先框选或指明 types";
                                }
                                result["types"] = a.Types;
                                result["layer"] = a.Layer;
                                result["handles"] = touchedHandles;
                                result["message"] = isCount
                                    ? $"按类型匹配到 {touched} 个图元"
                                    : isErase
                                    ? $"已按类型删除 {touched} 个图元"
                                    : $"已将 {touched} 个图元设为图层 '{a.Layer}'";
                            }
                        }
                        else if (action == "layerState")
                        {
                            // 图层状态批量管理（2026-11-10，柜体 CAD 需求）：
                            // op = on/off/freeze/thaw/lock/unlock/isolate/highlight/unhighlight/setCurrent
                            // 支持多图层批量 + allLayers；selected（按选区收集所在图层）也可。
                            string op = (a.Op ?? "").Trim().ToLowerInvariant();
                            var targetNames = new List<string>();

                            if (a.LayerNames.Count > 0)
                            {
                                targetNames.AddRange(a.LayerNames);
                            }
                            else if (!string.IsNullOrWhiteSpace(a.Layer))
                            {
                                targetNames.Add(a.Layer.Trim());
                            }
                            else if (a.SelectionOnly || a.Handles.Count > 0)
                            {
                                // 由选区/句柄反推图层
                                var ids = new List<ObjectId>();
                                if (a.Handles.Count > 0)
                                {
                                    foreach (string h in a.Handles)
                                        if (TryGetObjectId(db, h, out ObjectId hid)) ids.Add(hid);
                                }
                                else
                                {
                                    var psr = doc.Editor.SelectImplied();
                                    if (psr.Status == PromptStatus.OK && psr.Value != null)
                                        foreach (SelectedObject so in psr.Value)
                                            if (so != null) ids.Add(so.ObjectId);
                                }
                                foreach (ObjectId oid in ids)
                                {
                                    if (tr.GetObject(oid, OpenMode.ForRead) is Entity ent &&
                                        !targetNames.Contains(ent.Layer, StringComparer.OrdinalIgnoreCase))
                                        targetNames.Add(ent.Layer);
                                }
                            }
                            else if (a.AllLayers)
                            {
                                var ltAll = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                                foreach (ObjectId lid in ltAll)
                                    if (tr.GetObject(lid, OpenMode.ForRead) is LayerTableRecord l)
                                        targetNames.Add(l.Name);
                            }
                            else if (!string.IsNullOrWhiteSpace(a.NameContains))
                            {
                                // 按图层名关键词模糊匹配（2026-11-10）：用户说「所有墙体图层」时
                                // 模型给的是中文词，而图里图层可能叫 WALL / 墙体-承重 / QiangTi。
                                var ltK = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                                string kw = a.NameContains.Trim();
                                foreach (ObjectId lid in ltK)
                                {
                                    if (tr.GetObject(lid, OpenMode.ForRead) is not LayerTableRecord l) continue;
                                    if (l.Name.Contains(kw, StringComparison.OrdinalIgnoreCase))
                                        targetNames.Add(l.Name);
                                }
                            }

                            if (targetNames.Count == 0)
                            {
                                result["error"] = "未指定目标图层：请给 layer / layers 数组，或用选区、allLayers=true";
                                result["count"] = 0;
                            }
                            else if (op == "isolate")
                            {
                                // 隔离：关闭除目标外的所有图层（可撤销）
                                var lt3 = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                                var keep = new HashSet<string>(targetNames, StringComparer.OrdinalIgnoreCase);
                                int offCount = 0;
                                foreach (ObjectId lid in lt3)
                                {
                                    if (tr.GetObject(lid, OpenMode.ForRead) is not LayerTableRecord l) continue;
                                    bool keepThis = keep.Contains(l.Name);
                                    var lw = (LayerTableRecord)tr.GetObject(lid, OpenMode.ForWrite);
                                    if (!keepThis && !lw.IsOff) { lw.IsOff = true; offCount++; }
                                    else if (keepThis && lw.IsOff) lw.IsOff = false;
                                }
                                result["count"] = targetNames.Count;
                                result["hiddenLayers"] = offCount;
                                result["layers"] = targetNames;
                                result["message"] = $"已隔离 {targetNames.Count} 个图层（其余 {offCount} 个图层已关闭）";
                            }
                            else if (op == "highlight" || op == "unhighlight")
                            {
                                // 高亮：把目标图层上的实体设为醒目颜色（可撤销），或在视口内临时高亮
                                int changed = 0;
                                var hs = new HashSet<string>(targetNames, StringComparer.OrdinalIgnoreCase);
                                var btH = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                                var msH = (BlockTableRecord)tr.GetObject(btH[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                                short hlColor = (short)(a.ColorIndex > 0 && a.ColorIndex <= 256 ? a.ColorIndex : 1);
                                foreach (ObjectId oid in msH)
                                {
                                    if (tr.GetObject(oid, OpenMode.ForRead) is not Entity ent) continue;
                                    if (!hs.Contains(ent.Layer)) continue;
                                    var ew = (Entity)tr.GetObject(oid, OpenMode.ForWrite);
                                    ew.Color = op == "highlight"
                                        ? Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, hlColor)
                                        : Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByLayer, 256);
                                    ew.RecordGraphicsModified(true);
                                    changed++;
                                }
                                result["count"] = changed;
                                result["layers"] = targetNames;
                                result["message"] = op == "highlight"
                                    ? $"已高亮 {targetNames.Count} 个图层上的 {changed} 个图元"
                                    : $"已取消高亮（颜色恢复随层）{changed} 个图元";
                            }
                            else
                            {
                                var lt2 = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                                var done = new List<string>();
                                var skipped = new List<string>();
                                foreach (string name in targetNames)
                                {
                                    if (!lt2.Has(name)) { skipped.Add(name + "(不存在)"); continue; }
                                    var lw = (LayerTableRecord)tr.GetObject(lt2[name], OpenMode.ForWrite);
                                    switch (op)
                                    {
                                        case "on": lw.IsOff = false; break;
                                        case "off": lw.IsOff = true; break;
                                        case "freeze": lw.IsFrozen = true; break;
                                        case "thaw": lw.IsFrozen = false; break;
                                        case "lock": lw.IsLocked = true; break;
                                        case "unlock": lw.IsLocked = false; break;
                                        case "setcurrent":
                                            db.Clayer = lt2[name];
                                            break;
                                        default:
                                            skipped.Add(name + "(未知op " + op + ")");
                                            continue;
                                    }
                                    done.Add(name);
                                }
                                result["count"] = done.Count;
                                result["layers"] = done;
                                result["skipped"] = skipped;
                                result["op"] = op;
                                result["message"] = $"已对 {done.Count} 个图层执行 {op}" +
                                    (skipped.Count > 0 ? $"；跳过 {skipped.Count} 个" : "");
                            }
                        }
                        else if (action == "measure")
                        {
                            // 面积/周长测算 + 图上标注（2026-11-10，柜体 CAD 需求）。
                            // 柜体约定：矩形柜体按外框算净尺寸；此处按图元自身几何计算，不臆测工艺。
                            var targets = new List<ObjectId>();
                            if (a.Handles.Count > 0)
                            {
                                foreach (string h in a.Handles)
                                    if (TryGetObjectId(db, h, out ObjectId mid)) targets.Add(mid);
                            }
                            else
                            {
                                var psr = doc.Editor.SelectImplied();
                                if (psr.Status == PromptStatus.OK && psr.Value != null)
                                    foreach (SelectedObject so in psr.Value)
                                        if (so != null) targets.Add(so.ObjectId);
                            }

                            if (targets.Count == 0)
                            {
                                result["error"] = "measure 需要先框选图元（或提供 handles）";
                                result["count"] = 0;
                            }
                            else
                            {
                                string annLayer = string.IsNullOrWhiteSpace(a.Layer) ? "AI标注" : a.Layer;
                                EnsureLayer(tr, db, annLayer);
                                double textH = a.TextHeight > 0 ? a.TextHeight : 120;
                                var items = new List<object>();
                                var created = new List<string>();
                                var msM = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                                foreach (ObjectId oid in targets)
                                {
                                    if (tr.GetObject(oid, OpenMode.ForRead) is not Entity ent) continue;
                                    double area = 0, perim = 0;
                                    string kind = ent.GetType().Name;
                                    try
                                    {
                                        if (ent is Autodesk.AutoCAD.DatabaseServices.Curve cv)
                                        {
                                            perim = cv.GetDistanceAtParameter(cv.EndParam) - cv.GetDistanceAtParameter(cv.StartParam);
                                            if (cv.Closed) { try { area = cv.Area; } catch { area = 0; } }
                                        }
                                        else if (ent is Autodesk.AutoCAD.DatabaseServices.Region reg)
                                        {
                                            area = reg.Area;
                                            perim = reg.Perimeter;
                                        }
                                        else if (ent is Autodesk.AutoCAD.DatabaseServices.Hatch hx)
                                        {
                                            try { area = hx.Area; } catch { }
                                        }
                                    }
                                    catch { }

                                    var ext = ent.GeometricExtents;
                                    string label = area > 0
                                        ? $"面积 {area / 1e6:0.###}㎡ / 周长 {perim / 1000:0.###}m"
                                        : $"长度 {perim / 1000:0.###}m";

                                    var mt = new MText
                                    {
                                        Contents = label,
                                        TextHeight = textH,
                                        Layer = annLayer,
                                        Attachment = AttachmentPoint.TopLeft,
                                        Location = new Point3d(ext.MaxPoint.X + a.Offset, ext.MaxPoint.Y + a.Offset, 0)
                                    };
                                    msM.AppendEntity(mt);
                                    tr.AddNewlyCreatedDBObject(mt, true);
                                    created.Add(mt.Handle.ToString());

                                    items.Add(new
                                    {
                                        handle = ent.Handle.ToString(),
                                        type = kind,
                                        layer = ent.Layer,
                                        area = Math.Round(area, 3),
                                        areaM2 = Math.Round(area / 1e6, 4),
                                        perimeter = Math.Round(perim, 3),
                                        perimeterM = Math.Round(perim / 1000, 4),
                                        label
                                    });
                                }
                                result["count"] = items.Count;
                                result["items"] = items;
                                result["annotationLayer"] = annLayer;
                                result["annotationHandles"] = created;
                                result["message"] = $"已测量 {items.Count} 个图元并写入 {created.Count} 条标注（图层 {annLayer}）";
                            }
                        }
                        else if (action == "normalizeLayer")
                        {
                            // 图层规范化（2026-11-10，柜体 CAD 需求）：
                            // 按用户给定映射把多个别名图层合并到规范层名，并清理空层。
                            // 不预设"哪些算规范"——映射必须由调用方给出，避免臆造工艺口径。
                            var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var kv in a.LayerMapping)
                                if (!string.IsNullOrWhiteSpace(kv.Value)) mapping[kv.Key] = kv.Value;
                            if (mapping.Count == 0 && !string.IsNullOrWhiteSpace(a.Layer) && a.Types.Count >= 2)
                            {
                                // 兼容简写：{"op":"merge","layer":"规范层","types":["别名1","别名2"]}
                                foreach (var alias in a.Types) mapping[alias] = a.Layer;
                            }

                            if (mapping.Count == 0)
                            {
                                result["error"] = "normalizeLayer 需要 mapping（{别名层: 规范层}），不预设规范口径";
                                result["count"] = 0;
                            }
                            else
                            {
                                foreach (var dst in mapping.Values.Distinct(StringComparer.OrdinalIgnoreCase))
                                    EnsureLayer(tr, db, dst);

                                var btN = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                                var msN = (BlockTableRecord)tr.GetObject(btN[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                                int moved = 0;
                                var perLayer = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                                foreach (ObjectId oid in msN)
                                {
                                    if (tr.GetObject(oid, OpenMode.ForRead) is not Entity ent) continue;
                                    if (!mapping.TryGetValue(ent.Layer, out string? norm) || norm == null) continue;
                                    if (string.Equals(ent.Layer, norm, StringComparison.OrdinalIgnoreCase)) continue;
                                    var ew = (Entity)tr.GetObject(oid, OpenMode.ForWrite);
                                    ew.Layer = norm;
                                    ew.RecordGraphicsModified(true);
                                    moved++;
                                    perLayer[ent.Layer] = perLayer.TryGetValue(ent.Layer, out int c) ? c + 1 : 1;
                                }
                                result["count"] = moved;
                                result["mapping"] = mapping;
                                result["movedByLayer"] = perLayer;
                                result["message"] = $"已按映射把 {moved} 个图元归并到规范图层";
                            }
                        }
                        else if (action == "crossViewQuery")
                        {
                            // 跨视图尺寸联查（2026-11-10，柜体核心需求）：
                            // 按 X/Y 坐标带把多个视图（平面/立面/侧面）关联起来，返回同一部位在各视图的图元与尺寸。
                            // 做法：给定一条坐标带（axis + from/to）或一个窗口，扫描全图返回命中的图元与标注尺寸。
                            string axis = string.IsNullOrWhiteSpace(a.Op) ? "x" : a.Op.ToLowerInvariant();
                            bool useX = axis != "y";
                            double lo = useX ? Math.Min(a.X1, a.X2) : Math.Min(a.Y1, a.Y2);
                            double hi2 = useX ? Math.Max(a.X1, a.X2) : Math.Max(a.Y1, a.Y2);
                            double band = a.Factor > 0 && a.Factor != 1 ? a.Factor : 50;  // 容差带宽

                            var btC = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                            var msC = (BlockTableRecord)tr.GetObject(btC[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                            var hits = new List<object>();
                            var dims = new List<object>();

                            foreach (ObjectId oid in msC)
                            {
                                if (tr.GetObject(oid, OpenMode.ForRead) is not Entity ent) continue;
                                if (ent is Autodesk.AutoCAD.DatabaseServices.Dimension dim)
                                {
                                    try
                                    {
                                        string meas = dim.Measurement.ToString("0.###");
                                        dims.Add(new
                                        {
                                            handle = dim.Handle.ToString(),
                                            layer = dim.Layer,
                                            text = dim.DimensionText,
                                            measurement = meas
                                        });
                                    }
                                    catch { }
                                    continue;
                                }

                                try
                                {
                                    var ext = ent.GeometricExtents;
                                    double eLo = useX ? ext.MinPoint.X : ext.MinPoint.Y;
                                    double eHi = useX ? ext.MaxPoint.X : ext.MaxPoint.Y;
                                    // 命中判定：图元区间与查询带重叠
                                    if (eHi < lo - band || eLo > hi2 + band) continue;
                                    hits.Add(new
                                    {
                                        handle = ent.Handle.ToString(),
                                        type = ent.GetType().Name,
                                        layer = ent.Layer,
                                        minX = Math.Round(ext.MinPoint.X, 2),
                                        maxX = Math.Round(ext.MaxPoint.X, 2),
                                        minY = Math.Round(ext.MinPoint.Y, 2),
                                        maxY = Math.Round(ext.MaxPoint.Y, 2),
                                        width = Math.Round(ext.MaxPoint.X - ext.MinPoint.X, 2),
                                        height = Math.Round(ext.MaxPoint.Y - ext.MinPoint.Y, 2)
                                    });
                                }
                                catch { }
                            }

                            result["axis"] = useX ? "x" : "y";
                            result["range"] = new[] { Math.Round(lo, 2), Math.Round(hi2, 2) };
                            result["band"] = band;
                            result["count"] = hits.Count;
                            result["entities"] = hits;
                            result["dimensions"] = dims;
                            result["dimensionCount"] = dims.Count;
                            result["message"] = $"在 {(useX ? "X" : "Y")} 带 [{lo:0.##},{hi2:0.##}] 内找到 {hits.Count} 个图元、{dims.Count} 个尺寸标注";
                        }
                        else if (action == "exportText")
                        {
                            // 导出为 .txt（2026-11-10，柜体 CAD 需求）：把选区/全图的结构化清单落盘。
                            // 只写到 CadPaths 允许的目录，路径由调用方给；缺省落在 cadsys\NET\exports。
                            var rows = new List<string>();
                            var targets = new List<ObjectId>();
                            if (a.Handles.Count > 0)
                            {
                                foreach (string h in a.Handles)
                                    if (TryGetObjectId(db, h, out ObjectId eid)) targets.Add(eid);
                            }
                            else
                            {
                                var psr = doc.Editor.SelectImplied();
                                if (psr.Status == PromptStatus.OK)
                                {
                                    if (psr.Value != null)
                                        foreach (SelectedObject so in psr.Value)
                                            if (so != null) targets.Add(so.ObjectId);
                                }
                                else
                                {
                                    var btE = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                                    var msE = (BlockTableRecord)tr.GetObject(btE[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                                    foreach (ObjectId oid in msE) targets.Add(oid);
                                }
                            }

                            rows.Add("handle\ttype\tlayer\tminX\tminY\tmaxX\tmaxY\twidth\theight\tlength");
                            foreach (ObjectId oid in targets)
                            {
                                if (tr.GetObject(oid, OpenMode.ForRead) is not Entity ent) continue;
                                double len = 0;
                                try { if (ent is Autodesk.AutoCAD.DatabaseServices.Curve cv) len = cv.GetDistanceAtParameter(cv.EndParam) - cv.GetDistanceAtParameter(cv.StartParam); } catch { }
                                try
                                {
                                    var ext = ent.GeometricExtents;
                                    rows.Add(string.Join("\t",
                                        ent.Handle.ToString(), ent.GetType().Name, ent.Layer,
                                        ext.MinPoint.X.ToString("0.###"), ext.MinPoint.Y.ToString("0.###"),
                                        ext.MaxPoint.X.ToString("0.###"), ext.MaxPoint.Y.ToString("0.###"),
                                        (ext.MaxPoint.X - ext.MinPoint.X).ToString("0.###"),
                                        (ext.MaxPoint.Y - ext.MinPoint.Y).ToString("0.###"),
                                        len.ToString("0.###")));
                                }
                                catch
                                {
                                    rows.Add(string.Join("\t", ent.Handle.ToString(), ent.GetType().Name, ent.Layer,
                                        "", "", "", "", "", "", len.ToString("0.###")));
                                }
                            }

                            string outPath = a.FilePath;
                            if (string.IsNullOrWhiteSpace(outPath))
                            {
                                string dir = System.IO.Path.Combine(CadPaths.NetDir, "exports");
                                System.IO.Directory.CreateDirectory(dir);
                                outPath = System.IO.Path.Combine(dir,
                                    $"cad_export_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                            }
                            System.IO.File.WriteAllText(outPath, string.Join(Environment.NewLine, rows),
                                new System.Text.UTF8Encoding(false));

                            result["count"] = rows.Count - 1;
                            result["file"] = outPath;
                            result["message"] = $"已导出 {rows.Count - 1} 行到 {outPath}";
                        }
                        else if (action == "aiSubmit" || action == "aiTranscript")
                        {
                            // 面板可编程驱动（2026-11-11）：把一句话送进 AI 面板的提交入口，
                            // 走的是与「用户输入 + 回车/点发送」完全相同的路径（SubmitPrompt）。
                            // 存在理由：RDP 会话无前台焦点时系统丢弃键盘事件，无法用物理按键自动化验收；
                            // 有了它，验收脚本能像真人一样驱动面板并读回回复。
                            if (action == "aiTranscript")
                            {
                                result["transcript"] = _aiView?.GetTranscript() ?? "{\"error\":\"面板未创建\"}";
                                result["visible"] = _paletteSet?.Visible == true;
                                result["message"] = "已读回面板会话";
                            }
                            else
                            {
                                if (_aiView == null)
                                {
                                    result["error"] = "AI 面板尚未创建，请先执行 AI 命令";
                                    result["count"] = 0;
                                }
                                else
                                {
                                    string prompt = a.Text;
                                    if (string.IsNullOrWhiteSpace(prompt))
                                    {
                                        result["error"] = "aiSubmit 需要 text 参数";
                                        result["count"] = 0;
                                    }
                                    else
                                    {
                                        var view = _aiView;
                                        view.Dispatcher.BeginInvoke(new Action(() =>
                                        {
                                            try { view.SubmitPrompt(prompt, ""); }
                                            catch (System.Exception ex) { Logger.LogException("aiSubmit", ex); }
                                        }));
                                        result["count"] = 1;
                                        result["prompt"] = prompt;
                                        result["message"] = "已提交到面板，可用 aiTranscript 读回回复";
                                    }
                                }
                            }
                        }
                        else if (action == "drawDimension")
                        {
                            var pt1 = new Point3d(a.X1, a.Y1, 0);
                            var pt2 = new Point3d(a.X2, a.Y2, 0);
                            var dimPt = new Point3d(a.DimX, a.DimY, 0);
                            Dimension dim;
                            if (string.Equals(a.Kind, "aligned", StringComparison.OrdinalIgnoreCase))
                            {
                                dim = new AlignedDimension(pt1, pt2, dimPt, a.Text, db.Dimstyle);
                            }
                            else
                            {
                                dim = new RotatedDimension(a.Rotation * Math.PI / 180.0, pt1, pt2, dimPt, a.Text, db.Dimstyle);
                            }

                            if (!string.IsNullOrEmpty(a.Layer))
                            {
                                EnsureLayer(tr, db, a.Layer);
                                dim.Layer = a.Layer;
                            }

                            var ms = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                            ms.AppendEntity(dim);
                            tr.AddNewlyCreatedDBObject(dim, true);

                            result["handle"] = dim.Handle.ToString();
                            result["measurement"] = Math.Round(dim.Measurement, 2);
                            result["text"] = dim.DimensionText;
                            result["layer"] = dim.Layer;
                        }

                        tr.Commit();
                        reply(new { @event = "aiActionResult", action, ok = true, data = result, time = DateTime.Now.ToString("HH:mm:ss.fff") });
                    }
                    catch (System.Exception ex)
                    {
                        tr.Abort();
                        Logger.LogException($"RunExtendedAction({action}) Exception", ex);
                        reply(new { @event = "aiActionResult", action, ok = false, message = ex.Message, time = DateTime.Now.ToString("HH:mm:ss.fff") });
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogException("RunExtendedAction outer Exception", ex);
                reply(new { @event = "aiActionResult", action, ok = false, message = ex.Message, time = DateTime.Now.ToString("HH:mm:ss.fff") });
            }
        }

        private static void RecursiveExplode(Entity ent, int depth, bool skipHatch, List<Entity> leaves)
        {
            if (depth > 0 && ent is BlockReference br)
            {
                using var children = new DBObjectCollection();
                try
                {
                    br.Explode(children);
                    foreach (DBObject obj in children)
                    {
                        if (obj is Entity childEnt)
                        {
                            if (skipHatch && childEnt is Hatch)
                            {
                                leaves.Add(childEnt);
                            }
                            else if (childEnt is BlockReference childBr && depth > 1)
                            {
                                RecursiveExplode(childBr, depth - 1, skipHatch, leaves);
                            }
                            else
                            {
                                leaves.Add(childEnt);
                            }
                        }
                        else
                        {
                            obj.Dispose();
                        }
                    }
                }
                catch
                {
                    leaves.Add(ent);
                }
            }
            else
            {
                leaves.Add(ent);
            }
        }

        private static bool MatchesDocument(Document doc, string expected)
        {
            return string.IsNullOrWhiteSpace(expected) || string.Equals(doc.Name.Replace('\\', '/'), expected.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
        }

        private static void SendReply(object data, string? requestId)
        {
            // 面板同步调用期间，回执优先送给等待方（2026-11-11）
            var sink = _syncReplySink;
            if (sink != null)
            {
                try { sink(data); } catch { }
            }
        }

        /// <summary>兼容面板调用：无外部桥时只写日志。</summary>
        public static void Broadcast(object data)
        {
            try
            {
                Logger.Log("[AI-EVENT] " + JsonSerializer.Serialize(data));
            }
            catch { }
        }
    }
}
