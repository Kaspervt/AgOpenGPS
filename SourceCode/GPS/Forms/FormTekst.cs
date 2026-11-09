using System;
using AgLibrary.Johan;
using System.Drawing;
using System.Windows.Forms;

namespace AgOpenGPS
{
    // Johan fork: text entry in the canvas look. A bar along the bottom of the screen, like a
    // keyboard: title, the text being typed, a QWERTY keyboard with big keys, OK and Annuleren.
    // Used for every name the driver types (field, tool, tractor profile), so a tablet without
    // a keyboard never needs the Windows one. Same shape and behaviour as FormGetal.
    public class FormTekst : Form
    {
        private readonly Label lblTekst;
        private readonly int maxLengte;
        private string tekst;
        private bool hoofdletter = true, eersteToets = true;
        private readonly Button[] letterToetsen;

        public string Waarde => tekst;

        private static int S(int px) => JohanTheme.S(px);

        // Returns the typed text, or null when cancelled or empty
        // hoofdletters = false: starts in lower case (addresses, user names, mountpoints)
        public static string Vraag(IWin32Window owner, string titel, string huidig, int maxLengte = 40, bool hoofdletters = true)
        {
            using (var f = new FormTekst(titel, huidig ?? "", maxLengte, hoofdletters))
            {
                if (f.ShowDialog(owner) != DialogResult.OK) return null;
                string t = f.Waarde.Trim();
                return t.Length == 0 ? null : t;
            }
        }

        public FormTekst(string titel, string huidig, int maxLengte, bool hoofdletters = true)
        {
            this.maxLengte = maxLengte;
            hoofdletter = hoofdletters;
            tekst = huidig;
            Name = "FormTekst";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            BackColor = JohanTheme.Bar;
            ForeColor = JohanTheme.Text;
            Font = JohanTheme.Font(12F);
            ShowInTaskbar = false;
            KeyPreview = true;

            var scr = Screen.FromPoint(Cursor.Position).Bounds;
            int h = S(490), w = scr.Width;
            Bounds = new Rectangle(scr.Left, scr.Bottom - h, w, h);
            Paint += (s, e) => { using (var p = new Pen(JohanTheme.PanelLight)) e.Graphics.DrawLine(p, 0, 0, Width, 0); };

            int x = S(40);
            Controls.Add(new Label { Text = titel, Font = JohanTheme.Font(16F, FontStyle.Bold), AutoSize = false, Bounds = new Rectangle(x, S(18), w - 2 * x - S(380), S(32)), TextAlign = ContentAlignment.MiddleLeft });
            lblTekst = new Label
            {
                Font = JohanTheme.Font(22F, FontStyle.Bold),
                BackColor = JohanTheme.PanelLight,
                ForeColor = JohanTheme.Text,
                AutoSize = false,
                Bounds = new Rectangle(x, S(54), w - 2 * x - S(380), S(56)),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(S(14), 0, 0, 0)
            };
            Controls.Add(lblTekst);

            // keys
            string[] rijen = { "1234567890", "qwertyuiop", "asdfghjkl", "zxcvbnm" };
            int kw = S(84), kh = S(56), gap = S(8), ky = S(124);
            var letters = new System.Collections.Generic.List<Button>();
            for (int r = 0; r < rijen.Length; r++)
            {
                int kx = x + r * (r == 0 ? 0 : S(26)) + (r == 3 ? S(20) : 0);
                for (int c = 0; c < rijen[r].Length; c++)
                {
                    string t = rijen[r][c].ToString();
                    var b = Toets(new Rectangle(kx + c * (kw + gap), ky + r * (kh + gap), kw, kh), t, () => Typ(t));
                    if (r > 0) letters.Add(b);
                }
            }
            letterToetsen = letters.ToArray();
            // bottom row: shift, space, dash, backspace
            int y4 = ky + 4 * (kh + gap);
            var shift = Toets(new Rectangle(x, y4, S(130), kh), "⇧ Hoofdletter", () => { hoofdletter = !hoofdletter; ZetLetters(); });
            shift.Font = JohanTheme.Font(12F, FontStyle.Bold);
            Toets(new Rectangle(x + S(138), y4, S(360), kh), "spatie", () => Typ(" "));
            Toets(new Rectangle(x + S(506), y4, S(72), kh), ".", () => Typ("."));   // addresses (ntrip.example.nl)
            Toets(new Rectangle(x + S(586), y4, kw, kh), "-", () => Typ("-"));
            var back = Toets(new Rectangle(x + S(586) + kw + gap, y4, S(120), kh), "⌫", () => { if (eersteToets) { tekst = ""; eersteToets = false; } else if (tekst.Length > 0) tekst = tekst.Substring(0, tekst.Length - 1); Toon(); });
            var wis = Toets(new Rectangle(x + S(586) + kw + gap + S(128), y4, S(90), kh), "Wis", () => { tekst = ""; eersteToets = false; Toon(); });
            wis.Font = JohanTheme.Font(13F, FontStyle.Bold);
            ZetLetters();

            // right: cancel and OK
            int bx = w - x - S(340), bw = S(340);
            var annuleren = Toets(new Rectangle(bx, S(18), bw, S(64)), "Annuleren", () => { DialogResult = DialogResult.Cancel; Close(); });
            annuleren.Font = JohanTheme.Font(14F, FontStyle.Bold);
            var ok = Toets(new Rectangle(bx, S(96), bw, S(120)), "OK", Bevestig);
            ok.BackColor = JohanTheme.Green; ok.ForeColor = Color.White; ok.Font = JohanTheme.Font(18F, FontStyle.Bold);
            Controls.Add(new Label { Text = "Tik een naam; de eerste toets wist de oude tekst.", Font = JohanTheme.Font(10.5F), ForeColor = JohanTheme.Muted, AutoSize = false, Bounds = new Rectangle(bx, S(226), bw, S(44)) });
            Toon();

            // a real keyboard works too
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
                else if (e.KeyCode == Keys.Enter) Bevestig();
                else if (e.KeyCode == Keys.Back) { if (tekst.Length > 0) tekst = tekst.Substring(0, tekst.Length - 1); eersteToets = false; Toon(); }
            };
            KeyPress += (s, e) => { if (!char.IsControl(e.KeyChar)) Typ(e.KeyChar.ToString(), false); };
        }

        private Button Toets(Rectangle r, string t, Action actie)
        {
            var b = new JohanRoundButton
            {
                Bounds = r,
                Text = t,
                BackColor = JohanTheme.Panel,
                ForeColor = JohanTheme.Text,
                FlatStyle = FlatStyle.Flat,
                Font = JohanTheme.Font(18F, FontStyle.Bold),
                TabStop = false
            };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (s, e) => actie();
            Controls.Add(b);
            return b;
        }

        private void ZetLetters()
        {
            foreach (var b in letterToetsen) b.Text = hoofdletter ? b.Text.ToUpperInvariant() : b.Text.ToLowerInvariant();
        }

        private void Typ(string t, bool schermtoets = true)
        {
            if (eersteToets) { tekst = ""; eersteToets = false; }
            if (tekst.Length >= maxLengte) return;
            if (schermtoets && char.IsLetter(t[0])) t = hoofdletter ? t.ToUpperInvariant() : t.ToLowerInvariant();
            // characters a file name cannot hold are not typed (field and tool names become files)
            if (t.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0) return;
            tekst += t;
            // after the first letter of a word the keyboard goes to lower case, like a phone
            if (schermtoets && hoofdletter && char.IsLetter(t[0])) { hoofdletter = false; ZetLetters(); }
            if (schermtoets && t == " ") { hoofdletter = true; ZetLetters(); }
            Toon();
        }

        private void Toon() { lblTekst.Text = tekst; }

        private void Bevestig()
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
