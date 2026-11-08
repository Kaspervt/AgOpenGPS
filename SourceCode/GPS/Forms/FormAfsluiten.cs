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
            int w = 640, h = 360;
            Bounds = new Rectangle(scr.Left + (scr.Width - w) / 2, scr.Top + (scr.Height - h) / 2, w, h);

            Controls.Add(new Label { Text = "Afsluiten?", Font = JohanTheme.Font(24F, FontStyle.Bold), AutoSize = true, Location = new Point(48, 40) });
            Controls.Add(new Label { Text = "Het perceel en de lijnen worden bewaard. De volgende keer gaat het verder waar je was.", Font = JohanTheme.Font(11F), ForeColor = JohanTheme.Muted, AutoSize = false, Bounds = new Rectangle(50, 92, w - 100, 50) });

            Knop(new Rectangle(48, 160, w - 96, 64), "Programma afsluiten", "power_licht_64", Color.FromArgb(220, 38, 38), Color.White, () => { Close(); mf.JohanAfsluiten(false); });
            Knop(new Rectangle(48, 236, (w - 96 - 12) / 2, 64), "Tablet uitzetten", "power_licht_64", JohanTheme.Panel, JohanTheme.Text, () => { Close(); mf.JohanAfsluiten(true); });
            Knop(new Rectangle(48 + (w - 96 - 12) / 2 + 12, 236, (w - 96 - 12) / 2, 64), "Terug", null, JohanTheme.Green, Color.White, Close);
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
