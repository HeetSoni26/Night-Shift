using System;
using System.Diagnostics;

namespace NightShift {
  // Two users of this class:
  //  - `NightShift.exe --recover`: run every minute by the scheduled task. Undoes everything
  //    in the journal if the app died (or is gone) with a session still marked active.
  //  - startup self-heal inside the app: same logic at tray startup.
  static class Recovery {
    public static void Run() {
      Journal j = Journal.Load();
      if (!NeedsRecovery(j)) return;
      PerformUndo(j, "scheduled recovery");
    }

    public static bool NightShiftAppRunning() {
      int self = Process.GetCurrentProcess().Id;
      try {
        Process[] ps = Process.GetProcessesByName("NightShift");
        foreach (Process p in ps) {
          if (p.Id != self) return true;
        }
      } catch {}
      return false;
    }

    public static bool NeedsRecovery(Journal j) {
      bool hasEffects = j.Changes.Count > 0 || j.Frozen.Count > 0 || j.AudioMutedByUs ||
        j.SavedBrightness >= 0 || j.LowPowerBrightness >= 0;
      if (!hasEffects) return false;
      // A live NightShift process manages its own state; this check also prevents the
      // recovery task from racing the app right after the machine wakes from sleep.
      if (NightShiftAppRunning()) return false;
      if (!j.SessionActive) return true; // leftovers without a live session
      if (j.IsStale(120)) return true;   // app died mid-session
      DateTime ends;
      if (DateTime.TryParse(j.EndsUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out ends) &&
          DateTime.UtcNow > ends) return true;
      return false;
    }

    public static void PerformUndo(Journal j, string why) {
      Log.Info("recovery: " + why);
      try {
        if (j.SavedBrightness >= 0) {
          Display.SetBrightness(j.SavedBrightness);
          Log.Info("recovery: brightness restored to " + j.SavedBrightness);
        }
        if (j.LowPowerBrightness >= 0) {
          Display.SetBrightness(j.LowPowerBrightness);
          Log.Info("recovery: low power dim undone (back to " + j.LowPowerBrightness + "%)");
        }
        if (j.AudioMutedByUs) {
          Audio.TrySetMute(false);
          Log.Info("recovery: audio unmuted");
        }
        if (j.Frozen.Count > 0) {
          int ok = Freezer.Thaw(j.Frozen);
          Log.Info("recovery: thawed " + ok + "/" + j.Frozen.Count + " app(s)");
        }
      } catch (Exception ex) { Log.Error("recovery restore step failed", ex); }
      PowerManager pm = new PowerManager(j);
      int failed = pm.UndoAll();
      j.Clear();
      Log.Info("recovery complete (failed undos: " + failed + ")");
    }

    public static bool CreateRecoveryTask(string taskName, string exePath) {
      // Embedded quotes so paths with spaces survive schtasks' own argument parsing.
      string tr = "\\\"" + exePath + "\\\" --recover";
      return RunSchtasks("/Create /F /TN \"" + taskName + "\" /SC MINUTE /MO 1 /TR \"" + tr + "\"");
    }

    public static bool DeleteRecoveryTask(string taskName) {
      return RunSchtasks("/Delete /F /TN \"" + taskName + "\"");
    }

    static bool RunSchtasks(string args) {
      try {
        ProcessStartInfo psi = new ProcessStartInfo("schtasks", args);
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        using (Process p = Process.Start(psi)) {
          string so = p.StandardOutput.ReadToEnd();
          string se = p.StandardError.ReadToEnd();
          p.WaitForExit(15000);
          if (p.ExitCode != 0) Log.Warn("schtasks " + args + " -> exit " + p.ExitCode + " " + (se ?? so).Trim());
          return p.ExitCode == 0;
        }
      } catch (Exception ex) {
        Log.Error("schtasks failed", ex);
        return false;
      }
    }
  }
}
