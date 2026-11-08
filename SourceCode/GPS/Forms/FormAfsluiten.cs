using System;
using AgLibrary.Johan;
using System.Drawing;
using System.Windows.Forms;

namespace AgOpenGPS
{
    // Johan fork: the shutdown question in the theme. Program off, tablet off, or back.
    // The actual closing is the normal AgOpenGPS path (fields saved, AgIO closed).
    public class FormAfsluiten : Form
    {
        private static int S(int px) => JohanTheme.S(px);

        public FormAfsluiten(FormGPS mf)
        {
            Name = "FormAfsluiten";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            BackColor = JohanTheme.Bg;
            ForeColor = JohanTheme.Text;
            Font = JohanTheme.Font(12F);
            ShowInTaskbar = false;
            var scr = Screen.FromControl(mf).Bounds;
            int w = S(640), h = S(360);
            Bounds = new Rectangle(scr.Left + (scr.Width - w) / 2, scr.Top + (scr.Height - h) / 2, w, h);

            Controls.Add(new Label { Text = "Afsluiten?", Font = JohanTheme.Font(24F, FontStyle.Bold), AutoSize = true, Location = new Point(S(48), S(40)) });
            Controls.Add(new Label { Text = "Het perceel en de lijnen worden bewaard. De volgende keer gaat het verder waar je was.", Font = JohanTheme.Font(11F), ForeColor = JohanTheme.Muted, AutoSize = false, Bounds = new Rectangle(S(50), S(92), w - S(100), S(50)) });

            Knop(new Rectangle(S(48), S(160), w - S(96), S(64)), "Programma afsluiten", "power_licht_64", Color.FromArgb(220, 38, 38), Color.White, () => { Close(); mf.JohanAfsluiten(false); });
            Knop(new Rectangle(S(48), S(236), (w - S(96) - S(12)) / 2, S(64)), "Tablet uitzetten", "power_licht_64", JohanTheme.Panel, JohanTheme.Text, () => { Close(); mf.JohanAfsluiten(true); });
            Knop(new Rectangle(S(48) + (w - S(96) - S(12)) / 2 + S(12), S(236), (w - S(96) - S(12)) / 2, S(64)), "Terug", null, JohanTheme.Green, Color.White, Close);
        }

        private void Knop(Rectangle r, string tekst, string icon, Color back, Color fore, Action actie)
        {
            var b = new JohanRoundButton
            {
                Bounds = r,
                Text = tekst,
                Image = icon == null ? null : JohanSkin.Icon(icon, 28),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                BackColor = back,
                ForeColor = fore,
                FlatStyle = FlatStyle.Flat,
                Font = JohanTheme.Font(14F, FontStyle.Bold)
            };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (s, e) => actie();
            Controls.Add(b);
        }
    }
}
