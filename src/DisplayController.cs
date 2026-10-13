using System;
using System.Management;
using System.Runtime.InteropServices;

namespace NightShift {
  static class Display {
    // Panel brightness via WMI; only laptop panels with driver support answer here.
    public static int? GetBrightness() {
      try {
        using (ManagementObjectSearcher s = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightness")) {
          foreach (ManagementObject o in s.Get()) {
            object v = o["CurrentBrightness"];
            if (v != null) return Convert.ToInt32(v);
          }
        }
      } catch {}
      return null;
    }

    public static bool SetBrightness(int level) {
      try {
        using (ManagementObjectSearcher s = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightnessMethods")) {
          foreach (ManagementObject o in s.Get()) {
            o.InvokeMethod("WmiSetBrightness", new object[] { (uint)1, (byte)level });
            return true;
          }
        }
      } catch (Exception ex) { Log.Warn("SetBrightness(" + level + ") failed: " + ex.Message); }
      return false;
    }

    public static void TurnOffMonitor() {
      try {
        IntPtr result;
        Native.SendMessageTimeout(Native.HwndBroadcast, Native.WmSysCommand,
          (IntPtr)Native.ScMonitorPower, (IntPtr)2, Native.SmtoAbortIfHung, 1000, out result);
      } catch (Exception ex) { Log.Warn("TurnOffMonitor failed: " + ex.Message); }
    }
  }
}
