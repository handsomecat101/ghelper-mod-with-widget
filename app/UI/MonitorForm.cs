using GHelper.Helpers;
using GHelper.Mode;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace GHelper.UI
{
    public class MonitorForm : OSDNativeForm
    {
        private System.Windows.Forms.Timer timer;
        private bool isVisible = false;

        private List<float> cpuHistory = new();
        private List<float> gpuHistory = new();
        private const int MaxHistory = 50;

        private bool compactMode = false;
        private int fullHeight    = 248;
        private int compactHeight = 148;

        private Rectangle modeRect;
        private Rectangle fansRect;

        // Layout constants
        private const int W       = 310;   // widget width
        private const int ROW_H   = 32;    // row height
        private const int LABEL_X = 18;    // label left edge
        private const int VALUE_X = 110;   // value left edge
        private const int CORNER  = 20;    // rounded corner radius

        public MonitorForm()
        {
            Width  = W;
            Height = fullHeight;

            Screen screen = Screen.PrimaryScreen;
            int defaultX  = screen.WorkingArea.Right - Width - 20;
            int defaultY  = screen.WorkingArea.Top + 20;

            X = AppConfig.Get("monitor_x", defaultX);
            Y = AppConfig.Get("monitor_y", defaultY);
            compactMode = AppConfig.Get("monitor_compact", 0) == 1;

            if (compactMode) Height = compactHeight;

            ClickThrough = false;

            timer = new System.Windows.Forms.Timer();
            timer.Interval = 1000;
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        public override void Show()  { base.Show();  isVisible = true; }
        public override void Hide()  { base.Hide();  isVisible = false; }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (!isVisible) return;
            try
            {
                HardwareControl.ReadSensors();

                float cpuP = HardwareControl.cpuPower ?? 0;
                if (float.IsNaN(cpuP) || float.IsInfinity(cpuP)) cpuP = 0;
                cpuHistory.Add(cpuP);
                if (cpuHistory.Count > MaxHistory) cpuHistory.RemoveAt(0);

                float gpuP = (float)(HardwareControl.GpuControl?.GetGpuPower() ?? 0);
                if (float.IsNaN(gpuP) || float.IsInfinity(gpuP)) gpuP = 0;
                gpuHistory.Add(gpuP);
                if (gpuHistory.Count > MaxHistory) gpuHistory.RemoveAt(0);

                UpdateLayeredWindow();
            }
            catch (Exception ex) { Logger.WriteLine("Monitor Timer Error: " + ex.Message); }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_LBUTTONDOWN   = 0x0201;
            const int WM_RBUTTONDOWN   = 0x0204;
            const int WM_NCLBUTTONDOWN = 0x00A1;
            const int HTCAPTION        = 0x02;
            const int WM_EXITSIZEMOVE  = 0x0232;

            if (m.Msg == WM_LBUTTONDOWN)
            {
                POINT point = new POINT { x = Cursor.Position.X, y = Cursor.Position.Y };
                User32.ScreenToClient(Handle, ref point);
                Point pos = new Point(point.x, point.y);

                if (modeRect.Contains(pos)) { Program.modeControl.CyclePerformanceMode(); return; }
                if (fansRect.Contains(pos)) { Program.settingsForm.FansToggle(); return; }

                User32.ReleaseCapture();
                User32.SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
                return;
            }

            if (m.Msg == WM_RBUTTONDOWN)
            {
                compactMode = !compactMode;
                Height = compactMode ? compactHeight : fullHeight;
                AppConfig.Set("monitor_compact", compactMode ? 1 : 0);
                UpdateLayeredWindow();
                return;
            }

            if (m.Msg == WM_EXITSIZEMOVE)
            {
                AppConfig.Set("monitor_x", X);
                AppConfig.Set("monitor_y", Y);
            }

            base.WndProc(ref m);
        }

        // ── PAINT ────────────────────────────────────────────────────────────
        protected override void PerformPaint(PaintEventArgs e)
        {
            try
            {
                Graphics g = e.Graphics;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.SmoothingMode     = SmoothingMode.AntiAlias;

                // Theme colours
                int   mode      = Modes.GetCurrentBase();
                Color theme     = Color.White;
                Color backStart = Color.FromArgb(230, 20, 20, 25);

                if (mode == 1)       // Turbo
                {
                    theme     = Color.FromArgb(255, 80, 80);
                    backStart = Color.FromArgb(230, 60, 15, 15);
                }
                else if (mode == 2)  // Silent
                {
                    theme     = Color.FromArgb(80, 220, 80);
                    backStart = Color.FromArgb(230, 15, 50, 15);
                }
                else                 // Balanced
                {
                    theme     = Color.FromArgb(100, 160, 255);
                    backStart = Color.FromArgb(230, 18, 28, 50);
                }

                if (Width < 20 || Height < 20) return;

                // Background
                Rectangle bg = new Rectangle(0, 0, Width, Height);
                using (LinearGradientBrush bb = new LinearGradientBrush(
                           bg, backStart, Color.FromArgb(250, 5, 5, 10), 135f))
                using (GraphicsPath path = RoundedRect(bg, CORNER))
                {
                    if (path.PointCount > 0)
                    {
                        g.FillPath(bb, path);
                        using (Pen p1 = new Pen(Color.FromArgb(90, theme), 1.5f))
                            g.DrawPath(p1, path);
                        using (Pen p2 = new Pen(Color.FromArgb(25, theme), 3f))
                            g.DrawPath(p2, path);
                    }
                }

                // Sparklines (full mode only)
                if (!compactMode)
                {
                    DrawSparkline(g, cpuHistory, new Rectangle(10, 22, Width - 20, 36), Color.FromArgb(55, theme));
                    DrawSparkline(g, gpuHistory, new Rectangle(10, 62, Width - 20, 36), Color.FromArgb(55, Color.Cyan));
                }

                using Font fLabel = new Font("Segoe UI", 8f);
                using Font fValue = new Font("Cascadia Code", 10.5f, FontStyle.Bold);

                float y = 20;

                // CPU
                DrawRow(g, "CPU", BuildCpuLine(), y, fLabel, fValue, theme);
                y += ROW_H;

                // GPU / Battery (compact)
                if (compactMode)
                {
                    DrawRow(g, BuildBatLabel(), BuildBatValue(), y, fLabel, fValue,
                            HardwareControl.batteryRate > 0 ? Color.LightGreen : Color.White);
                    y += ROW_H;
                }
                else
                {
                    DrawRow(g, "GPU", BuildGpuLine(), y, fLabel, fValue, Color.Cyan);
                    y += ROW_H;
                }

                DrawSep(g, y - 6);

                if (!compactMode)
                {
                    // FANS
                    fansRect = new Rectangle(0, (int)y - 4, Width, ROW_H);
                    DrawRow(g, "FANS", BuildFansLine(), y, fLabel, fValue, Color.LightGray);
                    y += ROW_H;

                    // Battery
                    DrawRow(g, BuildBatLabel(), BuildBatValue(), y, fLabel, fValue,
                            HardwareControl.batteryRate > 0 ? Color.LightGreen : Color.White);
                    y += ROW_H;

                    // Mode
                    modeRect = new Rectangle(0, (int)y - 4, Width, ROW_H);
                    DrawRow(g, "MODE", Modes.GetCurrentName().ToUpper(), y, fLabel, fValue, theme);
                    y += ROW_H;

                    DrawSep(g, y - 6);
                }

                // Network (both modes)
                DrawNetRow(g, y, fLabel, fValue);
            }
            catch (Exception ex) { Logger.WriteLine("Monitor Paint Error: " + ex.Message); }
        }

        // ── DATA BUILDERS ────────────────────────────────────────────────────

        private string BuildCpuLine()
        {
            string w = $"{(HardwareControl.cpuPower ?? 0):F0}W";
            string t = HardwareControl.cpuTemp > 0 ? $"  {HardwareControl.cpuTemp:F0}°C" : "";
            string u = (HardwareControl.cpuUse ?? 0) > 0 ? $"  {HardwareControl.cpuUse}%" : "";
            return w + t + u;
        }

        private string BuildGpuLine()
        {
            int? pwr = HardwareControl.GpuControl?.GetGpuPower();
            if (pwr == null || pwr <= 0) return "OFF";
            string w = $"{pwr}W";
            string t = HardwareControl.gpuTemp > 0 ? $"  {HardwareControl.gpuTemp:F0}°C" : "";
            string u = (HardwareControl.gpuUse ?? 0) > 0 ? $"  {HardwareControl.gpuUse}%" : "";
            return w + t + u;
        }

        private string BuildFansLine()
        {
            string c  = HardwareControl.cpuFan?.Replace(" RPM", "") ?? "0";
            string gf = HardwareControl.gpuFan?.Replace(" RPM", "") ?? "0";
            string line = $"{c}  |  {gf}";
            if (!string.IsNullOrEmpty(HardwareControl.midFan) && HardwareControl.midFan != "0 RPM")
                line += $"  |  {HardwareControl.midFan?.Replace(" RPM", "")}";
            return line;
        }

        private string BuildBatLabel() => HardwareControl.batteryRate > 0 ? "Charging" : "Battery";

        private string BuildBatValue()
        {
            decimal draw = Math.Abs(HardwareControl.batteryRate ?? 0);
            string text  = $"{draw:F1}W";
            if (draw > 0.1m)
            {
                decimal rem = HardwareControl.chargeCapacity ?? 0;
                decimal mw  = draw * 1000;
                decimal hrs = HardwareControl.batteryRate > 0
                    ? ((HardwareControl.fullCapacity ?? 0) - rem) / mw
                    : rem / mw;
                if (hrs > 0 && hrs < 100)
                {
                    int h = (int)hrs, mi = (int)((hrs - h) * 60);
                    text += $"  ({h}h {mi}m)";
                }
            }
            return text;
        }

        // ── NETWORK: 2 lines ─────────────────────────────────────────────────
        // Line 1:  [dot] [SSID or OFFLINE]
        // Line 2:  [↓ dl in green]   [↑ ul in orange]
        private void DrawNetRow(Graphics g, float y, Font fLabel, Font fValue)
        {
            bool   online = NetworkControl.IsOnline;
            string ssid   = NetworkControl.WifiSSID;
            float  dl     = NetworkControl.DownloadSpeed;
            float  ul     = NetworkControl.UploadSpeed;

            Color dotClr = online ? Color.FromArgb(80, 220, 80) : Color.FromArgb(220, 70, 70);
            Color dlClr  = Color.FromArgb(60, 220, 120);   // ↓ green
            Color ulClr  = Color.FromArgb(255, 120, 60);   // ↑ orange

            // Line 1 – dot + SSID/OFFLINE
            using (SolidBrush db = new SolidBrush(dotClr))
                g.FillEllipse(db, LABEL_X, y + 7, 8, 8);

            string ssidLabel = !online
                ? "OFFLINE"
                : string.IsNullOrEmpty(ssid) ? "Connecting…"
                  : (ssid.Length > 22 ? ssid[..22] + "…" : ssid);

            using (SolidBrush sb = new SolidBrush(dotClr))
                g.DrawString(ssidLabel, fLabel, sb, LABEL_X + 13, y + 5);

            if (!online) return;

            // Line 2 – speeds (y + 22)
            float y2 = y + 22;
            string dlStr = "↓ " + NetworkControl.FormatSpeed(dl);
            string ulStr = "↑ " + NetworkControl.FormatSpeed(ul);

            using (SolidBrush dlBrush = new SolidBrush(dlClr))
                g.DrawString(dlStr, fValue, dlBrush, LABEL_X, y2);

            SizeF szUl = g.MeasureString(ulStr, fValue);
            float ulX  = Width - szUl.Width - LABEL_X;
            using (SolidBrush ulBrush = new SolidBrush(ulClr))
                g.DrawString(ulStr, fValue, ulBrush, ulX, y2);
        }

        // ── DRAW PRIMITIVES ──────────────────────────────────────────────────

        private void DrawRow(Graphics g, string label, string value, float y,
                             Font fLabel, Font fValue, Color valueColor)
        {
            using SolidBrush lb = new SolidBrush(Color.FromArgb(140, 150, 160));
            using SolidBrush vb = new SolidBrush(valueColor);
            g.DrawString(label, fLabel, lb, LABEL_X, y + 7);
            g.DrawString(value, fValue, vb, VALUE_X, y + 2);
        }

        private void DrawSep(Graphics g, float y)
        {
            using Pen p = new Pen(Color.FromArgb(35, 255, 255, 255), 1f);
            g.DrawLine(p, LABEL_X, y, Width - LABEL_X, y);
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
            path.AddArc(r.X,           r.Y,            d, d, 180, 90);
            path.AddArc(r.Right - d,   r.Y,            d, d, 270, 90);
            path.AddArc(r.Right - d,   r.Bottom - d,   d, d,   0, 90);
            path.AddArc(r.X,           r.Bottom - d,   d, d,  90, 90);
            path.CloseAllFigures();
            return path;
        }

        private void DrawSparkline(Graphics g, List<float> history, Rectangle rect, Color color)
        {
            if (history.Count < 2 || rect.Width <= 2 || rect.Height <= 2) return;
            float max = history.Max();
            if (max < 10 || float.IsNaN(max)) max = 10;

            using GraphicsPath path = new GraphicsPath();
            List<PointF> pts = new();
            for (int i = 0; i < history.Count; i++)
            {
                float v  = history[i];
                if (float.IsNaN(v) || float.IsInfinity(v)) v = 0;
                float px = rect.X + (float)i / (MaxHistory - 1) * rect.Width;
                float py = rect.Y + rect.Height - (v / max * rect.Height);
                if (!float.IsNaN(px) && !float.IsNaN(py)) pts.Add(new PointF(px, py));
            }
            if (pts.Count < 2) return;

            path.AddLines(pts.ToArray());
            using (Pen pen = new Pen(color, 1.5f)) g.DrawPath(pen, path);

            path.AddLine(rect.Right, rect.Bottom, rect.Left, rect.Bottom);
            path.CloseFigure();
            using LinearGradientBrush fill = new LinearGradientBrush(
                rect, Color.FromArgb(35, color), Color.Transparent, 90f);
            g.FillPath(fill, path);
        }
    }
}
