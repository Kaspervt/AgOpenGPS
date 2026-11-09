using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AgLibrary.Logging;
using AgOpenGPS.Core.Models;
using AgOpenGPS.IO;
using AgOpenGPS.Properties;

namespace AgOpenGPS
{
    // Johan fork: sharing fields between tablets. A package carries, per field, the three files that
    // matter for guidance (Field.txt with the origin, Boundary.txt, TrackLines.txt) as plain text;
    // worked areas, headland and the rest stay on the tablet they were made on. The same package goes
    // onto a USB stick (folder AgOpenGPS-delen) or through the mailbox on the field server (JohanBrievenbus).
    // Nothing is ever written without the receiver looking at it first (FormOntvangen).
    public static class JohanDelen
    {
        public const string StickMap = "AgOpenGPS-delen";

        public class Perceel
        {
            public string Naam;
            public string Field = "", Boundary = "", Tracks = "";
        }

        public class Pakket
        {
            public string Afzender = "";
            public DateTime Tijd = DateTime.Now;
            public List<Perceel> Percelen = new List<Perceel>();
            public string Bron = "";   // where it came from: a file on the stick, or the mailbox
        }

        public static string TabletNaam =>
            string.IsNullOrWhiteSpace(Settings.Default.setDisplay_johanTabletNaam) ? RegistrySettings.vehicleProfileName : Settings.Default.setDisplay_johanTabletNaam;

        // ---- building and (de)serialising ------------------------------------------------------

        public static Pakket Maak(IEnumerable<string> veldMappen)
        {
            var p = new Pakket { Afzender = TabletNaam };
            foreach (string dir in veldMappen)
            {
                var v = new Perceel { Naam = Path.GetFileName(dir) };
                v.Field = LeesOfLeeg(Path.Combine(dir, "Field.txt"));
                v.Boundary = LeesOfLeeg(Path.Combine(dir, "Boundary.txt"));
                v.Tracks = LeesOfLeeg(Path.Combine(dir, "TrackLines.txt"));
                if (v.Field.Length > 0) p.Percelen.Add(v);
            }
            return p;
        }

        private static string LeesOfLeeg(string pad) => File.Exists(pad) ? File.ReadAllText(pad) : "";

        // Plain text, line based, like the AgOpenGPS files themselves: no JSON library needed
        public static string NaarTekst(Pakket p)
        {
            var sb = new StringBuilder();
            sb.AppendLine("$JohanDelen 1");
            sb.AppendLine(p.Afzender);
            sb.AppendLine(p.Tijd.ToString("o", CultureInfo.InvariantCulture));
            foreach (var v in p.Percelen)
            {
                sb.AppendLine("$Perceel " + v.Naam);
                Blok(sb, "Field", v.Field);
                Blok(sb, "Boundary", v.Boundary);
                Blok(sb, "Tracks", v.Tracks);
            }
            return sb.ToString();
        }

        private static void Blok(StringBuilder sb, string naam, string inhoud)
        {
            string[] regels = Regels(inhoud);
            sb.AppendLine("$" + naam + " " + regels.Length);
            foreach (string r in regels) sb.AppendLine(r);
        }

        private static string[] Regels(string tekst) =>
            tekst.Length == 0 ? new string[0] : tekst.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');

        public static Pakket VanTekst(string tekst)
        {
            string[] r = Regels(tekst);
            if (r.Length < 3 || !r[0].StartsWith("$JohanDelen")) throw new FormatException("Geen deelpakket");
            var p = new Pakket { Afzender = r[1] };
            DateTime.TryParse(r[2], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out p.Tijd);
            int i = 3;
            Perceel huidig = null;
            while (i < r.Length)
            {
                string kop = r[i++];
                if (kop.StartsWith("$Perceel "))
                {
                    huidig = new Perceel { Naam = kop.Substring(9).Trim() };
                    p.Percelen.Add(huidig);
                }
                else if (kop.StartsWith("$") && huidig != null)
                {
                    string[] delen = kop.Substring(1).Split(' ');
                    int n = int.Parse(delen[1], CultureInfo.InvariantCulture);
                    string inhoud = string.Join("\r\n", r.Skip(i).Take(n)) + (n > 0 ? "\r\n" : "");
                    i += n;
                    if (delen[0] == "Field") huidig.Field = inhoud;
                    else if (delen[0] == "Boundary") huidig.Boundary = inhoud;
                    else if (delen[0] == "Tracks") huidig.Tracks = inhoud;
                }
            }
            return p;
        }

        // ---- the USB stick ---------------------------------------------------------------------

        public static List<DriveInfo> Sticks()
        {
            try { return DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Removable && d.IsReady).ToList(); }
            catch { return new List<DriveInfo>(); }
        }

        public static string NaarStick(Pakket p, DriveInfo stick)
        {
            string map = Path.Combine(stick.RootDirectory.FullName, StickMap);
            Directory.CreateDirectory(map);
            string naam = p.Tijd.ToString("yyyy-MM-dd HHmm", CultureInfo.InvariantCulture) + " " + Veilig(p.Afzender) + ".txt";
            string pad = Path.Combine(map, naam);
            File.WriteAllText(pad, NaarTekst(p), Encoding.UTF8);
            Log.EventWriter("Delen: " + p.Percelen.Count + " fields to stick " + pad);
            return pad;
        }

        // Newest first; unreadable files are skipped
        public static List<Pakket> VanStick(DriveInfo stick)
        {
            var uit = new List<Pakket>();
            string map = Path.Combine(stick.RootDirectory.FullName, StickMap);
            if (!Directory.Exists(map)) return uit;
            foreach (string f in Directory.GetFiles(map, "*.txt").OrderByDescending(f => f))
            {
                try
                {
                    var p = VanTekst(File.ReadAllText(f, Encoding.UTF8));
                    p.Bron = f;
                    uit.Add(p);
                }
                catch (Exception ex) { Log.EventWriter("Delen: skip " + f + ": " + ex.Message); }
            }
            return uit;
        }

        private static string Veilig(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, ' ');
            return s.Trim();
        }

        // ---- comparing with what is on this tablet ---------------------------------------------

        public class LijnVerschil
        {
            public CTrk Nieuw;        // as received, already in the local plane of this tablet
            public CTrk Bestaand;     // the line with the same name here, or null
            public string Status;     // nieuw, gelijk, anders
            public int Keuze;         // 0 = mijne houden / overnemen (when new), 1 = vervangen, 2 = allebei houden
        }

        public class Verschil
        {
            public Perceel Perceel;
            public string Map;            // local field folder
            public bool VeldBestaat;
            public string GrensStatus;    // nieuw, gelijk, anders, geen
            public bool GrensVervangen;   // choice when anders
            public List<LijnVerschil> Lijnen = new List<LijnVerschil>();
            public List<CBoundaryList> NieuweGrens;
            public int AantalNieuw => Lijnen.Count(l => l.Status == "nieuw");
            public int AantalAnders => Lijnen.Count(l => l.Status == "anders");
        }

        public static Verschil Vergelijk(Perceel p, FormGPS mf)
        {
            var v = new Verschil { Perceel = p, Map = Path.Combine(RegistrySettings.fieldsDirectory, p.Naam) };
            v.VeldBestaat = File.Exists(Path.Combine(v.Map, "Field.txt"));

            // the received files go into a scratch folder so the normal loaders can read them
            string tmp = Path.Combine(Path.GetTempPath(), "AgOpenGPS-delen", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmp);
            try
            {
                File.WriteAllText(Path.Combine(tmp, "Field.txt"), p.Field);
                File.WriteAllText(Path.Combine(tmp, "Boundary.txt"), p.Boundary);
                File.WriteAllText(Path.Combine(tmp, "TrackLines.txt"), p.Tracks);
                Wgs84 zenderOrigin = FieldPlaneFiles.LoadOrigin(tmp);
                var nieuweLijnen = p.Tracks.Length > 0 ? TrackFiles.Load(tmp) : new List<CTrk>();
                var nieuweGrens = p.Boundary.Length > 0 ? BoundaryFiles.Load(tmp) : new List<CBoundaryList>();

                List<CTrk> bestaande = new List<CTrk>();
                List<CBoundaryList> grensHier = new List<CBoundaryList>();
                if (v.VeldBestaat)
                {
                    Wgs84 hierOrigin = FieldPlaneFiles.LoadOrigin(v.Map);
                    if (Math.Abs(hierOrigin.Latitude - zenderOrigin.Latitude) > 1e-9 || Math.Abs(hierOrigin.Longitude - zenderOrigin.Longitude) > 1e-9)
                    {
                        // same field, other origin: move everything into the plane of this tablet
                        var van = new LocalPlane(zenderOrigin, mf.AppModel.SharedFieldProperties);
                        var naar = new LocalPlane(hierOrigin, mf.AppModel.SharedFieldProperties);
                        foreach (var t in nieuweLijnen) Verplaats(t, van, naar);
                        foreach (var b in nieuweGrens) b.fenceLine = b.fenceLine.Select(pt => Verplaats(pt, van, naar)).ToList();
                    }
                    try { bestaande = TrackFiles.Load(v.Map); } catch { }
                    try { grensHier = BoundaryFiles.Load(v.Map); } catch { }
                }

                v.NieuweGrens = nieuweGrens;
                if (nieuweGrens.Count == 0 || nieuweGrens[0].fenceLine.Count < 3) v.GrensStatus = "geen";
                else if (grensHier.Count == 0 || grensHier[0].fenceLine.Count < 3) v.GrensStatus = "nieuw";
                else v.GrensStatus = RingenGelijk(grensHier[0].fenceLine, nieuweGrens[0].fenceLine) ? "gelijk" : "anders";

                foreach (var t in nieuweLijnen)
                {
                    var hier = bestaande.FirstOrDefault(b => string.Equals(b.name, t.name, StringComparison.OrdinalIgnoreCase));
                    v.Lijnen.Add(new LijnVerschil
                    {
                        Nieuw = t,
                        Bestaand = hier,
                        Status = hier == null ? "nieuw" : LijnenGelijk(hier, t) ? "gelijk" : "anders",
                        Keuze = 0
                    });
                }
            }
            finally
            {
                try { Directory.Delete(tmp, true); } catch { }
            }
            return v;
        }

        private static vec3 Verplaats(vec3 pt, LocalPlane van, LocalPlane naar)
        {
            var w = van.ConvertGeoCoordToWgs84(new GeoCoord(pt.northing, pt.easting));
            var g = naar.ConvertWgs84ToGeoCoord(w);
            return new vec3(g.Easting, g.Northing, pt.heading);
        }

        private static vec2 Verplaats(vec2 pt, LocalPlane van, LocalPlane naar)
        {
            var w = van.ConvertGeoCoordToWgs84(new GeoCoord(pt.northing, pt.easting));
            var g = naar.ConvertWgs84ToGeoCoord(w);
            return new vec2(g.Easting, g.Northing);
        }

        private static void Verplaats(CTrk t, LocalPlane van, LocalPlane naar)
        {
            t.ptA = Verplaats(t.ptA, van, naar);
            t.ptB = Verplaats(t.ptB, van, naar);
            t.curvePts = t.curvePts.Select(pt => Verplaats(pt, van, naar)).ToList();
        }

        // Two lines are the same when start, end and shape are within 5 cm
        public static bool LijnenGelijk(CTrk a, CTrk b)
        {
            if (a.mode != b.mode) return false;
            if (Afstand(a.ptA, b.ptA) > 0.05 || Afstand(a.ptB, b.ptB) > 0.05) return false;
            if (Math.Abs(a.nudgeDistance - b.nudgeDistance) > 0.01) return false;
            if (a.curvePts.Count != b.curvePts.Count) return false;
            for (int i = 0; i < a.curvePts.Count; i += Math.Max(1, a.curvePts.Count / 50))
            {
                if (Afstand(a.curvePts[i], b.curvePts[i]) > 0.05) return false;
            }
            return true;
        }

        private static bool RingenGelijk(List<vec3> a, List<vec3> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i += Math.Max(1, a.Count / 50))
            {
                if (Afstand(a[i], b[i]) > 0.1) return false;
            }
            return true;
        }

        private static double Afstand(vec2 a, vec2 b) => Math.Sqrt((a.easting - b.easting) * (a.easting - b.easting) + (a.northing - b.northing) * (a.northing - b.northing));
        private static double Afstand(vec3 a, vec3 b) => Math.Sqrt((a.easting - b.easting) * (a.easting - b.easting) + (a.northing - b.northing) * (a.northing - b.northing));

        // How many metres the received line lies from the one here (at point A), for the hint
        public static double VerschilMeters(LijnVerschil l) => l.Bestaand == null ? 0 : Afstand(l.Bestaand.ptA, l.Nieuw.ptA);

        // ---- taking over -------------------------------------------------------------------------

        // Writes the choices into the field folder. Returns a short line for the log and the message.
        public static string Overnemen(Verschil v, FormGPS mf)
        {
            bool open = mf.isJobStarted && string.Equals(mf.currentFieldDirectory, v.Perceel.Naam, StringComparison.OrdinalIgnoreCase);
            int toegevoegd = 0, vervangen = 0;

            if (!v.VeldBestaat)
            {
                // a new field: the three files as sent, plus the empty files AgOpenGPS expects
                Directory.CreateDirectory(v.Map);
                File.WriteAllText(Path.Combine(v.Map, "Field.txt"), v.Perceel.Field);
                if (v.Perceel.Boundary.Length > 0) File.WriteAllText(Path.Combine(v.Map, "Boundary.txt"), v.Perceel.Boundary);
                else BoundaryFiles.CreateEmpty(v.Map);
                var lijnen = v.Lijnen.Select(l => l.Nieuw).ToList();
                TrackFiles.Save(v.Map, lijnen);
                SectionsFiles.CreateEmpty(v.Map);
                ContourFiles.CreateFile(v.Map);
                FlagsFiles.Save(v.Map, new List<CFlag>(0));
                RecPathFiles.CreateEmpty(v.Map);
                try { ElevationFiles.CreateHeader(v.Map, DateTime.Now, FieldPlaneFiles.LoadOrigin(v.Map)); } catch { }
                Log.EventWriter("Delen: new field " + v.Perceel.Naam + " with " + lijnen.Count + " lines");
                return v.Perceel.Naam + ": nieuw perceel met " + lijnen.Count + " lijnen";
            }

            if (open) mf.FileSaveTracks();   // the lines in memory first, so nothing of ours is lost
            var bestaande = new List<CTrk>();
            try { bestaande = TrackFiles.Load(v.Map); } catch { }

            foreach (var l in v.Lijnen)
            {
                if (l.Status == "nieuw") { bestaande.Add(l.Nieuw); toegevoegd++; continue; }
                if (l.Status != "anders") continue;
                if (l.Keuze == 1)
                {
                    int i = bestaande.FindIndex(b => string.Equals(b.name, l.Nieuw.name, StringComparison.OrdinalIgnoreCase));
                    if (i >= 0) bestaande[i] = l.Nieuw; else bestaande.Add(l.Nieuw);
                    vervangen++;
                }
                else if (l.Keuze == 2)
                {
                    l.Nieuw.name = UniekeNaam(bestaande, l.Nieuw.name);
                    bestaande.Add(l.Nieuw);
                    toegevoegd++;
                }
            }
            TrackFiles.Save(v.Map, bestaande);

            bool grens = false;
            if ((v.GrensStatus == "nieuw" || (v.GrensStatus == "anders" && v.GrensVervangen)) && v.NieuweGrens != null && !open)
            {
                BoundaryFiles.Save(v.Map, v.NieuweGrens);
                grens = true;
            }

            if (open) mf.FileLoadTracks();   // back into memory; the driving screen shows the new lines at once
            Log.EventWriter("Delen: field " + v.Perceel.Naam + " +" + toegevoegd + " lines, " + vervangen + " replaced" + (grens ? ", boundary" : ""));
            return v.Perceel.Naam + ": " + toegevoegd + " lijnen erbij, " + vervangen + " vervangen" + (grens ? ", grens overgenomen" : "");
        }

        // "Dwars" exists and both are kept: the received one becomes "Dwars (kopie)", then numbered
        private static string UniekeNaam(List<CTrk> lijnen, string basis)
        {
            string naam = basis + " (kopie)";
            int n = 2;
            while (lijnen.Any(b => string.Equals(b.name, naam, StringComparison.OrdinalIgnoreCase))) naam = basis + " (kopie " + n++ + ")";
            return naam;
        }
    }

    public partial class FormGPS
    {
        private int johanDelenTeller;
        private bool johanDelenBezig;
        private FormOntvangBar johanOntvangBar;

        // "Van de stick" on the field list: the packages on the stick (newest first); one opens at once,
        // more give a short list. Returns true when something was taken over (the list then reloads).
        public bool JohanVanStick(System.Windows.Forms.IWin32Window owner)
        {
            var sticks = JohanDelen.Sticks();
            if (sticks.Count == 0) { TimedMessageBox(2500, "USB-stick", "Geen stick gevonden"); return false; }
            var pakketten = sticks.SelectMany(JohanDelen.VanStick).OrderByDescending(p => p.Tijd).ToList();
            if (pakketten.Count == 0) { TimedMessageBox(3000, "USB-stick", "Geen percelen op de stick (map " + JohanDelen.StickMap + ")"); return false; }

            JohanDelen.Pakket keuze = pakketten[0];
            if (pakketten.Count > 1)
            {
                keuze = null;
                var scr = System.Windows.Forms.Screen.FromControl(this).Bounds;
                using (var f = new System.Windows.Forms.Form { FormBorderStyle = System.Windows.Forms.FormBorderStyle.None, StartPosition = System.Windows.Forms.FormStartPosition.Manual, Bounds = scr, BackColor = AgLibrary.Johan.JohanTheme.Bg, ForeColor = AgLibrary.Johan.JohanTheme.Text, ShowInTaskbar = false })
                {
                    int x = AgLibrary.Johan.JohanTheme.S(56);
                    f.Controls.Add(new System.Windows.Forms.Label { Text = "Wat staat er op de stick?", Font = AgLibrary.Johan.JohanTheme.Font(28F, System.Drawing.FontStyle.Bold), AutoSize = true, Location = new System.Drawing.Point(x, AgLibrary.Johan.JohanTheme.S(36)) });
                    var lijst = new System.Windows.Forms.FlowLayoutPanel { Bounds = new System.Drawing.Rectangle(x, AgLibrary.Johan.JohanTheme.S(120), f.Width - 2 * x, f.Height - AgLibrary.Johan.JohanTheme.S(250)), FlowDirection = System.Windows.Forms.FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
                    f.Controls.Add(lijst);
                    foreach (var p in pakketten)
                    {
                        var pk = p;
                        var b = new JohanRoundButton { Text = p.Tijd.ToString("d MMM HH:mm") + "   van " + p.Afzender + ":  " + string.Join(", ", p.Percelen.Select(v => v.Naam)), Size = new System.Drawing.Size(lijst.Width - AgLibrary.Johan.JohanTheme.S(24), AgLibrary.Johan.JohanTheme.S(72)), Margin = new System.Windows.Forms.Padding(0, 0, 0, AgLibrary.Johan.JohanTheme.S(10)), BackColor = AgLibrary.Johan.JohanTheme.Panel, ForeColor = AgLibrary.Johan.JohanTheme.Text, FlatStyle = System.Windows.Forms.FlatStyle.Flat, Font = AgLibrary.Johan.JohanTheme.Font(15F, System.Drawing.FontStyle.Bold), TextAlign = System.Drawing.ContentAlignment.MiddleLeft, Padding = new System.Windows.Forms.Padding(AgLibrary.Johan.JohanTheme.S(20), 0, 0, 0) };
                        b.FlatAppearance.BorderSize = 0;
                        b.Click += (s, e) => { keuze = pk; f.Close(); };
                        lijst.Controls.Add(b);
                    }
                    var terug = new JohanRoundButton { Text = "Terug", Bounds = new System.Drawing.Rectangle(x, f.Height - AgLibrary.Johan.JohanTheme.S(100), AgLibrary.Johan.JohanTheme.S(200), AgLibrary.Johan.JohanTheme.S(64)), BackColor = AgLibrary.Johan.JohanTheme.Panel, ForeColor = AgLibrary.Johan.JohanTheme.Text, FlatStyle = System.Windows.Forms.FlatStyle.Flat, Font = AgLibrary.Johan.JohanTheme.Font(14F, System.Drawing.FontStyle.Bold) };
                    terug.FlatAppearance.BorderSize = 0;
                    terug.Click += (s, e) => f.Close();
                    f.Controls.Add(terug);
                    f.ShowDialog(owner);
                }
                if (keuze == null) return false;
            }
            using (var f = new FormOntvangen(this, keuze))
            {
                f.ShowDialog(owner);
                return f.Overgenomen;
            }
        }

        // Test aid: show a form without activating it, draw it to a PNG after 2,5 s and close it.
        // DrawToBitmap goes through WM_PRINT, so this also works when the screen is locked.
        public void JohanSnapshot(System.Windows.Forms.Form f, string png)
        {
            f.Show(this);
            var t = new System.Windows.Forms.Timer { Interval = 2500 };
            t.Tick += (s, e) =>
            {
                t.Stop();
                try
                {
                    using (var bmp = new System.Drawing.Bitmap(f.Width, f.Height))
                    {
                        f.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, f.Width, f.Height));
                        bmp.Save(png, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    Log.EventWriter("Snapshot written: " + png);
                }
                catch (Exception ex) { Log.EventWriter("Snapshot failed: " + ex.Message); }
                f.Close();
                f.Dispose();
            };
            t.Start();
        }

        // Every 30 s (the driver tick runs twice a second): tell the mailbox we are here and look for a
        // package. A package shows the question bar once; nothing is written until the driver chooses.
        public async void JohanDelenPoll()
        {
            if (!Settings.Default.setDisplay_johanDelenOntvangen || !JohanBrievenbus.Ingesteld) return;
            if (++johanDelenTeller < 60) return;
            johanDelenTeller = 0;
            if (johanDelenBezig || johanOntvangBar != null) return;
            johanDelenBezig = true;
            try
            {
                await JohanBrievenbus.Melden();
                var p = await JohanBrievenbus.Ophalen();
                if (p == null || IsDisposed) return;
                Log.EventWriter("Delen: package from " + p.Afzender + " with " + p.Percelen.Count + " fields");
                johanOntvangBar = new FormOntvangBar(this, p, async bekeken =>
                {
                    johanOntvangBar = null;
                    await JohanBrievenbus.Klaar(p);   // seen (taken over or refused): out of the mailbox
                });
                johanOntvangBar.Show(this);
            }
            catch (Exception ex) { Log.EventWriter("Delen: poll: " + ex.Message); }
            finally { johanDelenBezig = false; }
        }
    }

    // The mailbox on the field server. Everything is one plain text request; tablets on 4G cannot
    // reach each other directly, so the server keeps a package for the receiver (a week at most).
    //   GET  {url}/{groep}/tablets            -> one tablet name per line, with the time last seen
    //   POST {url}/{groep}/{tablet}/melden     -> "I am here" (body empty)
    //   POST {url}/{groep}/{naar}              -> body = package text; stored for that tablet
    //   GET  {url}/{groep}/{tablet}/ophalen    -> the oldest waiting package as text, 204 when none
    //   POST {url}/{groep}/{tablet}/klaar      -> body = the package id from the X-Pakket header; removes it
    public static class JohanBrievenbus
    {
        private static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };

        public static bool Ingesteld =>
            !string.IsNullOrWhiteSpace(Settings.Default.setDisplay_johanBrievenbus) && !string.IsNullOrWhiteSpace(Settings.Default.setDisplay_johanDeelGroep);

        private static string Basis => Settings.Default.setDisplay_johanBrievenbus.TrimEnd('/') + "/" + Uri.EscapeDataString(Settings.Default.setDisplay_johanDeelGroep.Trim());

        public static string LaatsteFout = "";

        public static async Task<List<(string naam, string gezien)>> Tablets()
        {
            var uit = new List<(string, string)>();
            if (!Ingesteld) return uit;
            try
            {
                string tekst = await http.GetStringAsync(Basis + "/tablets");
                foreach (string r in tekst.Replace("\r", "").Split('\n'))
                {
                    if (r.Trim().Length == 0) continue;
                    int tab = r.IndexOf('\t');
                    string naam = tab < 0 ? r : r.Substring(0, tab);
                    if (naam == JohanDelen.TabletNaam) continue;
                    uit.Add((naam, tab < 0 ? "" : r.Substring(tab + 1)));
                }
                LaatsteFout = "";
            }
            catch (Exception ex) { LaatsteFout = ex.Message; }
            return uit;
        }

        public static async Task<bool> Melden()
        {
            if (!Ingesteld) return false;
            try
            {
                var r = await http.PostAsync(Basis + "/" + Uri.EscapeDataString(JohanDelen.TabletNaam) + "/melden", new StringContent(""));
                LaatsteFout = r.IsSuccessStatusCode ? "" : "Brievenbus antwoordt " + (int)r.StatusCode;
                return r.IsSuccessStatusCode;
            }
            catch (Exception ex) { LaatsteFout = ex.Message; return false; }
        }

        public static async Task<bool> Verstuur(JohanDelen.Pakket p, string naarTablet)
        {
            if (!Ingesteld) return false;
            try
            {
                var r = await http.PostAsync(Basis + "/" + Uri.EscapeDataString(naarTablet), new StringContent(JohanDelen.NaarTekst(p), Encoding.UTF8, "text/plain"));
                LaatsteFout = r.IsSuccessStatusCode ? "" : "Brievenbus antwoordt " + (int)r.StatusCode;
                Log.EventWriter("Delen: sent " + p.Percelen.Count + " fields to " + naarTablet + " -> " + (int)r.StatusCode);
                return r.IsSuccessStatusCode;
            }
            catch (Exception ex) { LaatsteFout = ex.Message; Log.EventWriter("Delen: send failed: " + ex.Message); return false; }
        }

        // The oldest waiting package, or null. Bron holds the package id for Klaar().
        public static async Task<JohanDelen.Pakket> Ophalen()
        {
            if (!Ingesteld) return null;
            try
            {
                var r = await http.GetAsync(Basis + "/" + Uri.EscapeDataString(JohanDelen.TabletNaam) + "/ophalen");
                if (r.StatusCode == System.Net.HttpStatusCode.NoContent) { LaatsteFout = ""; return null; }
                if (!r.IsSuccessStatusCode) { LaatsteFout = "Brievenbus antwoordt " + (int)r.StatusCode; return null; }
                var p = JohanDelen.VanTekst(await r.Content.ReadAsStringAsync());
                p.Bron = r.Headers.TryGetValues("X-Pakket", out var ids) ? ids.FirstOrDefault() ?? "" : "";
                LaatsteFout = "";
                return p;
            }
            catch (Exception ex) { LaatsteFout = ex.Message; return null; }
        }

        public static async Task Klaar(JohanDelen.Pakket p)
        {
            if (!Ingesteld || string.IsNullOrEmpty(p.Bron)) return;
            try { await http.PostAsync(Basis + "/" + Uri.EscapeDataString(JohanDelen.TabletNaam) + "/klaar", new StringContent(p.Bron)); }
            catch (Exception ex) { LaatsteFout = ex.Message; }
        }
    }
}
