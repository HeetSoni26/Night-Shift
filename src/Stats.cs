using System;
using System.IO;
using System.Web.Script.Serialization;

namespace NightShift {
  public class StatsData {
    public int Sessions { get; set; }
    public long AwakeSeconds { get; set; }
    public long LongestSeconds { get; set; }
  }

  // Lifetime counters for the tray menu line ("12 shifts, 34.5h kept awake").
  static class Stats {
    static string FilePath {
      get { return Path.Combine(Paths.DataDir, "stats.json"); }
    }

    public static StatsData Load() {
      try {
        if (!File.Exists(FilePath)) return new StatsData();
        StatsData d = new JavaScriptSerializer().Deserialize<StatsData>(File.ReadAllText(FilePath));
        return d ?? new StatsData();
      } catch {
        return new StatsData();
      }
    }

    public static void Add(int seconds) {
      try {
        StatsData d = Load();
        d.Sessions++;
        d.AwakeSeconds += seconds;
        if (seconds > d.LongestSeconds) d.LongestSeconds = seconds;
        Paths.EnsureDirs();
        File.WriteAllText(FilePath, new JavaScriptSerializer().Serialize(d));
      } catch (Exception ex) {
        Log.Error("stats save failed", ex);
      }
    }

    public static string Summary() {
      StatsData d = Load();
      double hours = d.AwakeSeconds / 3600.0;
      string h = hours >= 10 ? hours.ToString("0") : hours.ToString("0.0");
      return d.Sessions + (d.Sessions == 1 ? " shift, " : " shifts, ") + h + "h kept awake";
    }
  }
}
