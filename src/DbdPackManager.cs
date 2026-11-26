// FogForge 4.0 (vorher DBD Custom Pack Manager) – Icon-Packs für Dead by Daylight aktivieren, wechseln und entladen
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("FogForge")]
[assembly: System.Reflection.AssemblyProduct("FogForge – Icon-Packs für Dead by Daylight")]
[assembly: System.Reflection.AssemblyDescription("Icon-Packs für Dead by Daylight aktivieren, wechseln und entladen")]
[assembly: System.Reflection.AssemblyCompany("FogForge")]
[assembly: System.Reflection.AssemblyCopyright("© 2026 FogForge – inoffizielles Fan-Tool")]
[assembly: System.Reflection.AssemblyVersion(DbdCustom.AppInfo.Version + ".0")]
[assembly: System.Reflection.AssemblyFileVersion(DbdCustom.AppInfo.Version + ".0")]

namespace DbdCustom
{
    // Programmversion – bei jedem Release erhöhen und als Tag v<Version> auf GitHub veröffentlichen
    static class AppInfo { public const string Version = "4.3.2"; }

    // ============================================================ Design
    static class UI
    {
        public static float Scale = 1f;
        public static int S(float v) { return (int)Math.Round(v * Scale); }
        public static float Sf(float v) { return v * Scale; }

        public static readonly Color Bg = Color.FromArgb(10, 9, 12);
        public static readonly Color Panel = Color.FromArgb(200, 14, 13, 17);
        public static readonly Color Card = Color.FromArgb(26, 25, 30);
        public static readonly Color Line = Color.FromArgb(48, 46, 54);
        public static readonly Color Red = Color.FromArgb(222, 38, 46);
        public static readonly Color RedHi = Color.FromArgb(255, 70, 70);
        public static readonly Color RedDark = Color.FromArgb(140, 16, 24);
        public static readonly Color Orange = Color.FromArgb(240, 140, 30);
        public static readonly Color Text = Color.FromArgb(245, 244, 248);
        public static readonly Color Sub = Color.FromArgb(160, 158, 170);
        public static readonly Color Dim = Color.FromArgb(105, 103, 115);
        public static readonly Color Green = Color.FromArgb(52, 211, 100);

        static string family;
        public static Font F(float size, FontStyle st = FontStyle.Regular)
        {
            if (family == null)
            {
                family = "Segoe UI";
                using (var fc = new InstalledFontCollection())
                    foreach (var n in new[] { "Segoe UI Variable Display", "Segoe UI Variable Text", "Segoe UI" })
                        if (fc.Families.Any(f => f.Name == n)) { family = n; break; }
            }
            return new Font(family, size, st, GraphicsUnit.Point);
        }

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath(); float d = Math.Max(1, Math.Min(rad * 2, Math.Min(r.Width, r.Height)));
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure(); return p;
        }
        public static Color A(Color c, int a) { return Color.FromArgb(Math.Max(0, Math.Min(255, a)), c); }
        public static Color Mix(Color a, Color b, float t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb((int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }
        public static Font FitF(Graphics g, string s, float size, FontStyle st, float maxW)
        {
            var f = F(size, st);
            while (size > 7.5f && g.MeasureString(s, f).Width > maxW) { f.Dispose(); size -= 0.5f; f = F(size, st); }
            return f;
        }
        public static void HQ(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.CompositingQuality = CompositingQuality.AssumeLinear; // kein Gamma-Mischen: sonst werden halbtransparente Flächen unter Windows viel zu hell
        }
        public static RectangleF Cover(Size s, RectangleF box)
        {
            float k = Math.Max(box.Width / s.Width, box.Height / s.Height);
            float w = s.Width * k, h = s.Height * k;
            return new RectangleF(box.X + (box.Width - w) / 2, box.Y + (box.Height - h) / 2, w, h);
        }
        public static void Txt(Graphics g, string s, Font f, Color c, RectangleF r, StringAlignment h = StringAlignment.Near, StringAlignment v = StringAlignment.Center)
        {
            using (var b = new SolidBrush(c))
            using (var sf = new StringFormat { Alignment = h, LineAlignment = v, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(s, f, b, r, sf);
        }
        public static Bitmap Fit(Image src, Size sz, float anchorY = 0.06f)
        {
            var b = new Bitmap(Math.Max(1, sz.Width), Math.Max(1, sz.Height), PixelFormat.Format32bppPArgb);
            var r = Cover(src.Size, new RectangleF(0, 0, b.Width, b.Height));
            r.Y = (b.Height - r.Height) * anchorY; // oben ausrichten – Köpfe bleiben sichtbar, abgeschnitten wird unten
            using (var g = Graphics.FromImage(b)) { HQ(g); g.DrawImage(src, r); }
            return b;
        }
        public static Bitmap Contain(Image src, Size sz)
        {
            var b = Blur(src, sz, 120);
            float k = Math.Min((float)sz.Width / src.Width, (float)sz.Height / src.Height);
            int w = (int)Math.Round(src.Width * k), h = (int)Math.Round(src.Height * k);
            int x = (sz.Width - w) / 2, y = (sz.Height - h) / 2;
            var im = new Bitmap(Math.Max(1, w), Math.Max(1, h), PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(im)) { HQ(g); g.DrawImage(src, 0, 0, w, h); }
            int fadeW = Math.Min(w / 6, S(48));
            if (x > 0 && fadeW > 0)
            {
                var d = im.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                var px = new int[w * h]; Marshal.Copy(d.Scan0, px, 0, px.Length);
                for (int yy = 0; yy < h; yy++)
                    for (int i = 0; i < fadeW; i++)
                    {
                        float a = (float)i / fadeW; a = a * a * (3 - 2 * a);
                        int i1 = yy * w + i, i2 = yy * w + (w - 1 - i);
                        int p1 = px[i1]; px[i1] = (p1 & 0xFFFFFF) | ((int)(((p1 >> 24) & 255) * a) << 24);
                        int p2 = px[i2]; px[i2] = (p2 & 0xFFFFFF) | ((int)(((p2 >> 24) & 255) * a) << 24);
                    }
                Marshal.Copy(px, 0, d.Scan0, px.Length); im.UnlockBits(d);
            }
            using (var g = Graphics.FromImage(b)) { g.CompositingQuality = CompositingQuality.AssumeLinear; g.DrawImageUnscaled(im, x, y); }
            im.Dispose(); return b;
        }
        public static Bitmap Rounded(Image src, Size sz, int rad, bool heroGradient)
        {
            var full = heroGradient ? Fit(src, sz, 0.35f) : Fit(src, sz);
            var b = new Bitmap(sz.Width, sz.Height, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(b))
            {
                HQ(g);
                using (var tb = new TextureBrush(full)) using (var p = Round(new RectangleF(0, 0, sz.Width, sz.Height), rad)) g.FillPath(tb, p);
                if (heroGradient)
                    using (var p = Round(new RectangleF(0, 0, sz.Width, sz.Height), rad))
                    using (var lg = new LinearGradientBrush(new Rectangle(0, 0, sz.Width, sz.Height), Color.Black, Color.Black, 90f))
                    {
                        lg.InterpolationColors = new ColorBlend { Colors = new[] { Color.FromArgb(0, 8, 6, 10), Color.FromArgb(0, 8, 6, 10), Color.FromArgb(200, 8, 6, 10) }, Positions = new[] { 0f, 0.62f, 1f } };
                        g.FillPath(lg, p);
                    }
            }
            full.Dispose(); return b;
        }
        public static Bitmap Blur(Image src, Size target, int darken)
        {
            // echtes Weichzeichnen: klein rechnen, Box-Blur (3 Durchgänge), schrittweise hochskalieren
            int sw = Math.Max(16, target.Width / 8), sh = Math.Max(16, target.Height / 8);
            var small = new Bitmap(sw, sh, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(small)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(src, Cover(src.Size, new RectangleF(0, 0, sw, sh))); }
            var bd = small.LockBits(new Rectangle(0, 0, sw, sh), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            var px = new byte[bd.Stride * sh]; Marshal.Copy(bd.Scan0, px, 0, px.Length);
            int rad = 4;
            for (int pass = 0; pass < 3; pass++)
            {
                var tmp = (byte[])px.Clone();
                for (int y = 0; y < sh; y++) for (int x = 0; x < sw; x++) for (int c = 0; c < 3; c++)
                { int sum = 0, n = 0; for (int k = -rad; k <= rad; k++) { int xx = Math.Max(0, Math.Min(sw - 1, x + k)); sum += px[y * bd.Stride + xx * 4 + c]; n++; } tmp[y * bd.Stride + x * 4 + c] = (byte)(sum / n); }
                for (int y = 0; y < sh; y++) for (int x = 0; x < sw; x++) for (int c = 0; c < 3; c++)
                { int sum = 0, n = 0; for (int k = -rad; k <= rad; k++) { int yy = Math.Max(0, Math.Min(sh - 1, y + k)); sum += tmp[yy * bd.Stride + x * 4 + c]; n++; } px[y * bd.Stride + x * 4 + c] = (byte)(sum / n); }
            }
            Marshal.Copy(px, 0, bd.Scan0, px.Length); small.UnlockBits(bd);
            Bitmap cur = small;
            while (cur.Width * 2 < target.Width)
            {
                var nb = new Bitmap(cur.Width * 2, cur.Height * 2);
                using (var g = Graphics.FromImage(nb)) { g.InterpolationMode = InterpolationMode.HighQualityBilinear; g.PixelOffsetMode = PixelOffsetMode.Half; g.DrawImage(cur, 0, 0, nb.Width, nb.Height); }
                cur.Dispose(); cur = nb;
            }
            var res = new Bitmap(target.Width, target.Height, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(res))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBilinear; g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(cur, 0, 0, res.Width, res.Height);
                using (var b = new SolidBrush(Color.FromArgb(darken, 8, 6, 10))) g.FillRectangle(b, 0, 0, res.Width, res.Height);
                using (var lg = new LinearGradientBrush(new Rectangle(0, 0, res.Width, res.Height), Color.FromArgb(40, 0, 0, 0), Color.FromArgb(200, 6, 5, 8), 90f)) g.FillRectangle(lg, 0, 0, res.Width, res.Height);
            }
            cur.Dispose();
            return res;
        }
    }

    // ============================================================ Daten
    class Pack
    {
        public string Name, Dir;
        public bool Available; public PayloadFile Source; public string SourceDir; public Remote.RPack Rem; public bool Update;     // noch nicht installiert, aus Installer/ZIP verfügbar
        public Dictionary<string, int> Counts = new Dictionary<string, int>();
        public int Total { get { return Counts.Values.Sum(); } }
        public Image Preview; public Bitmap Backdrop, Card, Hero; public bool IsNew, Stale;
    }

    // Klickbarer Bereich mit weicher Hover-Animation
    class Hot
    {
        public string Id; public RectangleF R; public Action Click; public bool Enabled = true;
        public float H; public string Tip;
    }

    // ============================================================ Dunkler Dialog
    class DarkDialog : Form
    {
        string title, msg; string[] buttons; int result = -1; List<Hot> hots = new List<Hot>(); Hot over; Timer anim;
        TextBox box; Color accent;
        DarkDialog(string title, string msg, string[] buttons, Color accent, string longText)
        {
            this.title = title; this.msg = msg; this.buttons = buttons; this.accent = accent;
            FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
            BackColor = UI.Bg; DoubleBuffered = true; KeyPreview = true;
            int w = UI.S(longText != null ? 720 : 460);
            using (var g = CreateGraphics()) using (var f = UI.F(10.5f))
            {
                // breit genug für alle Knöpfe
                float need; using (var fb = UI.F(10.5f, FontStyle.Bold)) need = buttons.Sum(bt => g.MeasureString(bt, fb).Width + UI.S(44)) + UI.S(10) * (buttons.Length - 1) + UI.S(56);
                w = Math.Max(w, (int)Math.Ceiling(need));
                var sz = g.MeasureString(msg, f, w - UI.S(56));
                Size = new Size(w, (int)sz.Height + UI.S(150) + (longText != null ? UI.S(330) : 0));
            }
            if (longText != null)
            {
                box = new TextBox
                {
                    Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None,
                    BackColor = Color.FromArgb(20, 19, 24), ForeColor = UI.Sub, Font = new Font("Consolas", 9f), Text = longText,
                    Bounds = new Rectangle(UI.S(28), Height - UI.S(400), w - UI.S(56), UI.S(310))
                };
                Controls.Add(box);
            }
            anim = new Timer { Interval = 15 };
            anim.Tick += (s, e) => { bool more = false; foreach (var h in hots) { float t = h == over ? 1 : 0; if (Math.Abs(h.H - t) > 0.01f) { h.H += (t - h.H) * 0.3f; more = true; } else h.H = t; } Invalidate(); if (!more) anim.Stop(); };
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { result = buttons.Length - 1; Close(); } if (e.KeyCode == Keys.Enter) { result = 0; Close(); } };
        }
        protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; return cp; } }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); MainForm.Rounded(Handle); }
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e); Opacity = 0;
            var ft = new Timer { Interval = 15 }; ft.Tick += (s, a) => { Opacity = Math.Min(1, Opacity + 0.14); if (Opacity >= 1) { ft.Stop(); ft.Dispose(); } }; ft.Start();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; UI.HQ(g);
            using (var p = new Pen(UI.Line)) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            using (var b = new SolidBrush(accent)) g.FillRectangle(b, 0, 0, Width, UI.S(3));
            float x = UI.S(28);
            using (var f = UI.F(14f, FontStyle.Bold)) UI.Txt(g, title, f, UI.Text, new RectangleF(x, UI.S(22), Width - 2 * x, UI.S(34)));
            using (var f = UI.F(10.5f)) using (var b = new SolidBrush(UI.Sub))
                g.DrawString(msg, f, b, new RectangleF(x, UI.S(64), Width - 2 * x, Height));
            hots.Clear();
            float bx = Width - x, by = Height - UI.S(66), bh = UI.S(42);
            for (int i = buttons.Length - 1; i >= 0; i--)
            {
                float bw; using (var f = UI.F(10.5f, FontStyle.Bold)) bw = g.MeasureString(buttons[i], f).Width + UI.S(44);
                bx -= bw; var r = new RectangleF(bx, by, bw, bh); bx -= UI.S(10);
                int idx = i; var h = new Hot { Id = "b" + i, R = r, Click = () => { result = idx; Close(); } };
                var old = hotsPrev.FirstOrDefault(o => o.Id == h.Id); if (old != null) h.H = old.H;
                hots.Add(h);
                bool primary = i == 0;
                using (var path = UI.Round(r, bh / 2))
                {
                    var c = primary ? UI.Mix(accent, Color.White, h.H * 0.15f) : UI.Mix(Color.FromArgb(38, 37, 44), Color.FromArgb(54, 52, 62), h.H);
                    using (var b = new SolidBrush(c)) g.FillPath(b, path);
                }
                using (var f = UI.F(10.5f, FontStyle.Bold)) UI.Txt(g, buttons[i], f, UI.Text, r, StringAlignment.Center);
            }
            hotsPrev = hots.ToList();
        }
        List<Hot> hotsPrev = new List<Hot>();
        protected override void OnMouseMove(MouseEventArgs e)
        {
            var h = hots.FirstOrDefault(x => x.R.Contains(e.Location)); if (h != over) { over = h; anim.Start(); }
            Cursor = h != null ? Cursors.Hand : Cursors.Default;
        }
        protected override void OnMouseDown(MouseEventArgs e) { if (over == null && e.Button == MouseButtons.Left) MainForm.DragWindow(Handle); }
        protected override void OnMouseClick(MouseEventArgs e) { var h = hots.FirstOrDefault(x => x.R.Contains(e.Location)); if (h != null) h.Click(); }
        protected override void Dispose(bool d) { if (d) anim.Dispose(); base.Dispose(d); }

        public static int Ask(IWin32Window owner, string title, string msg, Color accent, params string[] buttons)
        {
            using (var d = new DarkDialog(title, msg, buttons, accent, null)) { d.ShowDialog(owner); return d.result; }
        }
        public static void Info(IWin32Window owner, string title, string msg) { Ask(owner, title, msg, UI.Red, L.T("OK")); }
        public static void ShowText(IWin32Window owner, string title, string msg, string text)
        {
            using (var d = new DarkDialog(title, msg, new[] { L.T("Schließen") }, UI.Red, text)) d.ShowDialog(owner);
        }
    }

    // ============================================================ Dunkles Menü
    class DarkColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Color.FromArgb(24, 23, 28); } }
        public override Color ImageMarginGradientBegin { get { return Color.FromArgb(24, 23, 28); } }
        public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(24, 23, 28); } }
        public override Color ImageMarginGradientEnd { get { return Color.FromArgb(24, 23, 28); } }
        public override Color MenuBorder { get { return UI.Line; } }
        public override Color MenuItemBorder { get { return Color.FromArgb(60, 222, 38, 46); } }
        public override Color MenuItemSelected { get { return Color.FromArgb(52, 30, 34); } }
        public override Color SeparatorDark { get { return UI.Line; } }
        public override Color SeparatorLight { get { return UI.Line; } }
    }

    // ============================================================ Eigenes Menü
    class PopupMenu : Form
    {
        class Item { public string Icon, Text, Flag; public Action Act; public bool Sep, Enabled = true, Check; public RectangleF R; public float H; }
        public float MenuWidth = 330;
        List<Item> items = new List<Item>(); Item over; Timer anim;
        public PopupMenu()
        {
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; TopMost = true;
            BackColor = Color.FromArgb(20, 19, 24); DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            anim = new Timer { Interval = 16 };
            anim.Tick += (s, e) => { bool more = false; foreach (var i in items) { float t = i == over ? 1 : 0; if (Math.Abs(i.H - t) > 0.01f) { i.H += (t - i.H) * 0.3f; more = true; } else i.H = t; } Invalidate(); if (!more) anim.Stop(); };
            Deactivate += (s, e) => Close();
            KeyPreview = true; KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        }
        public void Add(string icon, string text, Action act, bool enabled = true) { items.Add(new Item { Icon = icon, Text = text, Act = act, Enabled = enabled }); }
        public void Sep() { items.Add(new Item { Sep = true }); }
        public void AddFlag(string flag, string text, Action act, bool check) { items.Add(new Item { Flag = flag, Text = text, Act = act, Check = check }); }
        protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; cp.ExStyle |= 0x80; return cp; } }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); MainForm.Rounded(Handle); }
        public void ShowAt(Form owner, Point screenRightTop)
        {
            float y = UI.S(8), w = UI.S(MenuWidth);
            foreach (var i in items) { float h = i.Sep ? UI.S(11) : UI.S(40); i.R = new RectangleF(UI.S(6), y, w - UI.S(12), h); y += h; }
            Size = new Size((int)w, (int)(y + UI.S(8)));
            Location = new Point(screenRightTop.X - Width, screenRightTop.Y);
            Opacity = 0; Show(owner);
            var ft = new Timer { Interval = 15 }; ft.Tick += (s, e) => { Opacity = Math.Min(1, Opacity + 0.16); if (Opacity >= 1) { ft.Stop(); ft.Dispose(); } }; ft.Start();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; UI.HQ(g);
            using (var p = new Pen(Color.FromArgb(60, 255, 255, 255))) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            using (var b = new SolidBrush(UI.Red)) g.FillRectangle(b, 0, 0, Width, UI.S(2));
            foreach (var i in items)
            {
                if (i.Sep) { using (var p = new Pen(Color.FromArgb(40, 255, 255, 255))) g.DrawLine(p, i.R.X + UI.S(10), i.R.Y + i.R.Height / 2, i.R.Right - UI.S(10), i.R.Y + i.R.Height / 2); continue; }
                if (i.H > 0.01f)
                {
                    using (var path = UI.Round(i.R, UI.S(9))) using (var b = new SolidBrush(Color.FromArgb((int)(60 * i.H), 222, 38, 46))) g.FillPath(b, path);
                    using (var b = new SolidBrush(UI.A(UI.Red, (int)(255 * i.H)))) g.FillRectangle(b, i.R.X, i.R.Y + UI.S(10), UI.S(3), i.R.Height - UI.S(20));
                }
                var col = i.Enabled ? UI.Mix(UI.Sub, UI.Text, 0.6f + 0.4f * i.H) : UI.Dim;
                if (i.Flag != null) L.Flag(g, i.Flag, new RectangleF(i.R.X + UI.S(14), i.R.Y + i.R.Height / 2 - UI.S(8), UI.S(24), UI.S(16)));
                else using (var f = new Font("Segoe UI Symbol", 11f)) UI.Txt(g, i.Icon, f, i.Enabled ? UI.Mix(UI.Sub, UI.RedHi, i.H) : UI.Dim, new RectangleF(i.R.X + UI.S(12), i.R.Y, UI.S(26), i.R.Height), StringAlignment.Center);
                if (i.Check)
                    using (var pen = new Pen(UI.RedHi, UI.Sf(2.2f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                    { float cx = i.R.Right - UI.S(26), cy = i.R.Y + i.R.Height / 2; g.DrawLines(pen, new[] { new PointF(cx - UI.S(6), cy), new PointF(cx - UI.S(2), cy + UI.S(4)), new PointF(cx + UI.S(6), cy - UI.S(5)) }); }
                using (var f = UI.F(10f, FontStyle.Bold)) UI.Txt(g, i.Text, f, col, new RectangleF(i.R.X + UI.S(46), i.R.Y, i.R.Width - UI.S(52), i.R.Height));
            }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            var i = items.FirstOrDefault(x => !x.Sep && x.R.Contains(e.Location));
            if (i != over) { over = i; anim.Start(); }
            Cursor = i != null && i.Enabled ? Cursors.Hand : Cursors.Default;
        }
        protected override void OnMouseLeave(EventArgs e) { over = null; anim.Start(); }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            var i = items.FirstOrDefault(x => !x.Sep && x.R.Contains(e.Location));
            if (i != null && i.Enabled) { Close(); i.Act(); }
        }
        protected override void Dispose(bool d) { if (d) anim.Dispose(); base.Dispose(d); }
    }

    // ============================================================ Hilfe & FAQ
    class HelpDialog : Form
    {
        int sel; int over = -1; bool overClose; List<RectangleF> qr = new List<RectangleF>(); RectangleF closeR;
        public HelpDialog()
        {
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(16, 15, 20); Size = new Size(UI.S(920), UI.S(580)); Text = L.T("Hilfe");
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            KeyPreview = true; KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); else if (e.KeyCode == Keys.Down) { sel = Math.Min(L.FaqQ.Length - 1, sel + 1); Invalidate(); } else if (e.KeyCode == Keys.Up) { sel = Math.Max(0, sel - 1); Invalidate(); } };
        }
        protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; return cp; } }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); MainForm.Rounded(Handle); }
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e); Opacity = 0;
            var ft = new Timer { Interval = 15 }; ft.Tick += (s, a) => { Opacity = Math.Min(1, Opacity + 0.14); if (Opacity >= 1) { ft.Stop(); ft.Dispose(); } }; ft.Start();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; UI.HQ(g); int W = ClientSize.Width, H = ClientSize.Height, th = UI.S(60);
            g.Clear(BackColor);
            using (var p = new Pen(Color.FromArgb(55, 255, 255, 255))) g.DrawRectangle(p, 0, 0, W - 1, H - 1);
            using (var b = new SolidBrush(Color.FromArgb(10, 9, 13))) g.FillRectangle(b, 0, 0, W, th);
            using (var lg = new LinearGradientBrush(new RectangleF(0, th - 2, W, 2), UI.A(UI.Red, 0), UI.A(UI.Red, 220), 0f))
            { lg.InterpolationColors = new ColorBlend { Colors = new[] { UI.A(UI.Red, 0), UI.A(UI.Red, 230), UI.A(UI.Red, 0) }, Positions = new[] { 0f, 0.35f, 1f } }; g.FillRectangle(lg, 0, th - 2, W, 2); }
            var qc = new RectangleF(UI.S(20), th / 2f - UI.S(15), UI.S(30), UI.S(30));
            using (var b = new LinearGradientBrush(qc, UI.RedHi, UI.RedDark, 90f)) g.FillEllipse(b, qc);
            using (var f = UI.F(12f, FontStyle.Bold)) UI.Txt(g, "?", f, Color.White, qc, StringAlignment.Center);
            using (var f = UI.F(13f, FontStyle.Bold)) UI.Txt(g, L.T("Hilfe & häufige Fragen"), f, UI.Text, new RectangleF(UI.S(62), 0, W - UI.S(140), th));
            closeR = new RectangleF(W - UI.S(56), 0, UI.S(56), th - 2);
            if (overClose) using (var b = new SolidBrush(UI.Red)) g.FillRectangle(b, closeR);
            using (var p = new Pen(UI.Text, UI.Sf(1.4f))) { float s5 = UI.S(6); var c = new PointF(closeR.X + closeR.Width / 2, closeR.Y + closeR.Height / 2); g.DrawLine(p, c.X - s5, c.Y - s5, c.X + s5, c.Y + s5); g.DrawLine(p, c.X + s5, c.Y - s5, c.X - s5, c.Y + s5); }

            // Fragenliste mit Nummern
            float lx = UI.S(14), lw = UI.S(330), y = th + UI.S(14); qr.Clear();
            using (var f = UI.F(10f, FontStyle.Bold)) using (var fn = UI.F(8.5f, FontStyle.Bold))
                for (int i = 0; i < L.FaqQ.Length; i++)
                {
                    string q = L.T(L.FaqQ[i]);
                    float h = Math.Max(UI.S(42), g.MeasureString(q, f, (int)(lw - UI.S(64))).Height + UI.S(18));
                    var r = new RectangleF(lx, y, lw, h); qr.Add(r);
                    bool isSel = i == sel, isOver = i == over;
                    if (isSel || isOver)
                        using (var path = UI.Round(r, UI.S(10))) using (var b = new SolidBrush(isSel ? Color.FromArgb(60, 222, 38, 46) : Color.FromArgb(20, 255, 255, 255))) g.FillPath(b, path);
                    if (isSel) using (var path = UI.Round(r, UI.S(10))) using (var p = new Pen(UI.A(UI.Red, 120), 1f)) g.DrawPath(p, path);
                    var nc = new RectangleF(r.X + UI.S(12), r.Y + r.Height / 2 - UI.S(11), UI.S(22), UI.S(22));
                    using (var b = new SolidBrush(isSel ? UI.Red : Color.FromArgb(30, 255, 255, 255))) g.FillEllipse(b, nc);
                    UI.Txt(g, (i + 1).ToString(), fn, isSel ? Color.White : UI.Sub, nc, StringAlignment.Center);
                    using (var b = new SolidBrush(isSel ? UI.Text : (isOver ? UI.Text : UI.Sub)))
                    using (var sf = new StringFormat { LineAlignment = StringAlignment.Center })
                        g.DrawString(q, f, b, new RectangleF(r.X + UI.S(44), r.Y, lw - UI.S(56), h), sf);
                    y += h + UI.S(4);
                }
            using (var p = new Pen(Color.FromArgb(30, 255, 255, 255))) g.DrawLine(p, lx + lw + UI.S(14), th + UI.S(14), lx + lw + UI.S(14), H - UI.S(44));

            // Antwort
            float ax = lx + lw + UI.S(38), aw = W - ax - UI.S(32), ay = th + UI.S(22);
            using (var f = UI.F(15.5f, FontStyle.Bold))
            {
                string q = L.T(L.FaqQ[sel]); var sz = g.MeasureString(q, f, (int)aw);
                using (var b = new SolidBrush(UI.Text)) g.DrawString(q, f, b, new RectangleF(ax, ay, aw, sz.Height + 4)); ay += sz.Height + UI.S(8);
            }
            using (var lg = new LinearGradientBrush(new RectangleF(ax, ay, UI.S(60), UI.S(3)), UI.RedHi, UI.A(UI.Red, 0), 0f)) g.FillRectangle(lg, ax, ay, UI.S(60), UI.S(3));
            ay += UI.S(20);
            var oc = g.Clip; g.SetClip(new RectangleF(ax - UI.S(4), th, aw + UI.S(8), H - th - UI.S(44)), CombineMode.Intersect);
            var lines = L.T(L.FaqA[sel]).Split('\n');
            float stepTop = -1;
            using (var f = UI.F(10.5f)) using (var fb = UI.F(9f, FontStyle.Bold)) using (var fm = new Font("Consolas", 9.5f * 1f)) using (var tb = new SolidBrush(Color.FromArgb(222, 223, 228)))
                for (int li = 0; li < lines.Length; li++)
                {
                    string t = lines[li];
                    if (t.Length == 0) { ay += UI.S(12); continue; }
                    var m = System.Text.RegularExpressions.Regex.Match(t, @"^(\d+)\.\s+(.*)$");
                    if (m.Success)
                    {
                        // Schritt mit Nummernkreis und Verbindungslinie
                        float tx = ax + UI.S(40), tw = aw - UI.S(40);
                        var sz = g.MeasureString(m.Groups[2].Value, f, (int)tw);
                        var cr = new RectangleF(ax, ay - UI.S(1), UI.S(26), UI.S(26));
                        bool nextStep = li + 1 < lines.Length && System.Text.RegularExpressions.Regex.IsMatch(lines[li + 1], @"^\d+\.\s");
                        if (nextStep) using (var p = new Pen(UI.A(UI.Red, 70), UI.Sf(2f))) g.DrawLine(p, cr.X + cr.Width / 2, cr.Bottom, cr.X + cr.Width / 2, ay + Math.Max(sz.Height, UI.S(26)) + UI.S(12));
                        using (var b = new SolidBrush(Color.FromArgb(255, 40, 16, 20))) g.FillEllipse(b, cr);
                        using (var p = new Pen(UI.Red, UI.Sf(1.6f))) g.DrawEllipse(p, cr);
                        UI.Txt(g, m.Groups[1].Value, fb, Color.White, cr, StringAlignment.Center);
                        g.DrawString(m.Groups[2].Value, f, tb, new RectangleF(tx, ay + UI.S(3), tw, sz.Height + 4));
                        ay += Math.Max(sz.Height + UI.S(6), UI.S(26)) + UI.S(12);
                        continue;
                    }
                    if (t.StartsWith("• "))
                    {
                        string bt = t.Substring(2); float tx = ax + UI.S(22), tw = aw - UI.S(22);
                        var sz = g.MeasureString(bt, f, (int)tw);
                        using (var b = new SolidBrush(UI.Red)) g.FillEllipse(b, ax + UI.S(4), ay + UI.S(8), UI.S(7), UI.S(7));
                        g.DrawString(bt, f, tb, new RectangleF(tx, ay, tw, sz.Height + 4));
                        ay += sz.Height + UI.S(6);
                        continue;
                    }
                    if (t.StartsWith("…\\") || t.StartsWith("...\\"))
                    {
                        var sz = g.MeasureString(t, fm, (int)(aw - UI.S(24)));
                        var cr = new RectangleF(ax, ay, Math.Min(aw, sz.Width + UI.S(24)), sz.Height + UI.S(12));
                        using (var path = UI.Round(cr, UI.S(8)))
                        {
                            using (var b = new SolidBrush(Color.FromArgb(255, 9, 8, 11))) g.FillPath(b, path);
                            using (var p = new Pen(Color.FromArgb(45, 255, 255, 255))) g.DrawPath(p, path);
                        }
                        using (var b = new SolidBrush(Color.FromArgb(255, 255, 196, 140))) g.DrawString(t, fm, b, new RectangleF(cr.X + UI.S(12), cr.Y + UI.S(6), cr.Width - UI.S(20), sz.Height + 2));
                        ay += cr.Height + UI.S(8);
                        continue;
                    }
                    var psz = g.MeasureString(t, f, (int)aw);
                    g.DrawString(t, f, tb, new RectangleF(ax, ay, aw, psz.Height + 4));
                    ay += psz.Height + UI.S(6);
                }
            g.Clip = oc;

            // Fußzeile
            using (var p = new Pen(Color.FromArgb(28, 255, 255, 255))) g.DrawLine(p, 0, H - UI.S(40), W, H - UI.S(40));
            using (var f = UI.F(8.5f)) UI.Txt(g, "↑ ↓  " + L.T("Frage wechseln") + "     Esc  " + L.T("Schließen"), f, UI.Dim, new RectangleF(UI.S(22), H - UI.S(40), W - UI.S(44), UI.S(40)));
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            int o = qr.FindIndex(r => r.Contains(e.Location)); bool oc = closeR.Contains(e.Location);
            if (o != over || oc != overClose) { over = o; overClose = oc; Invalidate(); }
            Cursor = o >= 0 || oc ? Cursors.Hand : Cursors.Default;
        }
        protected override void OnMouseLeave(EventArgs e) { over = -1; overClose = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (closeR.Contains(e.Location)) { Close(); return; }
            int o = qr.FindIndex(r => r.Contains(e.Location));
            if (o >= 0) { sel = o; Invalidate(); return; }
            if (e.Y < UI.S(60)) MainForm.DragWindow(Handle);
        }
    }

    // ============================================================ Hauptfenster
    class MainForm : Form
    {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l);
        public static bool Win11 { get { try { var v = Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "CurrentBuildNumber", "0") as string; int b; return int.TryParse(v, out b) && b >= 22000; } catch { return false; } } }
        public static void Rounded(IntPtr h) { try { int round = 2; DwmSetWindowAttribute(h, 33, ref round, 4); int dark = 1; DwmSetWindowAttribute(h, 20, ref dark, 4); int border = 0x00423A3A; DwmSetWindowAttribute(h, 34, ref border, 4); } catch { } }
        public static void DragWindow(IntPtr h) { ReleaseCapture(); SendMessage(h, 0xA1, (IntPtr)2, IntPtr.Zero); }

        internal static readonly string[] Cats = { "CharPortraits", "Perks", "Powers", "items", "ItemAddons", "Favors", "StoreBackgrounds" };
        internal static readonly Dictionary<string, string> CatNames = new Dictionary<string, string> {
            {"CharPortraits","Porträts"},{"Perks","Perks"},{"Powers","Kräfte"},{"items","Items"},
            {"ItemAddons","Add-ons"},{"Favors","Opfergaben"},{"StoreBackgrounds","Store-Bilder"} };
        const string GameRel = @"steamapps\common\Dead by Daylight\DeadByDaylight\Content\UI\Icons";

        string baseDir, packsDir, backupDir, settingsFile, gamePath;
        List<Pack> packs = new List<Pack>(); Pack sel;
        List<Hot> hots = new List<Hot>(); Hot over, pressed;
        Dictionary<string, RectangleF> cardRects = new Dictionary<string, RectangleF>(); RectangleF mainRect, gameRect, progRect; int frame; Bitmap heroFrom; Timer anim; float pulse, progress, progressShown, scroll, fade = 1; Bitmap fadeFrom;
        string status = ""; bool busy;
        List<string> logLines = new List<string>();
        FileSystemWatcher watcher; Timer refreshTimer; BackgroundWorker worker;
        ToolTip tip; string tipShown;

        int TitleH { get { return UI.S(52); } }
        int SideW { get { return UI.S(300); } }

        public MainForm()
        {
            baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            packsDir = Path.Combine(baseDir, "Packs"); backupDir = Path.Combine(baseDir, "Backup");
            settingsFile = Path.Combine(baseDir, "settings.txt"); MigrateOldFiles(); LoadCats(); LoadLang();
            Directory.CreateDirectory(packsDir);

            using (var gr = CreateGraphics()) UI.Scale = gr.DpiX / 96f;
            Text = "FogForge"; BackColor = UI.Bg; ForeColor = UI.Text; Font = UI.F(10f);
            FormBorderStyle = FormBorderStyle.None; AutoScaleMode = AutoScaleMode.None;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(UI.S(1180), UI.S(820)); StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            tip = new ToolTip { InitialDelay = 300, AutoPopDelay = 12000 };

            worker = new BackgroundWorker { WorkerReportsProgress = true };
            worker.ProgressChanged += (s, e) => { progress = e.ProgressPercentage / 100f; if (e.UserState != null) status = (string)e.UserState; anim.Start(); };

            BuildMenu();
            anim = new Timer { Interval = 16 };
            anim.Tick += (s, e) => Animate();
            anim.Start();

            gamePath = LoadGamePath() ?? DetectGame();
            cacheDir = Path.Combine(baseDir, "Cache"); ThumbStore.Dir = Path.Combine(cacheDir, "thumbs");
            try { var cj = Path.Combine(cacheDir, "catalog.txt"); if (File.Exists(cj)) catalog = Remote.LoadCache(cj); } catch { }
            LoadPacks();
            FetchCatalog();
            CheckAppUpdate();
            LoadOv();
            periodic = new Timer { Interval = 30 * 60 * 1000 }; periodic.Tick += (s, e) => { if (!busy) { CheckAppUpdate(); CheckDrift(); } }; periodic.Start();
            livePoll = new Timer { Interval = 5000 }; livePoll.Tick += (s, e) => LivePollTick(); livePoll.Start();
            liveTick = new Timer { Interval = 500 }; liveTick.Tick += (s, e) => { if (WindowState != FormWindowState.Minimized) Inv(liveRect, 2); }; liveTick.Start();

            watcher = new FileSystemWatcher(packsDir) { NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName, IncludeSubdirectories = false };
            refreshTimer = new Timer { Interval = 900 };
            refreshTimer.Tick += (s, e) => { refreshTimer.Stop(); if (!busy) LoadPacks(); };
            FileSystemEventHandler h = (s, e) => BeginInvoke((Action)(() => { refreshTimer.Stop(); refreshTimer.Start(); }));
            watcher.Created += h; watcher.Deleted += h; watcher.Renamed += (s, e) => h(s, e);
            watcher.EnableRaisingEvents = true;
            Log(L.F("Bereit. Packs-Ordner: {0}", packsDir));
            status = gamePath != null ? L.T("Bereit.") : L.T("Spiel nicht gefunden – oben rechts auswählen.");
        }

        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; if (!isWin11) cp.ClassStyle |= 0x20000; cp.Style |= 0x20000; /* Minimieren über Taskleiste */ return cp; }
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Rounded(Handle); ApplyRegion(); }
        [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int w, int h);
        [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool redraw);
        Size lastRgn; static readonly bool isWin11 = Win11;
        void ApplyRegion()
        {
            if (!IsHandleCreated || WindowState == FormWindowState.Minimized || Width < 50) return;
            if (isWin11) return; // Windows 11 rundet das Fenster selbst – glatt und mit Schatten
            if (lastRgn == Size) return; lastRgn = Size;
            int d = WindowState == FormWindowState.Maximized ? 0 : UI.S(18) * 2;
            try { SetWindowRgn(Handle, CreateRoundRectRgn(0, 0, Width + 1, Height + 1, d, d), true); }
            catch { using (var p = UI.Round(new RectangleF(0, 0, Width, Height), d / 2f)) Region = new Region(p); }
        }
        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); ApplyRegion(); }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e); ApplyRegion(); Invalidate();
            EnsureDefault();
            CheckDrift();
        }

        // Default-Pack (Original-Icons) ist immer vorhanden – fehlt es, wird es aus dem Spiel erstellt
        void EnsureDefault()
        {
            if (busy || gamePath == null || AnyActive() || Directory.Exists(Path.Combine(packsDir, "Default"))) return;
            string game = gamePath, dst = Path.Combine(packsDir, "Default"), tmp = Path.Combine(packsDir, "_default_new"); int n = 0;
            Run(L.T("Default-Pack wird erstellt …"), bw =>
            {
                if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
                var files = new List<string>();
                foreach (var c in Cats) { var d = Path.Combine(game, c); if (Directory.Exists(d)) files.AddRange(Directory.GetFiles(d, "*.png", SearchOption.AllDirectories)); }
                for (int i = 0; i < files.Count; i++)
                {
                    var t = Path.Combine(tmp, files[i].Substring(game.Length).TrimStart('\\', '/'));
                    Directory.CreateDirectory(Path.GetDirectoryName(t)); File.Copy(files[i], t, true); n++;
                    if (i % 25 == 0) bw.ReportProgress(i * 100 / Math.Max(1, files.Count), L.F("Default-Pack wird erstellt … {0} / {1}", i + 1, files.Count));
                }
                if (n == 0) { Directory.Delete(tmp, true); return; }
                Directory.Move(tmp, dst);
            }, () => { if (n > 0) Log(L.F("Default-Pack erstellt ({0} Icons).", n.ToString("N0"))); }, "Pack installiert");
        }

        // ---------------- Menü ----------------
        void BuildMenu() { }
        DateTime menuClosed;
        void ShowMenu(RectangleF anchor)
        {
            if ((DateTime.Now - menuClosed).TotalMilliseconds < 250) return;   // erneuter Klick schließt nur
            var m = new PopupMenu { MenuWidth = 470 };
            m.Add("\u21BB", L.T("Nach Updates suchen"), () => CheckNow(true), !busy && !checking);
            m.Add(autoReapply ? "\u2611" : "\u2610", L.T("Packs nach Spiel-Updates automatisch neu anwenden"), () => { autoReapply = !autoReapply; SaveGamePath(); Log(autoReapply ? L.T("Packs werden nach Spiel-Updates automatisch neu angewendet.") : L.T("Nach Spiel-Updates wird vorher gefragt.")); });
            if (iconOv.Count > 0) m.Add("\u2605", L.F("Einzel-Icons zurücksetzen ({0})", iconOv.Count), ResetOverrides, !busy);
            m.Sep();
            m.Add("\uD83C\uDFAE", L.T("Spielordner ändern …"), ChooseGame, !busy);
            m.FormClosed += (s, e) => { menuClosed = DateTime.Now; m.Dispose(); };
            m.ShowAt(this, PointToScreen(new Point((int)anchor.Right, (int)anchor.Bottom + UI.S(8))));
        }

        // ---------------- Animation ----------------
        void Animate()
        {
            if (WindowState == FormWindowState.Minimized) return;
            bool anyMoving = fade < 1 || busy || doneAt != int.MinValue || Math.Abs(progressShown - progress) > 0.002f;
            if (busy || doneAt != int.MinValue) { bool was = doneAt != int.MinValue; DoneFlash(); Inv(progRect, 8); if (was && doneAt == int.MinValue) Invalidate(); }
            foreach (var h in hots) { float c; hv.TryGetValue(h.Id, out c); float t = (over != null && over.Id == h.Id && h.Enabled) ? 1 : 0; if (Math.Abs(c - t) > 0.001f) anyMoving = true; }
            foreach (var p in packs) { float c; hv.TryGetValue("sel:" + p.Name, out c); if (Math.Abs(c - (p == sel ? 1 : 0)) > 0.001f) anyMoving = true; }
            foreach (var c in Cats) { float v; if (!hv.TryGetValue("on:" + c, out v)) v = useCats.Contains(c) ? 1 : 0; if (Math.Abs(v - (useCats.Contains(c) ? 1 : 0)) > 0.001f) anyMoving = true; }
            if (!anyMoving) { anim.Stop(); return; }
            frame++;
            foreach (var h in hots)
            {
                float cur; hv.TryGetValue(h.Id, out cur);
                float t = (over != null && over.Id == h.Id && h.Enabled) ? 1 : 0;
                // weiches Ein-/Ausblenden: rein etwas zügiger, raus langsamer
                float k = t > cur ? 0.16f : 0.10f;
                float nv = Math.Abs(cur - t) < 0.008f ? t : cur + (t - cur) * k;
                if (nv != cur) { hv[h.Id] = nv; Inv(h.R, 14); }
            }
            foreach (var p in packs)
            {
                string k = "sel:" + p.Name; float cur; hv.TryGetValue(k, out cur); float t = p == sel ? 1 : 0;
                float nv = t;
                if (nv != cur) { hv[k] = nv; RectangleF cr; if (cardRects.TryGetValue(p.Name, out cr)) Inv(cr, 16); }
            }
            foreach (var c in Cats)
            {
                string k = "on:" + c; float cur, t = useCats.Contains(c) ? 1 : 0;
                if (!hv.TryGetValue(k, out cur)) { hv[k] = t; continue; }
                float nv = Math.Abs(cur - t) < 0.01f ? t : cur + (t - cur) * 0.17f;
                if (nv != cur) { hv[k] = nv; RectangleF tr; if (tileRects.TryGetValue(c, out tr)) Inv(tr, 2); else Invalidate(); }
            }
            if (Math.Abs(progressShown - progress) > 0.002f) { progressShown += (progress - progressShown) * 0.2f; Inv(progRect, 2); }
            if (fade < 1) { fade = Math.Min(1, fade + 0.08f); Invalidate(); if (fade >= 1) { if (fadeFrom != null) { fadeFrom.Dispose(); fadeFrom = null; } if (heroFrom != null) { heroFrom.Dispose(); heroFrom = null; } } }
            // Leuchten: nur Hauptknopf + Statuspunkt, 30 Bilder/s

        }
        void Inv(RectangleF r, int pad) { if (r.Width > 0) Invalidate(Rectangle.Inflate(Rectangle.Ceiling(r), UI.S(pad), UI.S(pad))); }

        // ---------------- Zeichnen ----------------
        Bitmap scene; bool sceneDirty = true;
        new void Invalidate() { sceneDirty = true; base.Invalidate(); }
        Rectangle dirty = Rectangle.Empty; Dictionary<string, RectangleF> tileRects = new Dictionary<string, RectangleF>();
        new void Invalidate(Rectangle r) { dirty = dirty.IsEmpty ? r : Rectangle.Union(dirty, r); base.Invalidate(r); }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (ClientSize.Width < 10 || ClientSize.Height < 10) return;
            if (scene == null || scene.Size != ClientSize) { if (scene != null) scene.Dispose(); scene = new Bitmap(ClientSize.Width, ClientSize.Height, PixelFormat.Format32bppPArgb); sceneDirty = true; }
            if (sceneDirty) { using (var sg = Graphics.FromImage(scene)) { sg.CompositingMode = CompositingMode.SourceCopy; using (var bb = new SolidBrush(UI.Bg)) sg.FillRectangle(bb, 0, 0, scene.Width, scene.Height); sg.CompositingMode = CompositingMode.SourceOver; RenderScene(sg); } sceneDirty = false; dirty = Rectangle.Empty; }
            else if (!dirty.IsEmpty)
            {
                var dr = Rectangle.Intersect(dirty, new Rectangle(Point.Empty, ClientSize));
                using (var sg = Graphics.FromImage(scene))
                {
                    // Bereich zuerst hart überschreiben – sonst stapeln sich halbtransparente Flächen bei jedem Hover
                    sg.CompositingMode = CompositingMode.SourceCopy;
                    using (var bb = new SolidBrush(UI.Bg)) sg.FillRectangle(bb, dr);
                    sg.CompositingMode = CompositingMode.SourceOver;
                    sg.SetClip(dr); RenderScene(sg);
                }
                dirty = Rectangle.Empty;
            }
            e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            e.Graphics.DrawImageUnscaled(scene, 0, 0);
        }
        void RenderScene(Graphics g)
        {
            UI.HQ(g);
            var prev = hots; hots = new List<Hot>();
            int W = ClientSize.Width, H = ClientSize.Height;

            // Hintergrund: unscharfes Cover des gewählten Packs
            g.Clear(UI.Bg);
            var oldIm = g.InterpolationMode; g.InterpolationMode = InterpolationMode.NearestNeighbor;
            if (fadeFrom != null) g.DrawImageUnscaled(fadeFrom, 0, 0);
            if (sel != null && sel.Backdrop != null)
            {
                if (fade < 1)
                {
                    var cm = new ColorMatrix { Matrix33 = fade };
                    using (var ia = new ImageAttributes()) { ia.SetColorMatrix(cm); g.DrawImage(sel.Backdrop, new Rectangle(0, 0, sel.Backdrop.Width, sel.Backdrop.Height), 0, 0, sel.Backdrop.Width, sel.Backdrop.Height, GraphicsUnit.Pixel, ia); }
                }
                else g.DrawImageUnscaled(sel.Backdrop, 0, 0);
            }
            else using (var lg = new LinearGradientBrush(new Rectangle(0, 0, W, H), Color.FromArgb(40, 0, 0, 0), Color.FromArgb(200, 6, 5, 8), 90f)) g.FillRectangle(lg, 0, 0, W, H);
            g.InterpolationMode = oldIm;

            DrawTitleBar(g, W);
            DrawSidebar(g, H);
            DrawMain(g, W, H);

            foreach (var h in hots) { var o = prev.FirstOrDefault(p => p.Id == h.Id); if (o != null) h.H = o.H; }
            if (over != null) over = hots.FirstOrDefault(h => h.Id == over.Id);

            if (!isWin11) using (var path = UI.Round(new RectangleF(0.5f, 0.5f, W - 1.5f, H - 1.5f), UI.S(18))) using (var p = new Pen(Color.FromArgb(70, 255, 255, 255), UI.Sf(1.2f))) g.DrawPath(p, path);
        }

        // Augen-Knopf auf der Pack-Karte: Inhalt ansehen
        void EyeButton(Graphics g, RectangleF r, float h, float cardHover)
        {
            int a = (int)(150 + 80 * cardHover);
            using (var b = new SolidBrush(UI.Mix(Color.FromArgb(a, 12, 11, 15), UI.A(UI.Red, 235), h))) g.FillEllipse(b, r);
            using (var p = new Pen(Color.FromArgb((int)(60 + 120 * Math.Max(h, cardHover * 0.6f)), 255, 255, 255), UI.Sf(1f))) g.DrawEllipse(p, r);
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, ew = UI.Sf(8.5f), eh = UI.Sf(5.2f);
            using (var gp = new GraphicsPath())
            {
                gp.AddBezier(cx - ew, cy, cx - ew * 0.45f, cy - eh * 1.25f, cx + ew * 0.45f, cy - eh * 1.25f, cx + ew, cy);
                gp.AddBezier(cx + ew, cy, cx + ew * 0.45f, cy + eh * 1.25f, cx - ew * 0.45f, cy + eh * 1.25f, cx - ew, cy);
                using (var p = new Pen(Color.FromArgb(235, 255, 255, 255), UI.Sf(1.6f))) g.DrawPath(p, gp);
            }
            float pr = UI.Sf(2.8f);
            using (var b = new SolidBrush(Color.FromArgb(240, 255, 255, 255))) g.FillEllipse(b, cx - pr, cy - pr, pr * 2, pr * 2);
        }
        void ShowPeek(Pack p)
        {
            string dir = p.Available ? p.SourceDir : p.Dir;
            MarkSeen(p);
            var def = FindInstalled("Default");
            using (var d = new PackPreview(p.Name, dir != null && Directory.Exists(dir) ? dir : null, p.Rem, cacheDir, !p.Available, def != null ? def.Dir : null, GetOv, SetOv)) d.ShowDialog(this);
            Invalidate();
        }

        Hot AddHot(string id, RectangleF r, Action click, bool enabled = true, string tipText = null)
        {
            var h = new Hot { Id = id, R = r, Click = click, Enabled = enabled, Tip = tipText };
            var o = hots.FirstOrDefault(x => x.Id == id); hots.Add(h); return h;
        }
        float HoverOf(string id) { var h = (over != null && over.Id == id) ? over : null; var p = hotsPrevValue(id); return p; }
        Dictionary<string, float> hv = new Dictionary<string, float>();
        float hotsPrevValue(string id) { float v; return hv.TryGetValue(id, out v) ? v : 0; }

        Bitmap logo;
        void DrawTitleBar(Graphics g, int W)
        {
            int th = TitleH;
            using (var b = new SolidBrush(Color.FromArgb(150, 8, 7, 10))) g.FillRectangle(b, 0, 0, W, th);
            using (var lg = new LinearGradientBrush(new RectangleF(0, th - 2, W, 2), UI.A(UI.Red, 0), UI.A(UI.Red, 220), 0f))
            {
                var blend = new ColorBlend { Colors = new[] { UI.A(UI.Red, 0), UI.A(UI.Red, 230), UI.A(UI.Red, 0) }, Positions = new[] { 0f, 0.35f, 1f } };
                lg.InterpolationColors = blend; g.FillRectangle(lg, 0, th - 2, W, 2);
            }
            // Logo
            int ls = UI.S(30);
            if (logo == null) try { using (var st = typeof(MainForm).Assembly.GetManifestResourceStream("logo.png")) using (var raw = Image.FromStream(st)) { logo = new Bitmap(ls, ls, PixelFormat.Format32bppPArgb); using (var lg2 = Graphics.FromImage(logo)) { UI.HQ(lg2); lg2.DrawImage(raw, 0, 0, ls, ls); } } } catch { }
            if (logo != null) g.DrawImageUnscaled(logo, UI.S(16), (th - ls) / 2);
            float tw1; using (var f = UI.F(12.5f, FontStyle.Bold)) { tw1 = g.MeasureString("FOGFORGE", f).Width; UI.Txt(g, "FOGFORGE", f, UI.Text, new RectangleF(UI.S(54), 0, tw1 + UI.S(10), th)); }
            float pmw; using (var f = UI.F(9.5f)) { pmw = g.MeasureString("Pack Manager", f).Width; UI.Txt(g, "Pack Manager", f, UI.Dim, new RectangleF(UI.S(54) + tw1 + UI.S(8), 0, pmw + UI.S(4), th)); }
            using (var f = UI.F(7.5f, FontStyle.Bold))
            {
                string vt = "v" + AppInfo.Version; float vw = g.MeasureString(vt, f).Width + UI.S(12);
                var vr = new RectangleF(UI.S(54) + tw1 + UI.S(8) + pmw + UI.S(8), (th - 2) / 2f - UI.S(9), vw, UI.S(18));
                using (var path = UI.Round(vr, UI.S(9))) { using (var b = new SolidBrush(Color.FromArgb(14, 255, 255, 255))) g.FillPath(b, path); using (var p = new Pen(Color.FromArgb(30, 255, 255, 255))) g.DrawPath(p, path); }
                UI.Txt(g, vt, f, UI.Dim, vr, StringAlignment.Center);
            }

            // Fensterknöpfe
            float bw = UI.S(46);
            float wch = UI.S(32), wtop = (float)Math.Round((th - 2) / 2f - wch / 2);
            var rc = new RectangleF(W - UI.S(12) - UI.S(38), wtop, UI.S(38), wch); var hc = AddHot("close", rc, Close);
            float hcv = Hv("close"); if (hcv > 0) using (var path = UI.Round(rc, UI.S(10))) using (var b = new SolidBrush(UI.A(UI.Red, (int)(235 * hcv)))) g.FillPath(b, path);
            using (var p = new Pen(UI.Text, UI.Sf(1.4f))) { float s = UI.S(5); var c = new PointF(rc.X + rc.Width / 2, rc.Y + rc.Height / 2); g.DrawLine(p, c.X - s, c.Y - s, c.X + s, c.Y + s); g.DrawLine(p, c.X - s, c.Y + s, c.X + s, c.Y - s); }
            var rm = new RectangleF(rc.X - UI.S(4) - UI.S(38), wtop, UI.S(38), wch); AddHot("min", rm, () => WindowState = FormWindowState.Minimized);
            float hmv = Hv("min"); if (hmv > 0) using (var path = UI.Round(rm, UI.S(10))) using (var b = new SolidBrush(Color.FromArgb((int)(34 * hmv), 255, 255, 255))) g.FillPath(b, path);
            using (var p = new Pen(UI.Text, UI.Sf(1.4f))) { float s = UI.S(5); var c = new PointF(rm.X + rm.Width / 2, rm.Y + rm.Height / 2); g.DrawLine(p, c.X - s, c.Y, c.X + s, c.Y); }

            // ---- Werkzeugleiste: [ ? | Sprache | ⋯ ]  +  Status-Pille, alles gleich hoch und mittig ----
            float ch = UI.S(32), cy = (th - 2) / 2f, top = (float)Math.Round(cy - ch / 2);
            float wHelp = UI.S(40), wLang = UI.S(70), wMenu = UI.S(40);
            float gx1 = rm.X - UI.S(12), gx0 = gx1 - (wHelp + wLang + wMenu);
            var grp = new RectangleF(gx0, top, gx1 - gx0, ch);
            var rh = new RectangleF(gx0, top, wHelp, ch);
            var rl = new RectangleF(gx0 + wHelp, top, wLang, ch);
            var rmenu = new RectangleF(gx0 + wHelp + wLang, top, wMenu, ch);
            AddHot("help", rh, () => { using (var d = new HelpDialog()) d.ShowDialog(this); }, true, L.T("Hilfe & häufige Fragen"));
            AddHot("lang", rl, () => ShowLangMenu(rl), true, L.T("Sprache") + ": " + L.Name(L.Code));
            AddHot("menu", rmenu, () => ShowMenu(rmenu), true, L.T("Mehr: Updates und Einstellungen"));
            using (var gp = UI.Round(grp, ch / 2))
            {
                using (var b = new SolidBrush(Color.FromArgb(16, 255, 255, 255))) g.FillPath(b, gp);
                var oc = g.Clip; g.SetClip(gp, CombineMode.Intersect);
                foreach (var seg in new[] { new { r = rh, id = "help" }, new { r = rl, id = "lang" }, new { r = rmenu, id = "menu" } })
                { float hv2 = Hv(seg.id); if (hv2 > 0.01f) using (var b = new SolidBrush(Color.FromArgb((int)(34 * hv2), 255, 255, 255))) g.FillRectangle(b, seg.r); }
                g.Clip = oc;
                using (var p = new Pen(Color.FromArgb(26, 255, 255, 255), 1f)) { g.DrawLine(p, rl.X, top + UI.S(8), rl.X, top + ch - UI.S(8)); g.DrawLine(p, rmenu.X, top + UI.S(8), rmenu.X, top + ch - UI.S(8)); }
                using (var p = new Pen(Color.FromArgb(34, 255, 255, 255), 1f)) g.DrawPath(p, gp);
            }
            // ? als Kreis-Symbol
            {
                float r8 = UI.S(8); var c = new PointF(rh.X + rh.Width / 2 + UI.S(1), cy);
                using (var p = new Pen(UI.Mix(UI.Sub, UI.Text, Hv("help")), UI.Sf(1.5f))) g.DrawEllipse(p, c.X - r8, c.Y - r8, 2 * r8, 2 * r8);
                using (var f = UI.F(8.5f, FontStyle.Bold)) UI.Txt(g, "?", f, UI.Mix(UI.Sub, UI.Text, Hv("help")), new RectangleF(c.X - r8, c.Y - r8, 2 * r8 + 1, 2 * r8 + 1), StringAlignment.Center);
            }
            // Sprache: Flagge + Kürzel
            L.Flag(g, L.Code, new RectangleF((float)Math.Round(rl.X + UI.S(13)), (float)Math.Round(cy - UI.S(7)), UI.S(21), UI.S(14)));
            using (var f = UI.F(9f, FontStyle.Bold)) UI.Txt(g, L.Code.ToUpperInvariant(), f, UI.Mix(UI.Sub, UI.Text, 0.6f + 0.4f * Hv("lang")), new RectangleF(rl.X + UI.S(39), top, rl.Width - UI.S(40), ch));
            // ⋯
            using (var b = new SolidBrush(UI.Mix(UI.Sub, UI.Text, 0.6f + 0.4f * Hv("menu"))))
                for (int i = -1; i <= 1; i++) g.FillEllipse(b, rmenu.X + rmenu.Width / 2 - UI.S(1) + i * UI.S(6) - UI.S(2), cy - UI.S(2), UI.S(4), UI.S(4));

            // Spiel-Status: Glas-Pille mit Symbol
            bool gameOk = gamePath != null;
            string gs = gameOk ? L.T("Spiel verbunden") : L.T("Spiel nicht gefunden");
            float gw; using (var f = UI.F(9.5f, FontStyle.Bold)) gw = g.MeasureString(gs, f).Width + UI.S(52);
            var rg = new RectangleF(grp.X - UI.S(10) - gw, top, gw, ch); gameRect = rg;
            AddHot("game", rg, ChooseGame, !busy, gameOk ? L.F("Spielordner:\n{0}\n\nKlicken zum Ändern", gamePath) : L.T("Klicken, um den Dead-by-Daylight-Ordner auszuwählen"));
            float hg = Hv("game");
            var gc = gameOk ? UI.Green : UI.Red;
            using (var path = UI.Round(rg, ch / 2))
            {
                using (var b = new SolidBrush(Color.FromArgb((int)(16 + 18 * hg), 255, 255, 255))) g.FillPath(b, path);
                using (var b = new LinearGradientBrush(new RectangleF(rg.X - 1, rg.Y, rg.Width + 2, rg.Height), UI.A(gc, (int)(40 + 24 * hg)), UI.A(gc, 0), 0f)) g.FillPath(b, path);
                using (var p = new Pen(Color.FromArgb((int)(34 + 40 * hg), 255, 255, 255), 1f)) g.DrawPath(p, path);
            }
            {
                float ir = UI.S(10); var ic = new PointF(rg.X + UI.S(7) + ir, cy);
                using (var b = new SolidBrush(UI.A(gc, 50))) g.FillEllipse(b, ic.X - ir - UI.S(2), ic.Y - ir - UI.S(2), 2 * ir + UI.S(4), 2 * ir + UI.S(4));
                using (var b = new SolidBrush(gc)) g.FillEllipse(b, ic.X - ir, ic.Y - ir, 2 * ir, 2 * ir);
                using (var p = new Pen(Color.FromArgb(240, 12, 12, 14), UI.Sf(1.9f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    if (gameOk) g.DrawLines(p, new[] { new PointF(ic.X - UI.Sf(4.2f), ic.Y + UI.Sf(0.2f)), new PointF(ic.X - UI.Sf(1.2f), ic.Y + UI.Sf(3.2f)), new PointF(ic.X + UI.Sf(4.4f), ic.Y - UI.Sf(3f)) });
                    else { g.DrawLine(p, ic.X, ic.Y - UI.Sf(4.5f), ic.X, ic.Y + UI.Sf(1f)); using (var bb = new SolidBrush(Color.FromArgb(240, 12, 12, 14))) g.FillEllipse(bb, ic.X - UI.Sf(1.3f), ic.Y + UI.Sf(2.7f), UI.Sf(2.6f), UI.Sf(2.6f)); }
                }
            }
            using (var f = UI.F(9.5f, FontStyle.Bold)) UI.Txt(g, gs, f, gameOk ? UI.Mix(UI.Text, UI.Green, 0.25f) : UI.Mix(UI.Text, UI.RedHi, 0.45f), new RectangleF(rg.X + UI.S(35), top, rg.Width - UI.S(38), ch));

            // Programm-Update verfügbar: Glas-Knopf mit Lichtschimmer
            updRect = RectangleF.Empty;
            if (appUpd != null)
            {
                string ut = L.T("Update verfügbar"), uv = appUpd.Version.ToString(3);
                float utw, uvw;
                using (var f = UI.F(9.5f, FontStyle.Bold)) utw = g.MeasureString(ut, f).Width;
                using (var f = UI.F(8.5f)) uvw = g.MeasureString(uv, f).Width;
                float uw = utw + uvw + UI.S(56);
                var ru = new RectangleF(rg.X - UI.S(10) - uw, top, uw, ch); updRect = ru;
                AddHot("upd", ru, DoAppUpdate, !busy, L.F("FogForge {0} ist verfügbar – klicken zum Aktualisieren", uv));
                float hu = Hv("upd");
                using (var path = UI.Round(ru, ch / 2))
                {
                    using (var b = new LinearGradientBrush(new RectangleF(ru.X - 1, ru.Y, ru.Width + 2, ru.Height), UI.A(UI.Red, (int)(150 + 70 * hu)), UI.A(UI.RedDark, (int)(120 + 80 * hu)), 0f)) g.FillPath(b, path);
                    // wandernder Lichtstreifen
                    var oc = g.Clip; g.SetClip(path, CombineMode.Intersect);
                    float period = 2800f, t = (Environment.TickCount % (int)period) / period, sw2 = UI.S(40);
                    if (t < 0.55f)
                    {
                        float sx = ru.X - sw2 + (ru.Width + sw2 * 2) * (t / 0.55f);
                        using (var lb = new LinearGradientBrush(new RectangleF(sx - sw2, ru.Y, sw2 * 2, ru.Height), Color.FromArgb(0, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 0f))
                        { lb.InterpolationColors = new ColorBlend { Colors = new[] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(60, 255, 255, 255), Color.FromArgb(0, 255, 255, 255) }, Positions = new[] { 0f, 0.5f, 1f } }; g.FillRectangle(lb, sx - sw2, ru.Y, sw2 * 2, ru.Height); }
                    }
                    g.Clip = oc;
                    using (var p = new Pen(Color.FromArgb((int)(60 + 90 * hu), 255, 200, 200), 1f)) g.DrawPath(p, path);
                }
                float ir = UI.S(10); var ic = new PointF(ru.X + UI.S(7) + ir, cy);
                using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, ic.X - ir, ic.Y - ir, 2 * ir, 2 * ir);
                using (var p = new Pen(UI.Red, UI.Sf(1.9f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    float bob = (float)Math.Sin(Environment.TickCount / 260.0) * UI.Sf(0.8f);
                    g.DrawLine(p, ic.X, ic.Y - UI.Sf(4.5f) + bob, ic.X, ic.Y + UI.Sf(3.5f) + bob);
                    g.DrawLines(p, new[] { new PointF(ic.X - UI.Sf(3.6f), ic.Y + bob), new PointF(ic.X, ic.Y + UI.Sf(3.8f) + bob), new PointF(ic.X + UI.Sf(3.6f), ic.Y + bob) });
                }
                using (var f = UI.F(9.5f, FontStyle.Bold)) UI.Txt(g, ut, f, Color.White, new RectangleF(ru.X + UI.S(35), top, utw + UI.S(4), ch));
                using (var f = UI.F(8.5f)) UI.Txt(g, uv, f, Color.FromArgb(215, 255, 225, 225), new RectangleF(ru.X + UI.S(39) + utw, top, uvw + UI.S(6), ch));
            }
        }

        float Hv(string id) { var h = hots.FirstOrDefault(x => x.Id == id); if (h == null) return 0; float v; return hv.TryGetValue(id, out v) ? v : 0; }

        void DrawSidebar(Graphics g, int H)
        {
            int top = TitleH, sw = SideW;
            using (var b = new SolidBrush(Color.FromArgb(120, 6, 5, 8))) g.FillRectangle(b, 0, top, sw, H - top);
            using (var p = new Pen(Color.FromArgb(40, 255, 255, 255))) g.DrawLine(p, sw, top, sw, H);
            using (var f = UI.F(9f, FontStyle.Bold)) UI.Txt(g, L.T("DEINE PACKS"), f, UI.Dim, new RectangleF(UI.S(22), top + UI.S(6), UI.S(200), UI.S(26)));
            using (var f = UI.F(9f, FontStyle.Bold)) UI.Txt(g, packs.Count(z => !z.Available).ToString(), f, UI.Dim, new RectangleF(sw - UI.S(60), top + UI.S(6), UI.S(38), UI.S(26)), StringAlignment.Far);

            int y0 = top + UI.S(38); int cardH = UI.S(128), gap = UI.S(14), x = UI.S(18), w = sw - UI.S(36);
            float listH = H - y0 - UI.S(106);
            float secH = UI.S(34); bool anyAvail = packs.Any(z => z.Available);
            float maxScroll = Math.Max(0, packs.Count * (cardH + gap) + (anyAvail ? secH : 0) - listH);
            scroll = Math.Max(0, Math.Min(scroll, maxScroll));
            var clip = new RectangleF(0, y0 - UI.S(6), sw, listH + UI.S(6));
            var oldClip = g.Clip; g.SetClip(clip);
            string active = ActivePack();
            float yy = y0 - scroll; bool secDone = false;
            for (int i = 0; i < packs.Count; i++)
            {
                var p = packs[i];
                if (p.Available && !secDone)
                {
                    secDone = true;
                    using (var f = UI.F(9f, FontStyle.Bold)) UI.Txt(g, L.T("VERFÜGBAR"), f, UI.Dim, new RectangleF(UI.S(22), (float)Math.Round(yy), UI.S(200), UI.S(24)));
                    using (var pen = new Pen(Color.FromArgb(30, 255, 255, 255))) g.DrawLine(pen, UI.S(110), yy + UI.S(12), sw - UI.S(22), yy + UI.S(12));
                    yy += secH;
                }
                var r = new RectangleF(x, (float)Math.Round(yy), w, cardH); yy += cardH + gap;
                if (r.Bottom < clip.Top || r.Top > clip.Bottom) continue;
                var pk = p; AddHot("pack:" + p.Name, r, () => SelectPack(pk), !busy);
                var er = new RectangleF(r.Right - UI.S(44), r.Y + UI.S(10), UI.S(34), UI.S(34));
                bool canPeek = p.Rem != null || Directory.Exists(p.Available ? (p.SourceDir ?? "") : p.Dir);
                if (canPeek) AddHot("peek:" + p.Name, er, () => ShowPeek(pk), true, L.T("Inhalt ansehen"));
                float ev = canPeek ? Hv("peek:" + p.Name) : 0;
                float hvv = Math.Max(Hv("pack:" + p.Name), ev); bool isSel = p == sel, isAct = p.Name == active;
                float sv; hv.TryGetValue("sel:" + p.Name, out sv); cardRects[p.Name] = r;
                var rr = r;
                if (hvv > 0.01f || sv > 0.01f)
                    for (int k = 3; k >= 1; k--)
                        using (var gp = UI.Round(RectangleF.Inflate(rr, k * UI.S(3), k * UI.S(3)), UI.S(14) + k * UI.S(3)))
                        using (var gb = new SolidBrush(UI.A(UI.Red, (int)((10 * hvv + 18 * sv) / k)))) g.FillPath(gb, gp);
                using (var path = UI.Round(rr, UI.S(16)))
                {
                    if (p.Preview != null)
                    {
                        var cs = new Size((int)r.Width, (int)r.Height);
                        if (p.Card == null || p.Card.Size != cs) { if (p.Card != null) p.Card.Dispose(); p.Card = UI.Rounded(p.Preview, cs, UI.S(16), false); }
                        g.DrawImageUnscaled(p.Card, (int)r.X, (int)r.Y);
                    }
                    else using (var b = new SolidBrush(UI.Card)) g.FillPath(b, path);
                    int topA = (int)(110 - 90 * Math.Max(sv, hvv * 0.8f));
                    using (var lg = new LinearGradientBrush(rr, Color.FromArgb(topA, 0, 0, 0), Color.FromArgb(235, 6, 5, 8), 90f)) g.FillPath(lg, path);
                    using (var pen = new Pen(Color.FromArgb((int)(40 + 70 * hvv * (1 - sv)), 255, 255, 255), UI.Sf(1f))) g.DrawPath(pen, path);
                    if (sv > 0.01f)
                    {
                        using (var pen = new Pen(UI.A(UI.Red, (int)(255 * sv)), UI.Sf(2.5f))) g.DrawPath(pen, path);
                        using (var pen = new Pen(UI.A(UI.Red, (int)(60 * sv)), UI.Sf(7f))) g.DrawPath(pen, path);
                    }
                }
                using (var f = UI.F(14f, FontStyle.Bold)) UI.Txt(g, p.Name, f, UI.Text, new RectangleF(rr.X + UI.S(16), rr.Bottom - UI.S(52), rr.Width - UI.S(32), UI.S(28)));
                using (var f = UI.F(8.5f)) UI.Txt(g, L.F("{0} Icons", p.Total.ToString("N0")), f, UI.Sub, new RectangleF(rr.X + UI.S(16), rr.Bottom - UI.S(26), rr.Width - UI.S(32), UI.S(18)));
                if (isAct) Badge(g, L.T("IM SPIEL"), UI.Green, rr.X + UI.S(12), rr.Y + UI.S(12));
                else if (p.Update && !p.Available) Badge(g, "⬆ " + L.T("UPDATE"), UI.Orange, rr.X + UI.S(12), rr.Y + UI.S(12));
                if (p.Available)
                {
                    using (var path = UI.Round(rr, UI.S(16))) using (var b = new SolidBrush(Color.FromArgb((int)(120 - 60 * Math.Max(hvv, isSel ? 1 : 0)), 10, 9, 12))) g.FillPath(b, path);
                    float nbw = Badge(g, "⬇ " + L.T("NICHT INSTALLIERT"), UI.Orange, rr.X + UI.S(12), rr.Y + UI.S(12));
                    if (p.IsNew) Badge(g, "\u2726 " + L.T("NEU"), UI.Green, rr.X + UI.S(18) + nbw, rr.Y + UI.S(12));
                }
                if (canPeek) EyeButton(g, er, ev, hvv);
            }
            g.Clip = oldClip;
            if (packs.Count == 0)
                using (var f = UI.F(10f)) using (var b = new SolidBrush(UI.Sub))
                    g.DrawString(L.T("Noch keine Packs.\n\nLege Ordner wie „Cartoon“\nin den Ordner „Packs“."), f, b, new RectangleF(UI.S(24), y0 + UI.S(10), sw - UI.S(48), UI.S(200)));

            // Live-Status: was gerade mit GitHub abgeglichen / aktualisiert wird
            liveRect = new RectangleF(UI.S(18), H - UI.S(92), sw - UI.S(36), UI.S(28));
            AddHot("live", liveRect, () => CheckNow(true), !busy && !checking, L.T("Klicken, um jetzt zu prüfen"));
            {
                Color dot; string lt = LiveText(out dot); float hl = Hv("live");
                using (var path = UI.Round(liveRect, UI.S(8))) using (var b = new SolidBrush(Color.FromArgb((int)(8 + 22 * hl), 255, 255, 255))) g.FillPath(b, path);
                float ph = (float)((Math.Sin(Environment.TickCount / 300.0) + 1) / 2);
                bool spin = fetching || (busy && autoName != null) || livePending.Count > 0;
                float cx = liveRect.X + UI.S(14), cyy = liveRect.Y + liveRect.Height / 2, rr0 = UI.Sf(3.5f);
                using (var b = new SolidBrush(UI.A(dot, (int)(60 + 60 * ph)))) g.FillEllipse(b, cx - rr0 * 2f, cyy - rr0 * 2f, rr0 * 4f, rr0 * 4f);
                using (var b = new SolidBrush(spin ? UI.A(dot, (int)(140 + 115 * ph)) : dot)) g.FillEllipse(b, cx - rr0, cyy - rr0, rr0 * 2, rr0 * 2);
                using (var f = UI.F(8.5f)) UI.Txt(g, lt, f, UI.Sub, new RectangleF(liveRect.X + UI.S(26), liveRect.Y, liveRect.Width - UI.S(32), liveRect.Height));
            }

            // Packs-Ordner-Knopf unten
            var rb = new RectangleF(UI.S(18), H - UI.S(56), sw - UI.S(36), UI.S(38));
            AddHot("folder", rb, () => Process.Start("explorer.exe", "\"" + packsDir + "\""), true, L.T("Neue Packs einfach als Ordner hier hineinlegen – sie erscheinen automatisch."));
            float hf = Hv("folder");
            using (var path = UI.Round(rb, UI.S(10)))
            {
                using (var b = new SolidBrush(Color.FromArgb((int)(16 + 30 * hf), 255, 255, 255))) g.FillPath(b, path);
                using (var pen = new Pen(Color.FromArgb(40, 255, 255, 255))) g.DrawPath(pen, path);
            }
            using (var f = UI.F(9.5f, FontStyle.Bold)) UI.Txt(g, "＋  " + L.T("Packs-Ordner öffnen"), f, UI.Sub, rb, StringAlignment.Center);
        }

        float Badge(Graphics g, string s, Color c, float x, float y)
        {
            float bw = 0;
            using (var f = UI.F(8f, FontStyle.Bold))
            {
                var sz = g.MeasureString(s, f); var r = new RectangleF(x, y, sz.Width + UI.S(22), UI.S(22));
                using (var path = UI.Round(r, r.Height / 2)) using (var b = new SolidBrush(Color.FromArgb(220, 10, 12, 10))) g.FillPath(b, path);
                using (var path = UI.Round(r, r.Height / 2)) using (var pen = new Pen(UI.A(c, 200), UI.Sf(1.2f))) g.DrawPath(pen, path);
                using (var b = new SolidBrush(c)) g.FillEllipse(b, r.X + UI.S(8), r.Y + r.Height / 2 - UI.S(3), UI.S(6), UI.S(6));
                bw = r.Width; UI.Txt(g, s, f, c, new RectangleF(r.X + UI.S(16), r.Y, r.Width - UI.S(16), r.Height));
            }
            return bw;
        }

        void DrawMain(Graphics g, int W, int H)
        {
            float x0 = SideW + UI.S(40), y0 = TitleH + UI.S(32), w = W - x0 - UI.S(40);
            string active = ActivePack();
            bool isActive = sel != null && sel.Name == active;

            // Vorschau
            float ideal = sel != null && sel.Preview != null ? w * sel.Preview.Height / sel.Preview.Width : w * 0.5f;
            float avail = H - y0 - UI.S(350);
            float hh = (float)Math.Round(Math.Max(UI.S(200), Math.Min(ideal, avail)));
            var hr = new RectangleF((float)Math.Round(x0), (float)Math.Round(y0), (float)Math.Round(w), hh);
            using (var path = UI.Round(hr, UI.S(22)))
            {
                using (var sh = UI.Round(new RectangleF(hr.X + UI.S(4), hr.Y + UI.S(10), hr.Width - UI.S(8), hr.Height), UI.S(20)))
                using (var b = new SolidBrush(Color.FromArgb(110, 0, 0, 0))) g.FillPath(b, sh);
                if (sel != null && sel.Preview != null)
                {
                    var hs = new Size((int)hr.Width, (int)hr.Height);
                    heroSize = hs;
                    if (sel.Hero == null || sel.Hero.Size != hs) { if (sel.Hero != null) sel.Hero.Dispose(); sel.Hero = UI.Rounded(sel.Preview, hs, UI.S(22), true); }
                    g.DrawImageUnscaled(sel.Hero, (int)hr.X, (int)hr.Y);
                    if (fade < 1 && heroFrom != null)
                    {
                        var cm = new ColorMatrix { Matrix33 = 1 - fade };
                        using (var ia = new ImageAttributes()) { ia.SetColorMatrix(cm); g.DrawImage(heroFrom, new Rectangle((int)hr.X, (int)hr.Y, heroFrom.Width, heroFrom.Height), 0, 0, heroFrom.Width, heroFrom.Height, GraphicsUnit.Pixel, ia); }
                    }
                }
                else using (var b = new SolidBrush(UI.Card)) g.FillPath(b, path);
                using (var pen = new Pen(Color.FromArgb(50, 255, 255, 255), UI.Sf(1f))) g.DrawPath(pen, path);
            }
            string title = sel != null ? sel.Name : L.T("Kein Pack");
            using (var f = UI.F(30f, FontStyle.Bold)) UI.Txt(g, title.ToUpperInvariant(), f, UI.Text, new RectangleF(hr.X + UI.S(28), hr.Bottom - UI.S(92), hr.Width - UI.S(56), UI.S(56)));
            if (sel != null)
            {
                string sub = L.F("{0} Icons   ·   {1} Kategorien", sel.Total.ToString("N0"), sel.Counts.Count);
                using (var f = UI.F(9.5f)) UI.Txt(g, sub, f, UI.Sub, new RectangleF(hr.X + UI.S(30), hr.Bottom - UI.S(40), hr.Width - UI.S(60), UI.S(22)));
            }
            if (isActive) Badge(g, L.T("AKTIV IM SPIEL"), UI.Green, hr.X + UI.S(20), hr.Y + UI.S(20));
            if (sel != null && sel.Available) Badge(g, "⬇ " + L.T("NICHT INSTALLIERT"), UI.Orange, hr.X + UI.S(20), hr.Y + UI.S(20));
            if (sel != null && !sel.Available && !sel.Name.Equals("Default", StringComparison.OrdinalIgnoreCase))
            {
                string rt = L.T("Deinstallieren"); float rw; using (var f = UI.F(9f, FontStyle.Bold)) rw = g.MeasureString(rt, f).Width + UI.S(40);
                var rr2 = new RectangleF((float)Math.Round(hr.Right - UI.S(18) - rw), (float)Math.Round(hr.Y + UI.S(16)), (float)Math.Round(rw), UI.S(30));
                bool canRm = !busy && !isActive;
                AddHot("remove", rr2, () => DoRemovePack(sel), canRm, isActive ? L.T("Zum Deinstallieren das Pack zuerst entladen.") : L.T("Pack deinstallieren"));
                float hr2 = Hv("remove");
                using (var path = UI.Round(rr2, rr2.Height / 2))
                {
                    using (var b = new SolidBrush(canRm ? Color.FromArgb((int)(150 + 60 * hr2), (int)(20 + 180 * hr2), 10, 14) : Color.FromArgb(120, 20, 18, 24))) g.FillPath(b, path);
                    using (var pen = new Pen(canRm ? UI.A(UI.Red, (int)(120 + 100 * hr2)) : Color.FromArgb(40, 255, 255, 255))) g.DrawPath(pen, path);
                }
                // kleines Papierkorb-Symbol
                using (var pen = new Pen(canRm ? Color.White : UI.Dim, UI.Sf(1.4f)))
                {
                    float ix = rr2.X + UI.S(14), iy = rr2.Y + UI.S(9);
                    g.DrawLine(pen, ix - UI.S(1), iy + UI.S(2), ix + UI.S(11), iy + UI.S(2));
                    g.DrawRectangle(pen, ix + UI.S(1), iy + UI.S(3), UI.S(8), UI.S(9));
                    g.DrawLine(pen, ix + UI.S(3), iy, ix + UI.S(7), iy);
                }
                using (var f = UI.F(9f, FontStyle.Bold)) UI.Txt(g, rt, f, canRm ? Color.White : UI.Dim, new RectangleF(rr2.X + UI.S(30), rr2.Y, rr2.Width - UI.S(32), rr2.Height));
            }

            if (sel != null && !sel.Available && sel.Update && sel.Rem != null)
            {
                float rw0; using (var f = UI.F(9f, FontStyle.Bold)) rw0 = g.MeasureString(L.T("Deinstallieren"), f).Width + UI.S(40);
                string ut = "⬆  " + L.T("Aktualisieren"); float uw; using (var f = UI.F(9f, FontStyle.Bold)) uw = g.MeasureString(ut, f).Width + UI.S(30);
                var ur = new RectangleF((float)Math.Round(hr.Right - UI.S(18) - rw0 - UI.S(10) - uw), (float)Math.Round(hr.Y + UI.S(16)), (float)Math.Round(uw), UI.S(30));
                bool canUp = !busy && !isActive;
                AddHot("update", ur, () => DoUpdatePack(sel), canUp, isActive ? L.T("Zum Aktualisieren das Pack zuerst entladen.") : L.T("Neue Version herunterladen"));
                float hu = Hv("update");
                using (var path = UI.Round(ur, ur.Height / 2))
                {
                    using (var b = new SolidBrush(canUp ? UI.A(UI.Mix(UI.Green, Color.Black, 0.35f), (int)(170 + 60 * hu)) : Color.FromArgb(120, 20, 18, 24))) g.FillPath(b, path);
                    using (var pen = new Pen(canUp ? UI.A(UI.Green, (int)(130 + 100 * hu)) : Color.FromArgb(40, 255, 255, 255))) g.DrawPath(pen, path);
                }
                using (var f = UI.F(9f, FontStyle.Bold)) using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                using (var b = new SolidBrush(canUp ? Color.White : UI.Dim)) g.DrawString(ut, f, b, ur, sf);
            }

            // Inhalt: Kategorien als Schalter-Kacheln (erst wählen, dann aktivieren)
            float gy = hr.Bottom + UI.S(22);
            if (sel != null)
            {
                var list = Cats.Where(c => sel.Counts.ContainsKey(c)).ToList();
                Func<string, int> cnt = c => { var sp = sel.Available ? sel : (SourceFor(sel, c) ?? sel); int v; return sp.Counts.TryGetValue(c, out v) ? v : 0; };
                int chosen = list.Where(c => useCats.Contains(c)).Sum(c => cnt(c)), total = list.Sum(c => cnt(c));
                bool allOn = list.All(c => useCats.Contains(c));
                float ilw; using (var f = UI.F(8.5f, FontStyle.Bold)) { ilw = g.MeasureString(L.T("INHALT"), f).Width; UI.Txt(g, L.T("INHALT"), f, UI.Dim, new RectangleF(x0 + UI.S(2), gy, UI.S(120), UI.S(20))); }
                int novs = ActiveOv().Count();
                if (novs > 0 && !sel.Available)
                {
                    string ot = "\u2605 " + L.F("{0} Einzel-Icons", novs); float ow; using (var f = UI.F(8.5f, FontStyle.Bold)) ow = g.MeasureString(ot, f).Width + UI.S(18);
                    var orr = new RectangleF(x0 + ilw + UI.S(14), gy - UI.S(2), ow, UI.S(22));
                    AddHot("ov", orr, () => { var m = new PopupMenu(); m.Add("\u2605", L.F("Einzel-Icons zurücksetzen ({0})", iconOv.Count), ResetOverrides, !busy); m.ShowAt(this, PointToScreen(new Point((int)orr.Right, (int)orr.Bottom + UI.S(6)))); }, !busy, L.T("Einzeln gewählte Icons aus anderen Packs – sie werden beim Aktivieren mit übernommen."));
                    float ho = Hv("ov");
                    using (var path = UI.Round(orr, orr.Height / 2)) { using (var b = new SolidBrush(UI.A(UI.Orange, (int)(30 + 30 * ho)))) g.FillPath(b, path); using (var pen = new Pen(UI.A(UI.Orange, 150))) g.DrawPath(pen, path); }
                    using (var f = UI.F(8.5f, FontStyle.Bold)) UI.Txt(g, ot, f, UI.Mix(UI.Orange, Color.White, 0.3f), orr, StringAlignment.Center);
                }
                string allTxt = allOn ? L.T("Alle abwählen") : L.T("Alle auswählen");
                float atw; using (var f = UI.F(9f, FontStyle.Bold)) atw = g.MeasureString(allTxt, f).Width + UI.S(20);
                var ar = new RectangleF((float)Math.Round(x0 + w - atw), gy - UI.S(4), (float)Math.Round(atw), UI.S(26));
                AddHot("cat:*", ar, () => ToggleCat("*"), !busy);
                float ha = Hv("cat:*");
                if (ha > 0.01f) using (var path = UI.Round(ar, UI.S(13))) using (var b = new SolidBrush(Color.FromArgb((int)(30 * ha), 255, 255, 255))) g.FillPath(b, path);
                using (var f = UI.F(9f, FontStyle.Bold)) UI.Txt(g, allTxt, f, UI.Mix(UI.RedHi, Color.White, 0.25f * ha), ar, StringAlignment.Center);
                string sum = L.F("{0} von {1} Icons ausgewählt", chosen.ToString("N0"), total.ToString("N0"));
                using (var f = UI.F(9f)) UI.Txt(g, sum, f, chosen == 0 ? UI.Orange : UI.Sub, new RectangleF(x0 + UI.S(70), gy, w - atw - UI.S(80), UI.S(20)), StringAlignment.Far);
                gy += UI.S(30);
                int cols = w > UI.S(640) ? 4 : 3; float gap = UI.S(10), th = UI.S(48);
                float tw = (float)Math.Floor((w - gap * (cols - 1)) / cols);
                for (int i = 0; i < list.Count; i++)
                {
                    string c = list[i]; bool on = useCats.Contains(c);
                    var tr = new RectangleF((float)Math.Round(x0 + (i % cols) * (tw + gap)), (float)Math.Round(gy + (i / cols) * (th + gap)), tw, th);
                    var cc = c; AddHot("cat:" + c, tr, () => ToggleCat(cc), !busy); tileRects[c] = tr;
                    float hc = Hv("cat:" + c);
                    using (var path = UI.Round(tr, UI.S(12)))
                    {
                        using (var b = new SolidBrush(on ? Color.FromArgb((int)(150 + 40 * hc), 34, 16, 20) : Color.FromArgb((int)(110 + 50 * hc), 22, 20, 26))) g.FillPath(b, path);
                        using (var pen = new Pen(on ? UI.A(UI.Red, (int)(120 + 80 * hc)) : Color.FromArgb((int)(35 + 45 * hc), 255, 255, 255), UI.Sf(1.2f))) g.DrawPath(pen, path);
                    }
                    // Schalter
                    float ov; if (!hv.TryGetValue("on:" + c, out ov)) ov = on ? 1 : 0;
                    float e = ov * ov * (3 - 2 * ov);
                    var sr = new RectangleF(tr.Right - UI.S(48), tr.Y + (th - UI.S(20)) / 2, UI.S(36), UI.S(20));
                    using (var path = UI.Round(sr, sr.Height / 2))
                    using (var b = new SolidBrush(UI.Mix(Color.FromArgb(255, 70, 68, 76), UI.Red, e))) g.FillPath(b, path);
                    float kx = sr.X + UI.S(2) + (sr.Width - UI.S(20)) * e;
                    using (var b = new SolidBrush(Color.FromArgb(40, 0, 0, 0))) g.FillEllipse(b, kx, sr.Y + UI.S(3), UI.S(16), UI.S(16));
                    using (var b = new SolidBrush(UI.Mix(Color.FromArgb(215, 215, 220), Color.White, e))) g.FillEllipse(b, kx, sr.Y + UI.S(2), UI.S(16), UI.S(16));
                    using (var f = UI.F(10f, FontStyle.Bold)) UI.Txt(g, L.T(CatNames[c]), f, on ? UI.Text : UI.Sub, new RectangleF(tr.X + UI.S(14), tr.Y + UI.S(6), tw - UI.S(70), UI.S(20)));
                    string ctx = L.F("{0} Icons", cnt(c).ToString("N0")); float ctw;
                    using (var f = UI.F(8.5f)) { ctw = g.MeasureString(ctx, f).Width; UI.Txt(g, ctx, f, on ? UI.Sub : UI.Dim, new RectangleF(tr.X + UI.S(14), tr.Y + UI.S(25), tw - UI.S(70), UI.S(17))); }
                    // Mischen: Quelle der Kategorie wählen (nur wenn mehrere installierte Packs sie haben)
                    if (!sel.Available && packs.Count(x => !x.Available && x.Counts.ContainsKey(c)) >= 2)
                    {
                        var sp = SourceFor(sel, c) ?? sel; bool mixed = sp != sel;
                        string chip = (mixed ? "\u21C4 " : "") + sp.Name + " \u25BE";
                        float cw2; using (var f = UI.F(7.5f, FontStyle.Bold)) cw2 = Math.Min(g.MeasureString(chip, f).Width + UI.S(12), tw - UI.S(84) - ctw);
                        if (cw2 > UI.S(30))
                        {
                            var crr = new RectangleF(tr.X + UI.S(14) + ctw + UI.S(4), tr.Y + UI.S(25), cw2, UI.S(17));
                            AddHot("src:" + c, crr, () => ShowSourceMenu(cc, crr), !busy, L.T("Aus welchem Pack diese Kategorie kommt – zum Mischen klicken"));
                            float hs = Hv("src:" + c); var ccol = mixed ? UI.Orange : UI.Sub;
                            using (var path = UI.Round(crr, crr.Height / 2)) { using (var b = new SolidBrush(UI.A(ccol, (int)((mixed ? 40 : 18) + 40 * hs)))) g.FillPath(b, path); using (var pen = new Pen(UI.A(ccol, (int)(90 + 80 * hs)))) g.DrawPath(pen, path); }
                            using (var f = UI.F(7.5f, FontStyle.Bold)) using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                            using (var b = new SolidBrush(mixed ? UI.Mix(UI.Orange, Color.White, 0.3f) : UI.Text)) g.DrawString(chip, f, b, crr, sf);
                        }
                    }
                }
                gy += ((list.Count + cols - 1) / cols) * (th + gap) - gap;
            }

            // Hauptknopf
            float by = gy + UI.S(22), bh = UI.S(56);
            float flash = DoneFlash();
            string label; Color acc;
            if (busy) { label = L.T("BITTE WARTEN …"); acc = Color.FromArgb(70, 68, 78); }
            else if (sel == null) { label = L.T("KEIN PACK"); acc = Color.FromArgb(70, 68, 78); }
            else if (sel.Available) { label = "⬇   " + L.T("INSTALLIEREN"); acc = UI.Red; }
            else if (isActive && PlanChanged(sel)) { label = "⟳   " + L.T("ÄNDERUNGEN ÜBERNEHMEN"); acc = UI.Red; }
            else if (isActive) { label = "■   " + L.T("ENTLADEN"); acc = UI.Orange; }
            else if (active != null) { label = "⇄   " + L.F("ZU „{0}“ WECHSELN", sel.Name.ToUpperInvariant()); acc = UI.Red; }
            else { label = "▶   " + L.T("AKTIVIEREN"); acc = UI.Red; }
            float mw = UI.S(340);   // feste Breite – der Knopf bewegt sich nie
            var mr = new RectangleF(x0, by, mw, bh); mainRect = mr;
            bool canMain = !busy && sel != null && (sel.Available || (gamePath != null && (isActive || PlanCats(sel).Any())));
            AddHot("main", mr, DoMainAction, canMain);
            float hmv = Hv("main");
            using (var path = UI.Round(mr, bh / 2))
            {
                if (canMain)
                {
                    float gl = 0.6f + 0.4f * hmv;
                    for (int i = 3; i >= 1; i--)
                        using (var gp = UI.Round(new RectangleF(mr.X - i * UI.S(4), mr.Y - i * UI.S(4), mr.Width + i * UI.S(8), mr.Height + i * UI.S(8)), bh / 2 + i * UI.S(4)))
                        using (var b = new SolidBrush(UI.A(acc, (int)(22 * gl / i)))) g.FillPath(b, gp);
                }
                using (var sp = UI.Round(new RectangleF(mr.X + UI.S(6), mr.Y + UI.S(6) + UI.S(4) * hmv, mr.Width - UI.S(12), mr.Height), bh / 2))
                using (var sb = new SolidBrush(Color.FromArgb((int)(60 + 50 * hmv), 0, 0, 0))) g.FillPath(sb, sp);
                bool down = pressed != null && pressed.Id == "main";
                var c1 = UI.Mix(acc, Color.White, down ? 0f : 0.12f + 0.14f * hmv); var c2 = UI.Mix(acc, Color.Black, down ? 0.45f : 0.3f);
                using (var lg = new LinearGradientBrush(mr, c1, c2, 90f)) g.FillPath(lg, path);
                using (var pen = new Pen(Color.FromArgb(70, 255, 255, 255), UI.Sf(1f))) g.DrawPath(pen, path);
            }
            using (var f = UI.FitF(g, label, 13f, FontStyle.Bold, mw - UI.S(44))) UI.Txt(g, label, f, canMain || busy ? Color.White : UI.Sub, mr, StringAlignment.Center);

            // Zweitknopf: Originale wiederherstellen (nur wenn sinnvoll)
            if (!busy && !isActive && AnyActive() && (sel == null || !sel.Available))
            {
                string s2 = L.T("Originale wiederherstellen");
                float w2 = UI.S(250);
                var r2 = new RectangleF(mr.Right + UI.S(14), by + UI.S(6), w2, bh - UI.S(12));
                AddHot("restore", r2, DoUnload, gamePath != null, L.T("Entlädt das aktive Pack – das Spiel nutzt wieder die Original-Icons."));
                float h2 = Hv("restore");
                using (var path = UI.Round(r2, r2.Height / 2))
                {
                    using (var b = new SolidBrush(Color.FromArgb((int)(22 + 34 * h2), 255, 255, 255))) g.FillPath(b, path);
                    using (var pen = new Pen(Color.FromArgb(60, 255, 255, 255))) g.DrawPath(pen, path);
                }
                using (var f = UI.FitF(g, s2, 10.5f, FontStyle.Bold, w2 - UI.S(32))) UI.Txt(g, s2, f, UI.Text, r2, StringAlignment.Center);
            }

            // Hinweis
            string hint;
            if (gamePath == null) hint = L.T("Spiel nicht gefunden – oben rechts auf „Spiel nicht gefunden“ klicken und den Ordner wählen.");
            else if (sel == null) hint = L.T("Lege einen Pack-Ordner in „Packs“ – er erscheint links automatisch.");
            else if (isActive) hint = L.F("„{0}“ ist im Spiel. „Entladen“ bringt die Original-Icons zurück.", sel.Name);
            else if (active != null) hint = L.F("„{0}“ wird automatisch entladen, danach wird „{1}“ geladen.", active, sel.Name);
            else if (busy) hint = L.T("Bitte warten – Dead by Daylight währenddessen nicht starten.");
            else hint = L.T("Lädt die gewählten Inhalte ins Spiel – die Originale werden vorher automatisch gesichert.");
            if (sel != null && sel.Available && !busy) hint = sel.SourceDir == null && sel.Source == null && sel.Rem != null
                ? L.F("„{0}“ ist noch nicht installiert – „Installieren“ lädt es herunter ({1} MB).", sel.Name, (sel.Rem.Bytes / 1048576).ToString("N0"))
                : L.F("„{0}“ ist noch nicht installiert – „Installieren“ fügt es deinen Packs hinzu.", sel.Name);
            if (sel != null && sel.Update && !sel.Available && !busy && !isActive) hint = L.F("Für „{0}“ gibt es eine neue Version – „Aktualisieren“ lädt sie herunter.", sel.Name);
            if (sel != null && isActive && CatsChanged(sel)) hint = L.T("Inhalt geändert – „Änderungen übernehmen“ lädt das Pack mit der neuen Auswahl neu.");
            using (var f = UI.F(9.5f)) UI.Txt(g, hint, f, UI.Sub, new RectangleF(x0 + UI.S(4), by + bh + UI.S(16), w, UI.S(22)));

            // Ladebalken – eigener, schlanker Bereich unter dem Hinweis; nur sichtbar beim Laden/Entladen
            if (busy || flash > 0) DrawProgress(g, new RectangleF((float)Math.Round(x0), (float)Math.Round(by + bh + UI.S(52)), (float)Math.Round(w), UI.S(34)), flash);

        }

        int doneAt = int.MinValue; string doneMsg = ""; bool doneOk = true;
        float DoneFlash()
        {
            if (doneAt == int.MinValue) return 0;
            int t = Environment.TickCount - doneAt;
            if (t < 2200) return 1; if (t < 2700) return 1 - (t - 2200) / 500f;
            doneAt = int.MinValue; return 0;
        }
        void DrawProgress(Graphics g, RectangleF r, float flash)
        {
            progRect = r;
            float barH = UI.S(6), by2 = r.Bottom - barH;
            var track = new RectangleF(r.X, by2, r.Width, barH);
            int alpha = busy ? 255 : (int)(255 * flash);
            // Zeile über dem Balken: Status links, Prozent rechts
            string left, right; Color tc;
            if (busy) { left = status; right = ((int)Math.Round(Math.Max(0, Math.Min(1, progressShown)) * 100)) + " %"; tc = UI.Text; }
            else { left = (doneOk ? "✓  " : "✕  ") + doneMsg; right = doneOk ? "100 %" : ""; tc = doneOk ? UI.Mix(UI.Green, Color.White, 0.25f) : UI.RedHi; }
            using (var f = UI.F(9.5f, FontStyle.Bold)) UI.Txt(g, left, f, UI.A(tc, alpha), new RectangleF(r.X, r.Y, r.Width - UI.S(60), by2 - r.Y - UI.S(4)));
            using (var f = UI.F(9.5f, FontStyle.Bold)) UI.Txt(g, right, f, UI.A(busy ? UI.Sub : tc, alpha), new RectangleF(r.Right - UI.S(60), r.Y, UI.S(60), by2 - r.Y - UI.S(4)), StringAlignment.Far);
            // Spur
            using (var path = UI.Round(track, barH / 2)) using (var b = new SolidBrush(Color.FromArgb((int)(28 * alpha / 255f), 255, 255, 255))) g.FillPath(b, path);
            float p = busy ? Math.Max(0, Math.Min(1, progressShown)) : 1f;
            if (p <= 0.002f) return;
            var fr = new RectangleF(track.X, track.Y, Math.Max(barH, track.Width * p), barH);
            Color c1 = busy ? UI.Red : UI.A(doneOk ? UI.Green : UI.Red, alpha), c2 = busy ? UI.Orange : UI.A(doneOk ? UI.Mix(UI.Green, Color.White, 0.3f) : UI.RedHi, alpha);
            // weiches Leuchten unter der Füllung
            for (int i = 3; i >= 1; i--)
                using (var gp = UI.Round(RectangleF.Inflate(fr, i * UI.S(2), i * UI.S(2)), barH / 2 + i * UI.S(2)))
                using (var b = new SolidBrush(UI.A(busy ? UI.Red : c1, (int)(18 * alpha / 255f / i)))) g.FillPath(b, gp);
            using (var fp = UI.Round(fr, barH / 2))
            {
                using (var lg = new LinearGradientBrush(new RectangleF(track.X - 1, track.Y, track.Width + 2, barH), c1, c2, 0f)) g.FillPath(lg, fp);
                if (busy)
                {
                    var oc = g.Clip; g.SetClip(fp, CombineMode.Intersect);
                    float sw = UI.S(120), span = fr.Width + sw * 2, sx = fr.X - sw + (Environment.TickCount / 3 % (int)Math.Max(1, span));
                    using (var lg = new LinearGradientBrush(new RectangleF(sx, fr.Y, sw, barH), Color.Transparent, Color.Transparent, 0f))
                    {
                        lg.InterpolationColors = new ColorBlend { Colors = new[] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(110, 255, 235, 210), Color.FromArgb(0, 255, 255, 255) }, Positions = new[] { 0f, 0.5f, 1f } };
                        g.FillRectangle(lg, sx, fr.Y, sw, barH);
                    }
                    g.Clip = oc;
                    // leuchtende Spitze
                    using (var b = new SolidBrush(Color.FromArgb(200, 255, 220, 180))) g.FillEllipse(b, fr.Right - barH, fr.Y, barH, barH);
                }
            }
        }

        // ---------------- Maus ----------------
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var h = hots.LastOrDefault(x => x.R.Contains(e.Location));
            if ((h == null ? null : h.Id) != (over == null ? null : over.Id)) { over = h; anim.Start(); }
            Cursor = h != null && h.Enabled ? Cursors.Hand : Cursors.Default;
            string t = h != null ? h.Tip : null;
            if (t != tipShown) { tipShown = t; if (t != null) tip.Show(t, this, e.X + UI.S(14), e.Y + UI.S(18), 6000); else tip.Hide(this); }
        }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); over = null; anim.Start(); tip.Hide(this); tipShown = null; }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            var h = hots.LastOrDefault(x => x.R.Contains(e.Location));
            if (h == null && e.Button == MouseButtons.Left && e.Y < TitleH) { DragWindow(Handle); return; }
            pressed = h; if (h != null) Inv(h.R, 18);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e); Inv(mainRect, 18);
            var h = hots.LastOrDefault(x => x.R.Contains(e.Location));
            if (h != null && pressed != null && h.Id == pressed.Id && h.Enabled && e.Button == MouseButtons.Left) h.Click();
            pressed = null;
        }
        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            var h = hots.LastOrDefault(x => x.R.Contains(e.Location));
            if (h != null && h.Id.StartsWith("pack:") && h.Enabled) DoMainAction();
        }
        protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); if (e.X < SideW) { scroll -= e.Delta * 0.6f; Invalidate(); } }

        // Hover-Werte weich nachführen
        void TickHover()
        {
            foreach (var h in hots)
            {
                float cur; hv.TryGetValue(h.Id, out cur);
                float t = (over != null && over.Id == h.Id && h.Enabled) ? 1 : 0;
                hv[h.Id] = Math.Abs(cur - t) < 0.01f ? t : cur + (t - cur) * 0.25f;
            }
        }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0231 && anim != null) anim.Stop();       // WM_ENTERSIZEMOVE
            if (m.Msg == 0x0232 && anim != null) anim.Start();      // WM_EXITSIZEMOVE
            base.WndProc(ref m);
        }

        // ---------------- Packs ----------------
        // Online-Katalog (GitHub): verfügbare Packs + Updates
        List<Remote.RPack> catalog = new List<Remote.RPack>(); string cacheDir;
        void FetchCatalog() { FetchCatalog(null); }
        // Live-Abgleich mit GitHub
        bool fetching, liveOk, catalogLive; DateTime liveAt = DateTime.MinValue, liveTry = DateTime.MinValue; List<string> livePending = new List<string>();
        Timer livePoll, liveTick; RectangleF liveRect;
        HashSet<string> autoFailed = new HashSet<string>(StringComparer.OrdinalIgnoreCase); string autoName;
        // done(neue Packs, noch nicht lesbare Packs, Verbindung ok) – läuft im UI-Thread
        void FetchCatalog(Action<List<string>, List<string>, bool> done)
        {
            if (fetching)
            {
                if (done != null) { var t = new Timer { Interval = 400 }; t.Tick += (s2, e2) => { if (fetching) return; t.Stop(); t.Dispose(); FetchCatalog(done); }; t.Start(); }
                return;
            }
            fetching = true; liveTry = DateTime.Now; Inv(liveRect, 2);
            var th = new System.Threading.Thread(() =>
            {
                var pending = new List<string>(); bool ok = false; List<Remote.RPack> c = null;
                try { c = Remote.FetchCatalog(15000, cacheDir, pending); ok = true; } catch { }
                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        fetching = false; liveOk = ok;
                        if (ok)
                        {
                            liveAt = DateTime.Now; catalogLive = true;
                            livePending = pending.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                            Func<List<Remote.RPack>, string> sig = l => string.Join("|", l.OrderBy(r => r.Name).Select(r => r.Name + ":" + r.Stamp + ":" + r.Icons + ":" + (r.Preview ?? "")).ToArray());
                            bool changed = catalog == null || sig(catalog) != sig(c);
                            if (changed)
                            {
                                var before = new HashSet<string>((catalog ?? new List<Remote.RPack>()).Select(r => r.Name), StringComparer.OrdinalIgnoreCase);
                                var after = new HashSet<string>(c.Select(r => r.Name), StringComparer.OrdinalIgnoreCase);
                                if (catalog != null && catalog.Count > 0)
                                {
                                    foreach (var n in after.Where(n => !before.Contains(n))) Log(L.F("Neues Pack verfügbar: {0}", n));
                                    foreach (var n in before.Where(n => !after.Contains(n) && !livePending.Contains(n, StringComparer.OrdinalIgnoreCase))) Log(L.F("„{0}“ ist nicht mehr verfügbar.", n));
                                }
                                try { Directory.CreateDirectory(cacheDir); Remote.SaveCache(c, Path.Combine(cacheDir, "catalog.txt")); } catch { }
                                catalog = c; if (!busy) LoadPacks();
                            }
                            else AutoUpdateNext();
                        }
                        Inv(liveRect, 2);
                        if (done != null)
                        {
                            var fresh = packs.Where(p => p.IsNew).Select(p => p.Name).ToList();
                            done(fresh, pending, ok);
                        }
                    }));
                    if (c != null && c.Any(rp => !string.IsNullOrEmpty(rp.Preview) && !File.Exists(Remote.PreviewFile(rp, cacheDir))))
                    {
                        Remote.FetchPreviews(c, cacheDir);
                        BeginInvoke((Action)(() => { if (!busy) LoadPacks(); }));
                    }
                }
                catch { fetching = false; }
            }) { IsBackground = true };
            th.Start();
        }
        // wie oft geprüft wird: im Vordergrund jede Minute, im Hintergrund seltener, beim Hochladen alle 20 s
        void LivePollTick()
        {
            if (fetching || (busy && livePending.Count == 0)) return;
            double sec = (DateTime.Now - liveTry).TotalSeconds, due;
            if (WindowState == FormWindowState.Minimized) due = 600;
            else if (!liveOk) due = 30;
            else if (livePending.Count > 0) due = 20;
            else if (Form.ActiveForm == this) due = 60;
            else due = 180;
            if (sec >= due) FetchCatalog();
        }
        // installierte Packs mit neuer Version auf GitHub direkt im Hintergrund aktualisieren
        void AutoUpdateNext()
        {
            if (busy || checking || !catalogLive) return;
            string active = ActivePack();
            var p = packs.FirstOrDefault(x => !x.Available && x.Update && x.Rem != null && x.Name != active && !autoFailed.Contains(x.Name + "|" + x.Rem.Stamp));
            if (p == null) return;
            var rem = p.Rem; string name = p.Name, dest = packsDir; autoName = name;
            Log(L.F("„{0}“ wird automatisch aktualisiert …", name));
            Run(L.F("„{0}“ wird automatisch aktualisiert …", name), bw => Remote.Install(rem, dest, (pc, t) => bw.ReportProgress(pc, t)),
                () => { autoName = null; Log(L.F("„{0}“ wurde aktualisiert.", name)); },
                "Pack installiert",
                ex => { autoName = null; autoFailed.Add(name + "|" + rem.Stamp); Log(L.F("Automatisches Update von „{0}“ fehlgeschlagen: {1}", name, ex.Message)); });
        }
        string LiveText(out Color dot)
        {
            string active = ActivePack();
            if (busy && autoName != null) { dot = UI.Orange; return status + "  " + ((int)Math.Round(Math.Max(0, Math.Min(1, progressShown)) * 100)) + " %"; }
            if (fetching && liveAt == DateTime.MinValue) { dot = UI.Orange; return L.T("Server wird geprüft …"); }
            if (!liveOk && liveTry != DateTime.MinValue && !fetching) { dot = UI.RedHi; return L.T("Offline – FogForge versucht es automatisch erneut."); }
            if (livePending.Count > 0) { dot = UI.Orange; return L.F("{0} wird auf dem Server vorbereitet …", string.Join(", ", livePending.ToArray())); }
            var up = packs.FirstOrDefault(x => !x.Available && x.Update && x.Name == active);
            if (up != null) { dot = UI.Orange; return L.F("„{0}“ hat ein Update – zum Aktualisieren zuerst entladen.", up.Name); }
            if (fetching) { dot = UI.Green; return L.T("Server wird geprüft …"); }
            dot = UI.Green;
            if (liveAt == DateTime.MinValue) return L.T("Server wird geprüft …");
            int s = (int)(DateTime.Now - liveAt).TotalSeconds;
            if (s < 10) return L.T("Live · gerade geprüft");
            if (s < 60) return L.F("Live · geprüft vor {0} s", s / 5 * 5);
            return L.F("Live · geprüft vor {0} min", s / 60);
        }
        // ---------------- Programm-Update über GitHub ----------------
        Remote.AppUpdate appUpd; RectangleF updRect; Timer updTimer;
        void CheckAppUpdate() { CheckAppUpdate(null); }
        // done(Update oder null, Verbindung ok) – läuft im UI-Thread
        void CheckAppUpdate(Action<Remote.AppUpdate, bool> done)
        {
            new System.Threading.Thread(() =>
            {
                Remote.AppUpdate u = null; bool ok = false;
                try { u = Remote.CheckAppUpdate(15000); ok = true; } catch { }
                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        if (u != null && !string.IsNullOrEmpty(u.Url) && (appUpd == null || appUpd.Version.CompareTo(u.Version) < 0))
                        {
                            appUpd = u; Log(L.F("FogForge {0} ist verfügbar.", u.Version.ToString(3))); Invalidate();
                            if (updTimer == null) { updTimer = new Timer { Interval = 40 }; updTimer.Tick += (s, e) => { if (updRect.Width > 0 && WindowState != FormWindowState.Minimized) Inv(updRect, 4); }; updTimer.Start(); }
                        }
                        if (done != null) done(appUpd, ok);
                    }));
                }
                catch { }
            }) { IsBackground = true }.Start();
        }
        void DoAppUpdate()
        {
            var u = appUpd; if (u == null || busy) return;
            using (var dlg = new UpdateDialog(u)) if (dlg.ShowDialog(this) != DialogResult.OK) return;
            var tmp = Path.Combine(Path.GetTempPath(), "FogForge_Update_" + u.Version.ToString(3) + ".exe");
            Run(L.T("Update wird heruntergeladen …"), bw => Remote.DownloadUpdate(u, tmp, (pc, t) => bw.ReportProgress(pc, t)), () =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo(tmp, "/update /dir \"" + baseDir + "\" /pid " + Process.GetCurrentProcess().Id) { UseShellExecute = true });
                    BeginInvoke((Action)Close);
                }
                catch (Exception ex) { DarkDialog.Info(this, L.T("Update fehlgeschlagen"), ex.Message); }
            }, "Fertig");
        }
        Remote.RPack Rem(string name) { return catalog.FirstOrDefault(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase)); }
        // Quellen für nachinstallierbare Packs: eigene Installer-Kopie neben FogForge + manuell gewählte Dateien
        List<string> extraSources = new List<string>();
        Dictionary<string, Tuple<DateTime, PayloadFile>> srcCache = new Dictionary<string, Tuple<DateTime, PayloadFile>>(StringComparer.OrdinalIgnoreCase);
        string UninstDir { get { return Path.Combine(backupDir, "Uninstalled Packs"); } }
        IEnumerable<PayloadFile> PackSources()
        {
            var paths = new List<string> { Path.Combine(baseDir, "Uninstall FogForge.exe"), Path.Combine(baseDir, "Uninstall Fog Forge.exe"), Path.Combine(baseDir, "Fog Forge deinstallieren.exe"), Path.Combine(baseDir, "FogForge Setup.exe"), Path.Combine(baseDir, "Fog Forge Setup.exe") };
            paths.AddRange(extraSources);
            foreach (var p in paths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!File.Exists(p)) continue;
                var mt = File.GetLastWriteTimeUtc(p); Tuple<DateTime, PayloadFile> c;
                if (!srcCache.TryGetValue(p, out c) || c.Item1 != mt) { c = Tuple.Create(mt, PayloadFile.Load(p, UI.S(640))); srcCache[p] = c; }
                if (c.Item2 != null) yield return c.Item2;
            }
        }
        static int PackOrder(string n)
        {
            var order = new[] { "default", "realistic", "comic", "cartoon" };
            int i = Array.IndexOf(order, n.ToLowerInvariant()); return i < 0 ? 99 : i;
        }
        void LoadPacks()
        {
            stateKnown = false;
            string keep = keepAfterLoad ?? (sel != null ? sel.Name : null); keepAfterLoad = null;
            foreach (var p in packs)
            {
                if (p.Preview != null) p.Preview.Dispose(); if (p.Backdrop != null) p.Backdrop.Dispose(); if (p.Card != null) p.Card.Dispose(); if (p.Hero != null) p.Hero.Dispose();
                p.Preview = null; p.Backdrop = null; p.Card = null; p.Hero = null; p.Stale = true;
            }
            packs.Clear();
            foreach (var d in Directory.GetDirectories(packsDir).OrderBy(x => PackOrder(Path.GetFileName(x))).ThenBy(x => x))
            {
                var p = new Pack { Name = Path.GetFileName(d), Dir = d };
                foreach (var c in Cats)
                {
                    var cd = Path.Combine(d, c);
                    if (Directory.Exists(cd)) { int n = Directory.GetFiles(cd, "*.png", SearchOption.AllDirectories).Count(f => !Remote.IsPlaceholder(f)); if (n > 0) p.Counts[c] = n; }
                }
                if (p.Counts.Count == 0) continue;
                p.Rem = Rem(p.Name); p.Update = p.Rem != null && Remote.NeedsUpdate(d, p.Rem);
                p.Preview = LoadPreview(p); packs.Add(p);
            }
            var def = packs.FirstOrDefault(x => x.Name.Equals("Default", StringComparison.OrdinalIgnoreCase));
            // Default-Pack: klassisches Vorschaubild (Trapper, Huntress, Meg)
            if (def != null)
                try
                {
                    var pf = Path.Combine(def.Dir, "preview.jpg"); long len = File.Exists(pf) ? new FileInfo(pf).Length : -1;
                    long bannerLen = -1; using (var bs = typeof(MainForm).Assembly.GetManifestResourceStream("banner.jpg")) if (bs != null) bannerLen = bs.Length;
                    if (len < 0 || len == bannerLen || len == 127249 || len == 133020)
                        using (var st = typeof(MainForm).Assembly.GetManifestResourceStream("default.jpg"))
                            if (st != null)
                            {
                                if (def.Preview != null) def.Preview.Dispose();
                                using (var o = File.Create(pf)) st.CopyTo(o);
                                def.Preview = LoadPreview(def); def.Card = null; def.Backdrop = null; def.Hero = null;
                            }
                }
                catch { }
            if (def != null) { packs.Remove(def); packs.Insert(0, def); }
            // Verfügbare (noch nicht installierte) Packs aus dem Installer bzw. gewählten Pack-Dateien
            var seen = new HashSet<string>(packs.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(UninstDir))
                foreach (var d in Directory.GetDirectories(UninstDir).OrderBy(x => PackOrder(Path.GetFileName(x))).ThenBy(x => x))
                {
                    var ap = new Pack { Name = Path.GetFileName(d), Dir = Path.Combine(packsDir, Path.GetFileName(d)), Available = true, SourceDir = d };
                    if (seen.Contains(ap.Name)) continue;
                    foreach (var c in Cats) { var cd = Path.Combine(d, c); if (Directory.Exists(cd)) { int n = Directory.GetFiles(cd, "*.png", SearchOption.AllDirectories).Count(f => !Remote.IsPlaceholder(f)); if (n > 0) ap.Counts[c] = n; } }
                    if (ap.Counts.Count == 0) continue;
                    var tmp = new Pack { Dir = d }; ap.Preview = LoadPreview(tmp);
                    seen.Add(ap.Name); packs.Add(ap);
                }
            foreach (var rp in catalog.OrderBy(x => PackOrder(x.Name)).ThenBy(x => x.Name))
            {
                if (seen.Contains(rp.Name) || rp.Name.Equals("Default", StringComparison.OrdinalIgnoreCase)) continue;
                var ap = new Pack { Name = rp.Name, Available = true, Rem = rp, Dir = Path.Combine(packsDir, rp.Name) };
                foreach (var kv in rp.Counts) if (Cats.Contains(kv.Key)) ap.Counts[kv.Key] = kv.Value;
                if (ap.Counts.Count == 0) continue;
                ap.Preview = Remote.LoadPreview(rp, cacheDir, false);
                seen.Add(ap.Name); packs.Add(ap);
            }
            // „NEU“: Packs, die es beim letzten Mal noch nicht gab
            if (catalog.Count > 0)
            {
                if (this.seen == null) try { if (File.Exists(SeenFile)) this.seen = new HashSet<string>(File.ReadAllLines(SeenFile), StringComparer.OrdinalIgnoreCase); } catch { }
                if (this.seen == null)
                {
                    this.seen = new HashSet<string>(catalog.Select(r => r.Name).Concat(packs.Select(x => x.Name)), StringComparer.OrdinalIgnoreCase);
                    try { Directory.CreateDirectory(Path.GetDirectoryName(SeenFile)); File.WriteAllLines(SeenFile, this.seen.ToArray()); } catch { }
                }
                foreach (var pk in packs) pk.IsNew = pk.Available && pk.Rem != null && !this.seen.Contains(pk.Name);
            }
            foreach (var src in PackSources())
                foreach (var pi in src.Packs.OrderBy(x => PackOrder(x.Name)).ThenBy(x => x.Name))
                {
                    if (seen.Contains(pi.Name) || pi.Name.Equals("Default", StringComparison.OrdinalIgnoreCase)) continue;
                    if (catalogLive && Rem(pi.Name) == null) continue;   // auf GitHub nicht (mehr) verfügbar -> nicht anbieten
                    seen.Add(pi.Name);
                    var ap = new Pack { Name = pi.Name, Available = true, Source = src, Dir = Path.Combine(packsDir, pi.Name) };
                    foreach (var kv in pi.Counts) if (Cats.Contains(kv.Key)) ap.Counts[kv.Key] = kv.Value;
                    if (pi.Preview != null) ap.Preview = new Bitmap(pi.Preview);
                    if (ap.Counts.Count > 0) packs.Add(ap);
                }
            string active = ActivePack();
            var next = packs.FirstOrDefault(x => x.Name == keep) ?? packs.FirstOrDefault(x => x.Name == active) ?? packs.FirstOrDefault();
            sel = null; SelectPack(next, false);
            Invalidate();
            if (!busy && IsHandleCreated) BeginInvoke((Action)AutoUpdateNext);
            WarmThumbs();
        }

        Image LoadPreview(Pack p)
        {
            foreach (var n in new[] { "preview.jpg", "preview.png", "vorschau.jpg", "vorschau.png" })
            {
                var f = Path.Combine(p.Dir, n);
                if (File.Exists(f)) try { using (var i = Image.FromFile(f)) return new Bitmap(i); } catch { }
            }
            var files = new List<string>();
            var cd = Path.Combine(p.Dir, "CharPortraits");
            if (Directory.Exists(cd)) files.AddRange(Directory.GetFiles(cd, "*.png", SearchOption.AllDirectories).Take(6));
            if (files.Count == 0) return null;
            var bmp = new Bitmap(6 * 256, 512);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(40, 12, 18)); g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                for (int i = 0; i < files.Count; i++)
                    try { using (var im = Image.FromFile(files[i])) g.DrawImage(im, UI.Cover(im.Size, new RectangleF(i * 256, 0, 256, 512))); } catch { }
            }
            return bmp;
        }

        Size heroSize; Timer warm;
        void StartWarm()
        {
            if (warm == null) { warm = new Timer { Interval = 40 }; warm.Tick += (s, e) => WarmStep(); }
            warm.Start();
        }
        void WarmStep()
        {
            if (busy || WindowState == FormWindowState.Minimized || Control.MouseButtons != MouseButtons.None) return;
            foreach (var p in packs)
            {
                if (p.Preview == null) continue;
                if (p.Backdrop == null || p.Backdrop.Size != ClientSize) { if (p.Backdrop != null) p.Backdrop.Dispose(); p.Backdrop = UI.Blur(p.Preview, ClientSize, 150); return; }
                if (heroSize.Width > 10 && (p.Hero == null || p.Hero.Size != heroSize))
                {
                    var hs = heroSize; if (p != sel) hs = new Size(heroSize.Width, HeroHeightFor(p));
                    if (p.Hero == null || p.Hero.Size != hs) { if (p.Hero != null) p.Hero.Dispose(); p.Hero = UI.Rounded(p.Preview, hs, UI.S(22), true); return; }
                }
            }
            warm.Stop();
        }
        int HeroHeightFor(Pack p)
        {
            float w = heroSize.Width, H = ClientSize.Height, y0 = TitleH + UI.S(32);
            float ideal = w * p.Preview.Height / p.Preview.Width, avail = H - y0 - UI.S(350);
            return (int)Math.Round(Math.Max(UI.S(200), Math.Min(ideal, avail)));
        }
        // Vorschau-Miniaturen im Hintergrund vorbereiten, damit die Pack-Vorschau sofort alles zeigt
        int thumbGen;
        void WarmThumbs()
        {
            int gen = ++thumbGen;
            var dirs = packs.Where(x => !x.Available && x.Dir != null).OrderBy(x => x == sel ? 0 : 1).Select(x => x.Dir).ToList();
            new System.Threading.Thread(() =>
            {
                System.Threading.Thread.Sleep(1500);
                foreach (var d in dirs) { if (gen != thumbGen || IsDisposed) return; try { ThumbStore.WarmLocal(d, () => gen != thumbGen || IsDisposed); } catch { } }
            }) { IsBackground = true, Priority = System.Threading.ThreadPriority.BelowNormal }.Start();
        }
        void PrefetchThumbs(Pack p)
        {
            if (p == null) return;
            var rp = p.Rem ?? Rem(p.Name); string dir = p.Available ? null : p.Dir;
            if (dir == null && (rp == null || string.IsNullOrEmpty(rp.Thumbs))) return;
            new System.Threading.Thread(() =>
            {
                try { if (dir != null) { if (Directory.Exists(dir)) ThumbStore.ForLocal(dir); } else ThumbStore.FetchRemote(rp); } catch { }
            }) { IsBackground = true, Priority = System.Threading.ThreadPriority.BelowNormal }.Start();
        }
        void SelectPack(Pack p, bool animate = true)
        {
            // Pack-Objekt von vor dem Neuladen (z. B. Klick während der Katalog im Hintergrund aktualisiert wurde) -> aktuelles nehmen
            if (p != null && p.Stale) p = packs.FirstOrDefault(x => x.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase));
            if (p == sel) return;
            sel = p; MarkSeen(p); if (anim != null) anim.Start(); PrefetchThumbs(p);
            if (p != null && p.Preview != null && (p.Backdrop == null || p.Backdrop.Size != ClientSize)) { if (p.Backdrop != null) p.Backdrop.Dispose(); p.Backdrop = UI.Blur(p.Preview, ClientSize, 150); }
            StartWarm();
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e); ApplyRegion();
            if (packs == null || WindowState == FormWindowState.Minimized || ClientSize.Width < 50) return;
            if (sel != null && sel.Backdrop != null && sel.Backdrop.Size == ClientSize) { Invalidate(); return; }
            foreach (var p in packs) { if (p.Backdrop != null) { p.Backdrop.Dispose(); p.Backdrop = null; } }
            if (sel != null && sel.Preview != null && ClientSize.Width > 0) sel.Backdrop = UI.Blur(sel.Preview, ClientSize, 150); StartWarm();
        }

        // ---------------- Spielpfad ----------------
        string LoadGamePath()
        {
            try
            {
                if (File.Exists(settingsFile))
                {
                    var l = File.ReadAllLines(settingsFile).FirstOrDefault(x => x.StartsWith("GamePath="));
                    if (l != null) { var v = l.Substring(9).Trim(); if (Directory.Exists(v)) return v; }
                }
            }
            catch { }
            return null;
        }
        HashSet<string> useCats = new HashSet<string>(Cats);
        // Ältere Versionen nutzten deutsche Datei- und Ordnernamen – einmalig auf Englisch umstellen
        void MigrateOldFiles()
        {
            try
            {
                var old = Path.Combine(baseDir, "einstellungen.txt");
                if (File.Exists(old))
                {
                    var map = new Dictionary<string, string> { { "Spielpfad=", "GamePath=" }, { "Kategorien=", "Categories=" }, { "Sprache=", "Language=" } };
                    var cur = File.Exists(settingsFile) ? File.ReadAllLines(settingsFile).ToList() : new List<string>();
                    foreach (var line in File.ReadAllLines(old))
                        foreach (var kv in map)
                            if (line.StartsWith(kv.Key) && !cur.Any(c => c.StartsWith(kv.Value))) cur.Add(kv.Value + line.Substring(kv.Key.Length));
                    File.WriteAllLines(settingsFile, cur); File.Delete(old);
                }
                if (Directory.Exists(backupDir))
                {
                    foreach (var pair in new[] { new[] { "aktiv.txt", "active.txt" }, new[] { "aktiv_kategorien.txt", "active_categories.txt" }, new[] { "neu_hinzugefuegt.txt", "added_files.txt" } })
                    {
                        var a = Path.Combine(backupDir, pair[0]); var b = Path.Combine(backupDir, pair[1]);
                        if (File.Exists(a) && !File.Exists(b)) File.Move(a, b);
                    }
                    var ou = Path.Combine(backupDir, "Deinstallierte Packs"); var nu = Path.Combine(backupDir, "Uninstalled Packs");
                    if (Directory.Exists(ou) && !Directory.Exists(nu)) Directory.Move(ou, nu);
                    foreach (var d in Directory.GetDirectories(backupDir, "Letzte_Sicherung_*"))
                    { var t = Path.Combine(backupDir, "Last_Backup_" + Path.GetFileName(d).Substring(17)); if (!Directory.Exists(t)) Directory.Move(d, t); }
                }
            }
            catch { }
        }
        string langSetting = "auto";
        void SaveGamePath()
        {
            try
            {
                File.WriteAllText(settingsFile, "GamePath=" + gamePath + Environment.NewLine + "Categories=" + string.Join(",", Cats.Where(c => useCats.Contains(c))) + Environment.NewLine + "Language=" + langSetting + Environment.NewLine
                    + "Mix=" + string.Join(";", mix.Select(kv => kv.Key + ":" + kv.Value)) + Environment.NewLine + "AutoReapply=" + (autoReapply ? "1" : "0") + Environment.NewLine);
            }
            catch { }
        }
        void LoadLang()
        {
            try
            {
                if (File.Exists(settingsFile)) { var l = File.ReadAllLines(settingsFile).FirstOrDefault(x => x.StartsWith("Language=")); if (l != null) langSetting = l.Substring(9).Trim(); }
            }
            catch { }
            if (langSetting != "auto" && !L.Codes.Contains(langSetting)) langSetting = "auto";
            L.Code = langSetting == "auto" ? L.SystemCode() : langSetting;
        }
        void SetLang(string code)
        {
            langSetting = code; L.Code = code == "auto" ? L.SystemCode() : code; SaveGamePath();
            status = gamePath != null ? L.T("Bereit.") : L.T("Spiel nicht gefunden – oben rechts auswählen.");
            tipShown = null; Invalidate();
        }
        DateTime langClosed;
        void ShowLangMenu(RectangleF anchor)
        {
            if ((DateTime.Now - langClosed).TotalMilliseconds < 250) return;
            var m = new PopupMenu { MenuWidth = 300 };
            string sys = L.SystemCode();
            m.AddFlag(sys, L.T("Systemsprache") + " (" + L.Name(sys) + ")", () => SetLang("auto"), langSetting == "auto");
            m.Sep();
            for (int i = 0; i < L.Codes.Length; i++) { var c = L.Codes[i]; m.AddFlag(c, L.Names[i], () => SetLang(c), langSetting == c); }
            m.FormClosed += (s, e) => { langClosed = DateTime.Now; m.Dispose(); };
            m.ShowAt(this, PointToScreen(new Point((int)anchor.Right, (int)anchor.Bottom + UI.S(8))));
        }
        void LoadCats()
        {
            try
            {
                if (!File.Exists(settingsFile)) return;
                var all = File.ReadAllLines(settingsFile);
                var l = all.FirstOrDefault(x => x.StartsWith("Categories="));
                if (l != null) useCats = new HashSet<string>(l.Substring(11).Split(',').Where(c => Cats.Contains(c)));
                var mx = all.FirstOrDefault(x => x.StartsWith("Mix="));
                if (mx != null) foreach (var part in mx.Substring(4).Split(';')) { var kv = part.Split(':'); if (kv.Length == 2 && Cats.Contains(kv[0]) && kv[1].Length > 0) mix[kv[0]] = kv[1]; }
                var ar = all.FirstOrDefault(x => x.StartsWith("AutoReapply="));
                if (ar != null) autoReapply = ar.Substring(12).Trim() == "1";
            }
            catch { }
        }
        // ---------------- Mix + Einzel-Icons ----------------
        Dictionary<string, string> mix = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);       // Kategorie -> Pack
        Dictionary<string, string> iconOv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);    // Icon-Pfad -> Pack
        bool autoReapply;
        string OvFile { get { return Path.Combine(baseDir, "icon_overrides.txt"); } }
        void LoadOv()
        {
            iconOv.Clear();
            try { if (File.Exists(OvFile)) foreach (var l in File.ReadAllLines(OvFile)) { var p = l.Split('|'); if (p.Length == 2 && p[0].Length > 0 && p[1].Length > 0) iconOv[p[0].Replace('/', '\\')] = p[1]; } } catch { }
        }
        void SaveOv() { try { File.WriteAllLines(OvFile, iconOv.Select(kv => kv.Key + "|" + kv.Value).ToArray()); } catch { } }
        string GetOv(string rel) { string v; return iconOv.TryGetValue(rel.Replace('/', '\\'), out v) ? v : null; }
        void SetOv(string rel, string pack)
        {
            rel = rel.Replace('/', '\\');
            if (pack == null) iconOv.Remove(rel); else iconOv[rel] = pack;
            SaveOv(); if (!IsDisposed) Invalidate();
        }
        void ResetOverrides()
        {
            if (DarkDialog.Ask(this, L.T("Einzel-Icons zurücksetzen"), L.F("Alle {0} einzeln gewählten Icons zurücksetzen?", iconOv.Count), UI.Orange, L.T("Zurücksetzen"), L.T("Abbrechen")) != 0) return;
            iconOv.Clear(); SaveOv(); Invalidate();
        }
        Pack FindInstalled(string name) { return packs.FirstOrDefault(p => !p.Available && p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && Directory.Exists(p.Dir)); }
        // Aus welchem Pack kommt eine Kategorie? (Mix hat Vorrang, sonst das gewählte Pack)
        Pack SourceFor(Pack basePack, string cat)
        {
            string m; if (mix.TryGetValue(cat, out m)) { var pk = FindInstalled(m); if (pk != null && pk.Counts.ContainsKey(cat)) return pk; }
            return basePack != null && basePack.Counts.ContainsKey(cat) ? basePack : null;
        }
        List<string> PlanCats(Pack basePack) { return Cats.Where(c => useCats.Contains(c) && SourceFor(basePack, c) != null).ToList(); }
        IEnumerable<KeyValuePair<string, string>> ActiveOv() { return iconOv.Where(kv => useCats.Contains(kv.Key.Split('\\')[0]) && FindInstalled(kv.Value) != null); }
        Dictionary<string, string> BuildPlan(Pack basePack)
        {
            var plan = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in PlanCats(basePack))
            {
                var src = SourceFor(basePack, c); var d = Path.Combine(src.Dir, c);
                if (!Directory.Exists(d)) continue;
                foreach (var f in Directory.GetFiles(d, "*.png", SearchOption.AllDirectories)) plan[f.Substring(src.Dir.Length + 1)] = f;
            }
            foreach (var kv in ActiveOv().ToList()) { var f = Path.Combine(FindInstalled(kv.Value).Dir, kv.Key); if (File.Exists(f)) plan[kv.Key] = f; }
            return plan;
        }
        string PlanSig(Pack basePack)
        {
            return basePack.Name + "|" + string.Join(",", PlanCats(basePack).Select(c => c + "=" + SourceFor(basePack, c).Name)) + "|" + string.Join(";", ActiveOv().OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).Select(kv => kv.Key + "=" + kv.Value));
        }
        string PlanFile { get { return Path.Combine(backupDir, "active_plan.txt"); } }
        string AppliedFile { get { return Path.Combine(backupDir, "applied_files.txt"); } }
        bool PlanChanged(Pack p)
        {
            if (!File.Exists(PlanFile)) return CatsChanged(p);
            try { return File.ReadAllText(PlanFile).Trim() != PlanSig(p); } catch { return false; }
        }
        void ShowSourceMenu(string cat, RectangleF r)
        {
            if (busy || sel == null) return;
            var cur = SourceFor(sel, cat) ?? sel;
            var m = new PopupMenu();
            foreach (var p in packs.Where(x => !x.Available && x.Counts.ContainsKey(cat)).ToList())
            {
                var pk = p;
                m.Add(p == cur ? "●" : "○", p == sel ? L.F("{0} (ausgewähltes Pack)", p.Name) : p.Name, () =>
                {
                    if (pk == sel) mix.Remove(cat); else mix[cat] = pk.Name;
                    SaveGamePath(); Invalidate(); anim.Start();
                });
            }
            m.ShowAt(this, PointToScreen(new Point((int)r.Right, (int)r.Bottom + UI.S(6))));
        }

        // ---------------- Nach Spiel-Updates erneut anwenden ----------------
        DateTime lastDriftCheck = DateTime.MinValue; int driftDismissed = -1; bool driftAsking;
        Dictionary<string, string> ReadApplied()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try { if (File.Exists(AppliedFile)) foreach (var l in File.ReadAllLines(AppliedFile)) { var p = l.Split('\t'); if (p.Length == 2) map[p[0]] = p[1]; } } catch { }
            return map;
        }
        List<string> FindDrift(string game)
        {
            var res = new List<string>();
            if (game == null || !Directory.Exists(game)) return res;
            foreach (var kv in ReadApplied())
            {
                try
                {
                    var src = new FileInfo(kv.Value); if (!src.Exists) continue;
                    var dst = new FileInfo(Path.Combine(game, kv.Key));
                    if (!dst.Exists || dst.Length != src.Length) res.Add(kv.Key);
                }
                catch { }
            }
            return res;
        }
        static bool GameRunning() { return Process.GetProcesses().Any(p => { try { return p.ProcessName.StartsWith("DeadByDaylight", StringComparison.OrdinalIgnoreCase); } catch { return false; } }); }
        void CheckDrift()
        {
            if (busy || driftAsking || ActivePack() == null || gamePath == null) return;
            lastDriftCheck = DateTime.Now; string game = gamePath;
            new System.Threading.Thread(() =>
            {
                List<string> drift; try { drift = FindDrift(game); } catch { return; }
                if (drift.Count == 0 || GameRunning()) return;
                try { BeginInvoke((Action)(() => OnDrift(drift))); } catch { }
            }) { IsBackground = true }.Start();
        }
        void OnDrift(List<string> drift)
        {
            if (busy || driftAsking) return;
            string pack = ActivePack(); if (pack == null) return;
            if (autoReapply) { Reapply(drift); return; }
            if (drift.Count == driftDismissed) return;
            driftAsking = true;
            int r = DarkDialog.Ask(this, L.T("Spiel wurde aktualisiert"),
                L.F("Dead by Daylight wurde aktualisiert und hat dabei {0} Icons von „{1}“ zurückgesetzt.\nSollen sie wieder eingespielt werden?", drift.Count.ToString("N0"), pack),
                UI.Red, L.T("Jetzt erneut anwenden"), L.T("Immer automatisch"), L.T("Später"));
            driftAsking = false;
            if (r == 1) { autoReapply = true; SaveGamePath(); }
            if (r == 0 || r == 1) Reapply(drift); else driftDismissed = drift.Count;
        }
        void Reapply(List<string> drift)
        {
            if (busy || gamePath == null) return;
            var applied = ReadApplied(); string game = gamePath, pack = ActivePack(); int n = 0;
            Run(L.T("Pack wird erneut angewendet …"), bw =>
            {
                var orig = Path.Combine(backupDir, "Original");
                var added = new HashSet<string>(File.Exists(AddedFile) ? File.ReadAllLines(AddedFile) : new string[0], StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < drift.Count; i++)
                {
                    string rel = drift[i], src; if (!applied.TryGetValue(rel, out src) || !File.Exists(src)) continue;
                    var dst = Path.Combine(game, rel); var bak = Path.Combine(orig, rel);
                    // das Spiel-Update hat ggf. neue Originale mitgebracht -> diese sichern
                    if (File.Exists(dst) && !added.Contains(rel)) { Directory.CreateDirectory(Path.GetDirectoryName(bak)); File.Copy(dst, bak, true); }
                    Directory.CreateDirectory(Path.GetDirectoryName(dst)); File.Copy(src, dst, true); n++;
                    if (i % 25 == 0) bw.ReportProgress(i * 100 / Math.Max(1, drift.Count), L.F("„{0}“ wird erneut angewendet … {1} / {2}", pack, i + 1, drift.Count));
                }
            }, () => { driftDismissed = -1; Log(L.F("„{0}“ wurde nach dem Spiel-Update erneut angewendet ({1} Icons).", pack, n.ToString("N0"))); }, "Fertig – das Pack ist im Spiel");
        }

        // ---------------- Updates im laufenden Betrieb ----------------
        DateTime lastOnlineCheck = DateTime.Now; Timer periodic;
        bool checking;
        void CheckNow(bool manual)
        {
            lastOnlineCheck = DateTime.Now;
            if (!manual) { FetchCatalog(); CheckAppUpdate(); CheckDrift(); return; }
            if (checking) return;
            checking = true; Log(L.T("Suche nach Updates …")); Cursor = Cursors.AppStarting;
            List<string> fresh = null, pending = null; Remote.AppUpdate au = null; bool okCat = false, okApp = false; int left = 2;
            Action fin = () =>
            {
                if (--left > 0) return;
                checking = false; Cursor = Cursors.Default; CheckDrift();
                var lines = new List<string>();
                if (au != null) lines.Add(L.F("FogForge {0} ist verfügbar.", au.Version.ToString(3)));
                if (fresh != null && fresh.Count > 0) lines.Add(L.F("Neue Packs: {0}", string.Join(", ", fresh.ToArray())));
                if (pending != null && pending.Count > 0) lines.Add(L.F("{0} wird auf dem Server noch vorbereitet – FogForge versucht es automatisch weiter.", string.Join(", ", pending.ToArray())));
                if (!okCat && !okApp) lines.Add(L.T("Keine Verbindung zum Server. Bitte später erneut versuchen."));
                else if (lines.Count == 0) lines.Add(L.T("Alles ist auf dem neuesten Stand."));
                Log(lines[0]);
                DarkDialog.Info(this, L.T("Nach Updates suchen"), string.Join("\n\n", lines.ToArray()));
            };
            FetchCatalog((f, p, ok) => { fresh = f; pending = p; okCat = ok; fin(); });
            CheckAppUpdate((u, ok) => { au = u; okApp = ok; fin(); });
        }
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            if (busy) return;
            if ((DateTime.Now - lastOnlineCheck).TotalMinutes >= 10) CheckNow(false);
            else if ((DateTime.Now - lastDriftCheck).TotalMinutes >= 2) CheckDrift();
        }

        // ---------------- „NEU“-Markierung ----------------
        HashSet<string> seen;
        string SeenFile { get { return Path.Combine(cacheDir ?? baseDir, "seen.txt"); } }
        void MarkSeen(Pack p)
        {
            if (p == null || !p.IsNew || seen == null) return;
            p.IsNew = false; seen.Add(p.Name);
            try { Directory.CreateDirectory(Path.GetDirectoryName(SeenFile)); File.WriteAllLines(SeenFile, seen.ToArray()); } catch { }
        }

        string CatsFile { get { return Path.Combine(backupDir, "active_categories.txt"); } }
        HashSet<string> LoadedCats() { if (!stateKnown) RefreshState(); return stateCats; }
        IEnumerable<string> PackCats(Pack p) { return Cats.Where(c => p.Counts.ContainsKey(c) && useCats.Contains(c)); }
        bool CatsChanged(Pack p)
        {
            var lc = LoadedCats(); if (lc == null) return false;
            return !new HashSet<string>(PackCats(p)).SetEquals(lc);
        }
        void ToggleCat(string c)
        {
            if (busy) return;
            if (c == "*") { if (useCats.Count == Cats.Length) useCats.Clear(); else useCats = new HashSet<string>(Cats); }
            else if (!useCats.Remove(c)) useCats.Add(c);
            SaveGamePath(); Invalidate(); anim.Start();
        }

        string DetectGame()
        {
            var libs = new List<string>();
            foreach (var key in new[] { @"SOFTWARE\WOW6432Node\Valve\Steam", @"SOFTWARE\Valve\Steam" })
                try { using (var k = Registry.LocalMachine.OpenSubKey(key)) { var v = k != null ? k.GetValue("InstallPath") as string : null; if (v != null) libs.Add(v); } } catch { }
            try { using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")) { var v = k != null ? k.GetValue("SteamPath") as string : null; if (v != null) libs.Add(v.Replace('/', '\\')); } } catch { }
            foreach (var s in libs.ToArray())
            {
                var vdf = Path.Combine(s, @"steamapps\libraryfolders.vdf");
                try { if (File.Exists(vdf)) foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s*\"([^\"]+)\"")) libs.Add(m.Groups[1].Value.Replace(@"\\", @"\")); } catch { }
            }
            foreach (var d in DriveInfo.GetDrives().Where(x => x.DriveType == DriveType.Fixed))
            { libs.Add(Path.Combine(d.Name, "Steam")); libs.Add(Path.Combine(d.Name, "SteamLibrary")); libs.Add(Path.Combine(d.Name, @"Program Files (x86)\Steam")); }
            foreach (var l in libs.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var p = Path.Combine(l, GameRel);
                if (Directory.Exists(p)) { gamePath = p; SaveGamePath(); return p; }
            }
            return null;
        }

        void ChooseGame()
        {
            if (busy) return;
            using (var fb = new FolderBrowserDialog { Description = L.T("Wähle den Dead-by-Daylight-Ordner (oder …\\Content\\UI\\Icons)") })
            {
                if (gamePath != null) fb.SelectedPath = gamePath;
                if (fb.ShowDialog(this) != DialogResult.OK) return;
                var p = fb.SelectedPath;
                foreach (var cand in new[] { p, Path.Combine(p, @"DeadByDaylight\Content\UI\Icons"), Path.Combine(p, @"Content\UI\Icons"), Path.Combine(p, @"UI\Icons") })
                    if (Directory.Exists(Path.Combine(cand, "Perks")) || Directory.Exists(Path.Combine(cand, "CharPortraits")))
                    { gamePath = cand; SaveGamePath(); Log(L.F("Spielordner gesetzt: {0}", cand)); status = L.T("Spielordner gesetzt."); Invalidate(); EnsureDefault(); return; }
                DarkDialog.Info(this, L.T("Spielordner"), L.T("In diesem Ordner wurden keine Dead-by-Daylight-Icons gefunden."));
            }
        }

        // ---------------- Zustand ----------------
        string StateFile { get { return Path.Combine(backupDir, "active.txt"); } }
        string AddedFile { get { return Path.Combine(backupDir, "added_files.txt"); } }
        bool stateKnown; string stateActive; bool stateAny; HashSet<string> stateCats;
        void RefreshState()
        {
            try { stateActive = File.Exists(StateFile) ? File.ReadAllText(StateFile).Trim() : null; } catch { stateActive = null; }
            if (stateActive == "") stateActive = null;
            stateAny = stateActive != null || Directory.Exists(Path.Combine(backupDir, "Original"));
            try { stateCats = File.Exists(CatsFile) ? new HashSet<string>(File.ReadAllText(CatsFile).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) : null; } catch { stateCats = null; }
            stateKnown = true;
        }
        string ActivePack() { if (!stateKnown) RefreshState(); return stateActive; }
        bool AnyActive() { if (!stateKnown) RefreshState(); return stateAny; }

        void Log(string s)
        {
            if (InvokeRequired) { BeginInvoke((Action)(() => Log(s))); return; }
            logLines.Add("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + s);
            status = s; Invalidate();
        }

        bool CheckGameClosed()
        {
            bool running = Process.GetProcesses().Any(p => { try { return p.ProcessName.StartsWith("DeadByDaylight", StringComparison.OrdinalIgnoreCase); } catch { return false; } });
            if (!running) return true;
            return DarkDialog.Ask(this, L.T("Spiel läuft"), L.T("Dead by Daylight läuft gerade.\nBitte zuerst das Spiel schließen, sonst werden die Icons evtl. nicht übernommen."),
                UI.Orange, L.T("Trotzdem fortfahren"), L.T("Abbrechen")) == 0;
        }

        static string DoneText(string title) // title = Schlüssel der Erfolgsmeldung
        {
            return L.T(title);
        }
        void Run(string title, Action<BackgroundWorker> job, Action done, string doneKey = "Fertig", Action<Exception> onError = null)
        {
            busy = true; progress = 0; progressShown = 0; status = title; Invalidate();
            DoWorkEventHandler w = null; RunWorkerCompletedEventHandler c = null;
            w = (s, e) => job(worker);
            c = (s, e) =>
            {
                worker.DoWork -= w; worker.RunWorkerCompleted -= c; busy = false; stateKnown = false;
                if (e.Error != null) { if (onError != null) onError(e.Error); else { Log(L.F("Fehler: {0}", e.Error.Message)); DarkDialog.Info(this, L.T("Fehler"), e.Error.Message); } }
                else { progress = 1; if (done != null) done(); }
                doneOk = e.Error == null; doneMsg = doneOk ? DoneText(doneKey) : L.T("Fehlgeschlagen"); doneAt = Environment.TickCount;
                LoadPacks(); anim.Start();
            };
            worker.DoWork += w; worker.RunWorkerCompleted += c;
            worker.RunWorkerAsync();
        }

        // ---------------- Kernlogik ----------------
        void RestoreCore(BackgroundWorker bw, string game, int pFrom, int pTo, ref int restored, ref int removed)
        {
            var orig = Path.Combine(backupDir, "Original");
            if (Directory.Exists(orig))
            {
                var files = Directory.GetFiles(orig, "*", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    var rel = files[i].Substring(orig.Length + 1); var dst = Path.Combine(game, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(dst)); File.Copy(files[i], dst, true); restored++;
                    if (i % 25 == 0) bw.ReportProgress(pFrom + i * (pTo - pFrom) / Math.Max(1, files.Length), L.F("Originale zurückschreiben … {0} / {1}", i + 1, files.Length));
                }
            }
            if (File.Exists(AddedFile))
                foreach (var rel in File.ReadAllLines(AddedFile))
                { var f = Path.Combine(game, rel); if (rel.Length > 0 && File.Exists(f)) { File.Delete(f); removed++; } }
            if (Directory.Exists(orig))
            {
                var stamp = Path.Combine(backupDir, "Last_Backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                Directory.Move(orig, stamp);
                foreach (var old in Directory.GetDirectories(backupDir, "Last_Backup_*").OrderByDescending(x => x).Skip(1)) try { Directory.Delete(old, true); } catch { }
            }
            if (File.Exists(AddedFile)) File.Delete(AddedFile);
            if (File.Exists(StateFile)) File.Delete(StateFile);
            if (File.Exists(CatsFile)) File.Delete(CatsFile);
            if (File.Exists(PlanFile)) File.Delete(PlanFile);
            if (File.Exists(AppliedFile)) File.Delete(AppliedFile);
        }

        void ImportCore(BackgroundWorker bw, Pack pack, Dictionary<string, string> plan, string sig, List<string> cats, string game, int pFrom, int pTo, ref int copied, ref int backed)
        {
            var orig = Path.Combine(backupDir, "Original"); Directory.CreateDirectory(orig);
            var added = new HashSet<string>(File.Exists(AddedFile) ? File.ReadAllLines(AddedFile) : new string[0], StringComparer.OrdinalIgnoreCase);
            var items = plan.ToList();
            for (int i = 0; i < items.Count; i++)
            {
                var rel = items[i].Key; var dst = Path.Combine(game, rel); var bak = Path.Combine(orig, rel);
                if (File.Exists(dst)) { if (!File.Exists(bak) && !added.Contains(rel)) { Directory.CreateDirectory(Path.GetDirectoryName(bak)); File.Copy(dst, bak); backed++; } }
                else if (!File.Exists(bak)) added.Add(rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dst)); File.Copy(items[i].Value, dst, true); copied++;
                if (i % 25 == 0) bw.ReportProgress(pFrom + i * (pTo - pFrom) / Math.Max(1, items.Count), L.F("„{0}“ wird geladen … {1} / {2}", pack.Name, i + 1, items.Count));
            }
            File.WriteAllLines(AddedFile, added.ToArray());
            File.WriteAllText(StateFile, pack.Name);
            File.WriteAllText(CatsFile, string.Join(",", cats));
            File.WriteAllText(PlanFile, sig);
            File.WriteAllLines(AppliedFile, items.Select(kv => kv.Key + "\t" + kv.Value).ToArray());
        }

        void DoMainAction()
        {
            if (sel == null || busy) return;
            if (sel.Available) { DoInstallPack(sel); return; }
            if (sel.Name == ActivePack() && !PlanChanged(sel)) DoUnload(); else DoActivate();
        }

        void DoActivate()
        {
            string old = ActivePack();
            if (!PlanCats(sel).Any()) { DarkDialog.Info(this, L.T("Nichts ausgewählt"), L.T("Bitte mindestens eine Kategorie anhaken.")); return; }
            if (!CheckGameClosed()) return;
            var pack = sel; string game = gamePath; bool hadBackup = AnyActive();
            var plan = BuildPlan(pack); var sig = PlanSig(pack); var pcats = PlanCats(pack);
            int copied = 0, backed = 0, restored = 0, removed = 0;
            Log(old != null ? L.F("Wechsle von „{0}“ zu „{1}“ …", old, pack.Name) : L.F("Aktiviere „{0}“ …", pack.Name));
            Run(old != null ? L.F("Wechsle zu „{0}“ …", pack.Name) : L.F("„{0}“ wird geladen …", pack.Name), bw =>
            {
                if (hadBackup) RestoreCore(bw, game, 0, 40, ref restored, ref removed);
                ImportCore(bw, pack, plan, sig, pcats, game, hadBackup ? 40 : 0, 100, ref copied, ref backed);
            }, () => Log(old != null ? L.F("„{0}“ ist aktiv – {1} Icons im Spiel, „{2}“ entladen.", pack.Name, copied.ToString("N0"), old) : L.F("„{0}“ ist aktiv – {1} Icons im Spiel.", pack.Name, copied.ToString("N0"))), "Fertig – das Pack ist im Spiel");
        }

        void DoUnload()
        {
            if (!AnyActive() || busy) return;
            string old = ActivePack();
            if (DarkDialog.Ask(this, L.T("Entladen"), old != null ? L.F("„{0}“ entladen und die Original-Icons wiederherstellen?", old) : L.T("Das aktive Pack entladen und die Original-Icons wiederherstellen?"),
                UI.Orange, L.T("Entladen"), L.T("Abbrechen")) != 0) return;
            if (!CheckGameClosed()) return;
            string game = gamePath; int restored = 0, removed = 0;
            Run(L.T("Originale werden wiederhergestellt …"), bw => RestoreCore(bw, game, 0, 100, ref restored, ref removed),
                () => Log(L.F("Entladen – das Spiel nutzt wieder die Original-Icons ({0} Dateien).", restored.ToString("N0"))), "Entladen – Original-Icons sind zurück");
        }

        void DoInstallPack(Pack p)
        {
            if (p == null || !p.Available || busy) return;
            var src = p.Source; string name = p.Name, dest = packsDir, srcDir = p.SourceDir; int n = 0; var rem = p.Rem;
            Run(srcDir == null && src == null && rem != null ? L.F("„{0}“ wird heruntergeladen …", name) : L.F("„{0}“ wird installiert …", name), bw =>
            {
                if (srcDir != null)
                {
                    var target = Path.Combine(dest, name);
                    if (Directory.Exists(target)) Directory.Delete(target, true);
                    Directory.Move(srcDir, target); bw.ReportProgress(100);
                }
                else if (src == null && rem != null) Remote.Install(rem, dest, (pc, t) => bw.ReportProgress(pc, t));
                else src.Extract(name, dest, (a, b) => { n = b; bw.ReportProgress(a * 100 / Math.Max(1, b), L.F("„{0}“ wird installiert … {1} / {2}", name, a, b)); });
            }, () => { Log(L.F("„{0}“ installiert ({1} Icons).", name, p.Total.ToString("N0"))); keepAfterLoad = name; }, "Pack installiert");
        }
        string keepAfterLoad;
        void DoUpdatePack(Pack p)
        {
            if (p == null || p.Available || p.Rem == null || busy) return;
            if (p.Name == ActivePack()) { DarkDialog.Info(this, L.T("Aktualisieren"), L.T("Zum Aktualisieren das Pack zuerst entladen.")); return; }
            var rem = p.Rem; string name = p.Name, dest = packsDir;
            Run(L.F("„{0}“ wird heruntergeladen …", name), bw => Remote.Install(rem, dest, (pc, t) => bw.ReportProgress(pc, t)),
                () => { Log(L.F("„{0}“ wurde aktualisiert.", name)); keepAfterLoad = name; }, "Pack installiert");
        }
        void DoRemovePack(Pack p)
        {
            if (p == null || p.Available || busy || p.Name.Equals("Default", StringComparison.OrdinalIgnoreCase)) return;
            if (p.Name == ActivePack()) { DarkDialog.Info(this, L.T("Pack deinstallieren"), L.T("Zum Deinstallieren das Pack zuerst entladen.")); return; }
            bool online = Rem(p.Name) != null;
            if (DarkDialog.Ask(this, L.T("Pack deinstallieren"), online ? L.F("„{0}“ deinstallieren?\nDas Pack wird gelöscht und kann jederzeit wieder heruntergeladen werden.", p.Name) : L.F("„{0}“ deinstallieren?\nDu kannst es danach jederzeit wieder installieren.", p.Name),
                UI.Red, L.T("Deinstallieren"), L.T("Abbrechen")) != 0) return;
            try
            {
                if (p.Preview != null) { p.Preview.Dispose(); p.Preview = null; }
                if (online)
                {
                    Directory.Delete(p.Dir, true);
                    Log(L.F("„{0}“ deinstalliert.", p.Name));
                    keepAfterLoad = p.Name; sel = null; LoadPacks(); return;
                }
                Directory.CreateDirectory(UninstDir);
                var target = Path.Combine(UninstDir, p.Name);
                if (Directory.Exists(target)) Directory.Delete(target, true);
                Directory.Move(p.Dir, target);
                Log(L.F("„{0}“ deinstalliert.", p.Name));
                keepAfterLoad = p.Name;
            }
            catch (Exception ex) { DarkDialog.Info(this, L.T("Fehler"), ex.Message); }
            sel = null; LoadPacks();
        }
        void PickPackSource()
        {
            using (var od = new OpenFileDialog { Title = L.T("Pack-Datei wählen"), Filter = L.T("FogForge-Installer oder Pack-ZIP") + "|*.exe;*.zip" })
            {
                if (od.ShowDialog(this) != DialogResult.OK) return;
                var pf = PayloadFile.Load(od.FileName, UI.S(640));
                if (pf == null) { DarkDialog.Info(this, L.T("Packs aus Datei installieren …"), L.T("In dieser Datei wurden keine Packs gefunden.")); return; }
                if (!extraSources.Contains(od.FileName)) extraSources.Add(od.FileName);
                LoadPacks();
                var first = packs.FirstOrDefault(x => x.Available && x.Source != null && x.Source.Path == od.FileName);
                if (first != null) SelectPack(first);
            }
        }

        void DoExportGame()
        {
            if (gamePath == null || busy) return;
            string name = "Export_" + DateTime.Now.ToString("yyyy-MM-dd_HHmm"), dst = Path.Combine(packsDir, name), game = gamePath; int n = 0;
            Run(L.T("Spiel-Icons werden gesichert …"), bw =>
            {
                var files = new List<string>();
                foreach (var c in Cats) { var d = Path.Combine(game, c); if (Directory.Exists(d)) files.AddRange(Directory.GetFiles(d, "*.png", SearchOption.AllDirectories)); }
                for (int i = 0; i < files.Count; i++)
                {
                    var t = Path.Combine(dst, files[i].Substring(game.Length).TrimStart('\\'));
                    Directory.CreateDirectory(Path.GetDirectoryName(t)); File.Copy(files[i], t, true); n++;
                    if (i % 25 == 0) bw.ReportProgress(i * 100 / Math.Max(1, files.Count), L.F("Sichere {0} / {1}", i + 1, files.Count));
                }
            }, () => Log(L.F("Gesichert als neues Pack „{0}“ ({1} Icons).", name, n.ToString("N0"))));
        }

        void DoZip()
        {
            if (sel == null || busy) return;
            using (var sf = new SaveFileDialog { Filter = L.T("ZIP-Archiv") + "|*.zip", FileName = sel.Name + ".zip", InitialDirectory = baseDir })
            {
                if (sf.ShowDialog(this) != DialogResult.OK) return;
                var pack = sel; var target = sf.FileName;
                Run(L.T("ZIP wird erstellt …"), bw =>
                {
                    if (File.Exists(target)) File.Delete(target);
                    using (var z = ZipFile.Open(target, ZipArchiveMode.Create))
                    {
                        var files = Directory.GetFiles(pack.Dir, "*", SearchOption.AllDirectories);
                        for (int i = 0; i < files.Length; i++)
                        {
                            z.CreateEntryFromFile(files[i], pack.Name + "\\" + files[i].Substring(pack.Dir.Length + 1), CompressionLevel.Fastest);
                            if (i % 25 == 0) bw.ReportProgress(i * 100 / Math.Max(1, files.Length), L.F("Packe {0} / {1}", i + 1, files.Length));
                        }
                    }
                }, () => Log(L.F("ZIP gespeichert: {0}", target)), "ZIP gespeichert");
            }
        }
    }

    // ============================================================ Miniaturen-Speicher
    // Kleine Vorschaubilder je Pack als fertige PNG-Bytes: im Speicher (zuletzt benutzte Packs) und auf der Platte
    // (Cache\thumbs\<pack>.fft). Damit erscheinen die Icons in der Vorschau sofort statt nach und nach.
    static class ThumbStore
    {
        public static string Dir;
        public static int Side { get { return UI.S(100); } }
        public class Entry { public long Len, Ticks; public byte[] Png; }
        public class Set
        {
            public string Key, File; public readonly Dictionary<string, Entry> Map = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            public volatile bool Loaded, Fetching, FetchFailed; public bool Dirty; public int Used; public readonly object Lock = new object();
        }
        static readonly List<Set> sets = new List<Set>();
        static string Hash(string s)
        {
            using (var h = System.Security.Cryptography.SHA1.Create()) return BitConverter.ToString(h.ComputeHash(System.Text.Encoding.UTF8.GetBytes(s))).Replace("-", "").Substring(0, 16).ToLowerInvariant();
        }
        static Set Get(string key)
        {
            lock (sets)
            {
                var s = sets.FirstOrDefault(x => x.Key == key);
                if (s == null)
                {
                    s = new Set { Key = key, File = Dir == null ? null : Path.Combine(Dir, key + ".fft") }; sets.Add(s);
                    while (sets.Count > 4) sets.Remove(sets.OrderBy(x => x.Used).First());
                }
                s.Used = Environment.TickCount; return s;
            }
        }
        public static Set ForLocal(string dir) { var s = Get("L" + Hash(Path.GetFullPath(dir).TrimEnd('\\').ToLowerInvariant())); EnsureLoaded(s); return s; }
        public static Set ForRemote(Remote.RPack p) { var s = Get("R" + Hash(p.Name + "|" + (p.Stamp ?? ""))); EnsureLoaded(s); return s; }

        public static void EnsureLoaded(Set s)
        {
            if (s.Loaded) return;
            lock (s.Lock)
            {
                if (s.Loaded) return;
                try
                {
                    if (s.File != null && System.IO.File.Exists(s.File))
                        using (var br = new BinaryReader(new BufferedStream(System.IO.File.OpenRead(s.File), 1 << 16)))
                        {
                            if (br.ReadInt32() == 0x31544646 && br.ReadInt32() == Side)
                            {
                                int n = br.ReadInt32();
                                for (int i = 0; i < n; i++)
                                {
                                    var id = br.ReadString(); var e = new Entry { Len = br.ReadInt64(), Ticks = br.ReadInt64() };
                                    int bl = br.ReadInt32(); e.Png = br.ReadBytes(bl); if (e.Png.Length == bl) s.Map[id] = e;
                                }
                            }
                        }
                }
                catch { s.Map.Clear(); }
                s.Loaded = true;
            }
        }
        public static void Save(Set s)
        {
            if (s.File == null) return;
            lock (io)
            {
            List<KeyValuePair<string, Entry>> snap;
            lock (s.Lock) { if (!s.Dirty) return; s.Dirty = false; snap = s.Map.ToList(); }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(s.File)); var tmp = s.File + ".part";
                using (var bw = new BinaryWriter(new BufferedStream(System.IO.File.Create(tmp), 1 << 16)))
                {
                    bw.Write(0x31544646); bw.Write(Side); bw.Write(snap.Count);
                    foreach (var kv in snap) { bw.Write(kv.Key); bw.Write(kv.Value.Len); bw.Write(kv.Value.Ticks); bw.Write(kv.Value.Png.Length); bw.Write(kv.Value.Png); }
                }
                if (System.IO.File.Exists(s.File)) System.IO.File.Delete(s.File);
                System.IO.File.Move(tmp, s.File);
            }
            catch { }
            }
        }
        static readonly object io = new object();
        // nur Einträge behalten, die es noch gibt (gelöschte/umbenannte Icons fliegen raus)
        public static void Prune(Set s, ICollection<string> keep)
        {
            var hs = new HashSet<string>(keep, StringComparer.OrdinalIgnoreCase);
            lock (s.Lock) foreach (var k in s.Map.Keys.Where(k => !hs.Contains(k)).ToList()) { s.Map.Remove(k); s.Dirty = true; }
        }

        // fertige Miniatur, wenn vorhanden und (bei lokalen Dateien) noch aktuell
        public static byte[] Peek(Set s, string id, long len, long ticks)
        {
            Entry e; lock (s.Lock) if (!s.Map.TryGetValue(id, out e)) return null;
            return (len == 0 || (e.Len == len && e.Ticks == ticks)) ? e.Png : null;
        }
        public static byte[] MakeLocal(Set s, string id, string file, long len, long ticks)
        {
            var have = Peek(s, id, len, ticks); if (have != null) return have;
            byte[] png;
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) using (var im = Image.FromStream(fs)) png = Encode(im);
            lock (s.Lock) { s.Map[id] = new Entry { Len = len, Ticks = ticks, Png = png }; s.Dirty = true; }
            return png;
        }
        static byte[] Encode(Image im)
        {
            using (var b = Shrink(im, Side)) using (var ms = new MemoryStream()) { b.Save(ms, ImageFormat.Png); return ms.ToArray(); }
        }
        public static Bitmap Decode(byte[] png)
        {
            using (var ms = new MemoryStream(png)) using (var im = Image.FromStream(ms))
            {
                var b = new Bitmap(im.Width, im.Height, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(b)) { g.CompositingMode = CompositingMode.SourceCopy; g.DrawImage(im, 0, 0, im.Width, im.Height); }
                return b;
            }
        }
        public static Bitmap Shrink(Image im, int side)
        {
            float k = Math.Min(1f, Math.Min((float)side / im.Width, (float)side / im.Height));
            int w = Math.Max(1, (int)Math.Round(im.Width * k)), h = Math.Max(1, (int)Math.Round(im.Height * k));
            var b = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(b)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.CompositingQuality = CompositingQuality.HighQuality; g.DrawImage(im, 0, 0, w, h); }
            return b;
        }
        static int Workers { get { return Math.Max(2, Math.Min(8, Environment.ProcessorCount - 1)); } }

        // alle Icons eines installierten Packs vorbereiten (im Hintergrund, z. B. beim Start)
        public static void WarmLocal(string dir, Func<bool> cancel)
        {
            if (Dir == null || dir == null || !Directory.Exists(dir)) return;
            var s = ForLocal(dir); var files = new List<string>();
            foreach (var c in MainForm.Cats)
            {
                var d = Path.Combine(dir, c); if (!Directory.Exists(d)) continue;
                files.AddRange(Directory.GetFiles(d, "*.png", SearchOption.AllDirectories).Where(f => !Remote.IsPlaceholder(f)));
            }
            var rels = files.Select(f => f.Substring(dir.TrimEnd('\\').Length + 1)).ToList();
            int next = -1; var ths = new List<System.Threading.Thread>();
            for (int w = 0; w < Workers; w++)
            {
                var t = new System.Threading.Thread(() =>
                {
                    int i;
                    while ((i = System.Threading.Interlocked.Increment(ref next)) < files.Count)
                    {
                        if (cancel != null && cancel()) return;
                        try { var fi = new FileInfo(files[i]); MakeLocal(s, rels[i], files[i], fi.Length, fi.LastWriteTimeUtc.Ticks); } catch { }
                    }
                }) { IsBackground = true, Priority = System.Threading.ThreadPriority.BelowNormal };
                ths.Add(t); t.Start();
            }
            ths.ForEach(t => t.Join());
            if (cancel == null || !cancel()) { Prune(s, rels); Save(s); }
        }

        // Miniaturen eines (noch nicht installierten) Packs: die kleine Vorschau-ZIP am Stück laden
        public static void FetchRemote(Remote.RPack p)
        {
            if (p == null || string.IsNullOrEmpty(p.Thumbs)) return;
            var s = ForRemote(p);
            lock (s.Lock) { if (s.Map.Count > 0 || s.Fetching) return; s.Fetching = true; s.FetchFailed = false; }
            try
            {
                var data = Remote.GetAsset(p.Thumbs);
                var raw = new List<KeyValuePair<string, byte[]>>();
                using (var za = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read))
                    foreach (var e in za.Entries)
                    {
                        if (!e.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
                        using (var es = e.Open()) using (var ms = new MemoryStream()) { es.CopyTo(ms); raw.Add(new KeyValuePair<string, byte[]>(e.FullName.Replace('\\', '/'), ms.ToArray())); }
                    }
                int next = -1; var ths = new List<System.Threading.Thread>();
                for (int w = 0; w < Workers; w++)
                {
                    var t = new System.Threading.Thread(() =>
                    {
                        int i;
                        while ((i = System.Threading.Interlocked.Increment(ref next)) < raw.Count)
                            try
                            {
                                byte[] png; using (var ms = new MemoryStream(raw[i].Value)) using (var im = Image.FromStream(ms)) png = Encode(im);
                                lock (s.Lock) s.Map[raw[i].Key] = new Entry { Png = png };
                            }
                            catch { }
                    }) { IsBackground = true };
                    ths.Add(t); t.Start();
                }
                ths.ForEach(t => t.Join());
                lock (s.Lock) s.Dirty = true;
                Save(s);
            }
            catch { s.FetchFailed = true; }
            finally { s.Fetching = false; }
        }
    }

    // ============================================================ Pack-Vorschau
    class PackPreview : Form
    {
        class Item { public string Name, File, TFile, Show, Key, Rel, Id; public long Len, Ticks; public char Kind; public Remote.ZEntry E, TE; public Bitmap Thumb; public bool Queued, Failed, Visible; }
        static readonly string[] Prefixes = { "T_UI_", "iconsPerks_", "iconPerks_", "iconAddons_", "iconAddon_", "iconItems_", "iconsFavors_", "iconFavors_", "iconsPowers_", "iconPowers_", "icons_", "icon_" };
        static Item MakeItem(string name)
        {
            var it = new Item { Name = name };
            string n = name; var m = Regex.Match(n, @"^(?:T_UI_)?([KS])(\d+)_", RegexOptions.IgnoreCase);
            if (m.Success) { it.Kind = char.ToUpperInvariant(m.Groups[1].Value[0]); it.Key = it.Kind + int.Parse(m.Groups[2].Value).ToString("D4") + n.ToLowerInvariant(); n = n.Substring(m.Length); }
            foreach (var p in Prefixes) if (n.StartsWith(p, StringComparison.OrdinalIgnoreCase)) { n = n.Substring(p.Length); break; }
            foreach (var sfx in new[] { "_Portrait", "_storeBackground", "storeBackground_" }) n = Regex.Replace(n, Regex.Escape(sfx), "", RegexOptions.IgnoreCase);
            n = Regex.Replace(n.Replace('_', ' '), "(?<=[a-z0-9])(?=[A-Z])", " ").Trim();
            if (n.Length > 0) n = char.ToUpperInvariant(n[0]) + n.Substring(1);
            it.Show = n.Length > 0 ? n : name;
            if (it.Key == null) it.Key = "~" + it.Show.ToLowerInvariant();
            return it;
        }
        // Platzhalter/Testbilder des Spiels (Missing, Missing01…, PowerTemp…, empty) nicht anzeigen
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, string l);
        static bool Placeholder(string name, string path)
        {
            return Remote.IsPlaceholder(path ?? name);
        }
        readonly string pack, localDir, peekDir, defaultDir; readonly Remote.RPack rem; readonly bool installed; readonly Func<string, string> getOv; readonly Action<string, string> setOv;
        Bitmap zoomOrig; float split = 0.5f; bool dragging; RectangleF zoomR, useR; bool overUse;
        readonly List<string> cats = new List<string>(); readonly Dictionary<string, List<Item>> items = new Dictionary<string, List<Item>>();
        int cat, kindSel; float scroll, scrollTarget; TextBox search; string query = ""; List<RectangleF> kindR = new List<RectangleF>(); int overKind = -1; RectangleF searchR; bool listing = true, listFail, closing; Item hover, zoom; Bitmap zoomImg;
        List<RectangleF> tabR = new List<RectangleF>(); RectangleF closeR; int overTab = -1; bool overClose;
        readonly List<Item> queue = new List<Item>(); readonly object qlock = new object(); volatile bool dirty; Timer tick;
        volatile ThumbStore.Set tset; List<Item> viewCache; string viewKey; readonly List<Item> lastVis = new List<Item>();
        const int Cell = 118, CellH = 140, Thumb = 100;

        public PackPreview(string pack, string localDir, Remote.RPack rem, string cacheDir, bool installed, string defaultDir, Func<string, string> getOv, Action<string, string> setOv)
        {
            this.pack = pack; this.localDir = localDir; this.rem = localDir == null ? rem : null; this.installed = installed && localDir != null; this.getOv = getOv; this.setOv = setOv;
            this.defaultDir = pack.Equals("Default", StringComparison.OrdinalIgnoreCase) ? null : defaultDir;
            if (this.rem != null) { var st = rem.Stamp ?? "v"; peekDir = Path.Combine(cacheDir ?? Path.GetTempPath(), "peek", pack + "-" + new string(st.Where(char.IsLetterOrDigit).Take(12).ToArray())); }
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(16, 15, 20); Size = new Size(UI.S(1040), UI.S(700)); Text = pack + " – " + L.T("Vorschau");
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            KeyPreview = true;
            search = new TextBox { Visible = false, BorderStyle = BorderStyle.None, BackColor = Color.FromArgb(30, 29, 35), ForeColor = UI.Text, Font = UI.F(10f) };
            search.TextChanged += (s, e) => { query = search.Text.Trim(); scroll = scrollTarget = 0; hover = null; Invalidate(); };
            Controls.Add(search);
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; if (zoom != null) CloseZoom(); else if (search.Text.Length > 0) search.Text = ""; else Close(); }
                else if (e.KeyCode == Keys.Tab || (!search.Focused && e.KeyCode == Keys.Right)) { e.SuppressKeyPress = true; SetCat(cat + (e.Shift ? -1 : 1)); }
                else if (!search.Focused && e.KeyCode == Keys.Left) SetCat(cat - 1);
            };
            KeyPress += (s, e) => { if (!search.Focused && !char.IsControl(e.KeyChar)) { search.Focus(); search.Text += e.KeyChar; search.SelectionStart = search.Text.Length; e.Handled = true; } };
            tick = new Timer { Interval = 16 };
            tick.Tick += (s, e) =>
            {
                bool inv = dirty; dirty = false;
                if (Math.Abs(scroll - scrollTarget) > 0.5f) { scroll += (scrollTarget - scroll) * 0.25f; inv = true; } else if (scroll != scrollTarget) { scroll = scrollTarget; inv = true; }
                if (inv) Invalidate();
            };
            tick.Start();
        }
        protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; return cp; } }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); MainForm.Rounded(Handle); }
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e); Opacity = 0;
            var ft = new Timer { Interval = 15 }; ft.Tick += (s, a) => { Opacity = Math.Min(1, Opacity + 0.14); if (Opacity >= 1) { ft.Stop(); ft.Dispose(); } }; ft.Start();
            ActiveControl = null; search.GotFocus += (s, a) => Invalidate(); search.LostFocus += (s, a) => Invalidate();
            new System.Threading.Thread(LoadList) { IsBackground = true }.Start();
            for (int i = 0; i < (rem != null ? 5 : 3); i++) new System.Threading.Thread(Worker) { IsBackground = true }.Start();
        }
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            closing = true; tick.Stop(); tick.Dispose();
            var tsc = tset; if (tsc != null && tsc.Dirty) new System.Threading.Thread(() => ThumbStore.Save(tsc)) { IsBackground = true }.Start();
            lock (qlock) { queue.Clear(); System.Threading.Monitor.PulseAll(qlock); }
            foreach (var l in items.Values) foreach (var it in l) if (it.Thumb != null) it.Thumb.Dispose();
            if (zoomImg != null) zoomImg.Dispose();
            base.OnFormClosed(e);
        }

        void LoadList()
        {
            var res = new Dictionary<string, List<Item>>();
            try
            {
                if (localDir != null)
                {
                    foreach (var c in MainForm.Cats)
                    {
                        var d = Path.Combine(localDir, c); if (!Directory.Exists(d)) continue;
                        var l = Directory.GetFiles(d, "*.png", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).Select(f => { var it = MakeItem(Path.GetFileNameWithoutExtension(f)); it.File = f; it.Rel = f.Substring(localDir.TrimEnd('\\').Length + 1); it.Id = it.Rel; try { var fi = new FileInfo(f); it.Len = fi.Length; it.Ticks = fi.LastWriteTimeUtc.Ticks; } catch { } return it; }).Where(it => !Placeholder(it.Name, it.File)).OrderBy(it => it.Key, StringComparer.Ordinal).ToList();
                        if (l.Count > 0) res[c] = l;
                    }
                    tset = ThumbStore.ForLocal(localDir);
                }
                else if (rem != null)
                {
                    List<Remote.ZEntry> ents = null;
                    for (int a = 0; ents == null; a++) { try { ents = Remote.ListEntries(rem); } catch { if (a >= 2 || closing) throw; System.Threading.Thread.Sleep(1200); } }
                    var thumbs = new Dictionary<string, Remote.ZEntry>(StringComparer.OrdinalIgnoreCase);
                    if (!string.IsNullOrEmpty(rem.Thumbs)) try { foreach (var te in Remote.ListEntries(rem, rem.Thumbs)) thumbs[te.Name] = te; } catch { }
                    foreach (var en in ents.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        if (!MainForm.Cats.Contains(en.Cat)) continue;
                        List<Item> l; if (!res.TryGetValue(en.Cat, out l)) res[en.Cat] = l = new List<Item>();
                        var nm = en.Name.Substring(en.Name.LastIndexOf('/') + 1);
                        var it = MakeItem(nm.Substring(0, nm.Length - 4)); if (Placeholder(it.Name, en.Name)) continue;
                        var safe = new string(en.Name.Select(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '.' || ch == '-' ? ch : '_').ToArray());
                        it.E = en; it.Id = en.Name; it.File = Path.Combine(peekDir, safe); it.Rel = en.Name.Substring(en.Name.IndexOf('/') + 1).Replace('/', '\\');
                        Remote.ZEntry tz; if (thumbs.TryGetValue(en.Name, out tz)) { it.TE = tz; it.TFile = Path.Combine(peekDir, "t", safe); }
                        l.Add(it);
                    }
                    foreach (var k in res.Keys.ToList()) res[k] = res[k].OrderBy(x => x.Key, StringComparer.Ordinal).ToList();
                    if (!string.IsNullOrEmpty(rem.Thumbs))
                    {
                        var ts = ThumbStore.ForRemote(rem);
                        if (ts.Map.Count == 0) { var rp = rem; new System.Threading.Thread(() => { ThumbStore.FetchRemote(rp); dirty = true; }) { IsBackground = true }.Start(); }
                        tset = ts;
                    }
                }
            }
            catch { listFail = true; }
            try
            {
                BeginInvoke((Action)(() =>
                {
                    foreach (var c in MainForm.Cats) if (res.ContainsKey(c)) { cats.Add(c); items[c] = res[c]; }
                    listing = false; if (cats.Count == 0 && rem != null) listFail = true; if (localDir != null && tset != null) StartFill(); search.Visible = cats.Count > 0; try { SendMessage(search.Handle, 0x1501, (IntPtr)1, L.T("Suchen …")); } catch { } Invalidate();
                }));
            }
            catch { }
        }

        void Worker()
        {
            while (!closing)
            {
                var batch = new List<Item>();
                lock (qlock)
                {
                    while (!closing && queue.Count == 0) System.Threading.Monitor.Wait(qlock, 500);
                    if (closing) return;
                    // zuletzt angeforderte (gerade sichtbare) zuerst; nicht mehr sichtbare verwerfen
                    for (int i = queue.Count - 1; i >= 0 && batch.Count < (rem != null ? 24 : 1); i--) { var q = queue[i]; queue.RemoveAt(i); if (q.Visible) batch.Add(q); else q.Queued = false; }
                }
                if (batch.Count == 0) continue;
                // fehlende Bilder aus dem Netz holen – mehrere pro Anfrage
                if (rem != null && localDir == null)
                {
                    var need = new Dictionary<Remote.ZEntry, string>();
                    foreach (var it in batch)
                    {
                        if (it.TE != null) { if (!System.IO.File.Exists(it.TFile) && !System.IO.File.Exists(it.File)) need[it.TE] = it.TFile; }
                        else if (it.E != null && !System.IO.File.Exists(it.File)) need[it.E] = it.File;
                    }
                    if (need.Count > 0)
                        try
                        {
                            foreach (var kv in Remote.ReadEntries(need.Keys.ToList()))
                            {
                                var f = need[kv.Key]; Directory.CreateDirectory(Path.GetDirectoryName(f)); var tmp = f + ".part";
                                System.IO.File.WriteAllBytes(tmp, kv.Value); if (System.IO.File.Exists(f)) System.IO.File.Delete(f); System.IO.File.Move(tmp, f);
                            }
                        }
                        catch { }
                }
                foreach (var it in batch)
                {
                    if (closing) return;
                    try
                    {
                        Bitmap th; var ts = tset;
                        if (localDir != null && ts != null) th = ThumbStore.Decode(ThumbStore.MakeLocal(ts, it.Id, it.File, it.Len, it.Ticks));
                        else
                        {
                            string src = it.TFile != null && System.IO.File.Exists(it.TFile) ? it.TFile : it.File;
                            using (var fs = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) using (var im = Image.FromStream(fs)) th = ThumbStore.Shrink(im, UI.S(Thumb));
                        }
                        if (closing) { th.Dispose(); return; }
                        it.Thumb = th;
                    }
                    catch { it.Failed = true; }
                    it.Queued = false; dirty = true;
                }
            }
        }
        // alle Miniaturen des Packs vorbereiten – aktuelle Kategorie zuerst; danach für das nächste Öffnen speichern
        void StartFill()
        {
            var ts = tset; if (ts == null) return;
            var order = new List<Item>(); if (cats.Count > 0) order.AddRange(items[cats[cat]]);
            foreach (var c in cats) if (c != cats[cat]) order.AddRange(items[c]);
            var ids = order.Select(x => x.Id).ToList();
            new System.Threading.Thread(() =>
            {
                int next = -1; var ths = new List<System.Threading.Thread>();
                for (int w = 0; w < Math.Max(2, Math.Min(6, Environment.ProcessorCount - 1)); w++)
                {
                    var t = new System.Threading.Thread(() =>
                    {
                        int i;
                        while (!closing && (i = System.Threading.Interlocked.Increment(ref next)) < order.Count)
                        {
                            var it = order[i];
                            try { if (ThumbStore.Peek(ts, it.Id, it.Len, it.Ticks) == null) { ThumbStore.MakeLocal(ts, it.Id, it.File, it.Len, it.Ticks); if (it.Visible) dirty = true; } } catch { }
                        }
                    }) { IsBackground = true, Priority = System.Threading.ThreadPriority.BelowNormal };
                    ths.Add(t); t.Start();
                }
                ths.ForEach(t => t.Join());
                if (!closing) { ThumbStore.Prune(ts, ids); ThumbStore.Save(ts); }
            }) { IsBackground = true }.Start();
        }
        void SetCat(int c)
        {
            if (cats.Count == 0) return; c = (c % cats.Count + cats.Count) % cats.Count;
            if (c == cat) return; cat = c; kindSel = 0; scroll = scrollTarget = 0; hover = null; Invalidate();
        }
        bool HasKinds(string c) { return items[c].Count(x => x.Kind != '\0') > 4; }
        bool Match(Item it, int kind)
        {
            if (kind == 1 && it.Kind != 'K') return false;
            if (kind == 2 && it.Kind != 'S') return false;
            if (kind == 3 && it.Kind != '\0') return false;
            if (query.Length == 0) return true;
            return it.Show.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || it.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        List<Item> View
        {
            get
            {
                if (cats.Count == 0) return new List<Item>();
                int k = HasKinds(cats[cat]) ? kindSel : 0; string key = cat + "|" + k + "|" + query;
                if (viewCache == null || viewKey != key) { viewCache = items[cats[cat]].Where(x => Match(x, k)).ToList(); viewKey = key; }
                return viewCache;
            }
        }
        RectangleF GridRect { get { int W = ClientSize.Width, H = ClientSize.Height; float top = UI.S(60) + UI.S(112); return new RectangleF(UI.S(22), top, W - UI.S(44), H - top - UI.S(42)); } }
        int Cols { get { return Math.Max(1, (int)((GridRect.Width + UI.S(8)) / UI.S(Cell))); } }
        float MaxScroll { get { if (cats.Count == 0) return 0; int n = View.Count; int rows = (n + Cols - 1) / Cols; return Math.Max(0, rows * UI.S(CellH) - GridRect.Height + UI.S(8)); } }
        RectangleF CellRect(int i)
        {
            var gr = GridRect; int cols = Cols; float cw = UI.S(Cell), pad = (gr.Width - cols * cw) / 2;
            return new RectangleF(gr.X + pad + (i % cols) * cw + UI.S(4), gr.Y + (i / cols) * UI.S(CellH) - scroll + UI.S(4), cw - UI.S(8), UI.S(CellH) - UI.S(8));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; UI.HQ(g); int W = ClientSize.Width, H = ClientSize.Height, th = UI.S(60);
            g.Clear(BackColor);
            using (var p = new Pen(Color.FromArgb(55, 255, 255, 255))) g.DrawRectangle(p, 0, 0, W - 1, H - 1);
            using (var b = new SolidBrush(Color.FromArgb(10, 9, 13))) g.FillRectangle(b, 0, 0, W, th);
            using (var lg = new LinearGradientBrush(new RectangleF(0, th - 2, W, 2), UI.A(UI.Red, 0), UI.A(UI.Red, 220), 0f))
            { lg.InterpolationColors = new ColorBlend { Colors = new[] { UI.A(UI.Red, 0), UI.A(UI.Red, 230), UI.A(UI.Red, 0) }, Positions = new[] { 0f, 0.35f, 1f } }; g.FillRectangle(lg, 0, th - 2, W, 2); }
            int total = items.Values.Sum(l => l.Count);
            using (var f = UI.F(13f, FontStyle.Bold)) UI.Txt(g, pack + "  ·  " + L.T("Vorschau"), f, UI.Text, new RectangleF(UI.S(24), 0, W - UI.S(300), th));
            if (total > 0) using (var f = UI.F(9f)) UI.Txt(g, L.F("{0} Icons", total.ToString("N0")), f, UI.Sub, new RectangleF(W - UI.S(260), 0, UI.S(190), th), StringAlignment.Far);
            closeR = new RectangleF(W - UI.S(56), 0, UI.S(56), th - 2);
            if (overClose) using (var b = new SolidBrush(UI.Red)) g.FillRectangle(b, closeR);
            using (var p = new Pen(UI.Text, UI.Sf(1.4f))) { float s5 = UI.S(6); var c = new PointF(closeR.X + closeR.Width / 2, closeR.Y + closeR.Height / 2); g.DrawLine(p, c.X - s5, c.Y - s5, c.X + s5, c.Y + s5); g.DrawLine(p, c.X + s5, c.Y - s5, c.X - s5, c.Y + s5); }

            if (listing || listFail || cats.Count == 0)
            {
                using (var f = UI.F(11f)) UI.Txt(g, listFail ? L.T("Vorschau konnte nicht geladen werden – bitte prüfe deine Internetverbindung.") : L.T("Inhalt wird geladen …"), f, UI.Sub, new RectangleF(0, th, W, H - th), StringAlignment.Center);
                return;
            }

            // Kategorien
            tabR.Clear(); float tx = UI.S(22), ty = th + UI.S(16), tabH = UI.S(36);
            using (var f = UI.F(9.5f, FontStyle.Bold)) using (var fn = UI.F(8.5f))
                for (int i = 0; i < cats.Count; i++)
                {
                    string nm = L.T(MainForm.CatNames[cats[i]]), cnt = (query.Length > 0 ? items[cats[i]].Count(x => Match(x, 0)) : items[cats[i]].Count).ToString("N0");
                    float w = g.MeasureString(nm, f).Width + g.MeasureString(cnt, fn).Width + UI.S(36);
                    var r = new RectangleF(tx, ty, w, tabH); tabR.Add(r); tx += w + UI.S(8);
                    bool on = i == cat, ov = i == overTab;
                    using (var path = UI.Round(r, tabH / 2))
                    {
                        using (var b = new SolidBrush(on ? UI.A(UI.Red, 210) : Color.FromArgb(ov ? 34 : 18, 255, 255, 255))) g.FillPath(b, path);
                        if (!on) using (var p = new Pen(Color.FromArgb(40, 255, 255, 255))) g.DrawPath(p, path);
                    }
                    float nw = g.MeasureString(nm, f).Width;
                    UI.Txt(g, nm, f, on ? Color.White : UI.Text, new RectangleF(r.X + UI.S(14), r.Y, nw + 4, r.Height));
                    UI.Txt(g, cnt, fn, on ? Color.FromArgb(230, 255, 255, 255) : UI.Dim, new RectangleF(r.X + UI.S(20) + nw, r.Y, r.Width - nw - UI.S(20), r.Height));
                }

            // Filter-Zeile: Killer/Survivor + Suche
            float fy = th + UI.S(64), fh = UI.S(32); kindR.Clear();
            if (HasKinds(cats[cat]))
            {
                var kl = new[] { L.T("Alle"), "Killer", "Survivor", L.T("Sonstige") };
                bool anyOther = items[cats[cat]].Any(x => x.Kind == '\0');
                float kx = UI.S(22);
                using (var f = UI.F(9f, FontStyle.Bold))
                    for (int i = 0; i < kl.Length; i++)
                    {
                        if (i == 3 && !anyOther) break;
                        float w = g.MeasureString(kl[i], f).Width + UI.S(26); var r = new RectangleF(kx, fy, w, fh); kindR.Add(r); kx += w + UI.S(6);
                        bool on = i == kindSel, ov = i == overKind;
                        using (var path = UI.Round(r, UI.S(8)))
                        {
                            using (var b = new SolidBrush(on ? Color.FromArgb(48, 255, 255, 255) : Color.FromArgb(ov ? 24 : 0, 255, 255, 255))) g.FillPath(b, path);
                            using (var p = new Pen(on ? UI.A(UI.Red, 200) : Color.FromArgb(34, 255, 255, 255))) g.DrawPath(p, path);
                        }
                        UI.Txt(g, kl[i], f, on ? UI.Text : UI.Sub, r, StringAlignment.Center);
                    }
            }
            searchR = new RectangleF(W - UI.S(22) - UI.S(280), fy, UI.S(280), fh);
            using (var path = UI.Round(searchR, UI.S(8)))
            {
                using (var b = new SolidBrush(Color.FromArgb(30, 29, 35))) g.FillPath(b, path);
                using (var p = new Pen(search.Focused ? UI.A(UI.Red, 200) : Color.FromArgb(44, 255, 255, 255))) g.DrawPath(p, path);
            }
            {   // Lupe
                float cx = searchR.X + UI.S(16), cy = searchR.Y + fh / 2 - UI.S(1), rr = UI.S(5);
                using (var p = new Pen(UI.Sub, UI.Sf(1.5f))) { g.DrawEllipse(p, cx - rr, cy - rr, rr * 2, rr * 2); g.DrawLine(p, cx + rr * 0.7f, cy + rr * 0.7f, cx + rr * 1.6f, cy + rr * 1.6f); }
            }
            var tbB = new Rectangle((int)(searchR.X + UI.S(32)), (int)(searchR.Y + (fh - search.PreferredHeight) / 2 + 1), (int)(searchR.Width - UI.S(42)), search.PreferredHeight);
            if (search.Bounds != tbB) search.Bounds = tbB;
            if (search.Text.Length == 0 && !search.Focused) using (var f = UI.F(10f)) UI.Txt(g, L.T("Suchen …"), f, UI.Dim, new RectangleF(tbB.X, searchR.Y, tbB.Width, fh));

            // Raster
            var gr = GridRect; var list = View;
            if (list.Count == 0) using (var f = UI.F(10.5f)) UI.Txt(g, L.T("Keine Treffer."), f, UI.Sub, gr, StringAlignment.Center);
            scrollTarget = Math.Max(0, Math.Min(scrollTarget, MaxScroll)); scroll = Math.Max(0, Math.Min(scroll, MaxScroll + 1));
            var oc = g.Clip; g.SetClip(new RectangleF(0, gr.Y, W, gr.Height), CombineMode.Intersect);
            var need = new List<Item>();
            foreach (var it in lastVis) it.Visible = false; lastVis.Clear();
            int budget = 48; var ts0 = tset;
            using (var fs = UI.F(8f))
                for (int i = 0; i < list.Count; i++)
                {
                    var r = CellRect(i); var it = list[i];
                    bool vis = r.Bottom > gr.Y - UI.S(Cell) && r.Y < gr.Bottom + UI.S(Cell);   // eine Reihe Vorlauf
                    if (!vis) continue;
                    it.Visible = true; lastVis.Add(it);
                    if (it.Thumb == null && !it.Failed && ts0 != null && budget > 0)
                    {
                        var png = ThumbStore.Peek(ts0, it.Id, it.Len, it.Ticks);
                        if (png != null) { budget--; try { it.Thumb = ThumbStore.Decode(png); } catch { it.Failed = true; } }
                    }
                    bool ov = it == hover;
                    using (var path = UI.Round(r, UI.S(10)))
                    {
                        using (var b = new SolidBrush(ov ? Color.FromArgb(40, 255, 255, 255) : Color.FromArgb(14, 255, 255, 255))) g.FillPath(b, path);
                        using (var p = new Pen(ov ? UI.A(UI.Red, 200) : Color.FromArgb(22, 255, 255, 255), ov ? UI.Sf(1.6f) : 1f)) g.DrawPath(p, path);
                    }
                    var tr = new RectangleF(r.X, r.Y + UI.S(4), r.Width, UI.S(Thumb));
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                    using (var b = new SolidBrush(ov ? UI.Text : UI.Sub)) g.DrawString(it.Show, fs, b, new RectangleF(r.X + UI.S(6), tr.Bottom + UI.S(2), r.Width - UI.S(12), r.Bottom - tr.Bottom - UI.S(4)), sf);
                    r = tr;
                    if (it.Thumb != null) g.DrawImageUnscaled(it.Thumb, (int)(r.X + (r.Width - it.Thumb.Width) / 2), (int)(r.Y + (r.Height - it.Thumb.Height) / 2));
                    if (installed && it.Rel != null && pack.Equals(getOv(it.Rel) ?? "", StringComparison.OrdinalIgnoreCase))
                    {
                        var sr = new RectangleF(r.Right - UI.S(26), r.Y - UI.S(2), UI.S(22), UI.S(22));
                        using (var b = new SolidBrush(UI.Orange)) g.FillEllipse(b, sr);
                        using (var f = UI.F(8.5f, FontStyle.Bold)) UI.Txt(g, "\u2605", f, Color.White, sr, StringAlignment.Center);
                    }
                    else if (it.Failed) UI.Txt(g, "✕", fs, UI.Dim, r, StringAlignment.Center);
                    else
                    {
                        float a = (Environment.TickCount / 4 + i * 40) % 360;
                        using (var p = new Pen(UI.A(UI.Red, 140), UI.Sf(2f))) g.DrawArc(p, r.X + r.Width / 2 - UI.S(10), r.Y + r.Height / 2 - UI.S(10), UI.S(20), UI.S(20), a, 100);
                        dirty = true;
                        if (!it.Queued && !(ts0 != null && localDir == null && (ts0.Fetching || ThumbStore.Peek(ts0, it.Id, 0, 0) != null))) need.Add(it);
                    }
                }
            g.Clip = oc;
            if (need.Count > 0) lock (qlock) { foreach (var it in need) { it.Queued = true; queue.Add(it); } System.Threading.Monitor.PulseAll(qlock); }
            // Scrollbalken
            float ms = MaxScroll;
            if (ms > 0)
            {
                float bh = Math.Max(UI.S(40), gr.Height * gr.Height / (gr.Height + ms)), by = gr.Y + (gr.Height - bh) * (scroll / ms);
                using (var path = UI.Round(new RectangleF(W - UI.S(12), by, UI.S(5), bh), UI.S(3))) using (var b = new SolidBrush(Color.FromArgb(70, 255, 255, 255))) g.FillPath(b, path);
            }
            // Fußzeile
            using (var p = new Pen(Color.FromArgb(28, 255, 255, 255))) g.DrawLine(p, 0, H - UI.S(40), W, H - UI.S(40));
            using (var f = UI.F(8.5f))
            {
                string left = hover != null ? hover.Name + ".png" : (query.Length > 0 ? L.F("{0} Treffer", list.Count.ToString("N0")) + "   ·   " : "") + L.T("Klicken zum Vergrößern");
                UI.Txt(g, left, f, hover != null ? UI.Text : UI.Dim, new RectangleF(UI.S(22), H - UI.S(40), W / 2, UI.S(40)));
                UI.Txt(g, "Tab  " + L.T("Kategorie wechseln") + "     Esc  " + L.T("Schließen"), f, UI.Dim, new RectangleF(W / 2, H - UI.S(40), W / 2 - UI.S(22), UI.S(40)), StringAlignment.Far);
            }
            // Vergrößerung mit Vorher/Nachher-Regler
            zoomR = RectangleF.Empty; useR = RectangleF.Empty;
            if (zoom != null)
            {
                using (var b = new SolidBrush(Color.FromArgb(225, 6, 5, 8))) g.FillRectangle(b, 0, th, W, H - th);
                if (zoomImg != null)
                {
                    float side = Math.Min(H - th - UI.S(150), UI.S(500));
                    var zr = new RectangleF((W - side) / 2, th + UI.S(28), side, side); zoomR = zr;
                    Action<Image, RectangleF> drawFit = (im, clip) =>
                    {
                        float sc = Math.Min(side / im.Width, side / im.Height); float iw = im.Width * sc, ih = im.Height * sc;
                        var oc2 = g.Clip; g.SetClip(clip, CombineMode.Intersect);
                        g.DrawImage(im, zr.X + (side - iw) / 2, zr.Y + (side - ih) / 2, iw, ih); g.Clip = oc2;
                    };
                    if (zoomOrig != null)
                    {
                        float sx = zr.X + side * split;
                        drawFit(zoomOrig, new RectangleF(zr.X, zr.Y, sx - zr.X, side));
                        drawFit(zoomImg, new RectangleF(sx, zr.Y, zr.Right - sx, side));
                        using (var p = new Pen(Color.FromArgb(230, 255, 255, 255), UI.Sf(2f))) g.DrawLine(p, sx, zr.Y, sx, zr.Bottom);
                        var hc = new RectangleF(sx - UI.S(15), zr.Y + side / 2 - UI.S(15), UI.S(30), UI.S(30));
                        using (var b = new SolidBrush(UI.Red)) g.FillEllipse(b, hc);
                        using (var p = new Pen(Color.White, UI.Sf(1.6f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                        {
                            float cx = hc.X + hc.Width / 2, cy = hc.Y + hc.Height / 2, a2 = UI.Sf(4f);
                            g.DrawLines(p, new[] { new PointF(cx - UI.Sf(3f), cy - a2), new PointF(cx - UI.Sf(7f), cy), new PointF(cx - UI.Sf(3f), cy + a2) });
                            g.DrawLines(p, new[] { new PointF(cx + UI.Sf(3f), cy - a2), new PointF(cx + UI.Sf(7f), cy), new PointF(cx + UI.Sf(3f), cy + a2) });
                        }
                        using (var f = UI.F(8.5f, FontStyle.Bold))
                        {
                            Action<string, float, StringAlignment> lab = (t, x, al) =>
                            {
                                float lw = g.MeasureString(t, f).Width + UI.S(16); var lr = new RectangleF(al == StringAlignment.Near ? x : x - lw, zr.Y - UI.S(26), lw, UI.S(20));
                                using (var path = UI.Round(lr, UI.S(10))) using (var bb = new SolidBrush(Color.FromArgb(200, 30, 28, 34))) g.FillPath(bb, path);
                                UI.Txt(g, t, f, UI.Sub, lr, StringAlignment.Center);
                            };
                            lab(L.T("Original"), zr.X, StringAlignment.Near); lab(pack, zr.Right, StringAlignment.Far);
                        }
                    }
                    else drawFit(zoomImg, zr);
                    float ny = zr.Bottom + UI.S(14);
                    using (var f = UI.F(10.5f, FontStyle.Bold)) UI.Txt(g, zoom.Show, f, UI.Text, new RectangleF(0, ny, W, UI.S(24)), StringAlignment.Center);
                    using (var f = UI.F(8.5f)) UI.Txt(g, zoom.Name + ".png" + (zoomOrig != null ? "   ·   " + L.T("Regler ziehen zum Vergleichen") : ""), f, UI.Dim, new RectangleF(0, ny + UI.S(22), W, UI.S(18)), StringAlignment.Center);
                    // Einzel-Icon übernehmen
                    if (zoom.Rel != null)
                    {
                        bool isOv = installed && pack.Equals(getOv(zoom.Rel) ?? "", StringComparison.OrdinalIgnoreCase);
                        string bt = !installed ? L.T("Zum Verwenden das Pack zuerst installieren") : isOv ? "\u2605  " + L.T("Wird verwendet – zurücksetzen") : (pack.Equals("Default", StringComparison.OrdinalIgnoreCase) ? L.T("Original-Icon verwenden") : L.T("Dieses Icon verwenden"));
                        float bw; using (var f = UI.F(9.5f, FontStyle.Bold)) bw = g.MeasureString(bt, f).Width + UI.S(36);
                        var br = new RectangleF((W - bw) / 2, ny + UI.S(50), bw, UI.S(36)); if (installed) useR = br;
                        using (var path = UI.Round(br, br.Height / 2))
                        {
                            if (!installed) using (var b = new SolidBrush(Color.FromArgb(20, 255, 255, 255))) g.FillPath(b, path);
                            else if (isOv) using (var b = new SolidBrush(UI.A(UI.Orange, overUse ? 90 : 60))) g.FillPath(b, path);
                            else using (var b = new LinearGradientBrush(br, overUse ? UI.RedHi : UI.Red, UI.RedDark, 90f)) g.FillPath(b, path);
                            using (var p = new Pen(Color.FromArgb(70, 255, 255, 255))) g.DrawPath(p, path);
                        }
                        using (var f = UI.F(9.5f, FontStyle.Bold)) UI.Txt(g, bt, f, installed ? Color.White : UI.Dim, br, StringAlignment.Center);
                    }
                }
            }
        }
        void CloseZoom() { zoom = null; dragging = false; search.Visible = cats.Count > 0; if (zoomImg != null) { zoomImg.Dispose(); zoomImg = null; } if (zoomOrig != null) { zoomOrig.Dispose(); zoomOrig = null; } Invalidate(); }
        Item HitItem(Point pt)
        {
            if (cats.Count == 0 || zoom != null || !GridRect.Contains(pt)) return null;
            var list = View;
            for (int i = 0; i < list.Count; i++) if (CellRect(i).Contains(pt)) return list[i];
            return null;
        }
        protected override void OnMouseUp(MouseEventArgs e) { dragging = false; }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (zoom != null)
            {
                if (dragging && zoomR.Width > 0) { split = Math.Max(0f, Math.Min(1f, (e.X - zoomR.X) / zoomR.Width)); Invalidate(); }
                bool ou = useR.Contains(e.Location); if (ou != overUse) { overUse = ou; Invalidate(); }
                Cursor = ou ? Cursors.Hand : (zoomOrig != null && zoomR.Contains(e.Location) ? Cursors.SizeWE : Cursors.Default);
                return;
            }
            int ot = zoom == null ? tabR.FindIndex(r => r.Contains(e.Location)) : -1, ok = zoom == null ? kindR.FindIndex(r => r.Contains(e.Location)) : -1; bool oc = closeR.Contains(e.Location); var hi = HitItem(e.Location);
            if (ot != overTab || ok != overKind || oc != overClose || hi != hover) { overTab = ot; overKind = ok; overClose = oc; hover = hi; Invalidate(); }
            Cursor = ot >= 0 || ok >= 0 || oc || (hi != null && hi.Thumb != null) ? Cursors.Hand : (searchR.Contains(e.Location) ? Cursors.IBeam : Cursors.Default);
        }
        protected override void OnMouseLeave(EventArgs e) { overTab = -1; overKind = -1; overClose = false; hover = null; Invalidate(); }
        protected override void OnMouseWheel(MouseEventArgs e) { if (zoom == null) { scrollTarget = Math.Max(0, Math.Min(MaxScroll, scrollTarget - e.Delta * 0.9f)); } }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (closeR.Contains(e.Location)) { Close(); return; }
            if (zoom != null)
            {
                if (useR.Contains(e.Location) && zoom.Rel != null)
                {
                    bool isOv = pack.Equals(getOv(zoom.Rel) ?? "", StringComparison.OrdinalIgnoreCase);
                    setOv(zoom.Rel, isOv ? null : pack); Invalidate(); return;
                }
                if (zoomOrig != null && zoomR.Contains(e.Location)) { dragging = true; split = Math.Max(0f, Math.Min(1f, (e.X - zoomR.X) / zoomR.Width)); Invalidate(); return; }
                if (zoomR.Contains(e.Location)) return;
                CloseZoom(); return;
            }
            int t = tabR.FindIndex(r => r.Contains(e.Location));
            if (t >= 0) { SetCat(t); return; }
            int kk = kindR.FindIndex(r => r.Contains(e.Location));
            if (kk >= 0) { kindSel = kk; scroll = scrollTarget = 0; Invalidate(); return; }
            if (searchR.Contains(e.Location)) { search.Focus(); Invalidate(); return; }
            if (search.Focused) { ActiveControl = null; Invalidate(); }
            var it = HitItem(e.Location);
            if (it != null && it.Thumb != null)
            {
                zoom = it; if (zoomImg != null) { zoomImg.Dispose(); zoomImg = null; }
                zoomImg = new Bitmap(it.Thumb); split = 0.5f; search.Visible = false;
                if (zoomOrig != null) { zoomOrig.Dispose(); zoomOrig = null; }
                if (defaultDir != null && it.Rel != null)
                    try { var of = Path.Combine(defaultDir, it.Rel); if (File.Exists(of)) using (var fs = new FileStream(of, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) using (var im = Image.FromStream(fs)) zoomOrig = new Bitmap(im); } catch { }
                Invalidate();
                var target = it;
                new System.Threading.Thread(() =>
                {
                    try
                    {
                        if (!System.IO.File.Exists(target.File) && target.E != null)
                        {
                            var data = Remote.ReadEntry(rem, target.E); Directory.CreateDirectory(Path.GetDirectoryName(target.File));
                            System.IO.File.WriteAllBytes(target.File + ".part", data); if (System.IO.File.Exists(target.File)) System.IO.File.Delete(target.File); System.IO.File.Move(target.File + ".part", target.File);
                        }
                        Bitmap full; using (var fs = new FileStream(target.File, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) using (var im = Image.FromStream(fs)) full = new Bitmap(im);
                        BeginInvoke((Action)(() => { if (zoom == target) { if (zoomImg != null) zoomImg.Dispose(); zoomImg = full; Invalidate(); } else full.Dispose(); }));
                    }
                    catch { }
                }) { IsBackground = true }.Start();
                return;
            }
            if (e.Y < UI.S(60)) MainForm.DragWindow(Handle);
        }
    }

    // ============================================================ Update-Fenster
    class UpdateDialog : Form
    {
        readonly Remote.AppUpdate u; readonly List<string> notes = new List<string>();
        RectangleF okR, laterR, closeR; string over;
        public UpdateDialog(Remote.AppUpdate u)
        {
            this.u = u;
            foreach (var raw in (u.Notes ?? "").Replace("\r", "").Split('\n'))
            {
                var l = raw.Trim(); if (l.Length == 0) continue;
                l = Regex.Replace(l, @"^(#+|[-*•]|\d+\.)\s*", "").Replace("**", "").Replace("__", "").Replace("`", "").Trim();
                if (l.Length > 0) notes.Add(l);
            }
            if (notes.Count > 8) { notes.RemoveRange(8, notes.Count - 8); notes.Add("…"); }
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(16, 15, 20);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            KeyPreview = true; KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } else if (e.KeyCode == Keys.Enter) { DialogResult = DialogResult.OK; Close(); } };
            Size = new Size(UI.S(540), UI.S(256 + Math.Max(1, notes.Count) * 26));
        }
        protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; return cp; } }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); MainForm.Rounded(Handle); }
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e); Opacity = 0;
            var ft = new Timer { Interval = 15 }; ft.Tick += (s, a) => { Opacity = Math.Min(1, Opacity + 0.14); if (Opacity >= 1) { ft.Stop(); ft.Dispose(); } }; ft.Start();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; UI.HQ(g); int W = ClientSize.Width, H = ClientSize.Height;
            g.Clear(BackColor);
            using (var lg = new LinearGradientBrush(new Rectangle(0, 0, W, UI.S(130)), Color.FromArgb(60, 222, 38, 46), Color.FromArgb(0, 222, 38, 46), 90f)) g.FillRectangle(lg, 0, 0, W, UI.S(130));
            using (var p = new Pen(Color.FromArgb(55, 255, 255, 255))) g.DrawRectangle(p, 0, 0, W - 1, H - 1);
            closeR = new RectangleF(W - UI.S(48), UI.S(10), UI.S(38), UI.S(34));
            if (over == "x") using (var path = UI.Round(closeR, UI.S(10))) using (var b = new SolidBrush(Color.FromArgb(40, 255, 255, 255))) g.FillPath(b, path);
            using (var p = new Pen(UI.Sub, UI.Sf(1.4f))) { float s5 = UI.S(5); var c = new PointF(closeR.X + closeR.Width / 2, closeR.Y + closeR.Height / 2); g.DrawLine(p, c.X - s5, c.Y - s5, c.X + s5, c.Y + s5); g.DrawLine(p, c.X + s5, c.Y - s5, c.X - s5, c.Y + s5); }

            // Symbol
            var ic = new RectangleF(UI.S(30), UI.S(30), UI.S(52), UI.S(52));
            using (var b = new SolidBrush(UI.A(UI.Red, 45))) g.FillEllipse(b, RectangleF.Inflate(ic, UI.S(6), UI.S(6)));
            using (var b = new LinearGradientBrush(ic, UI.RedHi, UI.RedDark, 90f)) g.FillEllipse(b, ic);
            using (var p = new Pen(Color.White, UI.Sf(3f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                float cx = ic.X + ic.Width / 2, cy = ic.Y + ic.Height / 2 - UI.S(2);
                g.DrawLine(p, cx, cy - UI.S(11), cx, cy + UI.S(7));
                g.DrawLines(p, new[] { new PointF(cx - UI.S(8), cy - UI.S(1)), new PointF(cx, cy + UI.S(8)), new PointF(cx + UI.S(8), cy - UI.S(1)) });
                g.DrawLine(p, cx - UI.S(11), cy + UI.S(14), cx + UI.S(11), cy + UI.S(14));
            }
            float tx = ic.Right + UI.S(22);
            using (var f = UI.F(15f, FontStyle.Bold)) UI.Txt(g, L.F("FogForge {0} ist verfügbar", u.Version.ToString(3)), f, UI.Text, new RectangleF(tx, UI.S(26), W - tx - UI.S(56), UI.S(32)));
            using (var f = UI.F(9f, FontStyle.Bold))
            {
                float y = UI.S(62), hh = UI.S(22);
                string a = "v" + AppInfo.Version, b2 = "v" + u.Version.ToString(3);
                float aw = g.MeasureString(a, f).Width + UI.S(16), bw = g.MeasureString(b2, f).Width + UI.S(16);
                var ra = new RectangleF(tx, y, aw, hh); var rb = new RectangleF(tx + aw + UI.S(30), y, bw, hh);
                using (var path = UI.Round(ra, hh / 2)) { using (var br = new SolidBrush(Color.FromArgb(18, 255, 255, 255))) g.FillPath(br, path); using (var p = new Pen(Color.FromArgb(40, 255, 255, 255))) g.DrawPath(p, path); }
                UI.Txt(g, a, f, UI.Sub, ra, StringAlignment.Center);
                using (var p = new Pen(UI.Dim, UI.Sf(1.6f)) { EndCap = LineCap.Round, StartCap = LineCap.Round })
                { float my = y + hh / 2, mx0 = ra.Right + UI.S(8), mx1 = rb.X - UI.S(8); g.DrawLine(p, mx0, my, mx1, my); g.DrawLine(p, mx1 - UI.S(5), my - UI.S(4), mx1, my); g.DrawLine(p, mx1 - UI.S(5), my + UI.S(4), mx1, my); }
                using (var path = UI.Round(rb, hh / 2)) using (var br = new SolidBrush(UI.A(UI.Red, 210))) g.FillPath(br, path);
                UI.Txt(g, b2, f, Color.White, rb, StringAlignment.Center);
                if (u.Bytes > 0) using (var f2 = UI.F(8.5f)) UI.Txt(g, "·  " + (u.Bytes / 1048576.0).ToString("0.0") + " MB", f2, UI.Dim, new RectangleF(rb.Right + UI.S(10), y, UI.S(120), hh));
            }
            // Neuerungen
            float ny = UI.S(112);
            using (var p = new Pen(Color.FromArgb(26, 255, 255, 255))) g.DrawLine(p, UI.S(30), ny, W - UI.S(30), ny);
            ny += UI.S(16);
            using (var f = UI.F(8.5f, FontStyle.Bold)) UI.Txt(g, L.T("WAS IST NEU"), f, UI.Dim, new RectangleF(UI.S(30), ny, W, UI.S(20)));
            ny += UI.S(26);
            using (var f = UI.F(10f)) using (var tb = new SolidBrush(Color.FromArgb(225, 226, 232)))
            {
                if (notes.Count == 0) { UI.Txt(g, L.T("Fehlerbehebungen und Verbesserungen."), f, UI.Sub, new RectangleF(UI.S(30), ny, W - UI.S(60), UI.S(24))); ny += UI.S(26); }
                foreach (var n in notes)
                {
                    using (var b = new SolidBrush(UI.Red)) g.FillEllipse(b, UI.S(33), ny + UI.S(8), UI.S(7), UI.S(7));
                    using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                        g.DrawString(n, f, tb, new RectangleF(UI.S(50), ny, W - UI.S(80), UI.S(24)), sf);
                    ny += UI.S(26);
                }
            }
            // Fuß
            float fy = H - UI.S(74);
            using (var b = new SolidBrush(Color.FromArgb(10, 9, 13))) g.FillRectangle(b, 1, fy, W - 2, H - fy - 1);
            using (var p = new Pen(Color.FromArgb(26, 255, 255, 255))) g.DrawLine(p, 0, fy, W, fy);
            using (var f = UI.F(10f, FontStyle.Bold))
            {
                float bh = UI.S(40), by = fy + (H - fy - bh) / 2;
                float okw = g.MeasureString(L.T("Jetzt aktualisieren"), f).Width + UI.S(40), lw = g.MeasureString(L.T("Später"), f).Width + UI.S(36);
                okR = new RectangleF(W - UI.S(24) - okw, by, okw, bh); laterR = new RectangleF(okR.X - UI.S(10) - lw, by, lw, bh);
                using (var f2 = UI.F(8.5f)) using (var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
                using (var b = new SolidBrush(UI.Dim)) g.DrawString(L.T("Packs und Einstellungen bleiben erhalten."), f2, b, new RectangleF(UI.S(28), fy, laterR.X - UI.S(40), H - fy), sf);
                using (var path = UI.Round(laterR, UI.S(12)))
                { using (var b = new SolidBrush(Color.FromArgb(over == "later" ? 36 : 18, 255, 255, 255))) g.FillPath(b, path); using (var p = new Pen(Color.FromArgb(40, 255, 255, 255))) g.DrawPath(p, path); }
                UI.Txt(g, L.T("Später"), f, UI.Text, laterR, StringAlignment.Center);
                using (var path = UI.Round(okR, UI.S(12)))
                {
                    using (var b = new LinearGradientBrush(okR, over == "ok" ? UI.RedHi : UI.Mix(UI.RedHi, UI.Red, 0.5f), UI.RedDark, 90f)) g.FillPath(b, path);
                    using (var p = new Pen(Color.FromArgb(over == "ok" ? 140 : 80, 255, 255, 255))) g.DrawPath(p, path);
                }
                UI.Txt(g, L.T("Jetzt aktualisieren"), f, Color.White, okR, StringAlignment.Center);
            }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            string o = okR.Contains(e.Location) ? "ok" : laterR.Contains(e.Location) ? "later" : closeR.Contains(e.Location) ? "x" : null;
            if (o != over) { over = o; Invalidate(); }
            Cursor = o != null ? Cursors.Hand : Cursors.Default;
        }
        protected override void OnMouseLeave(EventArgs e) { over = null; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (okR.Contains(e.Location)) { DialogResult = DialogResult.OK; Close(); return; }
            if (laterR.Contains(e.Location) || closeR.Contains(e.Location)) { DialogResult = DialogResult.Cancel; Close(); return; }
            if (e.Button == MouseButtons.Left) MainForm.DragWindow(Handle);
        }
    }

    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [STAThread]
        static void Main()
        {
            try { if (Environment.OSVersion.Version.Major >= 6) SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
