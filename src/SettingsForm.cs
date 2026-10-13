using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace NightShift {
  class SettingsForm : Form {
    readonly Config _cfg;
    CheckBox _cDisplay, _cMute, _cFreeze, _cFreezeAll, _cSkipMeetings, _cThermal, _cSleepOnEnd, _cRecovery;
    TextBox _tFreezeApps;
    NumericUpDown _nLowPower, _nEndBattery, _nLowPowerDim, _nThermalHigh, _nThermalEnd;
    Button _btnRepair;

    public SettingsForm(Config cfg) {
      _cfg = cfg;
      Text = "NightShift - Settings";
      FormBorderStyle = FormBorderStyle.FixedDialog;
      MaximizeBox = false;
      MinimizeBox = false;
      StartPosition = FormStartPosition.CenterScreen;
      ClientSize = new Size(486, 586);
      Font = new Font("Segoe UI", 9f);
      try { Icon = IconFactory.OwlIcon(false); } catch {}

      GroupBox g1 = Group("When the lid closes during a session", 10, 10, 466, 152);
      _cDisplay = Check(g1, "Turn the display off (backlight to 0)", 14, 24);
      _cMute = Check(g1, "Mute audio", 14, 50);
      _cFreeze = Check(g1, "Freeze apps (comma separated)", 14, 76);
      _tFreezeApps = new TextBox();
      _tFreezeApps.Location = new Point(210, 74);
      _tFreezeApps.Size = new Size(240, 24);
      g1.Controls.Add(_tFreezeApps);
      _cFreezeAll = Check(g1, "Freeze every other app (skips system and meeting apps)", 34, 104);
      _cSkipMeetings = Check(g1, "Never freeze meeting/recording apps (Zoom, Teams, Webex...)", 34, 126);

      GroupBox g2 = Group("Battery", 10, 170, 466, 122);
      Label l1 = Plain(g2, "Low power mode below", 14, 26);
      _nLowPower = Num(g2, 160, 22, 100);
      Label l2 = Plain(g2, "%  (0 = off)", 220, 26);
      Label l3 = Plain(g2, "End session below", 14, 56);
      _nEndBattery = Num(g2, 160, 52, 100);
      Label l4 = Plain(g2, "%  (0 = off)", 220, 56);
      Label l5 = Plain(g2, "Low power dim to", 14, 86);
      _nLowPowerDim = Num(g2, 160, 82, 100);
      Label l6 = Plain(g2, "% panel brightness (no-overlay fallback)", 220, 86);

      GroupBox g3 = Group("Heat (best effort)", 10, 300, 466, 92);
      _cThermal = Check(g3, "Use WMI thermal zones (unsupported hardware is ignored)", 14, 24);
      Plain(g3, "Low power above", 14, 56);
      _nThermalHigh = Num(g3, 160, 52, 100);
      Plain(g3, "C", 220, 56);
      Plain(g3, "End above", 300, 56);
      _nThermalEnd = Num(g3, 370, 52, 70);
      Plain(g3, "C", 445, 56);

      GroupBox g4 = Group("Session end", 10, 400, 466, 62);
      _cSleepOnEnd = Check(g4, "Sleep if the lid is still closed and no external display", 14, 24);

      GroupBox g5 = Group("Recovery", 10, 470, 466, 66);
      _cRecovery = Check(g5, "1-minute recovery task (undoes settings if NightShift dies)", 14, 24);
      _btnRepair = new Button();
      _btnRepair.Text = "Reinstall task now";
      _btnRepair.Location = new Point(14, 46);
      _btnRepair.Size = new Size(150, 26);
      _btnRepair.Click += delegate { EnsureRecoveryTask(true); };
      g5.Controls.Add(_btnRepair);

      Button ok = new Button();
      ok.Text = "Save";
      ok.Size = new Size(90, 30);
      ok.Location = new Point(272, 514);
      ok.Click += delegate { SaveAndClose(); };
      Button cancel = new Button();
      cancel.Text = "Cancel";
      cancel.Size = new Size(90, 30);
      cancel.Location = new Point(372, 514);
      cancel.DialogResult = DialogResult.Cancel;
      Controls.Add(ok);
      Controls.Add(cancel);
      AcceptButton = ok;
      CancelButton = cancel;

      LoadValues();
    }

    GroupBox Group(string title, int x, int y, int w, int h) {
      GroupBox g = new GroupBox();
      g.Text = title;
      g.Location = new Point(x, y);
      g.Size = new Size(w, h);
      Controls.Add(g);
      return g;
    }

    CheckBox Check(GroupBox parent, string text, int x, int y) {
      CheckBox c = new CheckBox();
      c.Text = text;
      c.Location = new Point(x, y);
      c.AutoSize = true;
      parent.Controls.Add(c);
      return c;
    }

    Label Plain(Control parent, string text, int x, int y) {
      Label l = new Label();
      l.Text = text;
      l.Location = new Point(x, y);
      l.AutoSize = true;
      parent.Controls.Add(l);
      return l;
    }

    NumericUpDown Num(Control parent, int x, int y, int w) {
      NumericUpDown n = new NumericUpDown();
      n.Minimum = 0;
      n.Maximum = 200;
      n.Location = new Point(x, y);
      n.Size = new Size(w, 24);
      parent.Controls.Add(n);
      return n;
    }

    void LoadValues() {
      _cDisplay.Checked = _cfg.DisplayOffOnLidClose;
      _cMute.Checked = _cfg.MuteAudioOnLidClose;
      _cFreeze.Checked = _cfg.FreezeAppsOnLidClose;
      _tFreezeApps.Text = _cfg.FreezeApps ?? "";
      _cFreezeAll.Checked = _cfg.FreezeAllOtherApps;
      _cSkipMeetings.Checked = _cfg.SkipMeetingApps;
      _nLowPower.Value = Clamp(_cfg.LowPowerBatteryPercent);
      _nEndBattery.Value = Clamp(_cfg.EndBatteryPercent);
      _nLowPowerDim.Value = Clamp(_cfg.LowPowerBrightnessPercent);
      _cThermal.Checked = _cfg.ThermalGuard;
      _nThermalHigh.Value = Clamp((int)_cfg.ThermalHighC);
      _nThermalEnd.Value = Clamp((int)_cfg.ThermalEndC);
      _cSleepOnEnd.Checked = _cfg.SleepOnEndIfLidClosed;
      _cRecovery.Checked = _cfg.RecoveryTaskEnabled;
    }

    decimal Clamp(int v) { return Math.Max(0, Math.Min(200, v)); }

    void SaveAndClose() {
      _cfg.DisplayOffOnLidClose = _cDisplay.Checked;
      _cfg.MuteAudioOnLidClose = _cMute.Checked;
      _cfg.FreezeAppsOnLidClose = _cFreeze.Checked;
      _cfg.FreezeApps = _tFreezeApps.Text.Trim();
      _cfg.FreezeAllOtherApps = _cFreezeAll.Checked;
      _cfg.SkipMeetingApps = _cSkipMeetings.Checked;
      _cfg.LowPowerBatteryPercent = (int)_nLowPower.Value;
      _cfg.EndBatteryPercent = (int)_nEndBattery.Value;
      _cfg.LowPowerBrightnessPercent = (int)_nLowPowerDim.Value;
      _cfg.ThermalGuard = _cThermal.Checked;
      _cfg.ThermalHighC = (double)_nThermalHigh.Value;
      _cfg.ThermalEndC = (double)_nThermalEnd.Value;
      _cfg.SleepOnEndIfLidClosed = _cSleepOnEnd.Checked;
      _cfg.RecoveryTaskEnabled = _cRecovery.Checked;
      _cfg.Save();
      if (_cfg.RecoveryTaskEnabled) EnsureRecoveryTask(true);
      DialogResult = DialogResult.OK;
      Close();
    }

    void EnsureRecoveryTask(bool report) {
      try {
        string exe = CurrentExePath();
        bool ok = Recovery.CreateRecoveryTask(_cfg.RecoveryTaskName, exe);
        if (report) {
          MessageBox.Show(ok ? "Recovery task installed." : "Could not install the recovery task (see log).",
            "NightShift", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
      } catch (Exception ex) {
        Log.Error("recovery task install failed", ex);
        if (report) MessageBox.Show("Could not install the recovery task: " + ex.Message,
          "NightShift", MessageBoxButtons.OK, MessageBoxIcon.Warning);
      }
    }

    internal static string CurrentExePath() {
      try {
        if (Process.GetCurrentProcess().MainModule != null) return Process.GetCurrentProcess().MainModule.FileName;
      } catch {}
      return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NightShift.exe");
    }
  }
}
