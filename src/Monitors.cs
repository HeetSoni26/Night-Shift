using System;
using System.Management;

namespace NightShift {
  static class Monitors {
    public static bool OnBattery() {
      Native.SystemPowerStatus sps = new Native.SystemPowerStatus();
      if (!Native.GetSystemPowerStatus(ref sps)) return false;
      return sps.AcLineStatus == 0; // 0 = offline, 1 = online, 255 = unknown
    }

    // 255 means "unknown" in the Win32 SYSTEM_POWER_STATUS.
    public static int? BatteryPercent() {
      Native.SystemPowerStatus sps = new Native.SystemPowerStatus();
      if (!Native.GetSystemPowerStatus(ref sps)) return null;
      if (sps.BatteryLifePercent == 255) return null;
      return sps.BatteryLifePercent;
    }

    // WMI thermal zones are motherboard zones on many machines and missing on others;
    // this is best effort; many machines expose no usable sensor.
    public static double? TemperatureC() {
      try {
        using (ManagementObjectSearcher s = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature")) {
          double hottest = double.MinValue;
          foreach (ManagementObject o in s.Get()) {
            object v = o["CurrentTemperature"];
            if (v != null) {
              double c = Convert.ToDouble(v) / 10.0 - 273.15;
              if (c > hottest) hottest = c;
            }
          }
          if (hottest > double.MinValue) return hottest;
        }
      } catch {}
      return null;
    }
  }
}
