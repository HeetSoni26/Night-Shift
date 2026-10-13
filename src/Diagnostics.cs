using System;
using System.IO;
using System.Threading;

namespace NightShift {
  // --selftest: read-only checks. --test-power: apply + undo power settings for real and
  // verify the restore. Both write to stdout (run them from a terminal) and to the log.
  static class Diagnostics {
    static void Out(string s) {
      Console.WriteLine(s);
      Log.Info("[diag] " + s);
    }

    static void IsolateDataDir() {
      string temp = Path.Combine(Path.GetTempPath(), "NightShiftDiag-" + Guid.NewGuid().ToString("N").Substring(0, 8));
      Environment.SetEnvironmentVariable("NIGHTSHIFT_DATA_DIR", temp);
      Paths.EnsureDirs();
    }

    public static int SelfTest() {
      int pass = 0, fail = 0;
      Action<string, bool> check = delegate(string name, bool ok) {
        Out((ok ? "PASS  " : "FAIL  ") + name);
        if (ok) pass++; else fail++;
      };
      try {
        IsolateDataDir();
        Log.Init();

        // config round trip
        Config c1 = new Config();
        c1.LowPowerBatteryPercent = 33;
        c1.Save();
        Config c2 = Config.Load();
        check("config round trip", c2.LowPowerBatteryPercent == 33);

        // journal round trip
        Journal j1 = new Journal();
        j1.SessionActive = true;
        j1.Changes.Add(new JournalChange { Name = "test", UndoArgs = "/q" });
        j1.Frozen.Add(new FrozenProc { Pid = 4242, Name = "Test" });
        j1.Save();
        Journal j2 = Journal.Load();
        check("journal round trip", j2.SessionActive && j2.Changes.Count == 1 && j2.Frozen.Count == 1 && j2.SavedBrightness == -1);
        j2.Clear();
        check("journal clear", Journal.Load().Changes.Count == 0 && !Journal.Load().SessionActive);

        // powercfg query parsing (read-only)
        int ac, dc;
        bool q = PowerManager.QueryIndices(PowerManager.SubButtons, PowerManager.LidAction, out ac, out dc);
        check("powercfg query parse (lid action)", q && ac >= 0 && dc >= 0);
        if (q) Out("      lid action AC=" + ac + " DC=" + dc + "  (0=do nothing, 1=sleep, 2=hibernate, 3=shut down)");
        int sac, sdc;
        bool sq = PowerManager.QueryIndices(PowerManager.SubSleep, PowerManager.StandbyIdle, out sac, out sdc);
        check("powercfg query parse (sleep timeout)", sq);
        if (sq) Out("      sleep timeout AC=" + sac + " DC=" + sdc + "  (0 = never)");

        // audio endpoint
        bool muted;
        check("audio endpoint read", Audio.TryGetMute(out muted));
        Out("      master mute: " + (muted ? "muted" : "unmuted"));

        // display / thermal / overlay (informational)
        int? b = Display.GetBrightness();
        Out("INFO  brightness: " + (b.HasValue ? b.Value + "%" : "not supported (desktop, or driver without WMI brightness)"));
        double? t = Monitors.TemperatureC();
        Out("INFO  thermal zone: " + (t.HasValue ? t.Value.ToString("0.0") + " C" : "not supported"));
        string ov = PowerManager.GetActiveOverlay();
        Out("INFO  active power overlay: " + (ov ?? "not supported"));

        // classification
        check("meeting app classification",
          MeetingApps.IsMeetingOrRecording("zoom") && MeetingApps.IsMeetingOrRecording("ms-teams") &&
          !MeetingApps.IsMeetingOrRecording("notepad"));

        // lid/power notification registration + initial state
        bool got = false;
        bool state = false;
        bool registered = false;
        using (PowerEventsWatcher w = new PowerEventsWatcher()) {
          using (ManualResetEvent ev = new ManualResetEvent(false)) {
            w.LidChanged += delegate(bool closed) { got = true; state = closed; ev.Set(); };
            w.Start(false);
            registered = w.Registered;
            ev.WaitOne(2000);
          }
          check("power notification registration", registered);
          Out("INFO  initial lid event: " + (got ? ("received, lid " + (state ? "closed" : "open")) : "not received yet (updates on next change)"));
        }
      } catch (Exception ex) {
        Out("FAIL  unexpected: " + ex.GetType().Name + ": " + ex.Message);
        fail++;
      }
      Out(pass + " passed, " + fail + " failed");
      return fail == 0 ? 0 : 1;
    }

    public static int TestPower() {
      try {
        IsolateDataDir();
        Log.Init();
        if (Recovery.NightShiftAppRunning()) {
          Out("FAIL  an NightShift session app is running; end it before --test-power");
          return 1;
        }
        Journal j = new Journal();
        PowerManager pm = new PowerManager(j);
        int lac, ldc, sac, sdc;
        PowerManager.QueryIndices(PowerManager.SubButtons, PowerManager.LidAction, out lac, out ldc);
        PowerManager.QueryIndices(PowerManager.SubSleep, PowerManager.StandbyIdle, out sac, out sdc);
        Out("BEFORE   lid=" + lac + "/" + ldc + "  sleep=" + sac + "/" + sdc);
        pm.ApplySessionSettings();
        int a1, a2, b1, b2;
        PowerManager.QueryIndices(PowerManager.SubButtons, PowerManager.LidAction, out a1, out a2);
        PowerManager.QueryIndices(PowerManager.SubSleep, PowerManager.StandbyIdle, out b1, out b2);
        Out("DURING   lid=" + a1 + "/" + a2 + "  sleep=" + b1 + "/" + b2);
        int failed = pm.UndoAll();
        int c1, c2, d1, d2;
        PowerManager.QueryIndices(PowerManager.SubButtons, PowerManager.LidAction, out c1, out c2);
        PowerManager.QueryIndices(PowerManager.SubSleep, PowerManager.StandbyIdle, out d1, out d2);
        Out("RESTORED lid=" + c1 + "/" + c2 + "  sleep=" + d1 + "/" + d2);
        bool ok = failed == 0 && c1 == lac && c2 == ldc && d1 == sac && d2 == sdc;
        Out(ok ? "PASS  power apply/undo verified (original settings restored)" : "FAIL  power state mismatch after undo");
        return ok ? 0 : 1;
      } catch (Exception ex) {
        Out("FAIL  " + ex.GetType().Name + ": " + ex.Message);
        return 1;
      }
    }

    // Watches the given process list for a few seconds and prints the activity state,
    // so the agent-watch detection can be verified without starting a session.
    public static int TestAgentWatch(string names, int seconds) {
      try {
        IsolateDataDir();
        Log.Init();
        Out("watching '" + names + "' for " + seconds + "s; use the watched apps now to see transitions");
        using (ActivityMonitor m = new ActivityMonitor(names)) {
          m.Start();
          DateTime until = DateTime.UtcNow.AddSeconds(seconds);
          while (DateTime.UtcNow < until) {
            Thread.Sleep(1000);
            TimeSpan quiet = DateTime.UtcNow - m.LastActivityUtc;
            Out("  running=" + (m.AnyWatchedRunning ? "yes" : "no ") +
                "  quiet=" + (int)quiet.TotalSeconds + "s" +
                (m.AnyWatchedRunning && quiet.TotalSeconds < 3 ? "  [ACTIVE]" : ""));
          }
        }
        Out("done");
        return 0;
      } catch (Exception ex) {
        Out("FAIL  " + ex.GetType().Name + ": " + ex.Message);
        return 1;
      }
    }
  }
}
