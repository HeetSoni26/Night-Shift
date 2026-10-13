using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NightShift {
  // Hidden top-level window that receives WM_POWERBROADCAST: lid switch state changes,
  // suspend/resume, and power status (AC/battery) changes. A plain hidden window is used
  // on purpose - message-only windows do not receive power broadcasts.
  class PowerEventsWatcher : Form {
    IntPtr _lidNotify;
    public bool Registered { get; private set; }
    public bool LidCurrentlyClosed { get; private set; }

    public event Action<bool> LidChanged = delegate { };
    public event Action Suspended = delegate { };
    public event Action Resumed = delegate { };
    public event Action PowerStatusChanged = delegate { };
    public event Action ThemeChanged = delegate { };

    public PowerEventsWatcher() {
      FormBorderStyle = FormBorderStyle.None;
      ShowInTaskbar = false;
      StartPosition = FormStartPosition.Manual;
      Location = new Point(-3000, -3000);
      Size = new Size(1, 1);
      Opacity = 0;
    }

    public void Start() {
      Guid g = Native.GuidLidSwitchStateChange;
      IntPtr h = Handle; // force handle creation; the window never needs to be shown
      _lidNotify = Native.RegisterPowerSettingNotification(h, ref g, 0 /* window handle */);
      Registered = _lidNotify != IntPtr.Zero;
      if (!Registered) Log.Warn("lid notification registration failed");
      else Log.Info("power events watcher running");
    }

    public void Stop() {
      if (_lidNotify != IntPtr.Zero) {
        try { Native.UnregisterPowerSettingNotification(_lidNotify); } catch {}
        _lidNotify = IntPtr.Zero;
      }
    }

    protected override void WndProc(ref Message m) {
      if (m.Msg == Native.WmPowerBroadcast) {
        long wParam = m.WParam.ToInt64();
        try {
          if (wParam == Native.PbtPowerSettingChange) {
            Native.PowerBroadcastSetting s = (Native.PowerBroadcastSetting)Marshal.PtrToStructure(
              m.LParam, typeof(Native.PowerBroadcastSetting));
            if (s.PowerSetting == Native.GuidLidSwitchStateChange && s.DataLength >= 1) {
              bool closed = Marshal.ReadByte(m.LParam, Native.PbsDataOffset) != 0;
              if (closed != LidCurrentlyClosed) {
                LidCurrentlyClosed = closed;
                Log.Info("lid " + (closed ? "closed" : "opened"));
                LidChanged(closed);
              }
            }
          } else if (wParam == Native.PbtApmSuspend) {
            Log.Info("system suspending");
            Suspended();
          } else if (wParam == Native.PbtApmResumeAutomatic || wParam == Native.PbtApmResumeSuspend) {
            Log.Info("system resumed");
            Resumed();
          } else if (wParam == Native.PbtApmPowerStatusChange) {
            PowerStatusChanged();
          }
        } catch (Exception ex) { Log.Error("power event handling failed", ex); }
      } else if (m.Msg == 0x001A /* WM_SETTINGCHANGE */) {
        // "ImmersiveColorSet" is broadcast when the light/dark system theme flips.
        try {
          string what = m.LParam == IntPtr.Zero ? null : Marshal.PtrToStringUni(m.LParam);
          if (what == "ImmersiveColorSet") {
            Log.Info("system theme changed");
            ThemeChanged();
          }
        } catch {}
      }
      base.WndProc(ref m);
    }
  }
}
