// FogForge – eingebettete Packs (ZIP-Daten am Ende einer EXE: ZIP | 8 Byte Länge | "FOGFORGE-PACKS-1")
// Wird vom Installer und von FogForge selbst gelesen (Packs nachinstallieren).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace DbdCustom
{
    class PayloadFile
    {
        static readonly byte[] Magic = System.Text.Encoding.ASCII.GetBytes("FOGFORGE-PACKS-1");
        public class PackInfo
        {
            public string Name; public int Icons; public long Bytes; public Bitmap Preview; public bool On = true; public string Dir; public Remote.RPack Rem;
            public Dictionary<string, int> Counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
        public string Path; long zipStart = -1, zipLen; bool plainZip;
        public List<PackInfo> Packs = new List<PackInfo>();

        // Eingebettete Packs aus einer EXE oder Packs aus einer normalen ZIP-Datei lesen
        public static PayloadFile Load(string path, int previewWidth)
        {
            try
            {
                if (path == null || !File.Exists(path)) return null;
                var pf = new PayloadFile { Path = path };
                if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) { pf.plainZip = true; }
                else
                {
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        int scan = (int)Math.Min(fs.Length, 4 * 1024 * 1024);
                        fs.Seek(fs.Length - scan, SeekOrigin.Begin);
                        var buf = new byte[scan]; int n = 0; while (n < scan) { int r = fs.Read(buf, n, scan - n); if (r <= 0) break; n += r; }
                        int pos = -1;
                        for (int i = n - Magic.Length; i >= 8; i--)
                        {
                            bool ok = true; for (int k = 0; k < Magic.Length; k++) if (buf[i + k] != Magic[k]) { ok = false; break; }
                            if (ok) { pos = i; break; }
                        }
                        if (pos < 0) return null;
                        pf.zipLen = BitConverter.ToInt64(buf, pos - 8);
                        pf.zipStart = fs.Length - scan + pos - 8 - pf.zipLen;
                        if (pf.zipStart < 0) return null;
                    }
                }
                using (var z = pf.Open())
                {
                    var byPack = z.Entries.Where(e => e.Length > 0 && Norm(e.FullName).Contains("/"))
                                          .GroupBy(e => Norm(e.FullName).Substring(0, Norm(e.FullName).IndexOf('/')), StringComparer.OrdinalIgnoreCase);
                    foreach (var grp in byPack)
                    {
                        var p = new PackInfo { Name = grp.Key, Bytes = grp.Sum(e => e.Length) };
                        foreach (var e in grp)
                        {
                            var parts = Norm(e.FullName).Split('/');
                            if (parts.Length >= 3 && e.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                            { int c; p.Counts.TryGetValue(parts[1], out c); p.Counts[parts[1]] = c + 1; p.Icons++; }
                        }
                        var pv = grp.FirstOrDefault(e => Norm(e.FullName).Equals(grp.Key + "/preview.jpg", StringComparison.OrdinalIgnoreCase));
                        if (pv != null && previewWidth > 0)
                            try { using (var st = pv.Open()) using (var ms = new MemoryStream()) { st.CopyTo(ms); ms.Position = 0; using (var im = Image.FromStream(ms)) p.Preview = new Bitmap(im, new Size(previewWidth, (int)(previewWidth * im.Height / (float)im.Width))); } } catch { }
                        if (p.Icons > 0) pf.Packs.Add(p);
                    }
                }
                return pf.Packs.Count > 0 ? pf : null;
            }
            catch { return null; }
        }
        public static string Norm(string n) { return n.Replace('\\', '/').TrimStart('/'); }

        public ZipArchive Open()
        {
            var fs = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Stream s = plainZip ? (Stream)fs : new SubStream(fs, zipStart, zipLen);
            return new ZipArchive(s, ZipArchiveMode.Read, false);
        }

        // Ein Pack in einen Zielordner (…\Packs) entpacken
        public void Extract(string packName, string packsDir, Action<int, int> progress)
        {
            using (var z = Open())
            {
                var ents = z.Entries.Where(e => e.Length > 0 && Norm(e.FullName).StartsWith(packName + "/", StringComparison.OrdinalIgnoreCase)).ToList();
                for (int i = 0; i < ents.Count; i++)
                {
                    var rel = Norm(ents[i].FullName);
                    if (rel.Contains("../") || rel.Contains(":")) continue;
                    var t = System.IO.Path.Combine(packsDir, rel.Replace('/', System.IO.Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(t));
                    using (var src = ents[i].Open()) using (var dst = File.Create(t)) src.CopyTo(dst);
                    if (progress != null && i % 20 == 0) progress(i + 1, ents.Count);
                }
            }
        }

        class SubStream : Stream
        {
            readonly Stream b; readonly long start, len; long pos;
            public SubStream(Stream baseStream, long s, long l) { b = baseStream; start = s; len = l; }
            public override bool CanRead { get { return true; } }
            public override bool CanSeek { get { return true; } }
            public override bool CanWrite { get { return false; } }
            public override long Length { get { return len; } }
            public override long Position { get { return pos; } set { pos = value; } }
            public override int Read(byte[] buffer, int offset, int count)
            {
                long left = len - pos; if (left <= 0) return 0; if (count > left) count = (int)left;
                b.Seek(start + pos, SeekOrigin.Begin); int r = b.Read(buffer, offset, count); pos += r; return r;
            }
            public override long Seek(long o, SeekOrigin org) { pos = org == SeekOrigin.Begin ? o : org == SeekOrigin.Current ? pos + o : len + o; return pos; }
            public override void Flush() { }
            public override void SetLength(long v) { throw new NotSupportedException(); }
            public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
            protected override void Dispose(bool d) { if (d) b.Dispose(); base.Dispose(d); }
        }
    }
}
