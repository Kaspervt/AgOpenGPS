using System;
using AgLibrary.Johan;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace AgOpenGPS
{
    // Johan fork: the lines of the field as one bar above the bottom buttons, the current one green.
    // Opened by "Andere lijn" (which also steps to the next line, so a quick tap keeps working);
    // a tap on a name selects it, the bar closes itself after a few seconds without a tap.
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

            var scr = Screen.FromControl(mf).Bounds;
            int h = S(96), w = Math.Min(S(1310), scr.Width - S(32));
            Bounds = new Rectangle(scr.Left + (scr.Width - w) / 2, scr.Bottom - S(132) - S(12) - h, w, h);
            BuildLayout();

            tick.Tick += (s, e) => { Highlight(); if (++stil >= 16) Close(); };   // 8 s without a tap
            Load += (s, e) => { Highlight(); tick.Start(); };
            FormClosed += (s, e) => tick.Stop();
        }

        private void BuildLayout()
        {
            for (int i = 0; i < mf.trk.gArr.Count; i++) if (mf.trk.gArr[i].isVisible) idx.Add(i);
            int pad = S(14), gap = S(10), klaarW = S(96);
            int beschikbaar = Width - 2 * pad - klaarW - gap;
            int n = Math.Max(1, idx.Count);
            int bw = Math.Min(S(260), (beschikbaar - (n - 1) * gap) / n);
            int x = pad;
            foreach (int i in idx)
            {
                int t = i;
                var b = new JohanRoundButton
                {
                    Bounds = new Rectangle(x, S(14), bw, Height - S(28)),
                    Text = mf.trk.gArr[i].name,
                    BackColor = JohanTheme.Panel,
                    ForeColor = JohanTheme.Text,
                    FlatStyle = FlatStyle.Flat,
                    Font = JohanTheme.Font(12F, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                b.FlatAppearance.BorderSize = 0;
                b.Click += (s, e) => { mf.JohanKiesLijn(t); stil = 0; Highlight(); mf.Activate(); };
                Controls.Add(b);
                knoppen.Add(b);
                x += bw + gap;
            }
            var klaar = new JohanRoundButton
            {
                Bounds = new Rectangle(Width - pad - klaarW, S(14), klaarW, Height - S(28)),
                Text = "Klaar",
                BackColor = JohanTheme.Green,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = JohanTheme.Font(11F, FontStyle.Bold),
                Image = JohanSkin.Icon("check_licht_64", 26),
                TextImageRelation = TextImageRelation.ImageBeforeText
            };
            klaar.FlatAppearance.BorderSize = 0;
            klaar.Click += (s, e) => Close();
            Controls.Add(klaar);
        }

        // Called by the bar itself and by "Andere lijn" after a cycle step
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
