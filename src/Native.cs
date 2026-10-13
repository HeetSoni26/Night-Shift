using System;
using System.Runtime.InteropServices;

namespace NightShift {
  static class Native {
    // Process freeze (Nt*Suspend/Resume takes PROCESS_SUSPEND_RESUME handles only).
    public const uint ProcessSuspendResume = 0x0800;
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr h);
    [DllImport("ntdll.dll")]
    public static extern uint NtSuspendProcess(IntPtr handle);
    [DllImport("ntdll.dll")]
    public static extern uint NtResumeProcess(IntPtr handle);

    // Power setting notifications (lid switch state, sleep/resume broadcasts).
    public const int WmPowerBroadcast = 0x0218;
    public const int PbtPowerSettingChange = 0x8013;
    public const int PbtApmSuspend = 0x0004;
    public const int PbtApmResumeAutomatic = 0x0007;
    public const int PbtApmResumeSuspend = 0x0008;
    public const int PbtApmPowerStatusChange = 0x000A;
    public static readonly Guid GuidLidSwitchStateChange = new Guid("BA3E0F4D-B817-4094-A2D1-D56379E6A0F3");
    // POWERBROADCAST_SETTING: GUID (0..15), DWORD DataLength (16..19), flexible byte Data at 20.
    public const int PbsDataOffset = 20;
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PowerBroadcastSetting { public Guid PowerSetting; public uint DataLength; public byte Data; }
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr RegisterPowerSettingNotification(IntPtr recipient, ref Guid powerSettingGuid, int flags);
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterPowerSettingNotification(IntPtr handle);

    // Turn the monitor off (the software equivalent of closing the lid on the backlight).
    public static readonly IntPtr HwndBroadcast = new IntPtr(0xffff);
    public const uint WmSysCommand = 0x0112;
    public const uint ScMonitorPower = 0xF170;
    public const uint SmtoAbortIfHung = 0x0002;
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    // Sleep at session end when the lid is still closed.
    [DllImport("powrprof.dll", SetLastError = true)]
    public static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    // Belt-and-braces against idle sleep while a session runs (Modern Standby machines
    // in particular). Never sets ES_DISPLAY_REQUIRED - the display must be allowed to
    // turn off. Called on the UI thread, which lives as long as the session does.
    public const uint EsContinuous = 0x80000000;
    public const uint EsSystemRequired = 0x00000001;
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint SetThreadExecutionState(uint esFlags);

    // Global hotkey (Ctrl+Alt+N by default).
    public const int WmHotkey = 0x0312;
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const int HotkeyStartId = 1;
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // Per-process IO counters, used by the agent-watch activity detection.
    [StructLayout(LayoutKind.Sequential)]
    public struct IoCounters {
      public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
      public ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }
    public const uint ProcessQueryLimitedInformation = 0x1000;
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetProcessIoCounters(IntPtr hProcess, out IoCounters counters);

    [StructLayout(LayoutKind.Sequential)]
    public struct SystemPowerStatus {
      public byte AcLineStatus;       // 0 = offline, 1 = online, 255 = unknown
      public byte BatteryFlag;
      public byte BatteryLifePercent; // 0-100, 255 = unknown
      public byte Reserved0;
      public int BatteryLifeTime;
      public int BatteryFullLifeTime;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetSystemPowerStatus(ref SystemPowerStatus sps);

    // SetSuspendState wants SeShutdownPrivilege enabled on the calling token.
    public const uint TokenAdjustPrivileges = 0x20;
    public const uint TokenQuery = 0x08;
    public const int SePrivilegeEnabled = 0x02;
    public const string SeShutdownPrivilege = "SeShutdownPrivilege";
    [DllImport("kernel32.dll")]
    public static extern IntPtr GetCurrentProcess();
    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool OpenProcessToken(IntPtr process, uint desiredAccess, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool LookupPrivilegeValue(string systemName, string privilegeName, out long luid);
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TokPriv1Luid { public int PrivilegeCount; public long Luid; public int Attributes; }
    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAllPrivileges, ref TokPriv1Luid newState, int bufferLength, IntPtr previousState, IntPtr returnLength);

    public static bool EnableShutdownPrivilege() {
      IntPtr token;
      if (!OpenProcessToken(GetCurrentProcess(), TokenAdjustPrivileges | TokenQuery, out token)) return false;
      try {
        TokPriv1Luid tp;
        tp.PrivilegeCount = 1;
        if (!LookupPrivilegeValue(null, SeShutdownPrivilege, out tp.Luid)) return false;
        tp.Attributes = SePrivilegeEnabled;
        return AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
      } finally { CloseHandle(token); }
    }
  }
}
