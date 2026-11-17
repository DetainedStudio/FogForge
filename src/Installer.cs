// FogForge Setup – eigener Installer im FogForge-Design (Installation, Spielordner, Verknüpfungen, Deinstallation)
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DbdCustom
{
    static class SetupProgram
    {
        public const string Version = AppInfo.Version;
        public const string UninstKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\FogForge";

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var f = new Form()) using (var g = f.CreateGraphics()) UI.Scale = g.DpiX / 96f;
            L.Code = L.SystemCode();
            string self = Application.ExecutablePath;
            if (args.Any(a => a.Equals("/update", StringComparison.OrdinalIgnoreCase)))
            {
                string udir = args.SkipWhile(a => !a.Equals("/dir", StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault();
                int pid; int.TryParse(args.SkipWhile(a => !a.Equals("/pid", StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault() ?? "", out pid);
                if (!string.IsNullOrEmpty(udir) && Directory.Exists(udir)) { Application.Run(new UpdateForm(udir.TrimEnd('\\'), pid)); return; }
            }
            bool uninstall = args.Any(a => a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase)) || (Path.GetFileName(self).IndexOf("uninstall", StringComparison.OrdinalIgnoreCase) >= 0 || Path.GetFileName(self).IndexOf("deinstall", StringComparison.OrdinalIgnoreCase) >= 0);
            if (uninstall)
            {
                string dir = args.SkipWhile(a => !a.Equals("/dir", StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault() ?? Path.GetDirectoryName(self);
                // Aus dem Installationsordner heraus nicht löschbar -> Kopie im Temp-Ordner starten
                if (Path.GetDirectoryName(self).TrimEnd('\\').Equals(dir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                {
                    var tmp = Path.Combine(Path.GetTempPath(), "FogForge_Uninstall_" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".exe");
                    File.Copy(self, tmp, true);
                    Process.Start(tmp, "/uninstall /dir \"" + dir + "\"");
                    return;
                }
                Application.Run(new SetupForm(true, dir));
                return;
            }
            Application.Run(new SetupForm(false, null));
        }

        // ---------------- Hilfsfunktionen ----------------
        public static string DetectGame()
        {
            const string rel = @"steamapps\common\Dead by Daylight\DeadByDaylight\Content\UI\Icons";
            var libs = new List<string>();
            foreach (var key in new[] { @"SOFTWARE\WOW6432Node\Valve\Steam", @"SOFTWARE\Valve\Steam" })
                try { using (var k = Registry.LocalMachine.OpenSubKey(key)) { var v = k != null ? k.GetValue("InstallPath") as string : null; if (v != null) libs.Add(v); } } catch { }
            try { using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")) { var v = k != null ? k.GetValue("SteamPath") as string : null; if (v != null) libs.Add(v.Replace('/', '\\')); } } catch { }
            foreach (var s in libs.ToArray())
            {
                var vdf = Path.Combine(s, @"steamapps\libraryfolders.vdf");
                try { if (File.Exists(vdf)) foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s*\"([^\"]+)\"")) libs.Add(m.Groups[1].Value.Replace(@"\\", @"\")); } catch { }
            }
            try { foreach (var d in DriveInfo.GetDrives().Where(x => x.DriveType == DriveType.Fixed)) { libs.Add(Path.Combine(d.Name, "Steam")); libs.Add(Path.Combine(d.Name, "SteamLibrary")); libs.Add(Path.Combine(d.Name, @"Program Files (x86)\Steam")); } } catch { }
            foreach (var l in libs.Distinct(StringComparer.OrdinalIgnoreCase))
                try { var p = Path.Combine(l, rel); if (Directory.Exists(p)) return p; } catch { }
            return null;
        }
        public static string NormalizeGame(string p)
        {
            if (string.IsNullOrEmpty(p)) return null;
            foreach (var cand in new[] { p, Path.Combine(p, @"DeadByDaylight\Content\UI\Icons"), Path.Combine(p, @"Content\UI\Icons"), Path.Combine(p, @"UI\Icons") })
                if (Directory.Exists(Path.Combine(cand, "Perks")) || Directory.Exists(Path.Combine(cand, "CharPortraits"))) return cand;
            return null;
        }
        public static void Shortcut(string lnk, string target, string desc)
        {
            var t = Type.GetTypeFromProgID("WScript.Shell"); object sh = Activator.CreateInstance(t);
            object o = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { lnk });
            var ot = o.GetType();
            ot.InvokeMember("TargetPath", BindingFlags.SetProperty, null, o, new object[] { target });
            ot.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, o, new object[] { Path.GetDirectoryName(target) });
            ot.InvokeMember("IconLocation", BindingFlags.SetProperty, null, o, new object[] { target + ",0" });
            ot.InvokeMember("Description", BindingFlags.SetProperty, null, o, new object[] { desc });
            ot.InvokeMember("Save", BindingFlags.InvokeMethod, null, o, null);
        }
        public static bool FogForgeRunning(string dir)
        {
            try
            {
                return Process.GetProcessesByName("FogForge").Concat(Process.GetProcessesByName("Fog Forge")).Any(p => { try { return p.MainModule.FileName.StartsWith(dir, StringComparison.OrdinalIgnoreCase); } catch { return true; } });
            }
            catch { return false; }
        }
        public static Stream Res(string name) { return typeof(SetupProgram).Assembly.GetManifestResourceStream(name); }
    }

    static class Payload
    {
        public static PayloadFile File;
        public static List<PayloadFile.PackInfo> Packs = new List<PayloadFile.PackInfo>();
        public static void Load()
        {
            File = PayloadFile.Load(Application.ExecutablePath, UI.S(300));
            var list = File != null ? File.Packs.ToList() : new List<PayloadFile.PackInfo>();
            // Ordner „Packs“ neben dem Setup: als eigene, abwählbare Packs anbieten
            try
            {
                var bundled = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.ExecutablePath), "Packs");
                if (Directory.Exists(bundled))
                    foreach (var d in Directory.GetDirectories(bundled))
                    {
                        var n = System.IO.Path.GetFileName(d);
                        if (n.Equals("Default", StringComparison.OrdinalIgnoreCase) || n.StartsWith("Export_", StringComparison.OrdinalIgnoreCase)) continue;
                        if (list.Any(p => p.Name.Equals(n, StringComparison.OrdinalIgnoreCase))) continue;
                        var files = Directory.GetFiles(d, "*.png", SearchOption.AllDirectories);
                        if (files.Length == 0) continue;
                        var pi = new PayloadFile.PackInfo { Name = n, Dir = d, Icons = files.Length, Bytes = files.Sum(f => new FileInfo(f).Length) };
                        var pv = System.IO.Path.Combine(d, "preview.jpg");
                        if (System.IO.File.Exists(pv)) try { using (var bmp = new Bitmap(pv)) { int pw = UI.S(300), ph = Math.Max(1, bmp.Height * pw / bmp.Width); pi.Preview = new Bitmap(bmp, pw, ph); } } catch { }
                        list.Add(pi);
                    }
            }
            catch { }
            Packs = list.OrderBy(p => Order(p.Name)).ThenBy(p => p.Name).ToList();
        }
        // Packs aus dem Internet (GitHub) dazuladen
        public static volatile bool RemoteLoading, RemoteFailed;
        public static void LoadRemoteAsync(Form owner, Action done)
        {
            RemoteLoading = true;
            new System.Threading.Thread(() =>
            {
                try
                {
                    var cat = Remote.FetchCatalog(15000, System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FogForgeSetup"));
                    var cache = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FogForgeSetup");
                    var list = Packs.ToList();
                    foreach (var rp in cat)
                    {
                        if (rp.Name.Equals("Default", StringComparison.OrdinalIgnoreCase) || list.Any(p => p.Name.Equals(rp.Name, StringComparison.OrdinalIgnoreCase))) continue;
                        var pi = new PayloadFile.PackInfo { Name = rp.Name, Rem = rp, Icons = rp.Icons, Bytes = rp.Bytes };
                        foreach (var kv in rp.Counts) pi.Counts[kv.Key] = kv.Value;
                        var im = Remote.LoadPreview(rp, cache, true);
                        if (im != null) using (im) { int pw = UI.S(300), ph = Math.Max(1, im.Height * pw / im.Width); pi.Preview = new Bitmap(im, pw, ph); }
                        list.Add(pi);
                    }
                    Packs = list.OrderBy(p => Order(p.Name)).ThenBy(p => p.Name).ToList();
                }
                catch { RemoteFailed = true; }
                RemoteLoading = false;
                for (int i = 0; i < 100 && !owner.IsHandleCreated; i++) System.Threading.Thread.Sleep(50);
                try { owner.BeginInvoke(done); } catch { }
            }) { IsBackground = true }.Start();
        }
        static int Order(string n) { var o = new[] { "realistic", "comic", "cartoon" }; int i = Array.IndexOf(o, n.ToLowerInvariant()); return i < 0 ? 99 : i; }
    }

    class SetupForm : Form
    {
        readonly bool uninst; string installDir, gameDir;
        string langSetting = "auto"; DateTime langClosed;
        bool optDesktop = true, optStart = true, optDefault = true, optLaunch = true, optKeepPacks = true;
        int page;          // 0 Willkommen, 1 Ort, 2 Spiel, 3 Packs, 4 Installieren, 5 Fertig
        const int PInstall = 3, PDone = 4, PCount = 5;   // Pack-Auswahl entfällt: Packs lädt man in FogForge herunter
        float progress; string status = ""; bool busy, failed; string error;
        Bitmap banner, logo, bannerScaled; TextBox pathBox;
        List<HotR> hots = new List<HotR>(); HotR over;
        class HotR { public string Id; public RectangleF R; public Action Act; public bool Enabled = true; }
        Timer anim; int TitleH { get { return UI.S(46); } }

        public SetupForm(bool uninstall, string dir)
        {
            uninst = uninstall;
            installDir = dir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\FogForge");
            gameDir = SetupProgram.DetectGame();
            if (!uninstall) Payload.Load();
            Text = uninst ? L.T("FogForge deinstallieren") : L.T("FogForge Installation");
            FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.None;
            BackColor = UI.Bg; ForeColor = UI.Text; Font = UI.F(10f);
            Size = uninst ? new Size(UI.S(620), UI.S(400)) : new Size(UI.S(820), UI.S(560));
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            try { using (var s = SetupProgram.Res("banner.jpg")) banner = new Bitmap(s); } catch { }
            try { using (var s = SetupProgram.Res("logo.png")) using (var raw = Image.FromStream(s)) { logo = new Bitmap(UI.S(28), UI.S(28)); using (var g = Graphics.FromImage(logo)) { UI.HQ(g); g.DrawImage(raw, 0, 0, logo.Width, logo.Height); } } } catch { }
            pathBox = new TextBox { BorderStyle = BorderStyle.None, BackColor = Color.FromArgb(24, 22, 28), ForeColor = UI.Text, Font = UI.F(10.5f), Visible = false };
            pathBox.TextChanged += (s, e) => { if (page == 1) installDir = pathBox.Text; };
            Controls.Add(pathBox);
            anim = new Timer { Interval = 15 }; anim.Tick += (s, e) => { bool m = Ease(); if (busy || m) Invalidate(); else anim.Stop(); };
            KeyPreview = true; KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape && !busy) Cancel(); };
        }
        protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; cp.Style |= 0x20000; return cp; } }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); MainForm.Rounded(Handle); }

        // ---------------- Zeichnen ----------------
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; UI.HQ(g); hots.Clear();
            int W = ClientSize.Width, H = ClientSize.Height, th = TitleH;
            g.Clear(UI.Bg);
            // Banner oben (Pack-Motiv, nach unten ausgeblendet)
            int bh = uninst ? UI.S(150) : UI.S(200);
            if (banner != null)
            {
                if (bannerScaled == null || bannerScaled.Width != W)
                {
                    if (bannerScaled != null) bannerScaled.Dispose();
                    bannerScaled = new Bitmap(W, bh);
                    using (var bg = Graphics.FromImage(bannerScaled)) { UI.HQ(bg); bg.DrawImage(banner, UI.Cover(banner.Size, new RectangleF(0, 0, W, bh))); }
                }
                g.DrawImageUnscaled(bannerScaled, 0, 0);
            }
            using (var lg = new LinearGradientBrush(new RectangleF(0, 0, W, bh + 1), Color.FromArgb(150, UI.Bg), UI.Bg, 90f))
            {
                lg.InterpolationColors = new ColorBlend { Colors = new[] { Color.FromArgb(170, UI.Bg), Color.FromArgb(70, UI.Bg), Color.FromArgb(255, UI.Bg) }, Positions = new[] { 0f, 0.45f, 1f } };
                g.FillRectangle(lg, 0, 0, W, bh + 1);
            }
            // Titelleiste
            if (logo != null) g.DrawImageUnscaled(logo, UI.S(14), (th - logo.Height) / 2);
            float tw1; using (var f = UI.F(12f, FontStyle.Bold)) { tw1 = g.MeasureString("FOGFORGE", f).Width; UI.Txt(g, "FOGFORGE", f, UI.Text, new RectangleF(UI.S(50), 0, tw1 + 10, th)); }
            using (var f = UI.F(9.5f)) UI.Txt(g, uninst ? L.T("Deinstallation") : "Setup  ·  " + SetupProgram.Version, f, UI.Sub, new RectangleF(UI.S(50) + tw1 + UI.S(8), 0, UI.S(240), th));
            var rc = new RectangleF(W - UI.S(48), UI.S(7), UI.S(38), th - UI.S(14));
            Hot("close", rc, Cancel, !busy);
            var rl = new RectangleF(rc.X - UI.S(10) - UI.S(70), UI.S(8), UI.S(70), th - UI.S(16));
            Hot("lang", rl, () => ShowLangMenu(rl), !busy);
            using (var p = UI.Round(rl, rl.Height / 2))
            {
                using (var b = new SolidBrush(Color.FromArgb((int)(22 + 22 * Hov("lang")), 255, 255, 255))) g.FillPath(b, p);
                using (var pen = new Pen(Color.FromArgb(40, 255, 255, 255))) g.DrawPath(pen, p);
            }
            L.Flag(g, L.Code, new RectangleF(rl.X + UI.S(12), rl.Y + rl.Height / 2 - UI.S(7), UI.S(21), UI.S(14)));
            using (var f = UI.F(9f, FontStyle.Bold)) UI.Txt(g, L.Code.ToUpperInvariant(), f, UI.Text, new RectangleF(rl.X + UI.S(38), rl.Y, rl.Width - UI.S(40), rl.Height));
            if (Hov("close") > 0.01f && !busy) using (var p = UI.Round(rc, UI.S(10))) using (var b = new SolidBrush(UI.A(UI.Red, (int)(230 * Hov("close"))))) g.FillPath(b, p);
            using (var p = new Pen(busy ? UI.Dim : UI.Text, UI.Sf(1.4f))) { float s = UI.S(5); var c = new PointF(rc.X + rc.Width / 2, rc.Y + rc.Height / 2); g.DrawLine(p, c.X - s, c.Y - s, c.X + s, c.Y + s); g.DrawLine(p, c.X - s, c.Y + s, c.X + s, c.Y - s); }

            float x0 = UI.S(40), w = W - UI.S(80);
            if (uninst) { PaintUninstall(g, W, H, x0, w, bh); return; }

            // Seitentitel über dem Banner-Ende
            string[] titles = { L.T("Willkommen bei FogForge"), L.T("Installationsort"), L.T("Wo ist Dead by Daylight installiert?"), failed ? L.T("Fehlgeschlagen") : L.T("FogForge wird installiert …"), L.T("FogForge ist bereit!") };
            using (var f = UI.F(20f, FontStyle.Bold)) UI.Txt(g, titles[page], f, UI.Text, new RectangleF(x0, bh - UI.S(64), w, UI.S(40)));
            using (var f = UI.F(9f, FontStyle.Bold)) UI.Txt(g, L.F("Schritt {0} von {1}", page + 1, PCount).ToUpperInvariant(), f, UI.RedHi, new RectangleF(x0 + UI.S(2), bh - UI.S(88), w, UI.S(22)));
            float y = bh + UI.S(10);
            pathBox.Visible = page == 1;

            if (page == 0)
            {
                using (var f = UI.F(11f)) using (var b = new SolidBrush(UI.Sub))
                    g.DrawString(L.T("FogForge lädt eigene Icon-Packs für Dead by Daylight ins Spiel – mit einem Klick, und genauso schnell wieder zurück zu den Originalen."), f, b, new RectangleF(x0, y, w, UI.S(60)));
                y += UI.S(70);
                foreach (var t in new[] { L.T("Packs aktivieren, wechseln und entladen"), L.T("Originale werden automatisch gesichert"), L.T("Verändert keine Spieldateien (.pak)") })
                {
                    using (var b = new SolidBrush(UI.Red)) g.FillEllipse(b, x0 + UI.S(2), y + UI.S(7), UI.S(8), UI.S(8));
                    using (var f = UI.F(10.5f)) UI.Txt(g, t, f, UI.Text, new RectangleF(x0 + UI.S(20), y, w, UI.S(22)));
                    y += UI.S(28);
                }
            }
            else if (page == 1)
            {
                using (var f = UI.F(10.5f)) UI.Txt(g, L.T("Wohin soll FogForge installiert werden?"), f, UI.Sub, new RectangleF(x0, y, w, UI.S(22)));
                y += UI.S(32);
                var box = new RectangleF(x0, y, w - UI.S(150), UI.S(40));
                using (var p = UI.Round(box, UI.S(10))) { using (var b = new SolidBrush(Color.FromArgb(24, 22, 28))) g.FillPath(b, p); using (var pen = new Pen(Color.FromArgb(50, 255, 255, 255))) g.DrawPath(pen, p); }
                var tb = new Rectangle((int)(box.X + UI.S(14)), (int)(box.Y + (box.Height - pathBox.PreferredHeight) / 2), (int)(box.Width - UI.S(28)), pathBox.PreferredHeight);
                if (pathBox.Bounds != tb) pathBox.Bounds = tb;
                if (pathBox.Text != installDir) pathBox.Text = installDir;
                SmallButton(g, "browse", new RectangleF(box.Right + UI.S(10), y, UI.S(140), UI.S(40)), L.T("Durchsuchen …"), BrowseInstall);
                y += UI.S(58);
                Toggle(g, "desk", x0, y, L.T("Desktop-Verknüpfung erstellen"), optDesktop, () => optDesktop = !optDesktop); y += UI.S(42);
                Toggle(g, "start", x0, y, L.T("Im Startmenü anzeigen"), optStart, () => optStart = !optStart); y += UI.S(46);
                using (var f = UI.F(9f)) UI.Txt(g, L.T("Benötigt keine Administratorrechte."), f, UI.Dim, new RectangleF(x0, y, w, UI.S(20)));
            }
            else if (page == 2)
            {
                bool ok = SetupProgram.NormalizeGame(gameDir) != null;
                var card = new RectangleF(x0, y, w, UI.S(78));
                var col = ok ? UI.Green : UI.Orange;
                using (var p = UI.Round(card, UI.S(12))) { using (var b = new SolidBrush(UI.A(col, 22))) g.FillPath(b, p); using (var pen = new Pen(UI.A(col, 90))) g.DrawPath(pen, p); }
                using (var b = new SolidBrush(col)) g.FillEllipse(b, card.X + UI.S(18), card.Y + UI.S(20), UI.S(10), UI.S(10));
                using (var f = UI.F(11f, FontStyle.Bold)) UI.Txt(g, ok ? L.T("Spiel automatisch gefunden") : L.T("Spiel nicht gefunden – bitte den Ordner auswählen."), f, UI.Mix(col, Color.White, 0.35f), new RectangleF(card.X + UI.S(38), card.Y + UI.S(12), card.Width - UI.S(200), UI.S(26)));
                using (var f = UI.F(9f)) UI.Txt(g, ok ? gameDir : L.T("Du kannst den Ordner auch später in FogForge festlegen."), f, UI.Sub, new RectangleF(card.X + UI.S(38), card.Y + UI.S(40), card.Width - UI.S(200), UI.S(24)));
                SmallButton(g, "pickgame", new RectangleF(card.Right - UI.S(156), card.Y + UI.S(19), UI.S(140), UI.S(40)), ok ? L.T("Ändern …") : L.T("Ordner wählen …"), PickGame);
                using (var f = UI.F(9.5f)) using (var b = new SolidBrush(UI.Sub))
                    g.DrawString(L.T("Das Default-Pack wird automatisch aus deinen Original-Icons erstellt. Weitere Packs lädst du danach mit einem Klick direkt in FogForge herunter."), f, b, new RectangleF(x0, card.Bottom + UI.S(22), w, UI.S(60)));
            }
            else if (page == 98)   // (nicht mehr verwendet)
            {
                // Pack-Kacheln mit Vorschau + Schalter
                bool gameOk = SetupProgram.NormalizeGame(gameDir) != null;
                var items = new List<Tuple<string, string, Bitmap, bool, Action>>();
                items.Add(Tuple.Create("Default", gameOk ? L.T("Immer dabei – aus deinen Original-Icons, perfekte Vorlage für eigene Packs") : L.T("Immer dabei – wird erstellt, sobald FogForge den Spielordner kennt"), (Bitmap)null, true, (Action)(() => { })));
                foreach (var p in Payload.Packs) { var pp = p; items.Add(Tuple.Create(p.Name, L.F("{0} Icons", p.Icons.ToString("N0")) + "  ·  " + (p.Rem != null ? L.F("{0} MB Download", (p.Bytes / 1048576).ToString("N0")) : (p.Bytes / 1048576) + " MB"), p.Preview, p.On, (Action)(() => pp.On = !pp.On))); }
                int cols = 2; float gap = UI.S(14), tw = (w - gap) / cols, th2 = UI.S(84);
                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i]; var r = new RectangleF(x0 + (i % cols) * (tw + gap), y + (i / cols) * (th2 + UI.S(12)), tw, th2);
                    var id = "pk" + i; var act = it.Item5; if (i > 0) Hot(id, r, () => { act(); Invalidate(); });
                    bool on = it.Item4; float hvv = Hov(id);
                    using (var p = UI.Round(r, UI.S(12)))
                    {
                        using (var b = new SolidBrush(on ? Color.FromArgb((int)(160 + 25 * hvv), 34, 16, 20) : Color.FromArgb((int)(110 + 35 * hvv), 22, 20, 26))) g.FillPath(b, p);
                        using (var pen = new Pen(on ? UI.A(UI.Red, (int)(130 + 60 * hvv)) : Color.FromArgb((int)(40 + 35 * hvv), 255, 255, 255), UI.Sf(1.2f))) g.DrawPath(pen, p);
                    }
                    var ir = new RectangleF(r.X + UI.S(8), r.Y + UI.S(8), UI.S(112), r.Height - UI.S(16));
                    using (var p = UI.Round(ir, UI.S(8)))
                    {
                        var oc = g.Clip; g.SetClip(p, CombineMode.Intersect);
                        if (it.Item3 != null) g.DrawImage(it.Item3, UI.Cover(it.Item3.Size, ir));
                        else if (banner != null) g.DrawImage(banner, UI.Cover(banner.Size, ir));
                        if (!on) using (var b = new SolidBrush(Color.FromArgb(140, 10, 9, 12))) g.FillRectangle(b, ir);
                        g.Clip = oc;
                    }
                    float txW = r.Right - UI.S(70) - (ir.Right + UI.S(12));
                    using (var f = UI.FitF(g, it.Item1, 11.5f, FontStyle.Bold, txW)) UI.Txt(g, it.Item1, f, on ? UI.Text : UI.Sub, new RectangleF(ir.Right + UI.S(12), r.Y + UI.S(8), txW, UI.S(26)));
                    // Untertitel: Schrift so lange verkleinern, bis er vollständig in die Kachel passt
                    var subBox = new RectangleF(ir.Right + UI.S(12), r.Y + UI.S(34), txW, r.Bottom - UI.S(8) - (r.Y + UI.S(34)));
                    float fs = 8.5f; Font sf2 = UI.F(fs);
                    while (fs > 6.5f && g.MeasureString(it.Item2, sf2, (int)subBox.Width).Height > subBox.Height) { sf2.Dispose(); fs -= 0.25f; sf2 = UI.F(fs); }
                    using (sf2) using (var b = new SolidBrush(UI.Dim)) g.DrawString(it.Item2, sf2, b, subBox);
                    if (i == 0)
                    {
                        // Default: fest dabei – Häkchen statt Schalter
                        var cr = new RectangleF(r.Right - UI.S(50), r.Y + r.Height / 2 - UI.S(13), UI.S(26), UI.S(26));
                        using (var b = new SolidBrush(UI.A(UI.Red, 60))) g.FillEllipse(b, cr);
                        using (var pen = new Pen(UI.Red, UI.Sf(1.4f))) g.DrawEllipse(pen, cr);
                        using (var pen = new Pen(Color.White, UI.Sf(2.2f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                            g.DrawLines(pen, new[] { new PointF(cr.X + UI.S(7), cr.Y + UI.S(13.5f)), new PointF(cr.X + UI.S(11.5f), cr.Y + UI.S(18)), new PointF(cr.X + UI.S(19), cr.Y + UI.S(8.5f)) });
                        continue;
                    }
                    var sr = new RectangleF(r.Right - UI.S(58), r.Y + r.Height / 2 - UI.S(11), UI.S(40), UI.S(22));
                    using (var p = UI.Round(sr, sr.Height / 2)) using (var b = new SolidBrush(on ? UI.Red : Color.FromArgb(255, 70, 68, 76))) g.FillPath(b, p);
                    using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, on ? sr.Right - UI.S(20) : sr.X + UI.S(2), sr.Y + UI.S(2), UI.S(18), UI.S(18));
                }
                string note = Payload.RemoteLoading ? L.T("Verfügbare Packs werden geladen …")
                    : Payload.RemoteFailed && Payload.Packs.Count == 0 ? L.T("Keine Internetverbindung – die Packs kannst du später in FogForge installieren.")
                    : Payload.Packs.Any(p => p.Rem != null) ? L.T("Die ausgewählten Packs werden während der Installation heruntergeladen.") : null;
                if (note != null)
                    using (var f = UI.F(9f)) UI.Txt(g, note, f, Payload.RemoteFailed ? UI.Orange : UI.Dim, new RectangleF(x0, y + ((items.Count + 1) / 2) * (th2 + UI.S(12)), w, UI.S(22)));
                if (Payload.RemoteLoading) anim.Start();
            }
            else if (page == PInstall)
            {
                if (failed)
                    using (var f = UI.F(10.5f)) using (var b = new SolidBrush(UI.RedHi)) g.DrawString(L.F("Fehler bei der Installation: {0}", error), f, b, new RectangleF(x0, y, w, UI.S(80)));
                else
                    ProgressBar(g, new RectangleF(x0, y + UI.S(20), w, UI.S(34)));
            }
            else if (page == PDone)
            {
                using (var f = UI.F(11f)) using (var b = new SolidBrush(UI.Sub)) g.DrawString(L.T("Die Installation ist abgeschlossen."), f, b, new RectangleF(x0, y, w, UI.S(30)));
                y += UI.S(44);
                Toggle(g, "launch", x0, y, L.T("FogForge jetzt starten"), optLaunch, () => optLaunch = !optLaunch);
                y += UI.S(52);
                if (dlFailed.Count > 0)
                    using (var f = UI.F(9.5f)) using (var b = new SolidBrush(UI.Orange)) g.DrawString(L.F("Nicht heruntergeladen: {0} – du kannst diese Packs jederzeit in FogForge installieren.", string.Join(", ", dlFailed)), f, b, new RectangleF(x0, y, w, UI.S(60)));
            }

            // Fußleiste: Schrittpunkte + Knöpfe
            float fy = H - UI.S(76);
            using (var p = new Pen(Color.FromArgb(26, 255, 255, 255))) g.DrawLine(p, 0, fy, W, fy);
            for (int i = 0; i < PCount; i++)
            {
                float dw = i == page ? UI.S(22) : UI.S(8);
                float dx = x0 + Enumerable.Range(0, i).Sum(k => (k == page ? UI.S(22) : UI.S(8)) + UI.S(6));
                using (var p = UI.Round(new RectangleF(dx, fy + UI.S(34), dw, UI.S(8)), UI.S(4))) using (var b = new SolidBrush(i == page ? UI.Red : (i < page ? UI.A(UI.Red, 120) : Color.FromArgb(50, 255, 255, 255)))) g.FillPath(b, p);
            }
            float bwid = UI.S(150), bht = UI.S(44), by2 = fy + UI.S(16);
            if (page < PInstall)
            {
                MainButton(g, "next", new RectangleF(W - x0 - bwid, by2, bwid, bht), page == 2 ? L.T("Installieren") : L.T("Weiter"), Next, page != 1 || !string.IsNullOrWhiteSpace(installDir));
                if (page > 0) SmallButton(g, "back", new RectangleF(W - x0 - bwid * 2 - UI.S(12), by2, bwid, bht), L.T("Zurück"), () => { page--; Invalidate(); });
            }
            else if (page == PInstall && failed) MainButton(g, "close2", new RectangleF(W - x0 - bwid, by2, bwid, bht), L.T("Schließen"), Close, true);
            else if (page == PDone) MainButton(g, "finish", new RectangleF(W - x0 - bwid, by2, bwid, bht), L.T("Fertig"), Finish, true);
        }

        void PaintUninstall(Graphics g, int W, int H, float x0, float w, int bh)
        {
            using (var f = UI.F(19f, FontStyle.Bold)) UI.Txt(g, page == 4 ? L.T("FogForge wurde entfernt.") : L.T("FogForge deinstallieren"), f, UI.Text, new RectangleF(x0, bh - UI.S(56), w, UI.S(40)));
            float y = bh + UI.S(6);
            if (page == 0)
            {
                using (var f = UI.F(10.5f)) using (var b = new SolidBrush(UI.Sub)) g.DrawString(L.T("FogForge wirklich vom PC entfernen?") + ((File.Exists(Path.Combine(installDir, @"Backup\active.txt")) || File.Exists(Path.Combine(installDir, @"Backup\aktiv.txt"))) ? "\n" + L.T("Ein Pack ist noch im Spiel aktiv. Die Original-Icons werden vorher wiederhergestellt.") : ""), f, b, new RectangleF(x0, y, w, UI.S(60)));
                y += UI.S(66);
                Toggle(g, "keep", x0, y, L.T("Eigene Packs behalten"), optKeepPacks, () => optKeepPacks = !optKeepPacks);
                float bwid = UI.S(170), bht = UI.S(44), by2 = H - UI.S(64);
                MainButton(g, "do", new RectangleF(W - x0 - bwid, by2, bwid, bht), L.T("Deinstallieren"), DoUninstall, true);
                SmallButton(g, "cancel", new RectangleF(W - x0 - bwid * 2 - UI.S(12), by2, bwid, bht), L.T("Abbrechen"), Close);
            }
            else if (page == 3) ProgressBar(g, new RectangleF(x0, y + UI.S(14), w, UI.S(34)));
            else if (page == 4)
            {
                float bwid = UI.S(170), bht = UI.S(44), by2 = H - UI.S(64);
                MainButton(g, "fin", new RectangleF(W - x0 - bwid, by2, bwid, bht), L.T("Schließen"), Close, true);
            }
        }

        // ---------------- Bausteine ----------------
        void Hot(string id, RectangleF r, Action a, bool en = true) { hots.Add(new HotR { Id = id, R = r, Act = a, Enabled = en }); }
        Dictionary<string, float> hv = new Dictionary<string, float>();
        bool IsOver(string id) { return Hov(id) > 0.5f; }
        float Hov(string id) { float v; return hv.TryGetValue(id, out v) ? v : 0; }
        bool Ease()
        {
            bool moving = false;
            foreach (var h in hots)
            {
                float t = over != null && over.Id == h.Id && h.Enabled ? 1 : 0, cur = Hov(h.Id);
                if (Math.Abs(cur - t) < 0.008f) { hv[h.Id] = t; continue; }
                hv[h.Id] = cur + (t - cur) * (t > cur ? 0.16f : 0.10f); moving = true;
            }
            return moving;
        }
        void MainButton(Graphics g, string id, RectangleF r, string text, Action a, bool en)
        {
            Hot(id, r, a, en); float h = en ? Hov(id) : 0;
            using (var p = UI.Round(r, r.Height / 2))
            {
                if (en) for (int i = 3; i >= 1; i--) using (var gp = UI.Round(RectangleF.Inflate(r, i * UI.S(3), i * UI.S(3)), r.Height / 2 + i * UI.S(3))) using (var b = new SolidBrush(UI.A(UI.Red, (int)((14 + 12 * h) / i)))) g.FillPath(b, gp);
                var c = en ? UI.Red : Color.FromArgb(70, 68, 78);
                using (var lg = new LinearGradientBrush(r, UI.Mix(c, Color.White, 0.10f + 0.10f * h), UI.Mix(c, Color.Black, 0.3f), 90f)) g.FillPath(lg, p);
                using (var pen = new Pen(Color.FromArgb(70, 255, 255, 255))) g.DrawPath(pen, p);
            }
            using (var f = UI.FitF(g, text, 11f, FontStyle.Bold, r.Width - UI.S(24))) UI.Txt(g, text, f, en ? Color.White : UI.Sub, r, StringAlignment.Center);
        }
        void SmallButton(Graphics g, string id, RectangleF r, string text, Action a)
        {
            Hot(id, r, a, !busy); float h = Hov(id);
            using (var p = UI.Round(r, r.Height / 2)) { using (var b = new SolidBrush(Color.FromArgb((int)(22 + 22 * h), 255, 255, 255))) g.FillPath(b, p); using (var pen = new Pen(Color.FromArgb(60, 255, 255, 255))) g.DrawPath(pen, p); }
            using (var f = UI.FitF(g, text, 10f, FontStyle.Bold, r.Width - UI.S(20))) UI.Txt(g, text, f, UI.Text, r, StringAlignment.Center);
        }
        void Toggle(Graphics g, string id, float x, float y, string text, bool on, Action a)
        {
            float tw; using (var f = UI.F(10.5f)) tw = g.MeasureString(text, f).Width;
            var r = new RectangleF(x, y, UI.S(58) + tw, UI.S(30)); Hot(id, r, () => { a(); Invalidate(); });
            var sr = new RectangleF(x, y + UI.S(5), UI.S(40), UI.S(22));
            using (var p = UI.Round(sr, sr.Height / 2)) using (var b = new SolidBrush(on ? UI.Red : Color.FromArgb(255, 70, 68, 76))) g.FillPath(b, p);
            float kx = on ? sr.Right - UI.S(20) : sr.X + UI.S(2);
            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, kx, sr.Y + UI.S(2), UI.S(18), UI.S(18));
            using (var f = UI.F(10.5f)) UI.Txt(g, text, f, on ? UI.Text : UI.Sub, new RectangleF(x + UI.S(56), y, tw + UI.S(10), UI.S(32)));
        }
        void ProgressBar(Graphics g, RectangleF r)
        {
            float barH = UI.S(6), y2 = r.Bottom - barH; var tr = new RectangleF(r.X, y2, r.Width, barH);
            using (var f = UI.F(9.5f, FontStyle.Bold)) UI.Txt(g, status, f, UI.Text, new RectangleF(r.X, r.Y, r.Width - UI.S(60), y2 - r.Y - UI.S(4)));
            using (var f = UI.F(9.5f, FontStyle.Bold)) UI.Txt(g, (int)(progress * 100) + " %", f, UI.Sub, new RectangleF(r.Right - UI.S(60), r.Y, UI.S(60), y2 - r.Y - UI.S(4)), StringAlignment.Far);
            using (var p = UI.Round(tr, barH / 2)) using (var b = new SolidBrush(Color.FromArgb(28, 255, 255, 255))) g.FillPath(b, p);
            var fr = new RectangleF(tr.X, tr.Y, Math.Max(barH, tr.Width * Math.Max(0.01f, progress)), barH);
            using (var p = UI.Round(fr, barH / 2))
            {
                using (var lg = new LinearGradientBrush(new RectangleF(tr.X - 1, tr.Y, tr.Width + 2, barH), UI.Red, UI.Orange, 0f)) g.FillPath(lg, p);
                var oc = g.Clip; g.SetClip(p, CombineMode.Intersect);
                float sw = UI.S(120), span = fr.Width + sw * 2, sx = fr.X - sw + (Environment.TickCount / 3 % (int)Math.Max(1, span));
                using (var lg = new LinearGradientBrush(new RectangleF(sx, fr.Y, sw, barH), Color.Transparent, Color.Transparent, 0f))
                { lg.InterpolationColors = new ColorBlend { Colors = new[] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(110, 255, 235, 210), Color.FromArgb(0, 255, 255, 255) }, Positions = new[] { 0f, 0.5f, 1f } }; g.FillRectangle(lg, sx, fr.Y, sw, barH); }
                g.Clip = oc;
            }
        }

        // ---------------- Maus ----------------
        protected override void OnMouseMove(MouseEventArgs e)
        {
            var h = hots.LastOrDefault(x => x.R.Contains(e.Location));
            if (h != over) { over = h; anim.Start(); }
            Cursor = h != null && h.Enabled ? Cursors.Hand : Cursors.Default;
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            var h = hots.LastOrDefault(x => x.R.Contains(e.Location));
            if (h != null) { if (h.Enabled) h.Act(); return; }
            if (e.Y < TitleH) MainForm.DragWindow(Handle);
        }
        protected override void OnMouseLeave(EventArgs e) { over = null; anim.Start(); }

        // ---------------- Ablauf ----------------
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
        void SetLang(string code)
        {
            langSetting = code; L.Code = code == "auto" ? L.SystemCode() : code;
            Text = uninst ? L.T("FogForge deinstallieren") : L.T("FogForge Installation");
            Invalidate();
        }
        void Cancel()
        {
            if (busy) return;
            if (uninst || page >= PInstall) { Close(); return; }
            if (DarkDialog.Ask(this, L.T("FogForge Installation"), L.T("Installation abbrechen?"), UI.Orange, L.T("Abbrechen"), L.T("Weiter")) == 0) Close();
        }
        void Next()
        {
            if (page == 1)
            {
                installDir = pathBox.Text.Trim();
                try { installDir = Path.GetFullPath(installDir); } catch { DarkDialog.Info(this, L.T("Installationsort"), L.T("Bitte einen gültigen Ordner angeben.")); return; }
                if (SetupProgram.FogForgeRunning(installDir)) { DarkDialog.Info(this, "FogForge", L.T("FogForge läuft noch. Bitte schließe FogForge und versuche es erneut.")); return; }
            }
            if (page == 2) { page = PInstall; Invalidate(); Install(); return; }
            page++; Invalidate();
        }
        void BrowseInstall()
        {
            using (var fb = new FolderBrowserDialog { Description = L.T("Wohin soll FogForge installiert werden?"), SelectedPath = Directory.Exists(installDir) ? installDir : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) })
                if (fb.ShowDialog(this) == DialogResult.OK)
                {
                    var p = fb.SelectedPath;
                    if (!Path.GetFileName(p).Equals("FogForge", StringComparison.OrdinalIgnoreCase)) p = Path.Combine(p, "FogForge");
                    installDir = p; pathBox.Text = p; Invalidate();
                }
        }
        void PickGame()
        {
            using (var fb = new FolderBrowserDialog { Description = L.T("Wähle den Dead-by-Daylight-Ordner (oder …\\Content\\UI\\Icons)") })
            {
                if (gameDir != null) fb.SelectedPath = gameDir;
                if (fb.ShowDialog(this) != DialogResult.OK) return;
                var n = SetupProgram.NormalizeGame(fb.SelectedPath);
                if (n == null) { DarkDialog.Info(this, L.T("Spielordner"), L.T("In diesem Ordner wurden keine Dead-by-Daylight-Icons gefunden.")); return; }
                gameDir = n; Invalidate();
            }
        }

        void Run(Action<BackgroundWorker> job, Action done)
        {
            busy = true; anim.Start();
            var bw = new BackgroundWorker { WorkerReportsProgress = true };
            bw.ProgressChanged += (s, e) => { progress = e.ProgressPercentage / 100f; if (e.UserState != null) status = (string)e.UserState; };
            bw.DoWork += (s, e) => job(bw);
            bw.RunWorkerCompleted += (s, e) =>
            {
                busy = false;
                if (e.Error != null) { failed = true; error = e.Error.Message; }
                else done();
                Invalidate();
            };
            bw.RunWorkerAsync();
        }

        List<string> dlFailed = new List<string>();
        void Install()
        {
            string dir = installDir, lang = langSetting, game = SetupProgram.NormalizeGame(gameDir); bool mkDefault = game != null, desk = optDesktop, start = optStart;
            dlFailed.Clear();
            string setupDir = Path.GetDirectoryName(Application.ExecutablePath), self = Application.ExecutablePath;
            Run(bw =>
            {
                bw.ReportProgress(2, L.T("Dateien werden kopiert …"));
                try
                {
                    var oldDir = Path.Combine(Path.GetDirectoryName(dir.TrimEnd('\\')) ?? "", "Fog Forge");
                    if (Path.GetFileName(dir.TrimEnd('\\')).Equals("FogForge", StringComparison.OrdinalIgnoreCase) && Directory.Exists(oldDir) && !Directory.Exists(dir))
                        Directory.Move(oldDir, dir);
                }
                catch { }
                Directory.CreateDirectory(dir); Directory.CreateDirectory(Path.Combine(dir, "Packs"));
                string exe = Path.Combine(dir, "FogForge.exe");
                using (var src = SetupProgram.Res("app.exe")) using (var dst = File.Create(exe)) src.CopyTo(dst);
                File.Copy(self, Path.Combine(dir, "Uninstall FogForge.exe"), true);
                foreach (var oldF in new[] { "Fog Forge deinstallieren.exe", "Uninstall Fog Forge.exe", "Fog Forge.exe" })
                    try { var op = Path.Combine(dir, oldF); if (File.Exists(op)) File.Delete(op); } catch { }
                foreach (var oldL in new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Fog Forge.lnk"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Fog Forge.lnk") })
                    try { if (File.Exists(oldL)) File.Delete(oldL); } catch { }
                // Einstellungen: Spielordner + Sprache übernehmen (vorhandene Auswahl bleibt erhalten)
                var set = Path.Combine(dir, "settings.txt");
                var lines = File.Exists(set) ? File.ReadAllLines(set).Where(l => !l.StartsWith("GamePath=") && !l.StartsWith("Language=")).ToList() : new List<string>();
                lines.Add("Language=" + lang);
                lines.Insert(0, "GamePath=" + (game ?? ""));
                File.WriteAllLines(set, lines);
                bw.ReportProgress(10);
                // Pack-Liste + Vorschaubilder schon jetzt in den Zwischenspeicher legen -> FogForge zeigt sie beim ersten Start sofort
                var cacheTh = new System.Threading.Thread(() =>
                {
                    try
                    {
                        var cdir = Path.Combine(dir, "Cache"); Directory.CreateDirectory(cdir);
                        var cat = Remote.FetchCatalog(15000, cdir);
                        if (cat.Count > 0) { Remote.SaveCache(cat, Path.Combine(cdir, "catalog.txt")); Remote.FetchPreviews(cat, cdir); }
                    }
                    catch { }
                }) { IsBackground = true };
                cacheTh.Start();

                // Eingebettete Packs entpacken
                var chosenPacks = Payload.Packs.Where(p => p.On && p.Dir == null && p.Rem == null).ToList();
                for (int k = 0; k < chosenPacks.Count; k++)
                {
                    int kk = k;
                    Payload.File.Extract(chosenPacks[k].Name, Path.Combine(dir, "Packs"), (a, b) =>
                        bw.ReportProgress(10 + (int)(55.0 * (kk + a / (double)b) / chosenPacks.Count), L.F("Packs werden installiert … {0} / {1}", a, b) + "  ·  " + chosenPacks[kk].Name));
                }
                // Ausgewählte Packs aus dem Internet herunterladen
                var dl = Payload.Packs.Where(p => p.On && p.Rem != null).ToList();
                for (int k = 0; k < dl.Count; k++)
                {
                    int kk = k;
                    try { Remote.Install(dl[k].Rem, Path.Combine(dir, "Packs"), (pc, t) => bw.ReportProgress(10 + (int)(60.0 * (kk + pc / 100.0) / dl.Count), t)); }
                    catch { lock (dlFailed) dlFailed.Add(dl[k].Name); }
                }
                // Nur die ausgewählten Packs aus dem Ordner „Packs“ neben dem Setup übernehmen
                foreach (var bp in Payload.Packs.Where(p => p.On && p.Dir != null).ToList())
                {
                    var target = Path.Combine(dir, "Packs", bp.Name);
                    if (bp.Dir.TrimEnd('\\', '/').Equals(target.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)) continue;
                    var files = Directory.GetFiles(bp.Dir, "*", SearchOption.AllDirectories);
                    for (int i = 0; i < files.Length; i++)
                    {
                        var t = Path.Combine(target, files[i].Substring(bp.Dir.Length).TrimStart('\\', '/'));
                        Directory.CreateDirectory(Path.GetDirectoryName(t)); File.Copy(files[i], t, true);
                        if (i % 20 == 0) bw.ReportProgress(65 + i * 5 / Math.Max(1, files.Length), L.F("Packs werden übernommen … {0} / {1}", i + 1, files.Length) + "  ·  " + bp.Name);
                    }
                }
                // Abgewählte Packs aus einer früheren Installation beiseitelegen (in FogForge wieder installierbar)
                string activePack = null;
                foreach (var af in new[] { @"Backup\active.txt", @"Backup\aktiv.txt" })
                    try { var ap = Path.Combine(dir, af); if (File.Exists(ap)) { activePack = File.ReadAllText(ap).Trim(); break; } } catch { }
                foreach (var np in Payload.Packs.Where(p => !p.On))
                {
                    var have = Path.Combine(dir, "Packs", np.Name);
                    if (!Directory.Exists(have) || np.Name.Equals(activePack, StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        if (np.Rem != null) { Directory.Delete(have, true); continue; }   // jederzeit wieder herunterladbar
                        var un = Path.Combine(dir, @"Backup\Uninstalled Packs"); Directory.CreateDirectory(un);
                        var to = Path.Combine(un, np.Name);
                        if (Directory.Exists(to)) Directory.Delete(to, true);
                        Directory.Move(have, to);
                    }
                    catch { }
                }
                // Default-Pack aus den eigenen Spiel-Icons
                if (mkDefault && !Directory.Exists(Path.Combine(dir, @"Packs\Default\Perks")))
                {
                    var cats = new[] { "CharPortraits", "Perks", "Powers", "items", "ItemAddons", "Favors", "StoreBackgrounds" };
                    var files = new List<string>();
                    foreach (var c in cats) { var d = Path.Combine(game, c); if (Directory.Exists(d)) files.AddRange(Directory.GetFiles(d, "*.png", SearchOption.AllDirectories)); }
                    for (int i = 0; i < files.Count; i++)
                    {
                        var t = Path.Combine(dir, @"Packs\Default", files[i].Substring(game.Length).TrimStart('\\', '/'));
                        Directory.CreateDirectory(Path.GetDirectoryName(t)); File.Copy(files[i], t, true);
                        if (i % 20 == 0) bw.ReportProgress(70 + i * 20 / Math.Max(1, files.Count), L.F("Default-Pack wird erstellt … {0} / {1}", i + 1, files.Count));
                    }
                    try { using (var s = SetupProgram.Res("default.jpg") ?? SetupProgram.Res("banner.jpg")) using (var f = File.Create(Path.Combine(dir, @"Packs\Default\preview.jpg"))) s.CopyTo(f); } catch { }
                }
                bw.ReportProgress(92, L.T("Verknüpfungen werden erstellt …"));
                try { if (desk) SetupProgram.Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "FogForge.lnk"), exe, "FogForge"); } catch { }
                try { if (start) SetupProgram.Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "FogForge.lnk"), exe, "FogForge"); } catch { }
                // Eintrag unter „Apps & Features“
                using (var k = Registry.CurrentUser.CreateSubKey(SetupProgram.UninstKey))
                {
                    k.SetValue("DisplayName", "FogForge"); k.SetValue("DisplayVersion", SetupProgram.Version); k.SetValue("Publisher", "FogForge");
                    k.SetValue("DisplayIcon", exe + ",0"); k.SetValue("InstallLocation", dir);
                    k.SetValue("UninstallString", "\"" + Path.Combine(dir, "Uninstall FogForge.exe") + "\" /uninstall");
                    k.SetValue("NoModify", 1, RegistryValueKind.DWord); k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    k.SetValue("EstimatedSize", (int)(2048 + Payload.Packs.Where(p => p.On).Sum(p => p.Bytes) / 1024), RegistryValueKind.DWord);
                    k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                }
                cacheTh.Join(20000);
                bw.ReportProgress(100, L.T("Fertig"));
            }, () => { page = PDone; });
        }
        void Finish()
        {
            if (optLaunch) try { Process.Start(new ProcessStartInfo(Path.Combine(installDir, "FogForge.exe")) { WorkingDirectory = installDir }); } catch { }
            Close();
        }

        void DoUninstall()
        {
            string dir = installDir; bool keep = optKeepPacks;
            if (SetupProgram.FogForgeRunning(dir)) { DarkDialog.Info(this, "FogForge", L.T("FogForge läuft noch. Bitte schließe FogForge und versuche es erneut.")); return; }
            page = 3; Invalidate();
            Run(bw =>
            {
                // Aktives Pack entladen: Originale zurückschreiben
                string game = null;
                try
                {
                    foreach (var sf in new[] { new[] { "settings.txt", "GamePath=" }, new[] { "einstellungen.txt", "Spielpfad=" } })
                    {
                        var fp = Path.Combine(dir, sf[0]); if (!File.Exists(fp)) continue;
                        var l = File.ReadAllLines(fp).FirstOrDefault(x => x.StartsWith(sf[1])); if (l != null) game = SetupProgram.NormalizeGame(l.Substring(sf[1].Length).Trim());
                        if (game != null) break;
                    }
                }
                catch { }
                var orig = Path.Combine(dir, @"Backup\Original");
                if (game != null && Directory.Exists(orig))
                {
                    var files = Directory.GetFiles(orig, "*", SearchOption.AllDirectories);
                    for (int i = 0; i < files.Length; i++)
                    {
                        var dst = Path.Combine(game, files[i].Substring(orig.Length + 1)); Directory.CreateDirectory(Path.GetDirectoryName(dst)); File.Copy(files[i], dst, true);
                        if (i % 25 == 0) bw.ReportProgress(i * 60 / Math.Max(1, files.Length), L.T("Originale werden wiederhergestellt …"));
                    }
                    var added = Path.Combine(dir, @"Backup\added_files.txt"); if (!File.Exists(added)) added = Path.Combine(dir, @"Backup\neu_hinzugefuegt.txt");
                    if (File.Exists(added)) foreach (var rel in File.ReadAllLines(added)) { var f = Path.Combine(game, rel); if (rel.Length > 0 && File.Exists(f)) try { File.Delete(f); } catch { } }
                }
                bw.ReportProgress(70, L.T("Dateien werden entfernt …"));
                foreach (var lnk in new[] { "FogForge.lnk", "Fog Forge.lnk" }.SelectMany(n => new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), n), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), n) }))
                    try { if (File.Exists(lnk)) File.Delete(lnk); } catch { }
                try { Registry.CurrentUser.DeleteSubKeyTree(SetupProgram.UninstKey, false); } catch { }
                foreach (var f in Directory.GetFiles(dir)) try { File.Delete(f); } catch { }
                foreach (var d in Directory.GetDirectories(dir))
                {
                    if (keep && Path.GetFileName(d).Equals("Packs", StringComparison.OrdinalIgnoreCase)) continue;
                    try { Directory.Delete(d, true); } catch { }
                }
                try { if (!Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir); } catch { }
                bw.ReportProgress(100, L.T("Fertig"));
            }, () => { page = 4; SelfDeleteLater(); });
        }
        static void SelfDeleteLater()
        {
            try
            {
                var me = Application.ExecutablePath;
                if (!me.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase)) return;
                Process.Start(new ProcessStartInfo("cmd.exe", "/c ping 127.0.0.1 -n 4 > nul & del \"" + me + "\"") { CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
            }
            catch { }
        }
    }

    // ============================================================ Stilles Update (von FogForge gestartet)
    class UpdateForm : Form
    {
        readonly string dir; readonly int pid; string text; float prog; Timer t;
        public UpdateForm(string dir, int pid)
        {
            this.dir = dir; this.pid = pid; text = L.T("FogForge wird aktualisiert …");
            FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterScreen; ShowInTaskbar = true; TopMost = true;
            BackColor = Color.FromArgb(16, 15, 20); Size = new Size(UI.S(460), UI.S(150)); Text = "FogForge Update";
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            t = new Timer { Interval = 30 }; t.Tick += (s, e) => Invalidate(); t.Start();
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); MainForm.Rounded(Handle); }
        protected override void OnShown(EventArgs e) { base.OnShown(e); new System.Threading.Thread(Work) { IsBackground = true }.Start(); }
        void Set(float p, string s) { prog = p; if (s != null) text = s; }
        void Work()
        {
            string exe = Path.Combine(dir, "FogForge.exe"), err = null;
            try
            {
                Set(0.1f, null);
                if (pid > 0) try { using (var pr = Process.GetProcessById(pid)) pr.WaitForExit(20000); } catch { }
                Set(0.3f, null);
                var nw = exe + ".new";
                using (var src = SetupProgram.Res("app.exe")) using (var dst = File.Create(nw)) src.CopyTo(dst);
                Set(0.6f, null);
                for (int i = 0; ; i++)
                {
                    try { if (File.Exists(exe)) File.Delete(exe); File.Move(nw, exe); break; }
                    catch (IOException) { if (i >= 60) throw; System.Threading.Thread.Sleep(250); }
                    catch (UnauthorizedAccessException) { if (i >= 60) throw; System.Threading.Thread.Sleep(250); }
                }
                // Deinstallationsprogramm nur aktualisieren, wenn es dort schon liegt (echte Installation)
                var un = Path.Combine(dir, "Uninstall FogForge.exe");
                if (File.Exists(un)) try { File.Copy(Application.ExecutablePath, un, true); } catch { }
                try
                {
                    using (var k = Registry.CurrentUser.OpenSubKey(SetupProgram.UninstKey, true))
                        if (k != null && string.Equals(((k.GetValue("InstallLocation") as string) ?? "").TrimEnd('\\'), dir, StringComparison.OrdinalIgnoreCase)) k.SetValue("DisplayVersion", SetupProgram.Version);
                }
                catch { }
                Set(1f, L.T("Fertig"));
            }
            catch (Exception ex) { err = ex.Message; }
            BeginInvoke((Action)(() =>
            {
                if (err != null) DarkDialog.Info(this, L.T("Update fehlgeschlagen"), L.F("Das Update konnte nicht installiert werden: {0}", err));
                try { if (File.Exists(exe)) Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = dir }); } catch { }
                Close();
            }));
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; UI.HQ(g); int W = ClientSize.Width, H = ClientSize.Height;
            g.Clear(BackColor);
            using (var p = new Pen(Color.FromArgb(55, 255, 255, 255))) g.DrawRectangle(p, 0, 0, W - 1, H - 1);
            using (var f = UI.F(13f, FontStyle.Bold)) UI.Txt(g, "FogForge  " + SetupProgram.Version, f, UI.Text, new RectangleF(UI.S(26), UI.S(18), W - UI.S(52), UI.S(34)));
            using (var f = UI.F(10f)) UI.Txt(g, text, f, UI.Sub, new RectangleF(UI.S(26), UI.S(56), W - UI.S(52), UI.S(26)));
            var bar = new RectangleF(UI.S(26), H - UI.S(42), W - UI.S(52), UI.S(8));
            using (var path = UI.Round(bar, UI.S(4))) using (var b = new SolidBrush(Color.FromArgb(30, 255, 255, 255))) g.FillPath(b, path);
            float shown = Math.Max(0.04f, prog);
            var fill = new RectangleF(bar.X, bar.Y, bar.Width * shown, bar.Height);
            using (var path = UI.Round(fill, UI.S(4))) using (var b = new LinearGradientBrush(bar, UI.RedHi, UI.Red, 0f)) g.FillPath(b, path);
        }
    }
}
