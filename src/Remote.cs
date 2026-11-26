// FogForge – Packs aus dem Internet
// Katalog: packs.json. Jedes Pack ist eine ZIP-Datei mit dem Ordner <Name>/…
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace DbdCustom
{
    static class Remote
    {
        // Download-Adresse liegt nur verschlüsselt im Programm und wird erst bei Bedarf zusammengesetzt
        static readonly byte[] bk = { 0x5A, 0xC3, 0x17, 0x8E, 0x2B, 0xF1, 0x64, 0x39, 0xA7 };
        static readonly byte[] bd = { 0x32, 0xA8, 0x5D, 0xA3, 0x24, 0x50, 0xF1, 0xCF, 0x38, 0x24, 0x81, 0x2A, 0x8F, 0xDA, 0x6D, 0xD6, 0xA6, 0xC5, 0x5B, 0xCA, 0x1E, 0x71, 0xE0, 0x51, 0xE2, 0x5B, 0xE5, 0x4C, 0xD3, 0xE1, 0x48, 0x83, 0x7E, 0xB4, 0x41, 0xF5, 0x61, 0x95, 0xEB, 0x58, 0x81, 0x61, 0x17, 0x21, 0x83, 0x48, 0x32, 0xCD, 0x2D, 0xEB, 0x8D, 0x2C, 0x19, 0xA9, 0xB1, 0x19, 0xBA, 0x1A, 0x02, 0xB8, 0x41, 0x2E, 0x40, 0x88, 0x77, 0xE7, 0x14, 0x59, 0xBA, 0x51, 0x2F, 0x51, 0x83, 0x70, 0xCE };
        static string baseUrl, apiUrl;
        static readonly byte[] ak = { 0x3C, 0x91, 0x5E, 0x07, 0xD2, 0x48, 0xA3 };
        static readonly byte[] ad = { 0x54, 0xF4, 0x08, 0x44, 0xE5, 0x27, 0xEA, 0x64, 0x78, 0xB7, 0xC4, 0x47, 0xE3, 0x17, 0xA6, 0x06, 0x3B, 0x44, 0xCE, 0x68, 0x98, 0x34, 0xC8, 0xAB, 0xFA, 0x0B, 0x9D, 0x1B, 0xCF, 0x38, 0xC5, 0x7C, 0x93, 0x10, 0x8F, 0x0A, 0x91, 0x78, 0xF5, 0x30, 0x84, 0x73, 0x99, 0x65, 0xD4, 0x95, 0xBB, 0x7A, 0xF5, 0x12, 0xB1, 0x5A, 0x16, 0x7A, 0xAE, 0x65, 0xE7, 0x33, 0xF7, 0xC3, 0x5C, 0x20, 0xD1, 0x76, 0xB0, 0x7C, 0x00, 0xD2, 0xE3, 0x5A, 0xFB, 0x52, 0xF3, 0xAD, 0x4C };
        public static string Api
        {
            get
            {
                if (apiUrl == null)
                {
                    var c = new char[ad.Length];
                    for (int i = 0; i < ad.Length; i++) c[i] = (char)(ad[i] ^ ak[i % ak.Length] ^ ((i * 17) & 0xFF));
                    apiUrl = new string(c);
                }
                return apiUrl;
            }
        }
        // Programm-Updates (eigenes Repository, ebenfalls nur verschlüsselt)
        static readonly byte[] uk = { 0x71, 0x2E, 0xC4, 0x9B, 0x13, 0x5D, 0xE8, 0x06 };
        static readonly byte[] ud = { 0x19, 0x4D, 0x9E, 0xAE, 0x3C, 0x14, 0x4D, 0x88, 0xA8, 0x91, 0x4B, 0x48, 0x60, 0x1F, 0xDE, 0x37, 0x74, 0xCB, 0x74, 0x4D, 0xB0, 0xD3, 0x3D, 0x65, 0x3C, 0x61, 0xFD, 0x85, 0xB8, 0x82, 0x3F, 0xBB, 0xF0, 0xB0, 0xA4, 0xDB, 0x4B, 0x5D, 0xF6, 0xF2, 0x8D, 0xE8, 0x6D, 0x69, 0x81, 0x39, 0xAD, 0x59, 0x4E, 0x3B, 0xDD, 0x6B, 0x90, 0xEC, 0x57, 0x9B, 0x1C, 0x50, 0x81, 0xB3, 0x04, 0x09, 0x16, 0xCE, 0xC5, 0x9C, 0x59, 0xEA };
        static string updUrl;
        public static string UpdApi
        {
            get
            {
                if (updUrl == null)
                {
                    var c = new char[ud.Length];
                    for (int i = 0; i < ud.Length; i++) c[i] = (char)(ud[i] ^ uk[i % uk.Length] ^ ((i * 23) & 0xFF));
                    updUrl = new string(c);
                }
                return updUrl;
            }
        }
        public class AppUpdate { public Version Version; public string Tag, Url, Sha256, Notes; public long Bytes; }
        // neuestes Programm-Release; null, wenn keins oder nicht neuer als die laufende Version
        public static AppUpdate CheckAppUpdate(int timeoutMs)
        {
            string json;
            var rq = Req(UpdApi, timeoutMs); rq.Accept = "application/vnd." + new string(new[] { (char)103, (char)105, (char)116 }) + "hub+json";
            using (var resp = rq.GetResponse()) using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) json = sr.ReadToEnd();
            var root = new Json(json).Value() as Dictionary<string, object>;
            if (root == null) return null;
            var tag = (S(root, "tag_name") ?? "").Trim(); var vs = tag.TrimStart('v', 'V');
            Version v; if (!Version.TryParse(vs.Contains(".") ? vs : vs + ".0", out v)) return null;
            var cur = new Version(AppInfo.Version);
            if (v.CompareTo(cur) <= 0) return null;
            var assets = root.ContainsKey("assets") ? root["assets"] as List<object> : null; if (assets == null) return null;
            foreach (var o in assets)
            {
                var d = o as Dictionary<string, object>; if (d == null) continue;
                var n = S(d, "name") ?? "";
                if (!n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || n.IndexOf("setup", StringComparison.OrdinalIgnoreCase) < 0) continue;
                var st = S(d, "state");
                if (st != null && st != "uploaded") continue;   // Upload läuft noch
                var dig = S(d, "digest") ?? "";
                return new AppUpdate { Version = v, Tag = tag, Url = S(d, "browser_download_url"), Bytes = (long)N(d, "size"), Sha256 = dig.StartsWith("sha256:") ? dig.Substring(7) : null, Notes = S(root, "body") };
            }
            return null;
        }
        // Programm-Update herunterladen (gleiche Technik wie Packs: mehrere Teile gleichzeitig + Prüfsumme)
        public static void DownloadUpdate(AppUpdate u, string file, Action<int, string> progress)
        {
            var p = new RPack { Name = "FogForge " + u.Version, File = null, Bytes = u.Bytes, Sha256 = u.Sha256, Url = u.Url };
            Download(p, file, progress);
        }
        public static string Base
        {
            get
            {
                if (baseUrl == null)
                {
                    var c = new char[bd.Length];
                    for (int i = 0; i < bd.Length; i++) c[i] = (char)(bd[i] ^ bk[i % bk.Length] ^ ((i * 31) & 0xFF));
                    baseUrl = new string(c);
                }
                return baseUrl;
            }
        }

        public class RPack
        {
            public string Name, File, Sha256, Preview, Stamp, Url, Thumbs; public long Bytes; public int Icons, Version = 1;
            public Dictionary<string, int> Counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        static Remote()
        {
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }   // TLS 1.2
            try { ServicePointManager.DefaultConnectionLimit = Math.Max(ServicePointManager.DefaultConnectionLimit, 12); ServicePointManager.Expect100Continue = false; } catch { }
        }
        // Proxy nur verwenden, wenn in Windows wirklich einer eingetragen ist (spart die langsame automatische Proxy-Suche)
        static IWebProxy proxy; static bool proxyChecked;
        static IWebProxy GetProxy()
        {
            if (proxyChecked) return proxy;
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings"))
                {
                    object en = k != null ? k.GetValue("ProxyEnable") : null, ac = k != null ? k.GetValue("AutoConfigURL") : null;
                    bool manual = en is int && (int)en != 0, pac = ac is string && ((string)ac).Length > 0;
                    proxy = (manual || pac) ? WebRequest.GetSystemWebProxy() : null;
                }
            }
            catch { proxy = WebRequest.GetSystemWebProxy(); }
            proxyChecked = true; return proxy;
        }

        static HttpWebRequest Req(string url, int timeout)
        {
            var r = (HttpWebRequest)WebRequest.Create(url);
            r.UserAgent = "FogForge/4.0"; r.AllowAutoRedirect = true; r.Timeout = timeout; r.ReadWriteTimeout = Math.Max(timeout, 30000);
            r.Proxy = GetProxy(); if (r.Proxy != null) r.Proxy.Credentials = CredentialCache.DefaultCredentials;
            return r;
        }

        // ---------- Katalog ----------
        // Packs direkt aus dem neuesten Release: jede .zip ist ein Pack, <Name>-preview.jpg das Vorschaubild
        static string catEtag, catJson;
        public static List<RPack> FetchCatalog(int timeoutMs, string cacheDir) { return FetchCatalog(timeoutMs, cacheDir, null); }
        // pending: Packs, deren Inhaltsverzeichnis (noch) nicht gelesen werden konnte – z. B. direkt nach dem Hochladen
        public static List<RPack> FetchCatalog(int timeoutMs, string cacheDir, List<string> pending)
        {
            string json = null;
            var rq = Req(Api, timeoutMs); rq.Accept = "application/vnd." + new string(new[] { (char)103, (char)105, (char)116 }) + "hub+json";
            // bedingte Abfrage: unverändert -> 304 (zählt nicht gegen das Abfrage-Limit, daher sind häufige Live-Prüfungen möglich)
            if (catEtag != null && catJson != null) rq.Headers[HttpRequestHeader.IfNoneMatch] = catEtag;
            try
            {
                using (var resp = (HttpWebResponse)rq.GetResponse())
                using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                { json = sr.ReadToEnd(); var et = resp.Headers[HttpResponseHeader.ETag]; catEtag = string.IsNullOrEmpty(et) ? null : et; catJson = json; }
            }
            catch (WebException ex)
            {
                var hr = ex.Response as HttpWebResponse;
                if (hr != null && hr.StatusCode == HttpStatusCode.NotModified && catJson != null) json = catJson; else throw;
            }
            var root = new Json(json).Value() as Dictionary<string, object>;
            var assets = root != null && root.ContainsKey("assets") ? root["assets"] as List<object> : null;
            var list = new List<RPack>();
            if (assets == null) throw new IOException("catalog");
            var names = new HashSet<string>(assets.Select(a => S(a as Dictionary<string, object>, "name") ?? ""), StringComparer.OrdinalIgnoreCase);
            foreach (var o in assets)
            {
                var d = o as Dictionary<string, object>; if (d == null) continue;
                var file = S(d, "name");
                if (file == null || !file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
                var name = file.Substring(0, file.Length - 4);
                if (name.EndsWith("-thumbs", StringComparison.OrdinalIgnoreCase)) continue;
                if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains("..") || name.Equals("Default", StringComparison.OrdinalIgnoreCase)) continue;
                var st = S(d, "state");
                if (st != null && st != "uploaded") { if (pending != null) pending.Add(name); continue; }   // Upload läuft noch
                var dig = S(d, "digest") ?? "";
                var p = new RPack { Name = name, File = file, Bytes = (long)N(d, "size"), Sha256 = dig.StartsWith("sha256:") ? dig.Substring(7) : null };
                p.Stamp = !string.IsNullOrEmpty(p.Sha256) ? p.Sha256 : (S(d, "updated_at") ?? "") + "/" + p.Bytes;
                if (names.Contains(name + "-preview.jpg")) p.Preview = name + "-preview.jpg";
                if (names.Contains(name + "-thumbs.zip")) p.Thumbs = name + "-thumbs.zip";
                list.Add(p);
            }
            // Icon-Anzahl aller Packs gleichzeitig lesen
            var ths = list.Select(p => new System.Threading.Thread(() => { try { ReadIndex(p, cacheDir); } catch { } }) { IsBackground = true }).ToList();
            ths.ForEach(t => t.Start()); ths.ForEach(t => t.Join(40000));
            // fehlgeschlagene Packs (z. B. gerade frisch hochgeladen) noch einmal versuchen …
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var failed = list.Where(p => p.Counts.Count == 0).ToList();
                if (failed.Count == 0) break;
                System.Threading.Thread.Sleep(2500 * (attempt + 1));
                foreach (var p in failed) try { ReadIndex(p, cacheDir); } catch { }
            }
            // noch nicht lesbare Packs (z. B. gerade beim Hochladen) erst anzeigen, wenn sie wirklich bereit sind
            if (pending != null) pending.AddRange(list.Where(p => p.Counts.Count == 0).Select(p => p.Name));
            return list.Where(p => p.Counts.Count > 0).ToList();
        }

        // Inhaltsverzeichnis der ZIP vom Dateiende lesen (ohne die ZIP herunterzuladen) -> Icons je Kategorie
        // Platzhalter/Testbilder des Spiels (Missing, Missing01…, PowerTemp…, empty) – nicht mitzählen/anzeigen
        public static bool IsPlaceholder(string path)
        {
            var p = (path ?? "").Replace('\\', '/'); var name = Path.GetFileNameWithoutExtension(p);
            return System.Text.RegularExpressions.Regex.IsMatch(name, @"^(Missing\d*|empty)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                || name.IndexOf("PowerTemp", StringComparison.OrdinalIgnoreCase) >= 0 || p.IndexOf("/Missing/", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        static void ReadIndex(RPack p, string cacheDir)
        {
            var cf = Path.Combine(cacheDir, p.Name + "-" + (p.Stamp.Length > 16 ? p.Stamp.Substring(0, 16) : p.Stamp.Replace(':', '_').Replace('/', '_')) + ".idx2");
            if (System.IO.File.Exists(cf))
            {
                foreach (var l in System.IO.File.ReadAllLines(cf)) { var kv = l.Split('='); int n; if (kv.Length == 2 && int.TryParse(kv[1], out n)) p.Counts[kv[0]] = n; }
                p.Icons = p.Counts.Values.Sum(); return;
            }
            List<ZEntry> ents;
            try { ents = ListEntries(p); }
            catch { if (string.IsNullOrEmpty(p.Thumbs)) throw; ents = ListEntries(p, p.Thumbs); }   // Ersatz: gleiches Inhaltsverzeichnis in der kleinen Vorschau-ZIP
            foreach (var e in ents) { if (IsPlaceholder(e.Name)) continue; int c; p.Counts.TryGetValue(e.Cat, out c); p.Counts[e.Cat] = c + 1; }
            p.Icons = p.Counts.Values.Sum();
            try { Directory.CreateDirectory(cacheDir); System.IO.File.WriteAllLines(cf, p.Counts.Select(kv => kv.Key + "=" + kv.Value).ToArray()); } catch { }
        }
        // Eintrag in der ZIP auf dem Server (für die Vorschau einzelner Bilder ohne Komplett-Download)
        public class ZEntry { public string Name, Cat, Asset; public long Offset, Size, USize; public int Method, NameLen; }
        public static List<ZEntry> ListEntries(RPack p) { return ListEntries(p, p.File); }
        public static List<ZEntry> ListEntries(RPack p, string asset)
        {
            var list = new List<ZEntry>();
            // aktuelle Größe über die direkte Adresse (Größe kann sich seit dem Zwischenspeichern geändert haben)
            long size = asset == p.File ? p.Bytes : 0;
            var r0 = AssetRange(asset, 0, 0); if (r0.Total > 0) size = r0.Total;
            if (size <= 0) throw new IOException("size");
            int tail = (int)Math.Min(size, 65557);
            var end = AssetRange(asset, size - tail, size - 1).Data;
            int i = -1;
            for (int k = end.Length - 22; k >= 0; k--) if (end[k] == 0x50 && end[k + 1] == 0x4B && end[k + 2] == 5 && end[k + 3] == 6) { i = k; break; }
            if (i < 0) return list;
            long cdSize = BitConverter.ToUInt32(end, i + 12), cdOff = BitConverter.ToUInt32(end, i + 16);
            var cd = AssetRange(asset, cdOff, cdOff + cdSize - 1).Data;
            int pos = 0;
            while (pos + 46 <= cd.Length && BitConverter.ToUInt32(cd, pos) == 0x02014b50)
            {
                int nl = BitConverter.ToUInt16(cd, pos + 28), el = BitConverter.ToUInt16(cd, pos + 30), cl = BitConverter.ToUInt16(cd, pos + 32);
                var n = Encoding.UTF8.GetString(cd, pos + 46, nl).Replace('\\', '/');
                var parts = n.Split('/');
                if (parts.Length >= 3 && n.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    list.Add(new ZEntry { Name = n, Cat = parts[1], Asset = asset, NameLen = nl, Method = BitConverter.ToUInt16(cd, pos + 10), Size = BitConverter.ToUInt32(cd, pos + 20), USize = BitConverter.ToUInt32(cd, pos + 24), Offset = BitConverter.ToUInt32(cd, pos + 42) });
                pos += 46 + nl + el + cl;
            }
            return list;
        }
        // kleine Datei aus dem Release komplett laden (z. B. <Name>-thumbs.zip für die Vorschau)
        public static byte[] GetAsset(string asset)
        {
            var r0 = AssetRange(asset, 0, 0); if (r0.Total <= 0) throw new IOException("size");
            var d = AssetRange(asset, 0, r0.Total - 1).Data;
            if (d.Length != r0.Total) throw new IOException("size");
            return d;
        }
        public static byte[] ReadEntry(RPack p, ZEntry e)
        {
            var res = ReadEntries(new List<ZEntry> { e });
            byte[] b; if (res.TryGetValue(e, out b)) return b;
            throw new IOException("zip");
        }
        // mehrere Einträge mit möglichst wenigen Anfragen lesen: nahe beieinanderliegende Einträge in einem Stück
        public static Dictionary<ZEntry, byte[]> ReadEntries(List<ZEntry> entries)
        {
            var res = new Dictionary<ZEntry, byte[]>();
            foreach (var grp in entries.GroupBy(e => e.Asset))
            {
                var sorted = grp.OrderBy(e => e.Offset).ToList();
                int k = 0;
                while (k < sorted.Count)
                {
                    long from = sorted[k].Offset, to = End(sorted[k]); int j = k + 1;
                    while (j < sorted.Count && sorted[j].Offset - to < 262144 && End(sorted[j]) - from < 4194304) { to = Math.Max(to, End(sorted[j])); j++; }
                    var buf = AssetRange(grp.Key, from, to).Data;
                    for (int q = k; q < j; q++)
                    {
                        var e = sorted[q]; byte[] data = Slice(buf, e.Offset - from, e);
                        if (data == null) try { var one = AssetRange(grp.Key, e.Offset, e.Offset + 30 + e.NameLen + 4096 + e.Size).Data; data = Slice(one, 0, e); } catch { }
                        if (data != null) res[e] = data;
                    }
                    k = j;
                }
            }
            return res;
        }
        static long End(ZEntry e) { return e.Offset + 30 + e.NameLen + 96 + e.Size; }
        static byte[] Slice(byte[] b, long at, ZEntry e)
        {
            if (at < 0 || at + 30 > b.Length || BitConverter.ToUInt32(b, (int)at) != 0x04034b50) return null;
            int nl = BitConverter.ToUInt16(b, (int)at + 26), el = BitConverter.ToUInt16(b, (int)at + 28); long st = at + 30 + nl + el;
            if (st + e.Size > b.Length) return null;
            if (e.Method == 0) { var r = new byte[e.Size]; Array.Copy(b, st, r, 0, e.Size); return r; }
            using (var ms = new MemoryStream(b, (int)st, (int)e.Size)) using (var ds = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionMode.Decompress)) using (var o = new MemoryStream()) { ds.CopyTo(o); return o.ToArray(); }
        }

        // Die Release-Adresse leitet jedes Mal auf den Download-Server um – die Zieladresse merken wir uns ein paar Minuten
        class Direct { public string Url; public int At; }
        static readonly Dictionary<string, Direct> direct = new Dictionary<string, Direct>();
        static string DirectUrl(string asset, bool refresh)
        {
            Direct d;
            lock (direct) if (!refresh && direct.TryGetValue(asset, out d) && Environment.TickCount - d.At < 180000) return d.Url;
            string url = Base + asset + (refresh ? "?r=" + (Environment.TickCount & 0x7fffffff) : "");
            try
            {
                for (int hop = 0; hop < 5; hop++)
                {
                    var r = Req(url, 20000); r.AllowAutoRedirect = false; r.Method = "HEAD";
                    using (var resp = (HttpWebResponse)r.GetResponse())
                    {
                        int code = (int)resp.StatusCode; var loc = resp.Headers["Location"];
                        if (code >= 300 && code < 400 && !string.IsNullOrEmpty(loc)) { url = new Uri(new Uri(url), loc).ToString(); continue; }
                        break;
                    }
                }
            }
            catch (WebException ex)
            {
                var resp = ex.Response as HttpWebResponse;
                if (resp != null) { int code = (int)resp.StatusCode; var loc = resp.Headers["Location"]; resp.Close(); if (code >= 300 && code < 400 && !string.IsNullOrEmpty(loc)) url = new Uri(new Uri(url), loc).ToString(); }
            }
            lock (direct) direct[asset] = new Direct { Url = url, At = Environment.TickCount };
            return url;
        }
        class RangeResult { public byte[] Data; public long Total; }
        static RangeResult AssetRange(string asset, long from, long to)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    var r = Req(DirectUrl(asset, attempt > 0), 30000); r.AddRange(from, to);
                    using (var resp = (HttpWebResponse)r.GetResponse()) using (var s = resp.GetResponseStream()) using (var ms = new MemoryStream())
                    {
                        // Server ignoriert den Teilbereich -> nicht die ganze Datei laden, sondern neue Adresse holen
                        if (resp.StatusCode != HttpStatusCode.PartialContent && (from > 0 || resp.ContentLength > to - from + 1)) throw new WebException("range");
                        s.CopyTo(ms); long t = 0; var cr = resp.Headers["Content-Range"];
                        if (cr != null && cr.Contains("/")) long.TryParse(cr.Substring(cr.LastIndexOf('/') + 1), out t);
                        return new RangeResult { Data = ms.ToArray(), Total = t };
                    }
                }
                catch (WebException) { if (attempt >= 2) throw; System.Threading.Thread.Sleep(400 * (attempt + 1)); }
            }
        }
        static byte[] Range(string url, long from, long to)
        {
            var r = Req(url, 20000); r.AddRange(from, to);
            using (var resp = r.GetResponse()) using (var s = resp.GetResponseStream()) using (var ms = new MemoryStream()) { s.CopyTo(ms); return ms.ToArray(); }
        }

        // Katalog für den Start ohne Internet zwischenspeichern
        public static void SaveCache(List<RPack> list, string file)
        {
            var lines = list.Select(p => string.Join("|", new[] { p.Name, p.File, p.Bytes.ToString(CultureInfo.InvariantCulture), p.Sha256 ?? "", p.Stamp ?? "", p.Preview ?? "", string.Join(";", p.Counts.Select(kv => kv.Key + "=" + kv.Value)), p.Thumbs ?? "" }));
            System.IO.File.WriteAllLines(file, lines.ToArray());
        }
        public static List<RPack> LoadCache(string file)
        {
            var list = new List<RPack>();
            foreach (var l in System.IO.File.ReadAllLines(file))
            {
                var f = l.Split('|'); if (f.Length < 7) continue;
                var p = new RPack { Name = f[0], File = f[1], Bytes = long.Parse(f[2], CultureInfo.InvariantCulture), Sha256 = f[3] == "" ? null : f[3], Stamp = f[4], Preview = f[5] == "" ? null : f[5] };
                foreach (var kv in f[6].Split(';')) { var a = kv.Split('='); int n; if (a.Length == 2 && int.TryParse(a[1], out n)) p.Counts[a[0]] = n; }
                if (f.Length > 7 && f[7] != "") p.Thumbs = f[7];
                p.Icons = p.Counts.Values.Sum(); list.Add(p);
            }
            return list;
        }
        public static List<RPack> ParseCatalog(string json)
        {
            var list = new List<RPack>();
            var root = new Json(json).Value() as Dictionary<string, object>;
            if (root == null || !root.ContainsKey("packs")) return list;
            foreach (var o in (root["packs"] as List<object>) ?? new List<object>())
            {
                var d = o as Dictionary<string, object>; if (d == null) continue;
                var p = new RPack { Name = S(d, "name"), File = S(d, "file"), Sha256 = S(d, "sha256"), Preview = S(d, "preview"), Bytes = (long)N(d, "bytes"), Icons = (int)N(d, "icons"), Version = Math.Max(1, (int)N(d, "version")) };
                var c = d.ContainsKey("counts") ? d["counts"] as Dictionary<string, object> : null;
                if (c != null) foreach (var kv in c) if (kv.Value is double) p.Counts[kv.Key] = (int)(double)kv.Value;
                if (!string.IsNullOrEmpty(p.Name) && !string.IsNullOrEmpty(p.File) && p.Name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && !p.Name.Contains("..")) list.Add(p);
            }
            return list;
        }
        static string S(Dictionary<string, object> d, string k) { object v; return d.TryGetValue(k, out v) ? v as string : null; }
        static double N(Dictionary<string, object> d, string k) { object v; return d.TryGetValue(k, out v) && v is double ? (double)v : 0; }

        // ---------- Vorschaubild (zwischengespeichert) ----------
        public static string PreviewFile(RPack p, string cacheDir) { var st = (p.Stamp ?? "v" + p.Version); return Path.Combine(cacheDir, p.Name + "-" + (st.Length > 12 ? st.Substring(0, 12) : st.Replace(':', '_').Replace('/', '_')) + ".jpg"); }
        // alle fehlenden Vorschaubilder gleichzeitig herunterladen
        public static void FetchPreviews(List<RPack> list, string cacheDir)
        {
            var ths = list.Select(p => new System.Threading.Thread(() => { var im = LoadPreview(p, cacheDir, true); if (im != null) im.Dispose(); }) { IsBackground = true }).ToList();
            ths.ForEach(t => t.Start()); ths.ForEach(t => t.Join(20000));
        }
        public static Image LoadPreview(RPack p, string cacheDir, bool allowDownload)
        {
            try
            {
                var f = PreviewFile(p, cacheDir);
                if (!System.IO.File.Exists(f) && allowDownload && !string.IsNullOrEmpty(p.Preview))
                {
                    Directory.CreateDirectory(cacheDir);
                    var tmp = f + ".part";
                    using (var resp = Req(Base + p.Preview, 15000).GetResponse()) using (var s = resp.GetResponseStream()) using (var o = System.IO.File.Create(tmp)) s.CopyTo(o);
                    if (System.IO.File.Exists(f)) System.IO.File.Delete(f);
                    System.IO.File.Move(tmp, f);
                }
                if (System.IO.File.Exists(f)) using (var im = Image.FromFile(f)) return new Bitmap(im);
            }
            catch { }
            return null;
        }

        // ---------- Herunterladen + prüfen + installieren ----------
        // progress(prozent 0–100, text); Pack landet in packsDir\<Name>, Version in packsDir\<Name>\pack.version
        public static void Install(RPack p, string packsDir, Action<int, string> progress)
        {
            Directory.CreateDirectory(packsDir);
            var stage = Path.Combine(packsDir, "_download");
            Directory.CreateDirectory(stage);
            var zip = Path.Combine(stage, p.Name + ".zip");
            try
            {
                Download(p, zip, progress);
                var pf = PayloadFile.Load(zip, 0);
                if (pf == null || !pf.Packs.Any(x => x.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase))) throw new IOException(L.T("Die heruntergeladene Datei ist beschädigt – bitte erneut versuchen."));
                var unpack = Path.Combine(stage, "unpack");
                if (Directory.Exists(unpack)) Directory.Delete(unpack, true);
                pf.Extract(p.Name, unpack, (a, b) => { if (progress != null) progress(85 + a * 14 / Math.Max(1, b), L.F("„{0}“ wird entpackt … {1} / {2}", p.Name, a, b)); });
                var src = Path.Combine(unpack, p.Name); var target = Path.Combine(packsDir, p.Name);
                if (Directory.Exists(target)) Directory.Delete(target, true);
                Directory.Move(src, target);
                System.IO.File.WriteAllText(Path.Combine(target, "pack.version"), p.Stamp ?? p.Version.ToString(CultureInfo.InvariantCulture));
                if (progress != null) progress(100, null);
            }
            finally { try { Directory.Delete(stage, true); } catch { } }
        }

        public static void Download(RPack p, string file, Action<int, string> progress)
        {
            Exception last = null;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try { DownloadOnce(p, file, progress); return; }
                catch (WebException ex) { last = ex; }
                catch (IOException ex) { last = ex; }
                System.Threading.Thread.Sleep(1500 * (attempt + 1));
            }
            if (last is WebException) throw new IOException(L.T("Download fehlgeschlagen – bitte prüfe deine Internetverbindung und versuche es erneut.") + "\n\n" + last.Message);
            throw last;
        }

        static void DownloadOnce(RPack p, string file, Action<int, string> progress)
        {
            if (p.Bytes > 8 * 1048576)
            {
                try { DownloadParallel(p, file, progress); return; }
                catch (NotSupportedException) { }   // Server kann keine Teilstücke -> normaler Download
            }
            DownloadSingle(p, file, progress);
        }
        static string Speed(long bytes, int ms) { return ms < 500 ? "" : " · " + (bytes / 1048576.0 / (ms / 1000.0)).ToString("0.0") + " MB/s"; }

        // in mehreren Teilen gleichzeitig laden (Heimanschlüsse werden pro Verbindung oft gedrosselt)
        static void DownloadParallel(RPack p, string file, Action<int, string> progress)
        {
            long total = p.Bytes; const int parts = 6; long chunk = (total + parts - 1) / parts;
            var done = new long[parts]; Exception err = null; bool rangeFail = false; int t0 = Environment.TickCount;
            using (var f = System.IO.File.Create(file)) f.SetLength(total);
            var ths = new List<System.Threading.Thread>();
            for (int i = 0; i < parts; i++)
            {
                int idx = i; long from = i * chunk, to = Math.Min(total, from + chunk) - 1;
                var th = new System.Threading.Thread(() =>
                {
                    for (int attempt = 0; attempt < 4 && err == null; attempt++)
                    {
                        try
                        {
                            long pos = from + System.Threading.Interlocked.Read(ref done[idx]); if (pos > to) return;
                            var rq = Req(p.Url ?? Base + p.File, 30000); rq.AddRange(pos, to);
                            using (var resp = (HttpWebResponse)rq.GetResponse())
                            {
                                if (resp.StatusCode != HttpStatusCode.PartialContent) { rangeFail = true; err = new NotSupportedException(); return; }
                                using (var s = resp.GetResponseStream()) using (var o = new FileStream(file, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
                                {
                                    o.Seek(pos, SeekOrigin.Begin); var buf = new byte[1 << 16]; int r;
                                    while (err == null && (r = s.Read(buf, 0, buf.Length)) > 0) { o.Write(buf, 0, r); System.Threading.Interlocked.Add(ref done[idx], r); }
                                }
                            }
                            if (from + System.Threading.Interlocked.Read(ref done[idx]) > to) return;
                        }
                        catch (Exception ex) { if (attempt == 3) err = ex; else System.Threading.Thread.Sleep(1000 * (attempt + 1)); }
                    }
                }) { IsBackground = true };
                ths.Add(th); th.Start();
            }
            while (ths.Any(t => t.IsAlive))
            {
                System.Threading.Thread.Sleep(150);
                long sum = done.Sum();
                if (progress != null) progress((int)(85 * sum / Math.Max(1, total)), L.F("„{0}“ wird heruntergeladen … {1} / {2} MB", p.Name, (sum / 1048576).ToString("N0"), (total / 1048576).ToString("N0")) + Speed(sum, Environment.TickCount - t0));
            }
            if (rangeFail) throw new NotSupportedException();
            if (err != null) throw err is WebException || err is IOException ? err : new IOException(err.Message, err);
            if (done.Sum() != total) throw new IOException(L.T("Die heruntergeladene Datei ist beschädigt – bitte erneut versuchen."));
            if (!string.IsNullOrEmpty(p.Sha256))
            {
                if (progress != null) progress(85, L.F("„{0}“ wird heruntergeladen … {1} / {2} MB", p.Name, (total / 1048576).ToString("N0"), (total / 1048576).ToString("N0")));
                string hex; using (var sha = SHA256.Create()) using (var f = System.IO.File.OpenRead(file)) hex = BitConverter.ToString(sha.ComputeHash(f)).Replace("-", "").ToLowerInvariant();
                if (!hex.Equals(p.Sha256, StringComparison.OrdinalIgnoreCase)) throw new IOException(L.T("Die heruntergeladene Datei ist beschädigt – bitte erneut versuchen."));
            }
        }

        static void DownloadSingle(RPack p, string file, Action<int, string> progress)
        {
            using (var resp = Req(p.Url ?? Base + p.File, 30000).GetResponse())
            using (var s = resp.GetResponseStream())
            using (var o = System.IO.File.Create(file))
            using (var sha = SHA256.Create())
            {
                long total = resp.ContentLength > 0 ? resp.ContentLength : p.Bytes, done = 0; var buf = new byte[1 << 16]; int r, lastTick = 0, t0 = Environment.TickCount;
                while ((r = s.Read(buf, 0, buf.Length)) > 0)
                {
                    o.Write(buf, 0, r); sha.TransformBlock(buf, 0, r, null, 0); done += r;
                    if (progress != null && Environment.TickCount - lastTick > 150)
                    {
                        lastTick = Environment.TickCount;
                        progress((int)(85 * done / Math.Max(1, total)), L.F("„{0}“ wird heruntergeladen … {1} / {2} MB", p.Name, (done / 1048576).ToString("N0"), (total / 1048576).ToString("N0")) + Speed(done, Environment.TickCount - t0));
                    }
                }
                sha.TransformFinalBlock(buf, 0, 0);
                var hex = BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
                if (!string.IsNullOrEmpty(p.Sha256) && !hex.Equals(p.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new IOException(L.T("Die heruntergeladene Datei ist beschädigt – bitte erneut versuchen."));
            }
        }

        // true, wenn das installierte Pack älter ist als das im Release
        public static bool NeedsUpdate(string packDir, RPack r)
        {
            try
            {
                var f = Path.Combine(packDir, "pack.version");
                if (string.IsNullOrEmpty(r.Stamp) || !System.IO.File.Exists(f)) return false;
                var cur = System.IO.File.ReadAllText(f).Trim(); int dummy;
                if (int.TryParse(cur, out dummy)) { System.IO.File.WriteAllText(f, r.Stamp); return false; }   // ältere Installation: als aktuell übernehmen
                return !cur.Equals(r.Stamp, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        // ---------- kleiner JSON-Leser ----------
        class Json
        {
            readonly string s; int i;
            public Json(string text) { s = text ?? ""; }
            void Ws() { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
            public object Value()
            {
                Ws(); if (i >= s.Length) return null;
                char c = s[i];
                if (c == '{')
                {
                    var d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase); i++; Ws();
                    if (i < s.Length && s[i] == '}') { i++; return d; }
                    while (i < s.Length) { Ws(); var k = Str(); Ws(); i++; d[k] = Value(); Ws(); if (i < s.Length && s[i] == ',') { i++; continue; } i++; break; }
                    return d;
                }
                if (c == '[')
                {
                    var l = new List<object>(); i++; Ws();
                    if (i < s.Length && s[i] == ']') { i++; return l; }
                    while (i < s.Length) { l.Add(Value()); Ws(); if (i < s.Length && s[i] == ',') { i++; continue; } i++; break; }
                    return l;
                }
                if (c == '"') return Str();
                if (s.Substring(i).StartsWith("true")) { i += 4; return true; }
                if (s.Substring(i).StartsWith("false")) { i += 5; return false; }
                if (s.Substring(i).StartsWith("null")) { i += 4; return null; }
                int st = i; while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
                double v; double.TryParse(s.Substring(st, i - st), NumberStyles.Float, CultureInfo.InvariantCulture, out v); return v;
            }
            string Str()
            {
                var sb = new StringBuilder(); i++;
                while (i < s.Length && s[i] != '"')
                {
                    if (s[i] == '\\' && i + 1 < s.Length)
                    {
                        i++; char e = s[i];
                        if (e == 'u' && i + 4 < s.Length) { sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; }
                        else sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e == 'r' ? '\r' : e == 'b' ? '\b' : e == 'f' ? '\f' : e);
                    }
                    else sb.Append(s[i]);
                    i++;
                }
                i++; return sb.ToString();
            }
        }
    }
}
