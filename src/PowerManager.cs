using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace NightShift {
  // Applies and undoes the Windows power settings that keep the machine awake with the
  // lid closed. Every change is journaled, with its exact undo, before it is applied.
  class PowerManager {
    // Well-known power setting GUIDs (not localized, stable across Windows versions).
    public const string SubButtons = "4f971e89-eebd-4455-a8de-9e59040e7347";
    public const string LidAction = "5ca83367-6e45-459f-a27b-476b1d01c936"; // 0 = do nothing
    public const string SubSleep = "238c9fa8-0aad-41ed-83f4-97be242c8f20";
    public const string StandbyIdle = "29f6c1db-86da-48c5-9fdb-f2b67b1f44da";
    public const string HibernateIdle = "9d7815a6-7ee4-497e-8888-515a05f02364";

    // Overlay ("power mode") used as the low-power state.
    public const string OverlayAliasMaxEnergySavings = "overlay_scheme_max_energy_savings";
    public const string OverlayMaxEnergySavings = "ded573b5-45a0-4f42-8737-46345c09c238";

    readonly Journal _journal;
    public PowerManager(Journal journal) { _journal = journal; }

    public static int RunPowercfg(string args) {
      try {
        ProcessStartInfo psi = new ProcessStartInfo("powercfg", args);
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        using (Process p = Process.Start(psi)) {
          p.StandardOutput.ReadToEnd();
          string err = p.StandardError.ReadToEnd();
          p.WaitForExit(10000);
          if (p.ExitCode != 0) Log.Warn("powercfg " + args + " -> exit " + p.ExitCode + " " + (err ?? "").Trim());
          return p.ExitCode;
        }
      } catch (Exception ex) {
        Log.Error("powercfg " + args + " could not run", ex);
        return -1;
      }
    }

    public static string RunPowercfgOut(string args, out int code) {
      try {
        ProcessStartInfo psi = new ProcessStartInfo("powercfg", args);
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        using (Process p = Process.Start(psi)) {
          string so = p.StandardOutput.ReadToEnd();
          p.StandardError.ReadToEnd();
          p.WaitForExit(10000);
          code = p.ExitCode;
          return so ?? "";
        }
      } catch (Exception ex) {
        Log.Error("powercfg " + args + " could not run", ex);
        code = -1;
        return "";
      }
    }

    // powercfg /qh prints "Current AC Power Setting Index: 0x..." then "Current DC ..."
    // as the last two hex tokens of the block, for visible AND hidden settings (on Modern
    // Standby laptops the lid action is hidden; /q shows nothing for it). The surrounding
    // words are localized, the hex values are not. On some desktops only the AC line
    // exists (then DC = AC).
    public static bool QueryIndices(string subgroup, string setting, out int ac, out int dc) {
      ac = -1; dc = -1;
      int code;
      string output = RunPowercfgOut("/qh SCHEME_CURRENT " + subgroup + " " + setting, out code);
      if (code != 0) return false;
      MatchCollection m = Regex.Matches(output, @"0x([0-9A-Fa-f]+)");
      if (m.Count == 1) {
        ac = (int)Convert.ToInt64(m[0].Groups[1].Value, 16);
        dc = ac;
        return true;
      }
      if (m.Count >= 2) {
        ac = (int)Convert.ToInt64(m[m.Count - 2].Groups[1].Value, 16);
        dc = (int)Convert.ToInt64(m[m.Count - 1].Groups[1].Value, 16);
        return true;
      }
      return false;
    }

    // Reads current values first; journals the undo entry; only then applies.
    public bool ApplySetting(string name, string subgroup, string setting, int desiredAc, int desiredDc) {
      int ac, dc;
      if (!QueryIndices(subgroup, setting, out ac, out dc)) {
        Log.Warn("apply " + name + ": could not read current values, skipping");
        return false;
      }
      bool needAc = ac != desiredAc;
      bool needDc = dc != desiredDc;
      if (!needAc && !needDc) return true; // already in the wanted state
      _journal.Changes.Add(new JournalChange {
        Name = name + "-ac",
        UndoArgs = "/setacvalueindex SCHEME_CURRENT " + subgroup + " " + setting + " 0x" + ac.ToString("x8")
      });
      _journal.Changes.Add(new JournalChange {
        Name = name + "-dc",
        UndoArgs = "/setdcvalueindex SCHEME_CURRENT " + subgroup + " " + setting + " 0x" + dc.ToString("x8")
      });
      _journal.Save(); // journal BEFORE applying
      int rc = 0;
      if (needAc) rc |= RunPowercfg("/setacvalueindex SCHEME_CURRENT " + subgroup + " " + setting + " " + desiredAc);
      if (needDc) rc |= RunPowercfg("/setdcvalueindex SCHEME_CURRENT " + subgroup + " " + setting + " " + desiredDc);
      RunPowercfg("/setactive SCHEME_CURRENT");
      if (rc == 0) Log.Info("applied " + name + " (ac=" + desiredAc + " dc=" + desiredDc + ")");
      return rc == 0;
    }

    // The heart of the app: lid close = do nothing, no idle sleep, no idle hibernate.
    public bool ApplySessionSettings() {
      bool ok = ApplySetting("lid", SubButtons, LidAction, 0, 0);       // critical
      ok = ApplySetting("sleep", SubSleep, StandbyIdle, 0, 0) && ok;    // critical
      ApplySetting("hibernate", SubSleep, HibernateIdle, 0, 0);         // optional (hibernate may be off)
      return ok;
    }

    public int UndoAll() {
      int failed = 0;
      for (int i = _journal.Changes.Count - 1; i >= 0; i--) {
        JournalChange ch = _journal.Changes[i];
        int rc = RunPowercfg(ch.UndoArgs);
        if (rc != 0) { failed++; Log.Warn("undo failed: " + ch.Name); }
        else Log.Info("undone " + ch.Name);
        _journal.Changes.RemoveAt(i);
        _journal.Save();
      }
      RunPowercfg("/setactive SCHEME_CURRENT");
      return failed;
    }

    public static string GetActiveOverlay() {
      int code;
      string output = RunPowercfgOut("/getactiveoverlay", out code);
      if (code != 0) return null;
      Match m = Regex.Match(output, @"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})");
      return m.Success ? m.Groups[1].Value.ToLowerInvariant() : null;
    }
  }
}
