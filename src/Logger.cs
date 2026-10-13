using System;
using System.IO;

namespace NightShift {
  static class Log {
    static readonly object Gate = new object();

    public static void Init() {
      try { Directory.CreateDirectory(Paths.LogsDir); TrimOld(); } catch {}
    }

    static string CurrentFile {
      get { return Path.Combine(Paths.LogsDir, "nightshift-" + DateTime.Now.ToString("yyyyMMdd") + ".log"); }
    }

    public static void Info(string msg) { Write("INFO ", msg); }
    public static void Warn(string msg) { Write("WARN ", msg); }
    public static void Error(string msg, Exception ex) {
      Write("ERROR", msg + (ex == null ? "" : " :: " + ex.GetType().Name + ": " + ex.Message));
    }

    static void Write(string level, string msg) {
      try {
        lock (Gate) {
          File.AppendAllText(CurrentFile,
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + level + "] " + msg + Environment.NewLine);
        }
      } catch {}
    }

    static void TrimOld() {
      try {
        string[] files = Directory.GetFiles(Paths.LogsDir, "nightshift-*.log");
        foreach (string f in files) {
          string name = Path.GetFileNameWithoutExtension(f);
          DateTime d;
          if (name != null && name.Length == 17 &&
              DateTime.TryParseExact(name.Substring(9), "yyyyMMdd", null,
                System.Globalization.DateTimeStyles.None, out d)) {
            if ((DateTime.Now - d).TotalDays > 14) File.Delete(f);
          }
        }
      } catch {}
    }
  }
}
