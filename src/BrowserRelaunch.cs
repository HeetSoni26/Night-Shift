using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace NightShift {
  // "Relaunch browser unthrottled": Chromium-based browsers throttle background timers
  // aggressively; relaunching with these flags keeps agent-driven tabs running at full
  // speed while the lid is closed and the display is off.
  static class BrowserRelaunch {
    public class BrowserInfo { public string Name; public string ExePath; }

    static readonly string[] ProcessNames = new string[] {
      "chrome", "chromium", "msedge", "brave", "arc", "opera", "vivaldi"
    };

    public static List<BrowserInfo> Detect() {
      List<BrowserInfo> found = new List<BrowserInfo>();
      HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      Process[] all = Process.GetProcesses();
      foreach (Process p in all) {
        try {
          string low = p.ProcessName.ToLowerInvariant();
          if (Array.IndexOf(ProcessNames, low) < 0) continue;
          string path = null;
          try { if (p.MainModule != null) path = p.MainModule.FileName; } catch {}
          if (string.IsNullOrEmpty(path)) continue;
          if (!seen.Add(path)) continue;
          found.Add(new BrowserInfo { Name = DisplayName(low), ExePath = path });
        } catch {}
      }
      return found;
    }

    static string DisplayName(string proc) {
      switch (proc) {
        case "chrome": return "Google Chrome";
        case "chromium": return "Chromium";
        case "msedge": return "Microsoft Edge";
        case "brave": return "Brave";
        case "arc": return "Arc";
        case "opera": return "Opera";
        case "vivaldi": return "Vivaldi";
        default: return proc;
      }
    }

    public static bool RelaunchUnthrottled(BrowserInfo browser) {
      string target;
      try { target = Path.GetFullPath(browser.ExePath).ToLowerInvariant(); }
      catch { target = browser.ExePath.ToLowerInvariant(); }
      List<Process> procs = new List<Process>();
      foreach (Process p in Process.GetProcesses()) {
        try {
          string path = null;
          try { if (p.MainModule != null) path = p.MainModule.FileName; } catch {}
          if (!string.IsNullOrEmpty(path) && path.ToLowerInvariant() == target) procs.Add(p);
        } catch {}
      }
      if (procs.Count == 0) return false;
      foreach (Process p in procs) { try { p.CloseMainWindow(); } catch {} }
      for (int i = 0; i < 16; i++) {
        bool anyAlive = false;
        foreach (Process p in procs) { try { if (!p.HasExited) anyAlive = true; } catch {} }
        if (!anyAlive) break;
        Thread.Sleep(250);
      }
      foreach (Process p in procs) { try { if (!p.HasExited) p.Kill(); } catch {} }
      Thread.Sleep(300);
      ProcessStartInfo psi = new ProcessStartInfo();
      psi.FileName = browser.ExePath;
      psi.Arguments = "--disable-background-timer-throttling --disable-renderer-backgrounding " +
        "--disable-backgrounding-occluded-windows " +
        "--disable-features=CalculateNativeWinOcclusion,IntensiveWakeUpThrottling " +
        "--restore-last-session";
      psi.UseShellExecute = false;
      Process started = Process.Start(psi);
      Log.Info("relaunched " + browser.Name + " unthrottled");
      return started != null;
    }
  }
}
