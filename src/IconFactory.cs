using System;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace NightShift {
  // The owl, from the logo image. tools\GenIcon.cs bakes icon\owl-source.png into two
  // multi-size .ico files that build.cmd embeds into the exe as resources: gray-navy
  // owl when idle, amber owl while a session runs.
  static class IconFactory {
    static Icon _idle;
    static Icon _active;

    public static Icon OwlIcon(bool active) {
      if (active) {
        if (_active == null) _active = Load("NightShiftActive.ico");
        return _active;
      }
      if (_idle == null) _idle = Load("NightShiftIdle.ico");
      return _idle;
    }

    static Icon Load(string name) {
      using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)) {
        if (s == null) throw new InvalidOperationException("embedded icon missing: " + name);
        return new Icon(s);
      }
    }
  }
}
