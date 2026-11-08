using System;
using AgLibrary.Johan;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace AgOpenGPS
{
    // Johan fork: a number pad in the canvas look that slides up from the bottom of the screen,
    // like a keyboard, so the row being edited stays visible above it. Title says what is being
    // entered, the unit and the allowed range are shown, big keys for a finger, the comma next
    // to the 0, OK and Annuleren always visible. Replaces FormNumeric for the fork's own screens.
    public class FormGetal : Form
    {
        private readonly double min, max;
        private readonly int decimalen;
        private readonly Label lblWaarde;
        private string tekst = "";
        private bool eersteToets = true;

        public double Waarde { get; private set; }

        private static int S(int px) => JohanTheme.S(px);

        // veld: the control being edited. When it would disappear under the bar, the page is shifted up
        // for as long as the bar is open (like a keyboard pushing a page up), then put back.
        public static double Vraag(IWin32Window owner, string titel, string eenheid, double min, double max, double nu, int decimalen = 2, Control veld = null)
        {
            using (var f = new FormGetal(titel, eenheid, min, max, nu, decimalen))
            {
                var pagina = veld?.FindForm();
                int schuif = 0;
                if (pagina != null)
                {
                    try
                    {
                        var r = veld.RectangleToScreen(veld.ClientRectangle);
                        int grens = f.Top - S(16);
                        if (r.Bottom > grens) schuif = r.Bottom - grens;
                    }
                    catch { schuif = 0; }
                }
                if (schuif > 0)
                {
                    pagina.SuspendLayout();
                    foreach (Control c in pagina.Controls) c.Top -= schuif;
                    pagina.ResumeLayout();
                }
                try
                {
                    return f.ShowDialog(owner) == DialogResult.OK ? f.Waarde : nu;
                }
                finally
                {
                    if (schuif > 0)
                    {
                        pagina.SuspendLayout();
                        foreach (Control c in pagina.Controls) c.Top += schuif;
                        pagina.ResumeLayout();
                    }
                }
            }
        }

        public FormGetal(string titel, string eenheid, double min, double max, double nu, int decimalen)
        {
            this.min = min; this.max = max; this.decimalen = decimalen;
            Waarde = nu;
            Name = "FormGetal";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            BackColor = JohanTheme.Bar;
            ForeColor = JohanTheme.Text;
            Font = JohanTheme.Font(12F);
            ShowInTaskbar = false;
            KeyPreview = true;

            // a bar along the bottom of the screen
            var scr = Screen.FromPoint(Cursor.Position).Bounds;
            int h = S(330), w = scr.Width;
            Bounds = new Rectangle(scr.Left, scr.Bottom - h, w, h);
            Paint += (s, e) => { using (var p = new Pen(JohanTheme.PanelLight)) e.Graphics.DrawLine(p, 0, 0, Width, 0); };

            // left: what is being entered and the value
            int lx = S(40), lw = S(380);
            Controls.Add(new Label { Text = titel, Font = JohanTheme.Font(16F, FontStyle.Bold), AutoSize = false, Bounds = new Rectangle(lx, S(28), lw, S(34)), TextAlign = ContentAlignment.MiddleLeft });
            string bereik = "Van " + Opmaak(min) + " tot " + Opmaak(max) + (eenheid == "" ? "" : " " + eenheid);
            Controls.Add(new Label { Text = bereik, Font = JohanTheme.Font(10.5F), ForeColor = JohanTheme.Muted, AutoSize = false, Bounds = new Rectangle(lx, S(64), lw, S(24)), TextAlign = ContentAlignment.MiddleLeft });
            lblWaarde = new Label
            {
                Font = JohanTheme.Font(30F, FontStyle.Bold),
                BackColor = JohanTheme.Panel,
                ForeColor = JohanTheme.Text,
                AutoSize = false,
                Bounds = new Rectangle(lx, S(100), lw - S(80), S(72)),
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, S(14), 0)
            };
            Controls.Add(lblWaarde);
            Controls.Add(new Label { Text = eenheid, Font = JohanTheme.Font(16F), ForeColor = JohanTheme.Muted, AutoSize = false, Bounds = new Rectangle(lx + lw - S(72), S(100), S(72), S(72)), TextAlign = ContentAlignment.MiddleLeft });
            Controls.Add(new Label { Text = "Tik een nieuw getal; de eerste toets wist het oude.", Font = JohanTheme.Font(10.5F), ForeColor = JohanTheme.Muted, AutoSize = false, Bounds = new Rectangle(lx, S(184), lw, S(24)) });
            ToonWaarde(Opmaak(nu));

            // middle: keys in four columns, comma big next to the 0
            int kw = S(104), kh = S(60), gap = S(10);
            int kx = lx + lw + S(60), ky = S(28);
            string[][] rijen =
            {
                new[] { "7", "8", "9", "⌫" },
                new[] { "4", "5", "6", "Wis" },
                new[] { "1", "2", "3", "±" },
                new[] { "0", ",", "", "" },
            };
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++)
                {
                    string t = rijen[r][c];
                    if (t == "") continue;
                    int breed = (r == 3 && c == 1) ? 2 * kw + gap : kw;   // the comma spans two columns
                    var knop = Toets(new Rectangle(kx + c * (kw + gap), ky + r * (kh + gap), breed, kh), t, () => Druk(t));
                    if (t == "⌫" || t == "Wis" || t == "±") knop.Font = JohanTheme.Font(15F, FontStyle.Bold);
                    if (t == "±" && min >= 0) knop.Enabled = false;
                    if (t == "," && decimalen == 0) knop.Enabled = false;
                }

            // right: cancel and OK, always visible
            int bx = kx + 4 * (kw + gap) + S(50), bw = Math.Max(S(160), w - bx - S(40));
            var annuleren = Toets(new Rectangle(bx, ky, bw, S(70)), "Annuleren", () => { DialogResult = DialogResult.Cancel; Close(); });
            annuleren.Font = JohanTheme.Font(14F, FontStyle.Bold);
            var ok = Toets(new Rectangle(bx, ky + S(70) + S(14), bw, S(126)), "OK", Bevestig);
            ok.BackColor = JohanTheme.Green; ok.ForeColor = Color.White; ok.Font = JohanTheme.Font(18F, FontStyle.Bold);

            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
                else if (e.KeyCode == Keys.Enter) Bevestig();
                else if (e.KeyCode == Keys.Back) Druk("⌫");
            };
            KeyPress += (s, e) =>
            {
                if (char.IsDigit(e.KeyChar)) Druk(e.KeyChar.ToString());
                else if (e.KeyChar == ',' || e.KeyChar == '.') Druk(",");
                else if (e.KeyChar == '-') Druk("±");
            };
        }

        private Button Toets(Rectangle r, string tekstKnop, Action actie)
        {
            var b = new JohanRoundButton
            {
                Bounds = r,
                Text = tekstKnop,
                BackColor = JohanTheme.Panel,
                ForeColor = JohanTheme.Text,
                FlatStyle = FlatStyle.Flat,
                Font = JohanTheme.Font(20F, FontStyle.Bold),
                TabStop = false
            };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (s, e) => actie();
            Controls.Add(b);
            return b;
        }

        private void Druk(string t)
        {
            switch (t)
            {
                case "⌫":
                    if (eersteToets) { tekst = ""; eersteToets = false; }
                    else if (tekst.Length > 0) tekst = tekst.Substring(0, tekst.Length - 1);
                    break;
                case "Wis":
                    tekst = ""; eersteToets = false;
                    break;
                case "±":
                    if (min >= 0) return;
                    if (eersteToets) { tekst = Opmaak(Waarde); eersteToets = false; }
                    tekst = tekst.StartsWith("-") ? tekst.Substring(1) : "-" + tekst;
                    break;
                case ",":
                    if (decimalen == 0) return;
                    if (eersteToets) { tekst = "0"; eersteToets = false; }
                    if (tekst == "" || tekst == "-") tekst += "0";
                    if (!tekst.Contains(",")) tekst += ",";
                    break;
                default:
                    if (eersteToets) { tekst = ""; eersteToets = false; }
                    if (tekst == "0") tekst = "";
                    if (tekst.Length < 9) tekst += t;
                    break;
            }
            ToonWaarde(tekst == "" ? "0" : tekst);
        }

        private string Opmaak(double v) => v.ToString(decimalen == 0 ? "0" : "0." + new string('#', decimalen), CultureInfo.InvariantCulture).Replace('.', ',');

        private void ToonWaarde(string t)
        {
            lblWaarde.Text = t;
            bool ok = double.TryParse(t.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && v >= min && v <= max;
            lblWaarde.ForeColor = ok || t == "" || t == "-" ? JohanTheme.Text : JohanTheme.Red;
        }

        private void Bevestig()
        {
            string t = eersteToets ? Opmaak(Waarde) : (tekst == "" ? "0" : tekst);
            if (!double.TryParse(t.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) { ToonWaarde(t); return; }
            v = Math.Round(v, decimalen);
            if (v < min || v > max) { lblWaarde.ForeColor = JohanTheme.Red; return; }
            Waarde = v;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
