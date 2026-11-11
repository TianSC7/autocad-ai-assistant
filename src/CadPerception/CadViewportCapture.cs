using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CadPerceptionPlugin
{
    /// <summary>
    /// CAD 视口抓图（2026-11-10）。面板「附图识图」原实现只用 Graphics.CopyFromScreen，
    /// 该 API 依赖交互式桌面 DC：RDP 会话断开或窗口最小化时 GetDC 返回无效句柄，
    /// 抛「句柄无效」并让视口识图整块失效。这里优先用 PrintWindow 直接让 CAD 主窗体
    /// 自行渲染到位图，不依赖桌面合成，断连会话同样可用。
    /// 句柄来源依次尝试：Application.MainWindow → 当前进程 MainWindowHandle → 桌面窗口枚举兜底。
    /// </summary>
    internal static class CadViewportCapture
    {
        private const int PW_RENDERFULLCONTENT = 0x00000002;
        private const uint WM_PRINT = 0x0317;
        private const int PRF_CLIENT = 0x04;
        private const int PRF_CHILDREN = 0x10;
        private const int PRF_ERASEBKGND = 0x08;
        private const int PRF_NONCLIENT = 0x02;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect { public int Left, Top, Right, Bottom; }

        /// <summary>抓取 CAD 主窗体到指定 PNG。失败返回 false，由调用方降级。</summary>
        public static bool TryCapture(string outputPath)
        {
            IntPtr hwnd = ResolveMainWindowHandle();
            if (hwnd == IntPtr.Zero)
            {
                Logger.Log("[VIEWPORT] 主窗体句柄解析失败");
                return false;
            }
            if (IsIconic(hwnd))
            {
                Logger.Log("[VIEWPORT] 主窗体已最小化，无可渲染内容");
                return false;
            }

            return Capture(hwnd, outputPath, PW_RENDERFULLCONTENT)
                || Capture(hwnd, outputPath, 0)
                || CaptureByWmPrint(hwnd, outputPath);
        }

        private static bool Capture(IntPtr hwnd, string outputPath, uint flags)
        {
            try
            {
                if (!GetWindowRect(hwnd, out Rect r)) return false;
                int w = r.Right - r.Left, h = r.Bottom - r.Top;
                if (w <= 16 || h <= 16) return false;

                using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    IntPtr hdc = g.GetHdc();
                    bool ok;
                    try { ok = PrintWindow(hwnd, hdc, flags); }
                    finally { g.ReleaseHdc(hdc); }
                    if (!ok)
                    {
                        Logger.Log($"[VIEWPORT] PrintWindow flags={flags} 返回 false，窗口 {w}x{h}");
                        return false;
                    }
                }
                bmp.Save(outputPath, ImageFormat.Png);
                Logger.Log($"[VIEWPORT] PrintWindow flags={flags} 成功 {w}x{h} -> {outputPath}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"[VIEWPORT] PrintWindow flags={flags} 异常: {ex.Message}");
                return false;
            }
        }

        private static bool CaptureByWmPrint(IntPtr hwnd, string outputPath)
        {
            try
            {
                if (!GetWindowRect(hwnd, out Rect r)) return false;
                int w = r.Right - r.Left, h = r.Bottom - r.Top;
                if (w <= 16 || h <= 16) return false;

                using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    IntPtr hdc = g.GetHdc();
                    try
                    {
                        SendMessage(hwnd, WM_PRINT, hdc,
                            (IntPtr)(PRF_CLIENT | PRF_CHILDREN | PRF_ERASEBKGND | PRF_NONCLIENT));
                    }
                    finally { g.ReleaseHdc(hdc); }
                }
                bmp.Save(outputPath, ImageFormat.Png);
                Logger.Log($"[VIEWPORT] WM_PRINT 兜底成功 {w}x{h}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log("[VIEWPORT] WM_PRINT 兜底异常: " + ex.Message);
                return false;
            }
        }

        private static IntPtr ResolveMainWindowHandle()
        {
            try
            {
                // AutoCAD 2027 的 Application.MainWindow 是 Autodesk.AutoCAD.Windows.Window（自有类型）
                var acadWin = Autodesk.AutoCAD.ApplicationServices.Application.MainWindow;
                if (acadWin != null)
                {
                    if (acadWin.Handle != IntPtr.Zero) return acadWin.Handle;
                    // UnmanagedWindow 在 2027 上是 nint（原始 HWND）
                    nint unmanaged = acadWin.UnmanagedWindow;
                    if (unmanaged != 0) return (IntPtr)unmanaged;
                }
            }
            catch (Exception ex)
            {
                Logger.Log("[VIEWPORT] Application.MainWindow 取句柄异常: " + ex.Message);
            }

            try
            {
                IntPtr p = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                if (p != IntPtr.Zero) return p;
            }
            catch (Exception ex)
            {
                Logger.Log("[VIEWPORT] Process.MainWindowHandle 取句柄异常: " + ex.Message);
            }

            return IntPtr.Zero;
        }
    }
}