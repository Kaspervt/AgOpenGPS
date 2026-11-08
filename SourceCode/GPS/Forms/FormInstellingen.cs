using System;
using AgLibrary.Johan;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AgLibrary.Logging;
using AgOpenGPS.Properties;

namespace AgOpenGPS
{
    // Johan fork: all settings on one page, in chapters ordered by how often they are needed
    // (canvas proposal D, 8-11). Each chapter lives in its own partial file FormInstellingen.<Naam>.cs
    // and only uses the row helpers below. Values are written into the AgOpenGPS settings objects
    // and applied with the same calls the configuration screen uses, never into mf.vehicle directly.
    public partial class FormInstellingen : Form
    {
        private readonly FormGPS mf;
        private Panel body;
        private FlowLayoutPanel chips;
        private readonly List<Action> verversers = new List<Action>();
        private readonly List<Panel> kaarten = new List<Panel>();
        private bool ontgrendeld;   // the code was entered once on this page
        private Control laatsteVeld;

        private static int S(int px) => JohanTheme.S(px);
        private static string Komma(double v, int dec) => v.ToString(dec == 0 ? "0" : "0." + new string('0', dec), System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');

        // Chapter order = order of use. Each method is in its own partial file.
        private void BouwHoofdstukken()
        {
            HoofdstukWerktuig();
            HoofdstukLijnenKopakker();
            HoofdstukVerbindingen();
            HoofdstukSchermGeluid();
            HoofdstukGpsRichting();
            HoofdstukTrekker();
            HoofdstukSturen();
            HoofdstukMachineRelais();
            HoofdstukOnderhoud();
        }

        public FormInstellingen(FormGPS formGPS)
        {
            mf = formGPS;
            Name = "FormInstellingen";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            BackColor = JohanTheme.Bg;
            ForeColor = JohanTheme.Text;
            Font = JohanTheme.Font(12F);
            ShowInTaskbar = false;
            KeyPreview = true;
            Bounds = Screen.FromControl(mf).Bounds;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
            BuildLayout();
        }

        private void BuildLayout()
        {
            int x = S(40);
            Controls.Add(new Label { Text = "Instellingen", Font = JohanTheme.Font(28F, FontStyle.Bold), AutoSize = true, Location = new Point(x, S(28)) });
            Controls.Add(new Label { Text = "Alles op één pagina. Tik een hoofdstuk om ernaartoe te springen.", Font = JohanTheme.Font(11F), ForeColor = JohanTheme.Muted, AutoSize = true, Location = new Point(x + S(2), S(80)) });

            chips = new FlowLayoutPanel { Bounds = new Rectangle(x, S(112), Width - 2 * x, S(52)), BackColor = JohanTheme.Bg, WrapContents = false, AutoScroll = false };
            Controls.Add(chips);

            body = new Panel { Bounds = new Rectangle(x, S(176), Width - 2 * x, Height - S(176) - S(100)), AutoScroll = true, BackColor = JohanTheme.Bg };
            Controls.Add(body);

            BouwHoofdstukken();
            Stapel();

            var terug = new JohanRoundButton
            {
                Text = "Klaar",
                Image = JohanSkin.Icon("check_licht_64", 28),
                Bounds = new Rectangle(Width - x - S(200), Height - S(88), S(200), S(64)),
                BackColor = JohanTheme.Green,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = JohanTheme.Font(14F, FontStyle.Bold)
            };
            terug.FlatAppearance.BorderSize = 0;
            terug.Click += (s, e) => Close();
            Controls.Add(terug);
            Ververs();
        }

        // Cards under each other in the scrolling body; a chip per card
        private void Stapel()
        {
            int y = 0, w = body.ClientSize.Width - S(24);
            foreach (var k in kaarten)
            {
                k.Location = new Point(0, y);
                k.Width = w;
                foreach (Control c in k.Controls) if (c.Tag is string t && t == "rij") c.Width = w - 2 * S(20);
                y += k.Height + S(16);
                body.Controls.Add(k);
                var kaart = k;
                var chip = new JohanRoundButton
                {
                    Text = (string)k.Controls[0].Text,
                    Size = new Size(TextRenderer.MeasureText((string)k.Controls[0].Text, JohanTheme.Font(12F, FontStyle.Bold)).Width + S(32), S(42)),
                    Margin = new Padding(0, 0, S(8), 0),
                    BackColor = JohanTheme.Panel,
                    ForeColor = JohanTheme.Text,
                    FlatStyle = FlatStyle.Flat,
                    Font = JohanTheme.Font(12F, FontStyle.Bold),
                    Radius = S(21)
                };
                chip.FlatAppearance.BorderSize = 0;
                chip.Click += (s, e) => { body.AutoScrollPosition = new Point(0, kaart.Top - body.AutoScrollPosition.Y - S(0)); };
                chips.Controls.Add(chip);
            }
        }

        // ---- card and rows -------------------------------------------------------------------

        // A chapter card. code = true: the rows ask for the code before the first change
        private Panel Hoofdstuk(string titel, string uitleg, bool code = false)
        {
            var k = new Panel { BackColor = JohanTheme.Panel, Height = S(88), Tag = code };
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
            k.Controls.Add(new Label { Text = titel, Font = JohanTheme.Font(18F, FontStyle.Bold), AutoSize = true, Location = new Point(S(20), S(16)), BackColor = Color.Transparent });
            k.Controls.Add(new Label { Text = uitleg + (code ? "   •   Achter de code" : ""), Font = JohanTheme.Font(10.5F), ForeColor = JohanTheme.Muted, AutoSize = true, Location = new Point(S(22), S(52)), BackColor = Color.Transparent });
            kaarten.Add(k);
            return k;
        }

        // Row frame: label (and hint) left, a panel for the controls right. Returns the control area.
        private Panel Rij(Panel kaart, string label, string hint)
        {
            int y = kaart.Height - S(12);
            var rij = new Panel { Bounds = new Rectangle(S(20), y, kaart.Width - 2 * S(20), S(62)), BackColor = JohanTheme.Bg, Tag = "rij" };
            rij.Paint += (s, e) =>
            {
                using (var path = JohanRoundButton.Rounded(new Rectangle(0, 0, rij.Width - 1, rij.Height - 1), S(12)))
                using (var b = new SolidBrush(JohanTheme.Bg))
                {
                    e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    e.Graphics.Clear(JohanTheme.Panel);
                    e.Graphics.FillPath(b, path);
                }
            };
            rij.Controls.Add(new Label { Text = label, Font = JohanTheme.Font(14F), ForeColor = JohanTheme.Text, AutoSize = false, Bounds = new Rectangle(S(16), hint == null ? 0 : S(4), S(440), hint == null ? S(62) : S(34)), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent });
            if (hint != null)
                rij.Controls.Add(new Label { Text = hint, Font = JohanTheme.Font(10F), ForeColor = JohanTheme.Muted, AutoSize = false, Bounds = new Rectangle(S(16), S(36), S(440), S(24)), BackColor = Color.Transparent });
            var vak = new Panel { Bounds = new Rectangle(S(470), 0, rij.Width - S(470), S(62)), BackColor = Color.Transparent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            rij.Controls.Add(vak);
            kaart.Controls.Add(rij);
            kaart.Height = y + S(62) + S(12) + S(8);
            return vak;
        }

        private JohanRoundButton Knop(Panel vak, int x, int w, string tekst, Action actie, bool groot = false)
        {
            var b = new JohanRoundButton
            {
                Bounds = new Rectangle(x, S(7), w, S(48)),
                Text = tekst,
                BackColor = JohanTheme.PanelLight,
                ForeColor = JohanTheme.Text,
                FlatStyle = FlatStyle.Flat,
                Font = JohanTheme.Font(groot ? 16F : 13F, FontStyle.Bold),
                Radius = S(10)
            };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (s, e) => { laatsteVeld = b; if (Mag(vak)) { actie(); Ververs(); } };
            vak.Controls.Add(b);
            return b;
        }

        // the chapter card this control area belongs to asks for the code when it is marked
        private bool Mag(Control vak)
        {
            Control c = vak;
            while (c != null && !(c is Panel p && kaarten.Contains(p))) c = c.Parent;
            if (c == null || !(c.Tag is bool code) || !code || ontgrendeld) return true;
            double v = FormGetal.Vraag(this, "Code", "", 0, 999999, 0, 0, laatsteVeld);
            if ((int)v == Settings.Default.setDisplay_johanCode) { ontgrendeld = true; return true; }
            mf.TimedMessageBox(2000, "Instellingen", "Dat is niet de code");
            return false;
        }

        private void Ververs() { foreach (var v in verversers) { try { v(); } catch { } } }

        // Number with minus/plus and a tap on the value for the keypad
        protected void RijGetal(Panel kaart, string label, string eenheid, Func<double> lees, Action<double> schrijf, double min, double max, double stap, int decimalen = 2, string hint = null)
        {
            var vak = Rij(kaart, label, hint);
            var minKnop = Knop(vak, 0, S(52), "−", () => schrijf(Math.Max(min, Math.Round(lees() - stap, decimalen))));
            var waarde = Knop(vak, S(60), S(150), "", () => schrijf(FormGetal.Vraag(this, label, eenheid, min, max, lees(), decimalen, laatsteVeld)), true);
            var plusKnop = Knop(vak, S(218), S(52), "+", () => schrijf(Math.Min(max, Math.Round(lees() + stap, decimalen))));
            vak.Controls.Add(new Label { Text = eenheid, Font = JohanTheme.Font(12F), ForeColor = JohanTheme.Muted, AutoSize = false, Bounds = new Rectangle(S(280), 0, S(80), S(62)), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent });
            verversers.Add(() => waarde.Text = Komma(lees(), decimalen));
        }

        protected void RijAanUit(Panel kaart, string label, Func<bool> lees, Action<bool> schrijf, string hint = null)
        {
            var vak = Rij(kaart, label, hint);
            var b = Knop(vak, 0, S(110), "", () => schrijf(!lees()));
            verversers.Add(() => { bool aan = lees(); b.Text = aan ? "Aan" : "Uit"; b.BackColor = aan ? JohanTheme.Green : JohanTheme.PanelLight; b.ForeColor = aan ? Color.White : JohanTheme.Text; });
        }

        protected void RijKeuze(Panel kaart, string label, string[] opties, Func<int> lees, Action<int> schrijf, string hint = null)
        {
            var vak = Rij(kaart, label, hint);
            int x = 0;
            var knoppen = new List<JohanRoundButton>();
            for (int i = 0; i < opties.Length; i++)
            {
                int k = i;
                int w = TextRenderer.MeasureText(opties[i], JohanTheme.Font(13F, FontStyle.Bold)).Width + S(28);
                knoppen.Add(Knop(vak, x, w, opties[i], () => schrijf(k)));
                x += w + S(8);
            }
            verversers.Add(() => { int cur = lees(); for (int i = 0; i < knoppen.Count; i++) { bool aan = i == cur; knoppen[i].BackColor = aan ? JohanTheme.Blue : JohanTheme.PanelLight; knoppen[i].ForeColor = aan ? Color.White : JohanTheme.Text; } });
        }

        // One or more buttons; a button text starting with "!" is red (destructive)
        protected void RijKnoppen(Panel kaart, string label, string hint, params (string tekst, Action actie)[] knoppen)
        {
            var vak = Rij(kaart, label, hint);
            int x = 0;
            foreach (var (tekst, actie) in knoppen)
            {
                bool rood = tekst.StartsWith("!");
                string t = rood ? tekst.Substring(1) : tekst;
                int w = TextRenderer.MeasureText(t, JohanTheme.Font(13F, FontStyle.Bold)).Width + S(32);
                var b = Knop(vak, x, w, t, actie);
                if (rood) { b.BackColor = JohanTheme.Red; b.ForeColor = Color.White; }
                x += w + S(8);
            }
        }

        // Status line: coloured dot and text (refreshed), optional buttons behind it
        protected void RijStatus(Panel kaart, string label, Func<Color> kleur, Func<string> tekst, string hint = null, params (string tekst, Action actie)[] knoppen)
        {
            var vak = Rij(kaart, label, hint);
            var stip = new Panel { Bounds = new Rectangle(0, S(24), S(14), S(14)), BackColor = JohanTheme.Grey };
            stip.Paint += (s, e) => { e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; e.Graphics.Clear(JohanTheme.Bg); using (var b = new SolidBrush(stip.BackColor)) e.Graphics.FillEllipse(b, 0, 0, S(13), S(13)); };
            vak.Controls.Add(stip);
            var lbl = new Label { Font = JohanTheme.Font(13F, FontStyle.Bold), ForeColor = JohanTheme.Text, AutoSize = false, Bounds = new Rectangle(S(24), 0, S(300), S(62)), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent };
            vak.Controls.Add(lbl);
            int x = S(330);
            foreach (var (t, actie) in knoppen)
            {
                int w = TextRenderer.MeasureText(t, JohanTheme.Font(13F, FontStyle.Bold)).Width + S(32);
                Knop(vak, x, w, t, actie);
                x += w + S(8);
            }
            verversers.Add(() => { stip.BackColor = kleur(); stip.Invalidate(); lbl.Text = tekst(); });
        }

        // Plain text on the right (refreshed), for information
        protected void RijTekst(Panel kaart, string label, Func<string> tekst, string hint = null)
        {
            var vak = Rij(kaart, label, hint);
            var lbl = new Label { Font = JohanTheme.Font(13F), ForeColor = JohanTheme.Muted, AutoSize = false, Bounds = new Rectangle(0, 0, S(520), S(62)), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent };
            vak.Controls.Add(lbl);
            verversers.Add(() => lbl.Text = tekst());
        }

        // ---- applying -------------------------------------------------------------------------

        // Save everything and let AgOpenGPS pick it up, the way FormConfig does on closing.
        // werktuig = true also rebuilds the tool (section layout, textures).
        protected void Toepassen(bool werktuig = false)
        {
            try
            {
                Settings.Default.Save();
                VehicleSettings.Default.Save();
                ToolSettings.Default.Save();
                if (werktuig) mf.tool = new CTool(mf);
                mf.LoadSettings();
                if (werktuig) mf.SetVehicleTextures();
                mf.SendSettings();
            }
            catch (Exception ex)
            {
                Log.EventWriter("Instellingen: toepassen mislukt: " + ex.Message);
                mf.TimedMessageBox(3000, "Instellingen", "Opslaan mislukt: " + ex.Message);
            }
        }
    }
}
