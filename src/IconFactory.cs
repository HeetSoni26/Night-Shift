using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace NightShift {
  // The owl from the logo image, adaptive like the logo itself: black owl on a light
  // taskbar, white owl on a dark taskbar. While a session runs the owl inverts, with a
  // thin contrast ring so the inverted state stays readable on the same background.
  // tools\GenIcon.cs bakes icon\owl-source.png into the four multi-size .ico files that
  // build.cmd embeds into the exe as resources.
  static class IconFactory {
    static Icon _idleLight;
    static Icon _idleDark;
    static Icon _activeLight;
    static Icon _activeDark;

    // The taskbar follows the system theme (not the app theme).
    public static bool SystemUsesDarkTheme() {
      try {
        using (RegistryKey k = Registry.CurrentUser.OpenSubKey(
          "Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize")) {
          if (k != null) {
            object v = k.GetValue("SystemUsesLightTheme");
            if (v is int) return (int)v == 0;
          }
        }
      } catch {}
      return false;
    }

    public static Icon OwlIcon(bool active) {
      bool dark = SystemUsesDarkTheme();
      if (dark) return active ? Get(ref _activeDark, "NightShiftActiveDark.ico")
                              : Get(ref _idleDark, "NightShiftIdleDark.ico");
      return active ? Get(ref _activeLight, "NightShiftActiveLight.ico")
                    : Get(ref _idleLight, "NightShiftIdleLight.ico");
    }

    // Called when the system theme flips (light <-> dark): drop the cached icons so
    // the next OwlIcon call reloads the matching polarity.
    public static void ClearCache() {
      Dispose(ref _idleLight);
      Dispose(ref _idleDark);
      Dispose(ref _activeLight);
      Dispose(ref _activeDark);
    }

    static Icon Get(ref Icon cache, string name) {
      if (cache == null) cache = Load(name);
      return cache;
    }

    static void Dispose(ref Icon cache) {
      if (cache != null) { try { cache.Dispose(); } catch {} cache = null; }
    }

    static Icon Load(string name) {
      using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)) {
        if (s == null) throw new InvalidOperationException("embedded icon missing: " + name);
        return new Icon(s);
      }
    }
  }
}
