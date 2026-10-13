using System;
using System.IO;
using System.Web.Script.Serialization;

namespace NightShift {
  public static class Paths {
    public static string DataDir {
      get {
        string over = Environment.GetEnvironmentVariable("NIGHTSHIFT_DATA_DIR");
        if (!string.IsNullOrEmpty(over)) return over;
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NightShift");
      }
    }
    public static string ConfigFile { get { return Path.Combine(DataDir, "config.json"); } }
    public static string JournalFile { get { return Path.Combine(DataDir, "journal.json"); } }
    public static string LogsDir { get { return Path.Combine(DataDir, "Logs"); } }
    public static void EnsureDirs() {
      Directory.CreateDirectory(DataDir);
      Directory.CreateDirectory(LogsDir);
    }
  }

  public class Config {
    public bool FirstRunSeen { get; set; }

    // Lid-close actions (apply only while a session is running).
    public bool DisplayOffOnLidClose { get; set; }
    public bool MuteAudioOnLidClose { get; set; }
    public bool FreezeAppsOnLidClose { get; set; }
    public string FreezeApps { get; set; }
    public bool FreezeAllOtherApps { get; set; }
    public bool SkipMeetingApps { get; set; }

    // Battery guards, percent (0 disables).
    public int LowPowerBatteryPercent { get; set; }
    public int EndBatteryPercent { get; set; }
    // Fallback when power overlays are unsupported: dim the panel to this instead.
    public int LowPowerBrightnessPercent { get; set; }

    // Heat guards (best effort; many machines expose no usable WMI thermal zone).
    public bool ThermalGuard { get; set; }
    public double ThermalHighC { get; set; }
    public double ThermalEndC { get; set; }

    public bool SleepOnEndIfLidClosed { get; set; }
    public bool RecoveryTaskEnabled { get; set; }
    public string RecoveryTaskName { get; set; }

    // Agent watch: keep awake while the watched process tree (ZCode) is working, end
    // the session after this many quiet minutes. MaxHours caps it (0 = no cap).
    public string AgentWatchProcesses { get; set; }
    public int AgentWatchGraceMinutes { get; set; }
    public int AgentWatchMaxHours { get; set; }

    // Global hotkey: Ctrl+Alt+N starts this many hours (or adds them if already running).
    public bool HotkeyEnabled { get; set; }
    public int HotkeyHours { get; set; }

    public Config() {
      FirstRunSeen = false;
      DisplayOffOnLidClose = true;
      MuteAudioOnLidClose = true;
      FreezeAppsOnLidClose = false;
      FreezeApps = "Slack,WhatsApp,discord";
      FreezeAllOtherApps = false;
      SkipMeetingApps = true;
      LowPowerBatteryPercent = 40;
      EndBatteryPercent = 10;
      LowPowerBrightnessPercent = 40;
      ThermalGuard = true;
      ThermalHighC = 85;
      ThermalEndC = 100;
      SleepOnEndIfLidClosed = true;
      RecoveryTaskEnabled = true;
      RecoveryTaskName = "NightShiftRecovery";
      AgentWatchProcesses = "ZCode";
      AgentWatchGraceMinutes = 5;
      AgentWatchMaxHours = 8;
      HotkeyEnabled = true;
      HotkeyHours = 1;
    }

    public static Config Load() {
      try {
        if (!File.Exists(Paths.ConfigFile)) return new Config();
        Config c = new JavaScriptSerializer().Deserialize<Config>(File.ReadAllText(Paths.ConfigFile));
        return c ?? new Config();
      } catch (Exception ex) {
        Log.Error("config load failed, using defaults", ex);
        return new Config();
      }
    }

    public void Save() {
      try {
        Paths.EnsureDirs();
        File.WriteAllText(Paths.ConfigFile, new JavaScriptSerializer().Serialize(this));
      } catch (Exception ex) { Log.Error("config save failed", ex); }
    }
  }
}
