using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Murmur.Core;

namespace Murmur.Windows;

public sealed class RecordingOverlay : Form
{
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 33 };
    private string state = "idle", label = "Ready";
    private float level;
    private readonly System.Diagnostics.Stopwatch motion = new();
    public event Action? Toggle, Cancel, Open;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string DockEdge { get; set; } = "bottom";
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Animation { get; set; } = "on";
    private bool Animate => Animation == "on" || (Animation == "system" && !ReducedMotion());
    public RecordingOverlay()
    {
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; BackColor = Color.FromArgb(24, 24, 24); ForeColor = Color.White; DoubleBuffered = true; Size = new(210, 42);
        timer.Tick += (_, _) => Invalidate();
        MouseDown += (_, e) => { if (e.Button == MouseButtons.Right) Open?.Invoke(); else if (e.X > ClientSize.Width - 38 && state != "idle") Cancel?.Invoke(); else Toggle?.Invoke(); };
        AccessibleName = "Murmur recording controls"; AccessibleDescription = "Click to start or stop. Right click to open Murmur. Right side cancels.";
    }
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ExStyle |= 0x08000000 | 0x00000080; return cp; } }
    protected override void WndProc(ref Message m) { if (m.Msg == 0x21) { m.Result = 3; return; } base.WndProc(ref m); }
    public void UpdateState(string value, string text, float inputLevel = 0)
    {
        if (value is "processing" or "starting" && state != value) motion.Restart();
        state = value; label = text; level = inputLevel;
        if (state is not ("processing" or "starting")) motion.Stop();
        AccessibleName = $"Murmur: {text}";
        var work = Screen.FromPoint(Cursor.Position).WorkingArea;
        int margin = (int)(12 * DeviceDpi / 96f);
        Location = DockEdge switch { "left" => new(work.Left + margin, work.Top + (work.Height - Height) / 2), "right" => new(work.Right - Width - margin, work.Top + (work.Height - Height) / 2), _ => new(work.Left + (work.Width - Width) / 2, work.Bottom - Height - margin) };
        timer.Interval = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline ? 67 : 33;
        bool animate = value is "processing" or "starting" && Animate;
        timer.Enabled = animate && Visible;
        Invalidate();
    }
    private static bool ReducedMotion() => Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop\WindowMetrics", "MinAnimate", "1") as string == "0";
    protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); timer.Enabled = Visible && state is "processing" or "starting" && Animate; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(state == "recording" ? Color.FromArgb(255, 181, 126) : Color.White, 1.6f);
        if (state is "processing" or "starting")
        {
            using var ink = new SolidBrush(Color.White);
            foreach (var d in ThinkingOrb.Frame(timer.Enabled ? motion.Elapsed.TotalSeconds : .6))
            {
                int gray = (int)Math.Round((1 - Math.Clamp(d.White, 0, 1)) * 255);
                ink.Color = Color.FromArgb((int)Math.Round(d.A * 255), gray, gray, gray);
                e.Graphics.FillEllipse(ink, 15 + (float)(d.X - d.R), 11 + (float)(d.Y - d.R), (float)d.R * 2, (float)d.R * 2);
            }
        }
        else for (int i = 0; i < 5; i++) { float h = state == "recording" ? 4 + level * 22 * (.6f + i % 3 * .2f) : 4; e.Graphics.DrawLine(pen, 16 + i * 4, 21 - h / 2, 16 + i * 4, 21 + h / 2); }
        using var font = new Font("Segoe UI", 9); TextRenderer.DrawText(e.Graphics, label, font, new Rectangle(45, 0, Width - 80, Height), Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if (state != "idle") { e.Graphics.DrawLine(pen, Width - 26, 17, Width - 18, 25); e.Graphics.DrawLine(pen, Width - 18, 17, Width - 26, 25); }
    }
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e); if (Height < 2) return;
        using var path = new GraphicsPath(); path.AddArc(0, 0, Height - 1, Height - 1, 90, 180); path.AddArc(Width - Height, 0, Height - 1, Height - 1, 270, 180); path.CloseFigure();
        var previous = Region; Region = new Region(path); previous?.Dispose();
    }
    protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
}
