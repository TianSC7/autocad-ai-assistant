using System;
using System.IO;

namespace CadPerceptionPlugin
{
    /// <summary>
    /// Runtime paths. Everything is user-scoped and overridable by environment variables:
    ///   CAD_SYS_ROOT  -> root that holds NET/ (assembly drop folder) and LSP/
    /// Default: %USERPROFILE%\Documents\cadsys
    /// </summary>
    internal static class CadPaths
    {
        /// <summary>Root directory (logs / LSP / presets live under it).</summary>
        public static string CadsysRoot { get; } = ResolveCadsysRoot();

        /// <summary>cadsys\NET: deployment folder, also holds ai_presets.json / ai_sessions.json / debug log.</summary>
        public static string NetDir => Path.Combine(CadsysRoot, "NET");

        /// <summary>cadsys\LSP: folder offered to the AI as the LISP command directory.</summary>
        public static string LspDir => Path.Combine(CadsysRoot, "LSP");

        public static string PresetsFile => Path.Combine(NetDir, "ai_presets.json");
        public static string SessionsFile => Path.Combine(NetDir, "ai_sessions.json");
        public static string DebugLogFile => Path.Combine(NetDir, "CadPerception_debug.log");

        /// <summary>Create NET before writing; returns false when creation fails.</summary>
        public static bool EnsureNetDir()
        {
            try
            {
                Directory.CreateDirectory(NetDir);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string ResolveCadsysRoot()
        {
            string env = Environment.GetEnvironmentVariable("CAD_SYS_ROOT");
            if (!string.IsNullOrWhiteSpace(env)) return env.TrimEnd('\\', '/');

            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return string.IsNullOrWhiteSpace(docs)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "cadsys")
                : Path.Combine(docs, "cadsys");
        }
    }
}
