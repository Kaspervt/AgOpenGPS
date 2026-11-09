using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AgLibrary.Johan;
using AgLibrary.Logging;
using AgOpenGPS.IO;
using AgOpenGPS.Properties;

namespace AgOpenGPS
{
    // Johan fork: "Percelen delen" (canvas board 16, 9-11). Left: the fields of this tablet, several
    // can be ticked. Right: where they go, a USB stick in the tablet or another tablet of the farm
    // through the mailbox. Only boundary and lines travel; the receiver always gets a question first.
    public class FormDelen : Form
    {
        private readonly FormGPS mf;
        private readonly HashSet<string> gekozen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<JohanRoundButton> veldKnoppen = new List<JohanRoundButton>();
        private FlowLayoutPanel doelen;
        private Label lblStatus;
        private static int S(int px) => JohanTheme.S(px);

        public FormDelen(FormGPS formGPS)
        {
            mf = formGPS;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = Screen.FromControl(mf).Bounds;
            BackColor = JohanTheme.Bg;
            ForeColor = JohanTheme.Text;
            Font = JohanTheme.Font(14F);
            KeyPreview = true;
            ShowInTaskbar = false;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
            if (mf.isJobStarted) gekozen.Add(mf.currentFieldDirectory);   // the open field is the likely one
            BuildLayout();
            Load += async (s, e) => await LaadTablets();
        }

        private void BuildLayout()
        {
            int w = Width, h = Height, x = S(56), gap = S(24);
            int kolom = (w - 2 * x - gap) / 2;
            Controls.Add(new Label { Text = "Percelen delen", Font = JohanTheme.Font(28F, FontStyle.Bold), AutoSize = true, Location = new Point(x, S(36)) });
            Controls.Add(new Label { Text = "Grens en lijnen gaan mee. Bewerkte vlakken en de kopakker blijven op deze tablet. De ander krijgt eerst een vraag.", Font = JohanTheme.Font(11F), ForeColor = JohanTheme.Muted, AutoSize = true, Location = new Point(x + S(2), S(92)) });

            // left: the fields
            var links = Kaart(new Rectangle(x, S(150), kolom, h - S(150) - S(130)), "Welke percelen", "Tik om aan of uit te zetten");
            var lijst = new FlowLayoutPanel { Bounds = new Rectangle(S(16), S(84), kolom - S(32), links.Height - S(100)), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = JohanTheme.Panel };
            links.Controls.Add(lijst);
            foreach (string dir in Directory.Exists(RegistrySettings.fieldsDirectory) ? Directory.GetDirectories(RegistrySettings.fieldsDirectory).OrderBy(d => d).ToArray() : new string[0])
            {
                if (!File.Exists(Path.Combine(dir, "Field.txt"))) continue;
                string naam = Path.GetFileName(dir);
                int lijnen = 0; bool grens = false;
                try { lijnen = TrackFiles.Load(dir).Count; } catch { }
                try { var b = BoundaryFiles.Load(dir); grens = b.Count > 0 && b[0].fenceLine.Count > 2; } catch { }
                string detail = (grens ? "grens" : "geen grens") + ", " + (lijnen == 0 ? "geen lijnen" : lijnen == 1 ? "1 lijn" : lijnen + " lijnen");
                var k = new JohanRoundButton
                {
                    Size = new Size(lijst.Width - S(24), S(64)),
                    Margin = new Padding(0, 0, 0, S(8)),
                    FlatStyle = FlatStyle.Flat,
                    Tag = naam,
                    Text = "",
                    BackColor = JohanTheme.Bg
                };
                k.FlatAppearance.BorderSize = 0;
                var vink = new Label { Bounds = new Rectangle(S(14), S(14), S(36), S(36)), TextAlign = ContentAlignment.MiddleCenter, Font = JohanTheme.Font(16F, FontStyle.Bold), ForeColor = Color.White, BackColor = JohanTheme.PanelLight, Tag = "vink" };
                k.Controls.Add(vink);
                k.Controls.Add(new Label { Text = naam, Font = JohanTheme.Font(15F, FontStyle.Bold), AutoSize = true, Location = new Point(S(64), S(8)), BackColor = Color.Transparent, ForeColor = JohanTheme.Text });
                k.Controls.Add(new Label { Text = detail, Font = JohanTheme.Font(10F), AutoSize = true, Location = new Point(S(64), S(36)), BackColor = Color.Transparent, ForeColor = JohanTheme.Muted });
                foreach (Control c in k.Controls) c.Click += (s, e) => Wissel(naam);
                k.Click += (s, e) => Wissel(naam);
                veldKnoppen.Add(k);
                lijst.Controls.Add(k);
            }
            ToonKeuze();

            // right: the destinations
            var rechts = Kaart(new Rectangle(x + kolom + gap, S(150), kolom, h - S(150) - S(130)), "Waarheen", "Een stick in de tablet, of een tablet van jullie met internet");
            doelen = new FlowLayoutPanel { Bounds = new Rectangle(S(16), S(84), kolom - S(32), rechts.Height - S(100)), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = JohanTheme.Panel };
            rechts.Controls.Add(doelen);
            VulDoelen(null, "zoeken…");

            lblStatus = new Label { Font = JohanTheme.Font(12F), ForeColor = JohanTheme.Muted, AutoSize = false, Bounds = new Rectangle(x + S(220), h - S(96), w - 2 * x - S(240), S(56)), TextAlign = ContentAlignment.MiddleLeft, Text = "Van de stick halen: perceellijst, knop Van de stick" };
            Controls.Add(lblStatus);

            var terug = new JohanRoundButton { Text = "Terug", Bounds = new Rectangle(x, h - S(100), S(200), S(64)), BackColor = JohanTheme.Panel, ForeColor = JohanTheme.Text, FlatStyle = FlatStyle.Flat, Font = JohanTheme.Font(14F, FontStyle.Bold) };
            terug.FlatAppearance.BorderSize = 0;
            terug.Click += (s, e) => Close();
            Controls.Add(terug);
        }

        private Panel Kaart(Rectangle r, string kop, string sub)
        {
            var p = new Panel { Bounds = r, BackColor = JohanTheme.Panel };
            p.Paint += (s, e) =>
            {
                using (var path = JohanRoundButton.Rounded(new Rectangle(0, 0, p.Width - 1, p.Height - 1), S(16)))
                using (var b = new SolidBrush(JohanTheme.Panel))
                {
                    e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    e.Graphics.Clear(JohanTheme.Bg);
                    e.Graphics.FillPath(b, path);
                }
            };
            p.Controls.Add(new Label { Text = kop, Font = JohanTheme.Font(18F, FontStyle.Bold), AutoSize = true, Location = new Point(S(20), S(16)), BackColor = Color.Transparent });
            p.Controls.Add(new Label { Text = sub, Font = JohanTheme.Font(10.5F), ForeColor = JohanTheme.Muted, AutoSize = true, Location = new Point(S(22), S(52)), BackColor = Color.Transparent });
            Controls.Add(p);
            return p;
        }

        private void Wissel(string naam)
        {
            if (!gekozen.Remove(naam)) gekozen.Add(naam);
            ToonKeuze();
        }

        private void ToonKeuze()
        {
            foreach (var k in veldKnoppen)
            {
                bool aan = gekozen.Contains((string)k.Tag);
                var vink = k.Controls.OfType<Label>().First(l => (string)l.Tag == "vink");
                vink.Text = aan ? "✓" : "";
                vink.BackColor = aan ? JohanTheme.Green : JohanTheme.PanelLight;
                k.BackColor = aan ? (JohanTheme.IsLight ? Color.FromArgb(220, 252, 231) : Color.FromArgb(20, 60, 40)) : JohanTheme.Bg;
            }
        }

        // The destination rows: sticks now in the tablet, then the tablets the mailbox knows
        private void VulDoelen(List<(string naam, string gezien)> tablets, string tabletsStatus)
        {
            doelen.SuspendLayout();
            foreach (Control c in doelen.Controls.OfType<Control>().ToArray()) { doelen.Controls.Remove(c); c.Dispose(); }
            var sticks = JohanDelen.Sticks();
            if (sticks.Count == 0) Doel("USB-stick", "Geen stick gevonden. Steek hem erin en tik hier.", JohanTheme.Grey, "Opnieuw zoeken", () => VulDoelen(tablets, tabletsStatus), false);
            foreach (var st in sticks)
            {
                var stick = st;
                string label = (string.IsNullOrEmpty(stick.VolumeLabel) ? "USB-stick" : stick.VolumeLabel) + " (" + stick.Name.TrimEnd('\\') + ")";
                Doel(label, "Map " + JohanDelen.StickMap, JohanTheme.Green, "Naar de stick", () => NaarStick(stick), true);
            }
            if (!JohanBrievenbus.Ingesteld)
            {
                Doel("Andere tablets", "Vul eerst de groepscode in bij Instellingen, Verbindingen", JohanTheme.Grey, null, null, false);
            }
            else if (tablets == null)
            {
                Doel("Andere tablets", tabletsStatus, JohanTheme.Grey, null, null, false);
            }
            else if (tablets.Count == 0)
            {
                Doel("Andere tablets", string.IsNullOrEmpty(JohanBrievenbus.LaatsteFout) ? "Geen andere tablet in groep " + Settings.Default.setDisplay_johanDeelGroep : "Brievenbus niet bereikbaar: " + JohanBrievenbus.LaatsteFout, JohanTheme.Amber, "Opnieuw", async () => await LaadTablets(), false);
            }
            else
            {
                foreach (var t in tablets)
                {
                    var tablet = t;
                    bool online = tablet.gezien.StartsWith("0") || tablet.gezien.Contains("min");
                    Doel(tablet.naam, string.IsNullOrEmpty(tablet.gezien) ? "" : "Laatst gezien " + tablet.gezien, online ? JohanTheme.Green : JohanTheme.Grey, "Versturen", async () => await Versturen(tablet.naam), true);
                }
            }
            doelen.ResumeLayout();
        }

        private void Doel(string kop, string sub, Color stip, string knopTekst, Action actie, bool blauw)
        {
            var rij = new Panel { Size = new Size(doelen.Width - S(24), S(72)), Margin = new Padding(0, 0, 0, S(8)), BackColor = JohanTheme.Bg };
            rij.Paint += (s, e) => { e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; using (var b = new SolidBrush(stip)) e.Graphics.FillEllipse(b, S(16), S(29), S(14), S(14)); };
            rij.Controls.Add(new Label { Text = kop, Font = JohanTheme.Font(15F, FontStyle.Bold), AutoSize = true, Location = new Point(S(44), S(10)), BackColor = Color.Transparent, ForeColor = JohanTheme.Text });
            rij.Controls.Add(new Label { Text = sub, Font = JohanTheme.Font(10F), AutoSize = false, Bounds = new Rectangle(S(44), S(40), rij.Width - S(260), S(24)), BackColor = Color.Transparent, ForeColor = JohanTheme.Muted });
            if (knopTekst != null)
            {
                int bw = TextRenderer.MeasureText(knopTekst, JohanTheme.Font(13F, FontStyle.Bold)).Width + S(36);
                var k = new JohanRoundButton { Text = knopTekst, Bounds = new Rectangle(rij.Width - bw - S(12), S(12), bw, S(48)), BackColor = blauw ? JohanTheme.Blue : JohanTheme.PanelLight, ForeColor = blauw ? Color.White : JohanTheme.Text, FlatStyle = FlatStyle.Flat, Font = JohanTheme.Font(13F, FontStyle.Bold), Radius = S(10) };
                k.FlatAppearance.BorderSize = 0;
                k.Click += (s, e) => actie();
                rij.Controls.Add(k);
            }
            doelen.Controls.Add(rij);
        }

        private async System.Threading.Tasks.Task LaadTablets()
        {
            if (!JohanBrievenbus.Ingesteld) return;
            VulDoelen(null, "Zoeken in de brievenbus…");
            await JohanBrievenbus.Melden();
            var tablets = await JohanBrievenbus.Tablets();
            if (!IsDisposed) VulDoelen(tablets, "");
        }

        private JohanDelen.Pakket Pakket()
        {
            if (gekozen.Count == 0) { Status("Kies eerst een of meer percelen", JohanTheme.Amber); return null; }
            return JohanDelen.Maak(gekozen.Select(n => Path.Combine(RegistrySettings.fieldsDirectory, n)));
        }

        private void NaarStick(DriveInfo stick)
        {
            var p = Pakket();
            if (p == null) return;
            try
            {
                string pad = JohanDelen.NaarStick(p, stick);
                Status("Op de stick gezet: " + Tel(p) + " (" + Path.GetFileName(pad) + ")", JohanTheme.Green);
            }
            catch (Exception ex)
            {
                Log.EventWriter("Delen: stick failed: " + ex.Message);
                Status("Schrijven naar de stick mislukt: " + ex.Message, JohanTheme.Red);
            }
        }

        private async System.Threading.Tasks.Task Versturen(string tablet)
        {
            var p = Pakket();
            if (p == null) return;
            Status("Versturen naar " + tablet + "…", JohanTheme.Muted);
            bool ok = await JohanBrievenbus.Verstuur(p, tablet);
            if (IsDisposed) return;
            Status(ok ? "Verstuurd naar " + tablet + ": " + Tel(p) + ". Die tablet krijgt een vraag zodra hij internet heeft." : "Versturen mislukt: " + JohanBrievenbus.LaatsteFout, ok ? JohanTheme.Green : JohanTheme.Red);
        }

        private static string Tel(JohanDelen.Pakket p) => p.Percelen.Count == 1 ? p.Percelen[0].Naam : p.Percelen.Count + " percelen";

        private void Status(string tekst, Color kleur)
        {
            lblStatus.Text = tekst;
            lblStatus.ForeColor = kleur;
        }
    }
}
