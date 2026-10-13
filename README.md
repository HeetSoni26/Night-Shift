<p align="center"><img src="icon/NightShift-256.png" width="150" alt="NightShift owl"></p>

# NightShift for Windows

**Keep your laptop awake with the lid closed - for timed agent sessions.**

The owl lives in your system tray. Dark while idle; the whole owl turns amber while a session runs. Click it, set a time, close the lid, walk away - your work keeps running.

Built as a Windows port of [krishhgg/Insomnia](https://github.com/krishhgg/Insomnia) (macOS): same ideas, same safety model, rebuilt on Windows mechanisms. No admin rights, no SDK, no runtime to install.

---

## What is NightShift? (the simple version)

Your laptop is trained to sleep the moment you close the lid. Great for battery, terrible when something is *working* for you in the background.

**Real-life example:** you tell Claude Code (or Codex): *"Build this feature, write the tests, fix anything broken."* That's a 2-hour job. You don't want to watch it. But if you close the lid, Windows sleeps the laptop within seconds → your agent freezes mid-task → you come back to half-done work.

With NightShift:

1. Click the **owl** in the tray.
2. Type `0` days, `2` hours, `0` minutes → press **Enter**.
3. Close the lid and walk away.

The laptop stays awake with the lid closed. The agent keeps working the full 2 hours. When the timer ends, the laptop goes back to sleep on its own. You open the lid later and the work is done.

## How to use it - only 3 gestures

| You want to... | Do this |
|---|---|
| **Start a session** | Click the owl → type days / hours / minutes → **Enter** |
| **Add more time** | Click the owl again → type more time (or use the +15m / +30m / +1h / +6h buttons) |
| **Stop early** | Click the owl → **press and hold** the red *Hold to end* button for 1.5 s |

Right-click the owl for the menu: status, add time, end early, *Relaunch browser unthrottled*, Settings, open logs, Quit. While a session runs, hovering the owl shows the countdown.

## What happens inside (still simple)

**When you press Start**, NightShift asks Windows - politely, with no admin rights - to change three power settings for the duration: *never sleep when idle*, *never hibernate when idle*, and *if the lid closes: do nothing*. Before touching anything, it writes down what it changed **and how to change it back** in a journal file. Like borrowing your friend's bike and leaving a note: *"I took it, here's how to return it."*

**Every 15 seconds** it taps the note: "still alive."

**When you close the lid**, the session stays on but the laptop goes quiet: panel brightness to 0 and display off, audio muted, and (if you enable it in Settings) chatty apps like Slack, WhatsApp and Discord get frozen - a true pause, like `SIGSTOP` - so they can't wake anything or drain the battery. Your agent in the terminal keeps running at full speed. The timer keeps counting.

**When the timer ends**, everything goes back exactly the way it was: brightness, sound, frozen apps, power settings. And since the lid is still closed with no external display, the laptop puts itself to sleep. Normal life resumes.

## The safety net

- **Journal before apply.** Every change - with its exact undo command - is written to disk *before* it is applied.
- **Recovery agent.** A scheduled task (`NightShiftRecovery`) runs `NightShift.exe --recover` every minute. If the journal shows a live session but the app is dead (crash, kill, power loss), it puts every setting back, thaws frozen apps, unmutes and restores brightness - by itself, within a minute.
- **Never races the app.** While any NightShift process is alive, the recovery agent does nothing.
- **Battery guards.** Switch to a low-power mode below 40% (configurable), end the session below 10% (configurable, 0 disables each).
- **Heat guards** (best effort; many laptops expose no WMI thermal sensor).
- **Quit is protected.** Quitting during a session asks for confirmation and undoes everything.

## Good to know

- **When no session is running, NightShift does nothing.** Windows behaves exactly as it always did.
- **App freezing ships off by default** (Settings → lid-close actions) because it pauses real apps - opt in when you want it.
- **"Relaunch browser unthrottled"** restarts a running Chromium browser (Chrome/Edge/Brave/Arc/Opera/Vivaldi) with background-tab throttling disabled, so web-based agents keep full speed with the lid closed. It asks first; tabs are restored.
- **Settings** live at `%LOCALAPPDATA%\NightShift\config.json` (edited via the Settings dialog); logs at `%LOCALAPPDATA%\NightShift\Logs` with 14-day retention.

## Install / uninstall

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1              # build + install + start
powershell -ExecutionPolicy Bypass -File .\uninstall.ps1            # remove, keep settings
powershell -ExecutionPolicy Bypass -File .\uninstall.ps1 -Purge     # remove + wipe everything
```

Requirements: Windows 10 (1709+) or 11. Works on Modern Standby (S0) and classic S3 laptops. A desktop works too - as an idle-sleep blocker only.

## Command line

```
NightShift.exe              tray app
NightShift.exe --recover    undo pending changes (used by the recovery task)
NightShift.exe --selftest   read-only diagnostics
NightShift.exe --test-power apply and undo the power settings, verify the restore
```

## Differences from the macOS original

Honest list: no iPhone hotspot handoff and no tmux nudge (both macOS-specific); "Low Power Mode" is Windows' power-mode overlay, with panel dimming as fallback on systems without overlays; the mic is not hardware-disconnected on lid close (Apple Silicon feature). Added for Windows: monitor power-off, a `SetThreadExecutionState` hold as extra insurance on Modern Standby machines, and Chromium relaunch flags tuned for Windows throttling. Unlike the macOS version, **no admin rights are needed at all**.

## Under the hood (for the curious)

- Written in C# (WinForms), compiled with the .NET Framework 4 compiler that ships with Windows - `build.cmd` needs no SDK and no internet.
- Lid state, suspend/resume and AC/battery changes arrive via `RegisterPowerSettingNotification` (`GUID_LIDSWITCH_STATE_CHANGE`).
- Power settings via `powercfg` (hidden settings included - needed on Modern Standby laptops where the lid action is hidden).
- App freezing via `NtSuspendProcess` / `NtResumeProcess`; audio via Core Audio (`IAudioEndpointVolume`); brightness via WMI (`WmiSetBrightness`).
- `tools\GenIcon.cs` bakes the owl into `icon\NightShift.ico`; the tray icon is drawn at runtime by the same geometry.

## Safety note

The same caution as the original: a running timer is not a thermal guarantee. Keep the machine on a hard, ventilated surface - not a closed bag or blanket - when running with the lid closed.

## Credits

Concept and macOS original: [krishhgg/Insomnia](https://github.com/krishhgg/Insomnia) (MIT). Windows port rebranded as **NightShift**. MIT licensed.
