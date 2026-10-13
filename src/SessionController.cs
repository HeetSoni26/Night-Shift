using System;
using System.Windows.Forms;

namespace NightShift {
  // Owns the countdown, the heartbeat, and the battery/heat guards.
  class SessionController {
    readonly Config _cfg;
    readonly Journal _journal;
    readonly PowerManager _power;
    readonly LidActions _lid;
    readonly PowerEventsWatcher _watcher;
    readonly Timer _tick;
    int _beat;
    int _batteryPhase;
    bool _lowPowerApplied;

    public bool Active { get; private set; }
    public DateTime StartedUtc { get; private set; }
    public DateTime EndsUtc { get; private set; }
    public TrayController Ui { get; set; }

    public SessionController(Config cfg, Journal journal, PowerManager power,
      LidActions lid, PowerEventsWatcher watcher) {
      _cfg = cfg;
      _journal = journal;
      _power = power;
      _lid = lid;
      _watcher = watcher;
      _tick = new Timer { Interval = 1000 };
      _tick.Tick += OnTick;
    }

    public void StartTicking() {
      _tick.Start();
      UpdateUi();
    }

    // Idle: starts a session. Active: adds time (same gesture as the macOS app).
    public string Start(int days, int hours, int minutes) {
      TimeSpan span = new TimeSpan(days, hours, minutes);
      if (span <= TimeSpan.Zero) return "Enter a duration above zero.";
      if (Active) {
        AddTime(span);
        return null;
      }
      StartedUtc = DateTime.UtcNow;
      EndsUtc = StartedUtc + span;
      Active = true;
      _journal.SessionActive = true;
      _journal.StartedUtc = StartedUtc.ToString("o");
      _journal.EndsUtc = EndsUtc.ToString("o");
      _journal.HeartbeatUtc = StartedUtc.ToString("o");
      _journal.Save(); // journal BEFORE touching power settings
      bool ok = _power.ApplySessionSettings();
      // Belt-and-braces: also hold the system awake via execution state while the
      // session thread lives. Does not block lid-close handling or display off.
      try { Native.SetThreadExecutionState(Native.EsContinuous | Native.EsSystemRequired); }
      catch (Exception ex) { Log.Warn("SetThreadExecutionState failed: " + ex.Message); }
      if (!ok) Log.Warn("some power settings could not be applied; the session continues anyway");
      Log.Info("session started, ends " + EndsUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
      UpdateUi();
      return null;
    }

    public void AddTime(TimeSpan span) {
      if (!Active || span <= TimeSpan.Zero) return;
      EndsUtc = EndsUtc + span;
      _journal.EndsUtc = EndsUtc.ToString("o");
      _journal.HeartbeatUtc = DateTime.UtcNow.ToString("o");
      _journal.Save();
      Log.Info("time added, now ends " + EndsUtc.ToLocalTime().ToString("HH:mm:ss"));
      UpdateUi();
    }

    public void EndEarly(string reason) {
      if (!Active) return;
      Active = false;
      Log.Info("session ended: " + reason);
      try { _lid.RestoreIfAnyJournal(); }
      catch (Exception ex) { Log.Error("restoring lid effects failed", ex); }
      try { _power.UndoAll(); }
      catch (Exception ex) { Log.Error("undoing power settings failed", ex); }
      try { Native.SetThreadExecutionState(Native.EsContinuous); } // release the hold
      catch {}
      _lowPowerApplied = false;
      _journal.Clear();
      MaybeSleep();
      UpdateUi();
    }

    // Mirror of the macOS behavior: a session that ends with the lid still closed puts
    // the machine to sleep, unless an external display is in use (clamshell mode).
    void MaybeSleep() {
      if (!_cfg.SleepOnEndIfLidClosed) return;
      if (!_watcher.LidCurrentlyClosed) return;
      if (Screen.AllScreens.Length > 1) return;
      if (!Native.EnableShutdownPrivilege()) {
        Log.Warn("could not acquire shutdown privilege; skipping the end-of-session sleep");
        return;
      }
      Log.Info("lid still closed and no external display; requesting sleep");
      try { Native.SetSuspendState(false, false, false); }
      catch (Exception ex) { Log.Error("sleep request failed", ex); }
    }

    void OnTick(object sender, EventArgs e) {
      if (!Active) return;
      _beat++;
      if (_beat >= 15) {
        _beat = 0;
        _journal.HeartbeatUtc = DateTime.UtcNow.ToString("o");
        _journal.Save();
      }
      if (DateTime.UtcNow >= EndsUtc) {
        EndEarly("timer expired");
        return;
      }
      _batteryPhase++;
      if (_batteryPhase >= 30) {
        _batteryPhase = 0;
        CheckGuards();
      }
      UpdateUi();
    }

    public void CheckGuardsNow() {
      if (!Active) return;
      CheckGuards();
      UpdateUi();
    }

    void CheckGuards() {
      try {
        if (Monitors.OnBattery()) {
          int? pct = Monitors.BatteryPercent();
          if (pct.HasValue) {
            if (_cfg.EndBatteryPercent > 0 && pct.Value <= _cfg.EndBatteryPercent) {
              EndEarly("battery at " + pct.Value + "% (end limit " + _cfg.EndBatteryPercent + "%)");
              return;
            }
            if (!_lowPowerApplied && _cfg.LowPowerBatteryPercent > 0 && pct.Value <= _cfg.LowPowerBatteryPercent) {
              ApplyLowPower("battery " + pct.Value + "%");
            }
          }
        }
        if (_cfg.ThermalGuard) {
          double? t = Monitors.TemperatureC();
          if (t.HasValue) {
            if (_cfg.ThermalEndC > 0 && t.Value >= _cfg.ThermalEndC) {
              EndEarly("temperature " + (int)t.Value + " C");
              return;
            }
            if (!_lowPowerApplied && t.Value >= _cfg.ThermalHighC) {
              ApplyLowPower("temperature " + (int)t.Value + " C");
            }
          }
        }
      } catch (Exception ex) { Log.Error("guard check failed", ex); }
    }

    // The "Low Power Mode" equivalent. First choice: the power-mode overlay with the
    // best energy savings. If this Windows has no overlays (common), fall back to
    // dimming the panel. Either way the restore path is journaled first.
    void ApplyLowPower(string why) {
      string current = PowerManager.GetActiveOverlay();
      if (current != null) {
        if (current == PowerManager.OverlayMaxEnergySavings) {
          _lowPowerApplied = true;
          return;
        }
        _journal.Changes.Add(new JournalChange {
          Name = "lowpower-overlay",
          UndoArgs = "/overlaysetactive " + current
        });
        _journal.Save();
      }
      if (PowerManager.RunPowercfg("/overlaysetactive " + PowerManager.OverlayAliasMaxEnergySavings) == 0) {
        _lowPowerApplied = true;
        Log.Info("low power mode on (" + why + ")");
        return;
      }
      Log.Warn("low power overlay not supported; falling back to dimming (" + why + ")");
      if (_journal.Changes.Count > 0 &&
          _journal.Changes[_journal.Changes.Count - 1].Name == "lowpower-overlay") {
        _journal.Changes.RemoveAt(_journal.Changes.Count - 1);
        _journal.Save();
      }
      int target = Math.Max(1, Math.Min(100, _cfg.LowPowerBrightnessPercent));
      int? bright = Display.GetBrightness();
      if (bright.HasValue && bright.Value > target) {
        _journal.LowPowerBrightness = bright.Value;
        _journal.Save();
        if (Display.SetBrightness(target)) {
          _lowPowerApplied = true;
          Log.Info("low power dim to " + target + "% (" + why + ")");
          return;
        }
      }
      Log.Warn("low power mode unavailable on this system (" + why + ")");
    }

    public string RemainingText() {
      TimeSpan r = EndsUtc - DateTime.UtcNow;
      if (r < TimeSpan.Zero) r = TimeSpan.Zero;
      return string.Format("{0}d {1:00}:{2:00}:{3:00}", (int)r.TotalDays, r.Hours, r.Minutes, r.Seconds);
    }

    void UpdateUi() {
      TrayController u = Ui;
      if (u != null) u.UpdateVisual();
    }
  }
}
