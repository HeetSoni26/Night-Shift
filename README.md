<p align="center">
  <img src="icon/owl-logo.svg" width="160" alt="NightShift logo">
</p>

<h1 align="center">NightShift</h1>

<p align="center">
  <strong>Keep your Windows laptop awake with the lid closed, for timed agent sessions.</strong><br>
  The owl watches the night shift so your machine can keep working while you are away.
</p>

<p align="center">
  <a href="https://github.com/HeetSoni26/Night-Shift/releases/latest">
    <img alt="Download for Windows" src="https://img.shields.io/badge/Download-Windows%2010%2F%2011-0078D6?style=for-the-badge&logo=windows11&logoColor=white">
  </a>
</p>

<p align="center">
  <img alt="dependencies" src="https://img.shields.io/badge/dependencies-none-success">
  <img alt="admin" src="https://img.shields.io/badge/admin%20rights-not%20required-success">
  <img alt="runtime" src="https://img.shields.io/badge/runtime-.NET%20Framework%204.8%20(built%20in)-informational">
  <img alt="license" src="https://img.shields.io/badge/license-MIT-informational">
</p>

---

## The problem

Windows puts your laptop to sleep the moment you close the lid. That is exactly wrong when something is working for you in the background.

> You hand your AI coding agent a two-hour task, close the lid and walk away. Windows suspends the machine within seconds, the agent freezes mid-job, and you come back to half-finished work.

NightShift holds the machine awake for exactly as long as you asked, then quietly puts everything back the way it was.

## Download

Grab the latest build from the [releases page](https://github.com/HeetSoni26/Night-Shift/releases/latest):

| File | For |
|---|---|
| **`NightShift-Windows-x.x.x.zip`** | Recommended. App + double-click installer + uninstaller |
| **`NightShift.exe`** | Portable. The app alone, no installer |

Extract the zip and double-click **`install.cmd`**. That single step installs the app, registers the recovery task, adds a startup shortcut and starts the owl in your tray. From a terminal instead:

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

## Features

| | |
|---|---|
| **Timed sessions** | Click the owl, enter days / hours / minutes, press Enter. Add time or end early at any moment. |
| **Adaptive tray owl** | Black on a light taskbar, white on a dark one, following the system theme live. The owl inverts while a session runs. |
| **Journal-before-apply safety** | Every power change is written to disk, with its exact undo, *before* it is applied. |
| **Self-healing** | A scheduled recovery task undoes everything within a minute if the app ever dies mid-session. |
| **Quiet lid close** | Display off, audio muted, and optionally apps frozen (Slack, Discord, ...). Meeting and recording apps are never touched. |
| **Guards** | Low-power mode and session-end thresholds for battery; best-effort heat rules. |
| **Browser unthrottling** | One-click relaunch of a running Chromium browser with background throttling disabled. |
| **Zero footprint** | No admin rights, no SDK, no runtime install. One exe built with the compiler Windows already ships. |

## Usage

| You want to... | Do this |
|---|---|
| Start a session | Click the owl, type `d` / `h` / `m`, press **Enter** |
| Add time | Click the owl again, or use the `+15m` `+30m` `+1h` `+6h` buttons |
| End early | **Press and hold** the red hold-to-end control for 1.5 s |

Right-click the owl for status, add time, end early, *Relaunch browser unthrottled*, Settings, logs and Quit. Quitting during a session asks for confirmation and restores all settings first. When no session is running, NightShift changes nothing about how Windows behaves.

## How it works

1. **Start:** NightShift sets the lid close action to "do nothing" and idle sleep / hibernate to "never" for the duration. Each change is journaled with its undo command before it is applied.
2. **While you are away:** a heartbeat refreshes the journal every 15 seconds. Closing the lid quiets the machine (display off, mute, optional app freeze) while your work keeps running.
3. **If anything dies:** the `NightShiftRecovery` task runs `NightShift.exe --recover` every minute and uses the journal to undo everything, resume frozen apps, unmute and restore brightness by itself.
4. **Timer end:** all settings are restored exactly; if the lid is still closed with no external display, the machine is put to sleep.

## Configuration

Right-click the owl and open Settings: lid-close actions (display off, mute, app freezing), battery thresholds (low-power below 40% and end below 10% by default, 0 disables each), heat rules, and whether to sleep at session end when the lid is still closed.

| File | Purpose |
|---|---|
| `%LOCALAPPDATA%\NightShift\config.json` | Settings |
| `%LOCALAPPDATA%\NightShift\journal.json` | Recovery journal (transient, empty when idle) |
| `%LOCALAPPDATA%\NightShift\Logs\` | Daily logs, 14-day retention |

Command line: `--recover` (used by the task), `--selftest` (read-only diagnostics), `--test-power` (apply and undo power settings, verify the restore).

## Build from source

```powershell
git clone https://github.com/HeetSoni26/Night-Shift.git
cd Night-Shift
.\build.cmd
```

Compiles `src\*.cs` with the .NET Framework 4 C# compiler that ships with Windows and regenerates the tray icons from `icon\owl-source.png`. No SDK, no NuGet, no internet access needed.

## FAQ

<details>
<summary>Does it change my settings permanently?</summary>
No. Everything it touches is journaled and restored when the session ends, when you quit, or by the recovery task if the app ever crashes mid-session.
</details>

<details>
<summary>What happens on a desktop PC?</summary>
It works as an idle-sleep blocker. The lid features simply never trigger.
</details>

<details>
<summary>Is it safe to run overnight on battery?</summary>
The battery guards end the session at your thresholds, and the recovery task means the machine cannot stay stuck awake. As with any closed-lid run, keep the laptop on a hard, ventilated surface, never in a bag.
</details>

<details>
<summary>How do I remove it?</summary>
`uninstall.cmd` from the release zip, or `powershell -ExecutionPolicy Bypass -File .\uninstall.ps1` keeps your settings; add `-Purge` to wipe them too.
</details>

## License

MIT © [HeetSoni26](https://github.com/HeetSoni26). See [LICENSE](LICENSE).
