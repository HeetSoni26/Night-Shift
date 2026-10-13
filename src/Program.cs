using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NightShift {
  static class Program {
    [STAThread]
    static int Main(string[] args) {
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);
      if (args != null && args.Length > 0) {
        for (int i = 0; i < args.Length; i++) {
          string a = args[i];
          if (a == "--recover") {
            try {
              Paths.EnsureDirs();
              Log.Init();
              Recovery.Run();
            } catch (Exception ex) { Log.Error("recovery failed", ex); return 1; }
            return 0;
          }
          if (a == "--selftest") { Log.Init(); return Diagnostics.SelfTest(); }
          if (a == "--test-power") { Log.Init(); return Diagnostics.TestPower(); }
          if (a == "--test-agent-watch") {
            string names = args.Length > i + 1 ? args[i + 1] : "ZCode";
            int secs = 30;
            if (args.Length > i + 2) {
              int parsed;
              if (int.TryParse(args[i + 2], out parsed)) secs = parsed;
            }
            Log.Init();
            return Diagnostics.TestAgentWatch(names, secs);
          }
          if (a == "--help" || a == "-h") {
            Console.WriteLine("NightShift - keeps your PC awake with the lid closed, for timed sessions");
            Console.WriteLine("  (no args)           run as a tray app");
            Console.WriteLine("  --recover           undo pending changes (used by the recovery task)");
            Console.WriteLine("  --selftest          read-only diagnostics");
            Console.WriteLine("  --test-power        apply and immediately undo power settings, verify restore");
            Console.WriteLine("  --test-agent-watch  <processes> <seconds>: watch a process tree, print activity");
            return 0;
          }
        }
      }
      bool created;
      using (Mutex mutex = new Mutex(true, "Local\\NightShift.SingleInstance", out created)) {
        if (!created) return 0;
        int rc;
        try { rc = RunApp(); }
        finally { mutex.ReleaseMutex(); }
        return rc;
      }
    }

    static int RunApp() {
      try {
        Paths.EnsureDirs();
        Log.Init();
        Log.Info("starting, pid " + Process.GetCurrentProcess().Id + ", data dir " + Paths.DataDir);
        Application.Run(new MainContext());
        Log.Info("exited");
        return 0;
      } catch (Exception ex) {
        Log.Error("fatal", ex);
        try {
          MessageBox.Show("NightShift failed to start: " + ex.Message, "NightShift",
            MessageBoxButtons.OK, MessageBoxIcon.Error);
        } catch {}
        return 1;
      }
    }
  }

  class MainContext : ApplicationContext {
    readonly Config _cfg;
    readonly Journal _journal;
    readonly PowerEventsWatcher _watcher;
    readonly PowerManager _power;
    readonly LidActions _lid;
    readonly SessionController _session;
    TrayController _tray;

    public MainContext() {
      _cfg = Config.Load();
      Journal j = Journal.Load();
      if (Recovery.NeedsRecovery(j)) Recovery.PerformUndo(j, "startup self-heal");
      _journal = Journal.Load();
      _power = new PowerManager(_journal);
      _watcher = new PowerEventsWatcher();
      _watcher.Start(_cfg.HotkeyEnabled);
      _lid = new LidActions(_cfg, _journal, IsSessionActive);
      _session = new SessionController(_cfg, _journal, _power, _lid, _watcher);
      _tray = new TrayController(_cfg, _session, ExitApp);
      _session.Ui = _tray;
      _session.StartTicking();

      // Watcher events arrive on the UI thread (the watcher window lives on it).
      _watcher.LidChanged += WatcherLidChanged;
      _watcher.Resumed += WatcherResumed;
      _watcher.PowerStatusChanged += delegate {
        try { _session.CheckGuardsNow(); }
        catch (Exception ex) { Log.Error("power status change", ex); }
      };
      _watcher.ThemeChanged += delegate {
        try {
          IconFactory.ClearCache();
          if (_tray != null) _tray.RefreshIconNow();
        } catch (Exception ex) { Log.Error("theme change refresh", ex); }
      };
      _watcher.HotkeyPressed += delegate {
        try {
          if (_session.Active) {
            _session.AddTime(new TimeSpan(Math.Max(1, _cfg.HotkeyHours), 0, 0));
          } else {
            string err = _session.Start(0, Math.Max(1, _cfg.HotkeyHours), 0);
            if (err != null) Log.Warn("hotkey start rejected: " + err);
          }
        } catch (Exception ex) { Log.Error("hotkey action", ex); }
      };
      _watcher.Suspended += delegate {
        try {
          _journal.HeartbeatUtc = DateTime.UtcNow.ToString("o");
          _journal.Save();
        } catch {}
      };
      SystemEvents.SessionEnding += OnSessionEnding;
      Log.Info("ready" + (_watcher.LidCurrentlyClosed ? " (lid currently closed)" : ""));
    }

    bool IsSessionActive() { return _session != null && _session.Active; }

    void WatcherLidChanged(bool closed) {
      try {
        if (closed) _lid.OnLidClosed();
        else _lid.OnLidOpened();
      } catch (Exception ex) { Log.Error("lid action", ex); }
    }

    void WatcherResumed() {
      try {
        // Fresh heartbeat first so the recovery task never races us after a wake.
        _journal.HeartbeatUtc = DateTime.UtcNow.ToString("o");
        _journal.Save();
        if (_session.Active && _watcher.LidCurrentlyClosed) _lid.OnLidClosed(); // re-assert
      } catch (Exception ex) { Log.Error("resume re-assert", ex); }
    }

    void OnSessionEnding(object sender, SessionEndingEventArgs e) {
      // Best effort during logoff/shutdown: put power settings back.
      try {
        if (_session.Active) {
          _lid.RestoreIfAnyJournal();
          _power.UndoAll();
          _journal.Clear();
        }
      } catch {}
    }

    void ExitApp() {
      try { SystemEvents.SessionEnding -= OnSessionEnding; } catch {}
      try { if (_tray != null) _tray.Dispose(); } catch {}
      try { _watcher.Stop(); } catch {}
      ExitThread();
    }
  }
}
