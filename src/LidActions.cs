using System;
using System.Collections.Generic;

namespace NightShift {
  // What happens when the lid closes while a session is running (and the undo of it).
  // Every step writes its restore information into the journal before acting.
  class LidActions {
    readonly Config _cfg;
    readonly Journal _journal;
    readonly Func<bool> _sessionActive;

    public LidActions(Config cfg, Journal journal, Func<bool> sessionActive) {
      _cfg = cfg;
      _journal = journal;
      _sessionActive = sessionActive;
    }

    public void OnLidClosed() {
      if (!_sessionActive()) return;
      Log.Info("lid closed: applying quiet actions");
      if (_cfg.DisplayOffOnLidClose) {
        try {
          int? b = Display.GetBrightness();
          if (b.HasValue && b.Value > 0) {
            _journal.SavedBrightness = b.Value;
            _journal.Save();
            Display.SetBrightness(0);
          }
        } catch (Exception ex) { Log.Warn("brightness handling failed: " + ex.Message); }
        Display.TurnOffMonitor();
      }
      if (_cfg.MuteAudioOnLidClose) {
        bool muted;
        if (Audio.TryGetMute(out muted) && !muted) {
          _journal.AudioMutedByUs = true;
          _journal.Save();
          Audio.TrySetMute(true);
        }
      }
      if (_cfg.FreezeAppsOnLidClose) {
        try {
          List<FrozenProc> frozen = Freezer.FreezeApps(_cfg);
          _journal.Frozen = frozen;
          _journal.Save();
          Log.Info("froze " + frozen.Count + " app(s)");
        } catch (Exception ex) { Log.Error("freezing failed", ex); }
      }
    }

    public void OnLidOpened() {
      if (!_sessionActive()) return; // nothing of ours should be in effect
      Log.Info("lid opened: restoring");
      RestoreIfAnyJournal();
    }

    // Restores everything the journal says is in effect. Used on lid open, session end,
    // and recovery. Idempotent: journal entries are cleared as they are restored.
    public void RestoreIfAnyJournal() {
      bool dirty = false;
      if (_journal.SavedBrightness >= 0) {
        try { Display.SetBrightness(_journal.SavedBrightness); }
        catch (Exception ex) { Log.Warn("brightness restore failed: " + ex.Message); }
        _journal.SavedBrightness = -1;
        dirty = true;
      }
      if (_journal.LowPowerBrightness >= 0) {
        try { Display.SetBrightness(_journal.LowPowerBrightness); }
        catch (Exception ex) { Log.Warn("low power brightness restore failed: " + ex.Message); }
        _journal.LowPowerBrightness = -1;
        dirty = true;
      }
      if (_journal.AudioMutedByUs) {
        Audio.TrySetMute(false);
        _journal.AudioMutedByUs = false;
        dirty = true;
      }
      if (_journal.Frozen.Count > 0) {
        int ok = Freezer.Thaw(_journal.Frozen);
        Log.Info("thawed " + ok + "/" + _journal.Frozen.Count + " app(s)");
        _journal.Frozen.Clear();
        dirty = true;
      }
      if (dirty) _journal.Save();
    }
  }
}
