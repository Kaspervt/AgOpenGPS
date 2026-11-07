using System;
using System.Drawing;
using System.Windows.Forms;
using AgOpenGPS.Properties;

namespace AgOpenGPS
{
    // Johan fork: "Lijn verschuiven" as one bar above the bottom buttons, in the theme.
    // Same calls as FormNudge (trk.NudgeTrack, SnapToPivot, NudgeDistanceReset); non-modal so driving continues.
    public class FormVerschuif : Form
    {
        private readonly FormGPS mf;
        private readonly Timer tick = new Timer { Interval = 500 };
        private Label lblOffset;
        private double stap;   // metres

        public FormVerschuif(FormGPS formGPS)
        {
            mf = formGPS;
            Name = "FormVerschuif";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            BackColor = JohanTheme.Bar;
            ForeColor = JohanTheme.Text;
            Font = JohanTheme.Font(12F);
            ShowInTaskbar = false;
            TopMost = false;

            var scr = Screen.FromControl(mf).Bounds;
            int h = 96, w = Math.Min(1200, scr.Width - 32);
            Bounds = new Rectangle(scr.Left + (scr.Width - w) / 2, scr.Bottom - 132 - 12 - h, w, h);

            stap = ToolSettings.Default.setAS_snapDistance * 0.01;
            if (stap <= 0) stap = 0.02;
            BuildLayout();

            tick.Tick += (s, e) => UpdateLabel();
            Load += (s, e) => { UpdateLabel(); tick.Start(); };
            FormClosed += (s, e) => { tick.Stop(); try { mf.FileSaveTracks(); } catch { } };
        }

        private Button Knop(int x, int wdt, string tekst, string icon, Action actie)
        {
            var b = new Button
            {
                Bounds = new Rectangle(x, 14, wdt, Height - 28),
                Text = tekst,
                BackColor = JohanTheme.Panel,
                ForeColor = JohanTheme.Text,
                FlatStyle = FlatStyle.Flat,
                Font = JohanTheme.Font(11F, FontStyle.Bold),
                Image = icon == null ? null : JohanSkin.Icon(icon, 26),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                TextAlign = ContentAlignment.MiddleCenter
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = JohanTheme.PanelLight;
            b.Click += (s, e) => { actie(); UpdateLabel(); mf.Activate(); };
            Controls.Add(b);
            return b;
        }

        private void BuildLayout()
        {
            int cm = (int)Math.Round(stap * 100);
            int x = 14, gap = 10;
            double half = (mf.tool.width - mf.tool.overlap) * 0.5;

            Knop(x, 140, "½ werktuig", "arrow-big-left_licht_64", () => mf.trk.NudgeTrack(-half)); x += 140 + gap;
            Knop(x, 110, cm + " cm", "chevron-left_licht_64", () => mf.trk.NudgeTrack(-stap)); x += 110 + gap;

            lblOffset = new Label
            {
                Bounds = new Rectangle(x, 14, 160, Height - 28),
                Font = JohanTheme.Font(18F, FontStyle.Bold),
                ForeColor = JohanTheme.Text,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            Controls.Add(lblOffset); x += 160 + gap;

            Knop(x, 110, cm + " cm", "chevron-right_licht_64", () => mf.trk.NudgeTrack(stap)); x += 110 + gap;
            Knop(x, 140, "½ werktuig", "arrow-big-right_licht_64", () => mf.trk.NudgeTrack(half)); x += 140 + 2 * gap;

            Knop(x, 150, "Naar trekker", "focus-2_licht_64", () => mf.trk.SnapToPivot()); x += 150 + gap;
            Knop(x, 100, "Op nul", "refresh_licht_64", () => mf.trk.NudgeDistanceReset()); x += 100 + gap;

            var klaar = Knop(Width - 14 - 110, 110, "Klaar", "check_licht_64", () => Close());
            klaar.BackColor = JohanTheme.Green;
            klaar.ForeColor = Color.White;
        }

        private void UpdateLabel()
        {
            if (mf.trk.idx < 0 || mf.trk.idx >= mf.trk.gArr.Count) { lblOffset.Text = "geen lijn"; return; }
            double d = mf.trk.gArr[mf.trk.idx].nudgeDistance * 100.0;
            int cm = (int)Math.Round(Math.Abs(d));
            lblOffset.Text = cm == 0 ? "0 cm" : d < 0 ? "◀ " + cm + " cm" : cm + " cm ▶";
        }
    }
}
