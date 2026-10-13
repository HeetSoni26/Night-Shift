using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace NightShift {
  // Meeting / recording apps are never frozen, even when "freeze every other app" is on
  // (same protected list as the macOS version: Zoom, Teams, Webex, ...).
  static class MeetingApps {
    static readonly string[] Tokens = new string[] {
      "zoom", "teams", "webex", "skype", "obs", "gotomeeting", "ringcentral",
      "bluejeans", "whereby", "jitsi", "vsee", "lifesize", "covideo"
    };

    public static bool IsMeetingOrRecording(string processName) {
      string n = (processName ?? "").ToLowerInvariant();
      if (n.Length == 0) return false;
      for (int i = 0; i < Tokens.Length; i++) {
        if (n.Contains(Tokens[i])) return true;
      }
      return false;
    }
  }

    // The Windows stand-in for SIGSTOP/SIGCONT: NtSuspendProcess / NtResumeProcess.
  static class Freezer {
    static readonly string[] SystemTokens = new string[] {
      "explorer", "shell", "applicationframehost", "runtimebroker", "search", "startmenu",
      "shellexperience", "textinput", "lockapp", "sihost", "taskhost", "dwm", "winlogon",
      "csrss", "services", "lsass", "smss", "svchost", "registry", "wininit", "audiodg",
      "fontdrvhost", "conhost", "openconsole", "widget", "webview2", "securityhealth",
      "cmd", "powershell", "pwsh", "terminal", "insomnia"
    };

    public static bool Suspend(int pid) {
      IntPtr h = Native.OpenProcess(Native.ProcessSuspendResume, false, pid);
      if (h == IntPtr.Zero) return false;
      try { return Native.NtSuspendProcess(h) == 0; } finally { Native.CloseHandle(h); }
    }

    public static bool Resume(int pid) {
      IntPtr h = Native.OpenProcess(Native.ProcessSuspendResume, false, pid);
      if (h == IntPtr.Zero) return false;
      try { return Native.NtResumeProcess(h) == 0; } finally { Native.CloseHandle(h); }
    }

    public static List<FrozenProc> FreezeApps(Config cfg) {
      List<string> wanted = new List<string>();
      foreach (string part in (cfg.FreezeApps ?? "").Split(',')) {
        string t = part.Trim().ToLowerInvariant();
        if (t.Length > 0) wanted.Add(t);
      }
      int selfPid = Process.GetCurrentProcess().Id;
      List<FrozenProc> frozen = new List<FrozenProc>();
      Process[] all = Process.GetProcesses();
      foreach (Process p in all) {
        int pid = -1;
        string name = null;
        try {
          pid = p.Id;
          name = p.ProcessName;
          if (pid <= 4 || pid == selfPid) continue;
          string low = (name ?? "").ToLowerInvariant();
          if (low.Length == 0) continue;
          if (cfg.SkipMeetingApps && MeetingApps.IsMeetingOrRecording(low)) continue;
          bool match = false;
          foreach (string t in wanted) {
            if (low.Contains(t) || t.Contains(low)) { match = true; break; }
          }
          if (!match && cfg.FreezeAllOtherApps) {
            if (p.MainWindowHandle == IntPtr.Zero) continue; // no window -> leave it alone
            if (IsSystemish(low)) continue;
            match = true;
          }
          if (!match) continue;
          if (Suspend(pid)) {
            frozen.Add(new FrozenProc { Pid = pid, Name = name });
            Log.Info("froze " + name + " (" + pid + ")");
          } else {
            Log.Warn("could not freeze " + name + " (" + pid + ")");
          }
        } catch (Exception ex) {
          Log.Warn("freeze skip " + (name ?? pid.ToString()) + ": " + ex.Message);
        }
      }
      return frozen;
    }

    static bool IsSystemish(string low) {
      for (int i = 0; i < SystemTokens.Length; i++) {
        if (low.Contains(SystemTokens[i])) return true;
      }
      return false;
    }

    // PID is only trusted when the process name still matches (guards against PID reuse).
    public static int Thaw(List<FrozenProc> procs) {
      if (procs == null) return 0;
      int ok = 0;
      foreach (FrozenProc f in procs) {
        try {
          Process p = null;
          try { p = Process.GetProcessById(f.Pid); } catch (ArgumentException) { p = null; }
          catch (InvalidOperationException) { p = null; }
          if (p != null && string.Equals(p.ProcessName, f.Name, StringComparison.OrdinalIgnoreCase)) {
            if (Resume(f.Pid)) ok++;
            else Log.Warn("could not thaw " + f.Name + " (" + f.Pid + ")");
          }
        } catch (Exception ex) { Log.Warn("thaw skip " + f.Name + ": " + ex.Message); }
      }
      return ok;
    }
  }
}
