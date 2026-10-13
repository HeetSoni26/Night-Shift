using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace NightShift {
  // One reversible power change. UndoArgs is the powercfg command line that puts the
  // original value back. Written to disk BEFORE the change is applied.
  public class JournalChange {
    public string Name { get; set; }
    public string UndoArgs { get; set; }
  }

  public class FrozenProc {
    public int Pid { get; set; }
    public string Name { get; set; }
  }

  // The recovery journal: everything NightShift changed on this machine, so any process
  // (the app itself, or the scheduled task after a crash) can put it all back.
  public class Journal {
    public bool SessionActive { get; set; }
    public string StartedUtc { get; set; }
    public string EndsUtc { get; set; }
    public string HeartbeatUtc { get; set; }
    public bool AudioMutedByUs { get; set; }
    public int SavedBrightness { get; set; }
    public int LowPowerBrightness { get; set; }
    public List<JournalChange> Changes { get; set; }
    public List<FrozenProc> Frozen { get; set; }

    public Journal() {
      SessionActive = false;
      SavedBrightness = -1;
      LowPowerBrightness = -1;
      Changes = new List<JournalChange>();
      Frozen = new List<FrozenProc>();
    }

    public static Journal Load() {
      try {
        if (!File.Exists(Paths.JournalFile)) return new Journal();
        Journal j = new JavaScriptSerializer().Deserialize<Journal>(File.ReadAllText(Paths.JournalFile));
        if (j == null) return new Journal();
        if (j.Changes == null) j.Changes = new List<JournalChange>();
        if (j.Frozen == null) j.Frozen = new List<FrozenProc>();
        return j;
      } catch (Exception ex) {
        Log.Error("journal load failed", ex);
        return new Journal();
      }
    }

    public void Save() {
      try {
        Paths.EnsureDirs();
        File.WriteAllText(Paths.JournalFile, new JavaScriptSerializer().Serialize(this));
      } catch (Exception ex) { Log.Error("journal save failed", ex); }
    }

    public void Clear() {
      SessionActive = false;
      StartedUtc = null;
      EndsUtc = null;
      HeartbeatUtc = null;
      AudioMutedByUs = false;
      SavedBrightness = -1;
      LowPowerBrightness = -1;
      Changes.Clear();
      Frozen.Clear();
      Save();
    }

    public bool IsStale(int seconds) {
      DateTime hb;
      if (!DateTime.TryParse(HeartbeatUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out hb)) return true;
      return (DateTime.UtcNow - hb).TotalSeconds > seconds;
    }
  }
}
