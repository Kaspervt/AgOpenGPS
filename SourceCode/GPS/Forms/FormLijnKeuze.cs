using System;
using AgLibrary.Johan;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace AgOpenGPS
{
    // Johan fork: the lines of the field as a list along the right side of the map, the current one
    // green, with the real names from the field. Opened by "Andere lijn" (which also steps to the
    // next line, so a quick tap keeps working); a tap on a name selects it, the list closes itself
    // after a few seconds without a tap.
    public class FormLijnKeuze : Form
    {
        private readonly FormGPS mf;
        private readonly Timer tick = new Timer { Interval = 500 };
        private readonly List<Button> knoppen = new List<Button>();
        private readonly List<int> idx = new List<int>();
        private int stil;

        private static int S(int px) => JohanTheme.S(px);

        public FormLijnKeuze(FormGPS formGPS)
        {
            mf = formGPS;
            Name = "FormLijnKeuze";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            BackColor = JohanTheme.Bar;
            ForeColor = JohanTheme.Text;
            Font = JohanTheme.Font(12F);
            ShowInTaskbar = false;
            TopMost = false;

            // a drawer between the top and bottom bar, right of the zoom buttons
            var scr = Screen.FromControl(mf).Bounds;
            int w = S(360), top = scr.Top + S(68) + S(12), h = scr.Bottom - S(132) - S(12) - top;
            Bounds = new Rectangle(scr.Right - S(96) - w, top, w, h);
            BuildLayout();

            tick.Tick += (s, e) => { Highlight(); if (++stil >= 16) Close(); };   // 8 s without a tap
            Load += (s, e) => { Highlight(); tick.Start(); };
            FormClosed += (s, e) => { tick.Stop(); tick.Dispose(); };
        }

        private void BuildLayout()
        {
            for (int i = 0; i < mf.trk.gArr.Count; i++) if (mf.trk.gArr[i].isVisible) idx.Add(i);
            int pad = S(14), gap = S(10), bh = S(64);
            Controls.Add(new Label { Text = "Welke lijn?", Font = JohanTheme.Font(15F, FontStyle.Bold), AutoSize = false, Bounds = new Rectangle(pad, pad, Width - 2 * pad, S(34)), TextAlign = ContentAlignment.MiddleLeft });

            var lijst = new Panel { Bounds = new Rectangle(pad, pad + S(44), Width - 2 * pad, Height - pad - S(44) - S(14) - bh - gap), AutoScroll = true, BackColor = JohanTheme.Bar };
            lijst.MouseDown += (s, e) => stil = 0;
            Controls.Add(lijst);
            int y = 0;
            foreach (int i in idx)
            {
                int t = i;
                var b = new JohanRoundButton
                {
                    Bounds = new Rectangle(0, y, lijst.Width - S(20), bh),
                    Text = mf.trk.gArr[i].name,
                    BackColor = JohanTheme.Panel,
                    ForeColor = JohanTheme.Text,
                    FlatStyle = FlatStyle.Flat,
                    Font = JohanTheme.Font(13F, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(S(10), 0, 0, 0)
                };
                b.FlatAppearance.BorderSize = 0;
                b.MouseDown += (s, e) => stil = 0;
                b.Click += (s, e) => { mf.JohanKiesLijn(t); stil = 0; Highlight(); mf.Activate(); };
                lijst.Controls.Add(b);
                knoppen.Add(b);
                y += bh + gap;
            }
            var klaar = new JohanRoundButton
            {
                Bounds = new Rectangle(pad, Height - S(14) - bh, Width - 2 * pad, bh),
                Text = "Klaar",
                BackColor = JohanTheme.Green,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = JohanTheme.Font(13F, FontStyle.Bold),
                Image = JohanSkin.Icon("check_licht_64", 26),
                TextImageRelation = TextImageRelation.ImageBeforeText
            };
            klaar.FlatAppearance.BorderSize = 0;
            klaar.MouseDown += (s, e) => stil = 0;
            klaar.Click += (s, e) => Close();
            Controls.Add(klaar);
        }

        // Called by the list itself and by "Andere lijn" after a cycle step
        public void Highlight()
        {
            stil = Math.Min(stil, 15);
            for (int k = 0; k < knoppen.Count; k++)
            {
                bool huidig = idx[k] == mf.trk.idx;
                knoppen[k].BackColor = huidig ? JohanTheme.Green : JohanTheme.Panel;
                knoppen[k].ForeColor = huidig ? Color.White : JohanTheme.Text;
            }
        }

        public void Wakker() { stil = 0; Highlight(); }
    }
}
