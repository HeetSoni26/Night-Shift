using System;
using System.Runtime.InteropServices;

namespace NightShift {
  // Master mute of the default output device, via Core Audio COM interop.
  static class Audio {
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    class MMDeviceEnumeratorCom { }

    // Vtable order matters; unused early methods are stubbed with the right arity.
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator {
      void EnumAudioEndpoints();                                                        // 1
      void GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);       // 2
      void GetDevice();                                                                 // 3
      void RegisterEndpointNotificationCallback();                                      // 4
      void UnregisterEndpointNotificationCallback();                                    // 5
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice {
      void Activate(ref Guid iid, int clsCtx, IntPtr activationParams,
        [MarshalAs(UnmanagedType.IUnknown)] out object iface);                          // 1
      void OpenPropertyStore();                                                         // 2
      void GetId();                                                                     // 3
      void GetState();                                                                  // 4
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioEndpointVolume {
      void RegisterControlChangeNotify();                                               // 1
      void UnregisterControlChangeNotify();                                             // 2
      void GetChannelCount();                                                           // 3
      void SetMasterVolumeLevel(float levelDb, ref Guid eventContext);                  // 4
      void SetMasterVolumeLevelScalar(float level, ref Guid eventContext);              // 5
      void GetMasterVolumeLevel(out float levelDb);                                     // 6
      void GetMasterVolumeLevelScalar(out float level);                                 // 7
      void SetChannelVolumeLevel(uint channel, float levelDb, ref Guid eventContext);   // 8
      void SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext); // 9
      void GetChannelVolumeLevel(uint channel, out float levelDb);                      // 10
      void GetChannelVolumeLevelScalar(uint channel, out float level);                  // 11
      void SetMute(bool mute, ref Guid eventContext);                                   // 12
      void GetMute(out bool mute);                                                      // 13
    }

    static IAudioEndpointVolume GetEndpointVolume() {
      object eo = new MMDeviceEnumeratorCom();
      IMMDeviceEnumerator en = (IMMDeviceEnumerator)eo;
      IMMDevice dev;
      en.GetDefaultAudioEndpoint(0 /* eRender */, 1 /* eMultimedia */, out dev);
      Guid iid = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
      object iface;
      dev.Activate(ref iid, 23 /* CLSCTX_ALL */, IntPtr.Zero, out iface);
      return (IAudioEndpointVolume)iface;
    }

    public static bool TryGetMute(out bool muted) {
      muted = false;
      try {
        IAudioEndpointVolume v = GetEndpointVolume();
        Guid ctx = Guid.Empty;
        bool m;
        v.GetMute(out m);
        muted = m;
        return true;
      } catch (Exception ex) { Log.Warn("audio read failed: " + ex.Message); return false; }
    }

    public static bool TrySetMute(bool mute) {
      try {
        IAudioEndpointVolume v = GetEndpointVolume();
        Guid ctx = Guid.Empty;
        v.SetMute(mute, ref ctx);
        return true;
      } catch (Exception ex) { Log.Warn("audio mute(" + mute + ") failed: " + ex.Message); return false; }
    }
  }
}
