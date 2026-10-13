using System;
using System.Drawing;
using System.Windows.Forms;

namespace NightShift {
  // The flyout. Idle: enter days/hours/minutes, press Enter. Active: big countdown,
  // quick-add buttons, custom add, and the press-and-hold end control.
  class TimePopup : Form {
    static readonly Color Bg = Color.FromArgb(28, 33, 48);
    static readonly Color Fg = Color.FromArgb(232, 235, 244);
    static readonly Color Dim = Color.FromArgb(154, 163, 184);
    static readonly Color Accent = Color.FromArgb(244, 245, 248);
    static readonly Color Danger = Color.FromArgb(255, 93, 93);
    static readonly Color ButtonBg = Color.FromArgb(42, 49, 69);
    static readonly Color ButtonHover = Color.FromArgb(57, 66, 95);
    static readonly Color FieldBg = Color.FromArgb(18, 21, 31);

    const int IdleHeight = 200;
    const int ActiveHeight = 288;
    const int HoldTicksNeeded = 30; // 30 * 50ms = 1.5 s

    readonly SessionController _session;
    readonly Timer _holdTimer;
    int _holdTicks;

    Panel _idlePanel;
    Panel _activePanel;
    NumericUpDown _numD, _numH, _numM;
    Button _btnStart;
    Button _btnAdd;
    Label _lblCountdown;
    Label _lblEndsAt;
    Label _lblHint;
    Button _btnEnd;
    Label _lblHoldHint;
    HoldBar _holdBar;

    Point _dragOffset;
    bool _dragging;

    public TimePopup(SessionController session) {
      _session = session;
      FormBorderStyle = FormBorderStyle.None;
      ShowInTaskbar = false;
      StartPosition = FormStartPosition.Manual;
      TopMost = true;
      BackColor = Bg;
      DoubleBuffered = true;
      KeyPreview = true;
      Size = new Size(316, IdleHeight);
      Paint += delegate(object s, PaintEventArgs e) {
        using (Pen p = new Pen(Color.FromArgb(70, 80, 110))) {
          e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
      };
      _holdTimer = new Timer { Interval = 50 };
      _holdTimer.Tick += HoldTick;

      BuildIdle();
      BuildActive();
      UpdateState();
    }

    // ---------- construction ----------

    Label Lbl(string text, int x, int y, Color color, float size, bool bold) {
      Label l = new Label();
      l.Text = text;
      l.Location = new Point(x, y);
      l.AutoSize = true;
      l.ForeColor = color;
      l.BackColor = Color.Transparent;
      l.Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular);
      HookDrag(l);
      return l;
    }

    Button Btn(string text, int x, int y, int w, int h, Color back, Color fore) {
      Button b = new Button();
      b.Text = text;
      b.Location = new Point(x, y);
      b.Size = new Size(w, h);
      b.FlatStyle = FlatStyle.Flat;
      b.FlatAppearance.BorderSize = 0;
      b.FlatAppearance.MouseOverBackColor = ButtonHover;
      b.FlatAppearance.MouseDownBackColor = ButtonHover;
      b.BackColor = back;
      b.ForeColor = fore;
      b.Font = new Font("Segoe UI", 9.5f);
      b.TabStop = false;
      b.UseVisualStyleBackColor = false;
      return b;
    }

    NumericUpDown Num() {
      NumericUpDown n = new NumericUpDown();
      n.Minimum = 0;
      n.Maximum = 99;
      n.Value = 0;
      n.Size = new Size(56, 26);
      n.BackColor = FieldBg;
      n.ForeColor = Fg;
      n.BorderStyle = BorderStyle.FixedSingle;
      n.Font = new Font("Segoe UI", 10.5f);
      n.TextAlign = HorizontalAlignment.Center;
      return n;
    }

    void HookDrag(Control c) {
      c.MouseDown += delegate(object s, MouseEventArgs e) {
        if (e.Button == MouseButtons.Left) { _dragging = true; _dragOffset = e.Location; }
      };
      c.MouseMove += delegate(object s, MouseEventArgs e) {
        if (_dragging) {
          Point np = PointToScreen(e.Location);
          Location = new Point(np.X - _dragOffset.X, np.Y - _dragOffset.Y);
        }
      };
      c.MouseUp += delegate { _dragging = false; };
    }

    void BuildIdle() {
      _idlePanel = new Panel();
      _idlePanel.Location = new Point(0, 0);
      _idlePanel.Size = new Size(316, IdleHeight);
      _idlePanel.BackColor = Bg;
      HookDrag(_idlePanel);

      _idlePanel.Controls.Add(Lbl("NightShift", 16, 12, Fg, 12, true));
      _idlePanel.Controls.Add(Lbl("Keep awake for", 16, 44, Dim, 9.5f, false));

      _numD = Num(); _numD.Location = new Point(16, 68);
      _numH = Num(); _numH.Location = new Point(96, 68);
      _numM = Num(); _numM.Location = new Point(176, 68);
      Label ld = Lbl("d", 76, 72, Dim, 10, false);
      Label lh = Lbl("h", 156, 72, Dim, 10, false);
      Label lm = Lbl("m", 236, 72, Dim, 10, false);
      _idlePanel.Controls.Add(_numD);
      _idlePanel.Controls.Add(_numH);
      _idlePanel.Controls.Add(_numM);
      _idlePanel.Controls.Add(ld);
      _idlePanel.Controls.Add(lh);
      _idlePanel.Controls.Add(lm);

      _btnStart = Btn("Start", 16, 108, 284, 34, Accent, Color.FromArgb(24, 22, 12));
      _btnStart.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
      _btnStart.Click += delegate { DoStart(); };
      _idlePanel.Controls.Add(_btnStart);

      _lblHint = Lbl("Press Enter to start. Click the eye again to add time.", 16, 152, Dim, 8.75f, false);
      _idlePanel.Controls.Add(_lblHint);

      HookDrag(this);
      Controls.Add(_idlePanel);
    }

    void BuildActive() {
      _activePanel = new Panel();
      _activePanel.Location = new Point(0, 0);
      _activePanel.Size = new Size(316, ActiveHeight);
      _activePanel.BackColor = Bg;
      HookDrag(_activePanel);

      _activePanel.Controls.Add(Lbl("NightShift", 16, 12, Fg, 12, true));
      _lblEndsAt = Lbl("ends 00:00", 240, 16, Dim, 9, false);
      _activePanel.Controls.Add(_lblEndsAt);

      _lblCountdown = new Label();
      _lblCountdown.Text = "0d 00:00:00";
      _lblCountdown.Location = new Point(16, 42);
      _lblCountdown.AutoSize = true;
      _lblCountdown.ForeColor = Accent;
      _lblCountdown.Font = new Font("Consolas", 20, FontStyle.Bold);
      _lblCountdown.BackColor = Color.Transparent;
      HookDrag(_lblCountdown);
      _activePanel.Controls.Add(_lblCountdown);

      string[] quick = { "+15m", "+30m", "+1h", "+6h" };
      double[] minutes = { 15, 30, 60, 360 };
      for (int i = 0; i < quick.Length; i++) {
        string text = quick[i];
        double mins = minutes[i];
        Button b = Btn(text, 16 + i * 73, 88, 68, 30, ButtonBg, Fg);
        b.Click += delegate { _session.AddTime(TimeSpan.FromMinutes(mins)); };
        _activePanel.Controls.Add(b);
      }

      NumericUpDown nd = Num(); nd.Location = new Point(16, 130);
      NumericUpDown nh = Num(); nh.Location = new Point(84, 130);
      NumericUpDown nm = Num(); nm.Location = new Point(152, 130);
      Label ld2 = Lbl("d", 74, 134, Dim, 10, false);
      Label lh2 = Lbl("h", 142, 134, Dim, 10, false);
      Label lm2 = Lbl("m", 210, 134, Dim, 10, false);
      _btnAdd = Btn("Add", 234, 130, 66, 26, ButtonBg, Fg);
      _btnAdd.Click += delegate {
        _session.Start((int)nd.Value, (int)nh.Value, (int)nm.Value); // Active -> adds time
        Hide();
      };
      _activePanel.Controls.Add(nd);
      _activePanel.Controls.Add(nh);
      _activePanel.Controls.Add(nm);
      _activePanel.Controls.Add(ld2);
      _activePanel.Controls.Add(lh2);
      _activePanel.Controls.Add(lm2);
      _activePanel.Controls.Add(_btnAdd);

      _btnEnd = Btn("Hold to end", 16, 172, 180, 34, Color.FromArgb(70, 40, 44), Danger);
      _btnEnd.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
      _btnEnd.MouseDown += delegate { StartHold(); };
      _btnEnd.MouseUp += delegate { StopHold(); };
      _btnEnd.MouseLeave += delegate { StopHold(); };
      _activePanel.Controls.Add(_btnEnd);

      _lblHoldHint = Lbl("hold 1.5 s", 204, 181, Dim, 8.75f, false);
      _activePanel.Controls.Add(_lblHoldHint);

      _holdBar = new HoldBar();
      _holdBar.Location = new Point(16, 214);
      _holdBar.Size = new Size(284, 10);
      _activePanel.Controls.Add(_holdBar);

      _activePanel.Controls.Add(Lbl("Ending early wakes your apps and restores sleep.", 16, 236, Dim, 8.75f, false));
      _activePanel.Controls.Add(Lbl("Esc closes. Drag anywhere to move.", 16, 256, Dim, 8.75f, false));

      Controls.Add(_activePanel);
    }

    class HoldBar : Panel {
      public int Percent;
      public HoldBar() {
        DoubleBuffered = true;
        BackColor = FieldBg;
      }
      public void Set(int p) { Percent = p; Invalidate(); }
      protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        if (Percent > 0) {
          using (SolidBrush b = new SolidBrush(Danger)) {
            int w = (int)(Width * Percent / 100.0);
            e.Graphics.FillRectangle(b, 0, 0, w, Height);
          }
        }
      }
    }

    // ---------- behavior ----------

    public void UpdateState() {
      bool active = _session.Active;
      if (active) {
        _lblCountdown.Text = _session.RemainingText();
        _lblEndsAt.Text = "ends " + _session.EndsUtc.ToLocalTime().ToString("HH:mm");
        _idlePanel.Visible = false;
        _activePanel.Visible = true;
        if (ClientSize.Height != ActiveHeight) ClientSize = new Size(316, ActiveHeight);
        AcceptButton = _btnAdd;
      } else {
        _activePanel.Visible = false;
        _idlePanel.Visible = true;
        if (ClientSize.Height != IdleHeight) ClientSize = new Size(316, IdleHeight);
        AcceptButton = _btnStart;
      }
      Invalidate();
    }

    public void ShowNearTray() {
      UpdateState();
      Point anchor = Cursor.Position;
      Rectangle wa = Screen.FromPoint(anchor).WorkingArea;
      int x = anchor.X - Width + 24;
      int y = anchor.Y - Height - 12;
      x = Math.Max(wa.Left + 8, Math.Min(x, wa.Right - Width - 8));
      y = Math.Max(wa.Top + 8, Math.Min(y, wa.Bottom - Height - 8));
      Location = new Point(x, y);
      Show();
      if (_session.Active) _btnAdd.Focus();
      else { _numM.Focus(); }
    }

    void DoStart() {
      string err = _session.Start((int)_numD.Value, (int)_numH.Value, (int)_numM.Value);
      if (err != null) {
        _lblHint.Text = err;
        _lblHint.ForeColor = Danger;
        return;
      }
      Hide();
    }

    void StartHold() {
      _holdTicks = 0;
      _btnEnd.Text = "Keep holding...";
      _holdTimer.Start();
    }

    void StopHold() {
      _holdTimer.Stop();
      _holdTicks = 0;
      _holdBar.Set(0);
      _btnEnd.Text = "Hold to end";
    }

    void HoldTick(object sender, EventArgs e) {
      _holdTicks++;
      int pct = Math.Min(100, _holdTicks * 100 / HoldTicksNeeded);
      _holdBar.Set(pct);
      if (_holdTicks >= HoldTicksNeeded) {
        StopHold();
        _session.EndEarly("user ended (press-and-hold)");
        Hide();
      }
    }

    protected override void OnDeactivate(EventArgs e) {
      base.OnDeactivate(e);
      StopHold();
      Hide();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData) {
      if (keyData == Keys.Escape) {
        StopHold();
        Hide();
        return true;
      }
      return base.ProcessCmdKey(ref msg, keyData);
    }
  }
}
