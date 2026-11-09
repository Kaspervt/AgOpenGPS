using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AgLibrary.Johan;
using AgLibrary.Logging;

namespace AgOpenGPS
{
    // Johan fork: "Ontvangen" (canvas board 17, 9-11). One package, from the stick or the mailbox,
    // laid out per field: the boundary and every line with what it means here (nieuw, gelijk, anders)
    // and, where it differs, the choice mijne houden / vervangen / allebei. Nothing is written until
    // Overnemen. Used for the stick and for the mailbox alike.
    public class FormOntvangen : Form
    {
        private readonly FormGPS mf;
        private readonly JohanDelen.Pakket pakket;
        private readonly List<JohanDelen.Verschil> verschillen = new List<JohanDelen.Verschil>();
        private readonly HashSet<string> overnemen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Action> verversers = new List<Action>();
        private Label lblStatus;
        private static int S(int px) => JohanTheme.S(px);

        public bool Overgenomen { get; private set; }

        public FormOntvangen(FormGPS formGPS, JohanDelen.Pakket p)
        {
            mf = formGPS;
            pakket = p;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = Screen.FromControl(mf).Bounds;
            BackColor = JohanTheme.Bg;
            ForeColor = JohanTheme.Text;
            Font = JohanTheme.Font(14F);
            KeyPreview = true;
            ShowInTaskbar = false;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
            foreach (var v in p.Percelen)
            {
                try { verschillen.Add(JohanDelen.Vergelijk(v, mf)); overnemen.Add(v.Naam); }
                catch (Exception ex) { Log.EventWriter("Delen: compare " + v.Naam + ": " + ex.Message); }
            }
            BuildLayout();
            Ververs();
        }

        private void BuildLayout()
        {
            int w = Width, h = Height, x = S(56);
            string van = string.IsNullOrEmpty(pakket.Afzender) ? "een andere tablet" : pakket.Afzender;
            Controls.Add(new Label { Text = "Ontvangen van " + van, Font = JohanTheme.Font(28F, FontStyle.Bold), AutoSize = true, Location = new Point(x, S(36)) });
            Controls.Add(new Label { Text = "Verstuurd " + pakket.Tijd.ToString("d MMMM HH:mm") + ". Per onderdeel kies je zelf; wat hier al staat blijft staan tenzij je op Vervangen tikt. Bewerkte vlakken blijven altijd per tablet.", Font = JohanTheme.Font(11F), ForeColor = JohanTheme.Muted, AutoSize = true, Location = new Point(x + S(2), S(92)) });

            var body = new Panel { Bounds = new Rectangle(x, S(150), w - 2 * x, h - S(150) - S(130)), AutoScroll = true, BackColor = JohanTheme.Bg };
            Controls.Add(body);
            int y = 0;
            foreach (var v in verschillen)
            {
                var kaart = Kaart(v, body.Width - S(24));
                kaart.Location = new Point(0, y);
                body.Controls.Add(kaart);
                y += kaart.Height + S(16);
            }
            if (verschillen.Count == 0)
            {
                body.Controls.Add(new Label { Text = "Dit pakket bevat geen bruikbaar perceel.", Font = JohanTheme.Font(14F), ForeColor = JohanTheme.Muted, AutoSize = true, Location = new Point(S(8), S(8)) });
            }

            lblStatus = new Label { Font = JohanTheme.Font(12F), ForeColor = JohanTheme.Muted, AutoSize = false, Bounds = new Rectangle(x + S(220), h - S(96), w - 2 * x - S(700), S(56)), TextAlign = ContentAlignment.MiddleLeft, Text = string.IsNullOrEmpty(pakket.Bron) ? "" : "Bron: " + (pakket.Bron.Contains("\\") ? System.IO.Path.GetFileName(pakket.Bron) : "brievenbus") };
            Controls.Add(lblStatus);

            var annuleren = new JohanRoundButton { Text = "Annuleren", Bounds = new Rectangle(x, h - S(100), S(200), S(64)), BackColor = JohanTheme.Panel, ForeColor = JohanTheme.Text, FlatStyle = FlatStyle.Flat, Font = JohanTheme.Font(14F, FontStyle.Bold) };
            annuleren.FlatAppearance.BorderSize = 0;
            annuleren.Click += (s, e) => Close();
            Controls.Add(annuleren);

            var ok = new JohanRoundButton { Text = "Overnemen", Image = JohanSkin.Icon("check_licht_64", 28), Bounds = new Rectangle(w - x - S(280), h - S(100), S(280), S(64)), BackColor = JohanTheme.Green, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = JohanTheme.Font(14F, FontStyle.Bold) };
            ok.FlatAppearance.BorderSize = 0;
            ok.Click += (s, e) => Overnemen();
            Controls.Add(ok);
        }

        // One field: header with a tick (take this field or not), then boundary and lines
        private Panel Kaart(JohanDelen.Verschil v, int breedte)
        {
            var k = new Panel { Width = breedte, Height = S(88), BackColor = JohanTheme.Panel };
            k.Paint += (s, e) =>
            {
                using (var path = JohanRoundButton.Rounded(new Rectangle(0, 0, k.Width - 1, k.Height - 1), S(16)))
                using (var b = new SolidBrush(JohanTheme.Panel))
                {
                    e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    e.Graphics.Clear(JohanTheme.Bg);
                    e.Graphics.FillPath(b, path);
                }
            };
            string naam = v.Perceel.Naam;
            var vink = new JohanRoundButton { Bounds = new Rectangle(S(20), S(20), S(44), S(44)), FlatStyle = FlatStyle.Flat, Font = JohanTheme.Font(18F, FontStyle.Bold), ForeColor = Color.White, Radius = S(10) };
            vink.FlatAppearance.BorderSize = 0;
            vink.Click += (s, e) => { if (!overnemen.Remove(naam)) overnemen.Add(naam); Ververs(); };
            k.Controls.Add(vink);
            verversers.Add(() => { bool aan = overnemen.Contains(naam); vink.Text = aan ? "✓" : ""; vink.BackColor = aan ? JohanTheme.Green : JohanTheme.PanelLight; });
            k.Controls.Add(new Label { Text = naam, Font = JohanTheme.Font(18F, FontStyle.Bold), AutoSize = true, Location = new Point(S(80), S(16)), BackColor = Color.Transparent });
            string sub = v.VeldBestaat
                ? "Staat hier al: " + (v.AantalNieuw == 0 && v.AantalAnders == 0 && v.GrensStatus != "nieuw" && v.GrensStatus != "anders" ? "alles is gelijk" : (v.AantalNieuw + " nieuw, " + v.AantalAnders + " anders"))
                : "Nieuw perceel op deze tablet, met " + v.Lijnen.Count + " lijnen";
            k.Controls.Add(new Label { Text = sub, Font = JohanTheme.Font(10.5F), ForeColor = JohanTheme.Muted, AutoSize = true, Location = new Point(S(82), S(52)), BackColor = Color.Transparent });

            // boundary
            if (v.GrensStatus == "geen") Rij(k, "Perceelgrens", "Niet meegestuurd", JohanTheme.Grey, "—");
            else if (!v.VeldBestaat || v.GrensStatus == "nieuw") Rij(k, "Perceelgrens", Hectare(v), JohanTheme.Green, "nieuw");
            else if (v.GrensStatus == "gelijk") Rij(k, "Perceelgrens", "Gelijk aan wat hier al staat", JohanTheme.Green, "al gelijk");
            else
            {
                bool open = mf.isJobStarted && string.Equals(mf.currentFieldDirectory, naam, StringComparison.OrdinalIgnoreCase);
                var vak = Rij(k, "Perceelgrens", open ? "Wijkt af; vervangen kan pas als dit perceel niet open staat" : "Wijkt af van de grens die hier staat", JohanTheme.Amber, null);
                if (!open)
                {
                    var houden = Knop(vak, 0, "Mijne houden", () => { v.GrensVervangen = false; Ververs(); });
                    var vervangen = Knop(vak, houden.Right + S(8), "Vervangen", () => { v.GrensVervangen = true; Ververs(); });
                    verversers.Add(() => { Kleur(houden, !v.GrensVervangen); Kleur(vervangen, v.GrensVervangen); });
                }
            }

            // lines
            foreach (var l in v.Lijnen)
            {
                var lijn = l;
                string soort = lijn.Nieuw.mode == TrackMode.AB ? "Rechte lijn" : lijn.Nieuw.mode == TrackMode.Curve ? "Bochtlijn" : "Lijn";
                if (lijn.Status == "nieuw") Rij(k, lijn.Nieuw.name, soort + (v.VeldBestaat ? ", nieuw op deze tablet" : ""), JohanTheme.Green, "nieuw");
                else if (lijn.Status == "gelijk") Rij(k, lijn.Nieuw.name, soort + ", gelijk aan die van jou", JohanTheme.Green, "al gelijk");
                else
                {
                    var vak = Rij(k, lijn.Nieuw.name, soort + ", staat hier al en ligt " + Komma(JohanDelen.VerschilMeters(lijn)) + " m anders", JohanTheme.Amber, null);
                    var a = Knop(vak, 0, "Mijne houden", () => { lijn.Keuze = 0; Ververs(); });
                    var b = Knop(vak, a.Right + S(8), "Vervangen", () => { lijn.Keuze = 1; Ververs(); });
                    var c = Knop(vak, b.Right + S(8), "Allebei houden", () => { lijn.Keuze = 2; Ververs(); });
                    verversers.Add(() => { Kleur(a, lijn.Keuze == 0); Kleur(b, lijn.Keuze == 1); Kleur(c, lijn.Keuze == 2); });
                }
            }
            return k;
        }

        private static string Hectare(JohanDelen.Verschil v)
        {
            try
            {
                var ring = v.NieuweGrens[0].fenceLine;
                double a = 0;
                for (int i = 0; i < ring.Count; i++) { var p = ring[i]; var q = ring[(i + 1) % ring.Count]; a += p.easting * q.northing - q.easting * p.northing; }
                return Komma(Math.Abs(a) / 20000.0) + " ha";
            }
            catch { return ""; }
        }

        private static string Komma(double v) => v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');

        // Row: label and hint left, coloured dot and either a word or buttons right. Returns the control area.
        private Panel Rij(Panel kaart, string label, string hint, Color stip, string woord)
        {
            int y = kaart.Height - S(12);
            var rij = new Panel { Bounds = new Rectangle(S(20), y, kaart.Width - 2 * S(20), S(62)), BackColor = JohanTheme.Bg, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            rij.Paint += (s, e) =>
            {
                using (var path = JohanRoundButton.Rounded(new Rectangle(0, 0, rij.Width - 1, rij.Height - 1), S(12)))
                using (var b = new SolidBrush(JohanTheme.Bg))
                {
                    e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    e.Graphics.Clear(JohanTheme.Panel);
                    e.Graphics.FillPath(b, path);
                }
                using (var b = new SolidBrush(stip)) e.Graphics.FillEllipse(b, S(470), S(24), S(14), S(14));
            };
            rij.Controls.Add(new Label { Text = label, Font = JohanTheme.Font(14F), ForeColor = JohanTheme.Text, AutoSize = false, Bounds = new Rectangle(S(16), S(4), S(440), S(34)), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent });
            rij.Controls.Add(new Label { Text = hint, Font = JohanTheme.Font(10F), ForeColor = JohanTheme.Muted, AutoSize = false, Bounds = new Rectangle(S(16), S(36), S(440), S(24)), BackColor = Color.Transparent });
            var vak = new Panel { Bounds = new Rectangle(S(496), 0, rij.Width - S(496), S(62)), BackColor = Color.Transparent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            if (woord != null) vak.Controls.Add(new Label { Text = woord, Font = JohanTheme.Font(13F, FontStyle.Bold), ForeColor = JohanTheme.Muted, AutoSize = false, Bounds = new Rectangle(0, 0, S(300), S(62)), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent });
            rij.Controls.Add(vak);
            kaart.Controls.Add(rij);
            kaart.Height = y + S(62) + S(12) + S(8);
            return vak;
        }

        private JohanRoundButton Knop(Panel vak, int x, string tekst, Action actie)
        {
            int w = TextRenderer.MeasureText(tekst, JohanTheme.Font(13F, FontStyle.Bold)).Width + S(32);
            var b = new JohanRoundButton { Bounds = new Rectangle(x, S(7), w, S(48)), Text = tekst, FlatStyle = FlatStyle.Flat, Font = JohanTheme.Font(13F, FontStyle.Bold), Radius = S(10) };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (s, e) => actie();
            vak.Controls.Add(b);
            return b;
        }

        private static void Kleur(JohanRoundButton b, bool aan)
        {
            b.BackColor = aan ? JohanTheme.Blue : JohanTheme.PanelLight;
            b.ForeColor = aan ? Color.White : JohanTheme.Text;
        }

        private void Ververs() { foreach (var v in verversers) v(); }

        private void Overnemen()
        {
            var regels = new List<string>();
            foreach (var v in verschillen.Where(v => overnemen.Contains(v.Perceel.Naam)))
            {
                try { regels.Add(JohanDelen.Overnemen(v, mf)); }
                catch (Exception ex) { Log.EventWriter("Delen: take over " + v.Perceel.Naam + ": " + ex.Message); regels.Add(v.Perceel.Naam + ": mislukt, " + ex.Message); }
            }
            Overgenomen = regels.Count > 0;
            if (regels.Count > 0) mf.TimedMessageBox(4000, "Overgenomen", string.Join("\n", regels));
            Close();
        }
    }

    // The question under the top bar when a package arrives: who sent what, Nee or Bekijken.
    // Non-modal, the driving goes on. Nee removes it from the mailbox; Bekijken opens FormOntvangen.
    public class FormOntvangBar : Form
    {
        private static int S(int px) => JohanTheme.S(px);

        public FormOntvangBar(FormGPS mf, JohanDelen.Pakket p, Action<bool> klaar)
        {
            Name = "FormOntvangBar";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            BackColor = JohanTheme.Panel;
            ForeColor = JohanTheme.Text;
            ShowInTaskbar = false;
            var scr = Screen.FromControl(mf).Bounds;
            int w = Math.Min(S(900), scr.Width - S(64)), h = S(96);
            Bounds = new Rectangle(scr.Left + (scr.Width - w) / 2, scr.Top + S(96) + S(16), w, h);

            string wat = p.Percelen.Count == 1 ? p.Percelen[0].Naam : p.Percelen.Count + " percelen";
            string welke = string.Join(", ", p.Percelen.Take(3).Select(v => v.Naam)) + (p.Percelen.Count > 3 ? ", …" : "");
            Controls.Add(new Label { Text = "Van " + p.Afzender + ": " + wat, Font = JohanTheme.Font(15F, FontStyle.Bold), AutoSize = false, Bounds = new Rectangle(S(24), S(12), w - S(340), S(36)), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent, AutoEllipsis = true });
            Controls.Add(new Label { Text = (p.Percelen.Count == 1 ? "Grens en lijnen" : welke) + ". Overnemen?", Font = JohanTheme.Font(10.5F), ForeColor = JohanTheme.Muted, AutoSize = false, Bounds = new Rectangle(S(24), S(50), w - S(340), S(28)), BackColor = Color.Transparent, AutoEllipsis = true });

            var nee = new JohanRoundButton { Text = "Nee", Bounds = new Rectangle(w - S(300), S(20), S(110), S(56)), BackColor = JohanTheme.PanelLight, ForeColor = JohanTheme.Text, FlatStyle = FlatStyle.Flat, Font = JohanTheme.Font(13F, FontStyle.Bold), Radius = S(12) };
            nee.FlatAppearance.BorderSize = 0;
            nee.Click += (s, e) => { Close(); klaar(false); };
            Controls.Add(nee);
            var kijk = new JohanRoundButton { Text = "Bekijken", Bounds = new Rectangle(w - S(180), S(20), S(156), S(56)), BackColor = JohanTheme.Blue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = JohanTheme.Font(13F, FontStyle.Bold), Radius = S(12) };
            kijk.FlatAppearance.BorderSize = 0;
            kijk.Click += (s, e) =>
            {
                Close();
                using (var f = new FormOntvangen(mf, p)) f.ShowDialog(mf);
                klaar(true);
            };
            Controls.Add(kijk);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(JohanTheme.Blue, S(2))) e.Graphics.DrawRectangle(p, 1, 1, Width - 3, Height - 3);
        }
    }
}
