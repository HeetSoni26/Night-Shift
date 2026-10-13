using System;
using System.Collections.Generic;
using System.Management;
using System.Threading;

namespace NightShift {
  // Watches a process tree (ZCode by default) for signs of active agent work. Three
  // signals, none of which need admin rights:
  //   1. new child processes appearing in the tree (every tool call spawns one)
  //   2. CPU time ticking across the tree (builds, tests, the streaming UI)
  //   3. IO bytes moving (session transcripts and project files are written while
  //      the model streams its response)
  // A background poller computes the state; the UI thread only reads LastActivityUtc
  // through the session tick, so all session decisions stay on the UI thread.
  class ActivityMonitor : IDisposable {
    const int PollMs = 2000;
    const long CpuActiveMs = 120;     // summed over the tree, per poll
    const long IoActiveBytes = 65536; // 64 KB summed over the tree, per poll

    readonly List<string> _names = new List<string>();
    Timer _poll;
    int _polling;
    Dictionary<int, Proc> _prev;
    readonly object _gate = new object();
    DateTime _lastActivityUtc;
    bool _anyRunning;

    public DateTime LastActivityUtc {
      get { lock (_gate) { return _lastActivityUtc; } }
    }

    public bool AnyWatchedRunning {
      get { lock (_gate) { return _anyRunning; } }
    }

    public ActivityMonitor(string watchList) {
      foreach (string part in (watchList ?? "").Split(',')) {
        string t = part.Trim().ToLowerInvariant();
        if (t.Length > 0) _names.Add(t);
      }
    }

    public void Start() {
      lock (_gate) {
        _lastActivityUtc = DateTime.UtcNow;
        _anyRunning = false;
      }
      _prev = null;
      _poll = new Timer(Poll, null, 200, PollMs);
      Log.Info("agent watch started (processes: " + string.Join(", ", _names.ToArray()) + ")");
    }

    public void Stop() {
      Timer t = _poll;
      _poll = null;
      if (t != null) { try { t.Dispose(); } catch {} }
    }

    public void Dispose() { Stop(); }

    void Poll(object state) {
      if (Interlocked.CompareExchange(ref _polling, 1, 0) != 0) return;
      try {
        bool activity = false;
        bool anyRunning = false;
        Dictionary<int, Proc> cur = Snapshot();
        HashSet<int> tree = BuildTree(cur, ref anyRunning);
        AddIo(cur, tree);

        Dictionary<int, Proc> prev = _prev;
        if (prev != null) {
          long cpuMs = 0;
          long ioBytes = 0;
          foreach (int pid in tree) {
            Proc nowP;
            if (!cur.TryGetValue(pid, out nowP)) continue;
            Proc before;
            if (!prev.TryGetValue(pid, out before) || !before.InTree) {
              activity = true; // a new child appeared in the tree: a tool call
              continue;
            }
            if (nowP.CpuMs > before.CpuMs) cpuMs += nowP.CpuMs - before.CpuMs;
            if (nowP.IoBytes > before.IoBytes) ioBytes += nowP.IoBytes - before.IoBytes;
          }
          foreach (Proc p in prev.Values) {
            if (p.InTree && !tree.Contains(p.Pid)) {
              activity = true; // a short-lived tool finished between polls
              break;
            }
          }
          if (cpuMs > CpuActiveMs || ioBytes > IoActiveBytes) activity = true;
        }
        foreach (Proc p in cur.Values) p.InTree = tree.Contains(p.Pid);

        lock (_gate) {
          _anyRunning = anyRunning;
          if (activity) _lastActivityUtc = DateTime.UtcNow;
        }
        _prev = cur;
      } catch (Exception ex) {
        Log.Warn("agent watch poll failed: " + ex.Message);
      } finally {
        Interlocked.Exchange(ref _polling, 0);
      }
    }

    bool Matches(string nameLower) {
      foreach (string t in _names) {
        if (nameLower.Contains(t) || t.Contains(nameLower)) return true;
      }
      return false;
    }

    HashSet<int> BuildTree(Dictionary<int, Proc> map, ref bool anyRunning) {
      HashSet<int> tree = new HashSet<int>();
      foreach (Proc p in map.Values) {
        if (Matches(p.Name)) {
          anyRunning = true;
          tree.Add(p.Pid);
        }
      }
      if (tree.Count == 0) return tree;
      for (int i = 0; i < 16; i++) { // agent tool chains are shallow; 16 sweeps is plenty
        bool grew = false;
        foreach (Proc p in map.Values) {
          if (!tree.Contains(p.Pid) && tree.Contains(p.ParentPid)) {
            tree.Add(p.Pid);
            grew = true;
          }
        }
        if (!grew) break;
      }
      return tree;
    }

    Dictionary<int, Proc> Snapshot() {
      Dictionary<int, Proc> map = new Dictionary<int, Proc>();
      using (ManagementObjectSearcher s = new ManagementObjectSearcher(
        "SELECT ProcessId, ParentProcessId, Name, KernelModeTime, UserModeTime FROM Win32_Process")) {
        foreach (ManagementObject o in s.Get()) {
          try {
            Proc p = new Proc();
            p.Pid = Convert.ToInt32(o["ProcessId"]);
            p.ParentPid = Convert.ToInt32(o["ParentProcessId"]);
            p.Name = (o["Name"] ?? "").ToString().ToLowerInvariant();
            p.CpuMs = (Convert.ToInt64(o["KernelModeTime"]) + Convert.ToInt64(o["UserModeTime"])) / 10000;
            map[p.Pid] = p;
          } catch {}
        }
      }
      return map;
    }

    // IO counters only for tree processes; opening handles for every process on the
    // machine every poll would be wasteful.
    static void AddIo(Dictionary<int, Proc> map, HashSet<int> tree) {
      foreach (int pid in tree) {
        Proc p;
        if (!map.TryGetValue(pid, out p)) continue;
        IntPtr h = Native.OpenProcess(Native.ProcessQueryLimitedInformation, false, pid);
        if (h == IntPtr.Zero) continue;
        try {
          Native.IoCounters io;
          if (Native.GetProcessIoCounters(h, out io)) {
            p.IoBytes = (long)(io.ReadTransferCount + io.WriteTransferCount + io.OtherTransferCount);
          }
        } finally { Native.CloseHandle(h); }
      }
    }

    class Proc {
      public int Pid;
      public int ParentPid;
      public string Name;
      public long CpuMs;
      public long IoBytes;
      public bool InTree;
    }
  }
}
