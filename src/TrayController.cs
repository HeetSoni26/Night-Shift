using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace NightShift {
  // The eye that lives in the tray. No dock icon equivalent, no main window: left click
  // opens the time popup, right click opens the menu.
  class TrayController : IDisposable {
    readonly Config _cfg;
    readonly SessionController _session;
    readonly Action _exitRequest;
    NotifyIcon _icon;
    ContextMenuStrip _menu;
    ToolStripMenuItem _mStatus;
    ToolStripMenuItem _mToggle;
    ToolStripMenuItem _mEnd;
    ToolStripMenuItem _mBrowser;
    ToolStripMenuItem _mQuit;
    TimePopup _popup;
    SettingsForm _settings;

    public TrayController(Config cfg, SessionController session, Action exitRequest) {
      _cfg = cfg;
      _session = session;
      _exitRequest = exitRequest;
      _menu = BuildMenu();
      _icon = new NotifyIcon();
      _icon.Icon = IconFactory.OwlIcon(_session.Active);
      _icon.Text = "NightShift - idle";
      _icon.ContextMenuStrip = _menu;
      _icon.Visible = true;
      _icon.MouseClick += OnTrayClick;
      _menu.Opening += MenuOpening;
      if (!_cfg.FirstRunSeen) {
        try {
          _icon.ShowBalloonTip(5000, "NightShift", "Watching from the tray. Click the owl to keep your PC awake with the lid closed.", ToolTipIcon.Info);
        } catch {}
        _cfg.FirstRunSeen = true;
        _cfg.Save();
      }
    }

    ContextMenuStrip BuildMenu() {
      ContextMenuStrip menu = new ContextMenuStrip();
      _mStatus = new ToolStripMenuItem("Idle");
      _mStatus.Enabled = false;
      _mToggle = new ToolStripMenuItem("Keep awake for...");
      _mEnd = new ToolStripMenuItem("End early...");
      _mEnd.Enabled = false;
      _mBrowser = new ToolStripMenuItem("Relaunch browser unthrottled");
      _mBrowser.Enabled = false;
      _mQuit = new ToolStripMenuItem("Quit");
      _mToggle.Click += delegate { ShowPopup(); };
      _mEnd.Click += delegate { ShowPopup(); };
      _mQuit.Click += delegate { Quit(); };
      _mBrowser.DropDownOpening += BrowserDropOpening;
      menu.Items.Add(_mStatus);
      menu.Items.Add(new ToolStripSeparator());
      menu.Items.Add(_mToggle);
      menu.Items.Add(_mEnd);
      menu.Items.Add(new ToolStripSeparator());
      menu.Items.Add(_mBrowser);
      menu.Items.Add(new ToolStripSeparator());
      menu.Items.Add(new ToolStripMenuItem("Settings...", null, delegate { OpenSettings(); }));
      menu.Items.Add(new ToolStripMenuItem("Open logs folder", null, delegate {
        try { System.Diagnostics.Process.Start("explorer.exe", "\"" + Paths.LogsDir + "\""); } catch {}
      }));
      menu.Items.Add(new ToolStripSeparator());
      menu.Items.Add(_mQuit);
      return menu;
    }

    void MenuOpening(object sender, System.ComponentModel.CancelEventArgs e) {
      _mStatus.Text = _session.Active
        ? "Awake until " + _session.EndsUtc.ToLocalTime().ToString("HH:mm") + "  (" + _session.RemainingText() + " left)"
        : "Idle";
      _mToggle.Text = _session.Active ? "Add time..." : "Keep awake for...";
      _mEnd.Enabled = _session.Active;
    }

    void BrowserDropOpening(object sender, EventArgs e) {
      _mBrowser.DropDownItems.Clear();
      List<BrowserRelaunch.BrowserInfo> list = BrowserRelaunch.Detect();
      if (list.Count == 0) {
        ToolStripItem item = _mBrowser.DropDownItems.Add("No running browser detected");
        item.Enabled = false;
        return;
      }
      foreach (BrowserRelaunch.BrowserInfo info in list) {
        string name = info.Name;
        ToolStripItem item = _mBrowser.DropDownItems.Add(name);
        item.Click += delegate {
          DialogResult r = MessageBox.Show(
            "Quits " + name + " and relaunches it with timer throttling disabled.\r\nTabs are restored on relaunch. Continue?",
            "Relaunch unthrottled", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
          if (r == DialogResult.Yes) {
            try { BrowserRelaunch.RelaunchUnthrottled(info); }
            catch (Exception ex) { Log.Error("browser relaunch failed", ex); }
          }
        };
      }
    }

    void OnTrayClick(object sender, MouseEventArgs e) {
      if (e.Button == MouseButtons.Left) ShowPopup();
    }

    public void ShowPopup() {
      if (_popup == null || _popup.IsDisposed) _popup = new TimePopup(_session);
      _popup.ShowNearTray();
    }

    void OpenSettings() {
      if (_settings != null && !_settings.IsDisposed) {
        _settings.Activate();
        return;
      }
      _settings = new SettingsForm(_cfg);
      _settings.ShowDialog();
      _settings.Dispose();
      _settings = null;
    }

    void Quit() {
      if (_session.Active) {
        DialogResult r = MessageBox.Show(
          "A session is running (" + _session.RemainingText() + " left).\r\nEnd it and quit?",
          "NightShift", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
        if (r != DialogResult.Yes) return;
        _session.EndEarly("quit requested");
      }
      _exitRequest();
    }

    public void UpdateVisual() {
      if (_icon == null) return;
      bool active = _session.Active;
      _icon.Icon = IconFactory.OwlIcon(active); // adaptive: black on light taskbar, white on dark
      _icon.Text = active ? "NightShift - " + _session.RemainingText() + " left" : "NightShift - idle";
      if (_popup != null && !_popup.IsDisposed && _popup.Visible) _popup.UpdateState();
    }

    public void Dispose() {
      try { _icon.Visible = false; _icon.Dispose(); } catch {}
      _icon = null;
    }
  }
}
