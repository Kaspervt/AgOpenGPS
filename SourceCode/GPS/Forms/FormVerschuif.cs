using System;
using AgLibrary.Johan;
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
        private Button btnSlot;
        private readonly System.Collections.Generic.List<Button> schuifKnoppen = new System.Collections.Generic.List<Button>();
        private double stap;   // metres, the configured snap distance (coarse step)
        private double fijn;   // metres, the small step (5 cm, or 1 cm when snap is already 5 cm)
        private static int S(int px) => JohanTheme.S(px);

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
            int h = S(96), w = Math.Min(S(1310), scr.Width - S(32));
            Bounds = new Rectangle(scr.Left + (scr.Width - w) / 2, scr.Bottom - S(132) - S(12) - h, w, h);

            stap = ToolSettings.Default.setAS_snapDistance * 0.01;
            if (stap <= 0) stap = 0.20;
            // a small step next to the configured one: sometimes you only want a nudge
            if (Math.Abs(stap - 0.05) < 0.001) fijn = 0.01;                 // snap is 5 cm: fine step 1 cm
            else if (stap < 0.05) { fijn = stap; stap = 0.20; }              // snap finer than 5 cm: that is the fine step
            else fijn = 0.05;
            BuildLayout();

            tick.Tick += (s, e) => UpdateLabel();
            Load += (s, e) => { UpdateLabel(); tick.Start(); };
            FormClosed += (s, e) => { tick.Stop(); try { mf.FileSaveTracks(); } catch { } };
        }

        private Button Knop(int x, int wdt, string tekst, string icon, Action actie)
        {
            var b = new JohanRoundButton
            {
                Bounds = new Rectangle(x, S(14), wdt, Height - S(28)),
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
            b.Click += (s, e) => { if (schuifKnoppen.Contains(b) && mf.JohanIsTrackLocked(mf.trk.idx)) { mf.TimedMessageBox(1500, "Lijn", "Deze lijn is vergrendeld"); return; } actie(); UpdateLabel(); mf.Activate(); };
            Controls.Add(b);
            return b;
        }

        private void BuildLayout()
        {
            string grof = (int)Math.Round(stap * 100) + " cm";
            string klein = (int)Math.Round(fijn * 100) + " cm";
            int x = S(14), gap = S(10);
            double half = (mf.tool.width - mf.tool.overlap) * 0.5;

            // from the outside in: half a tool, coarse step, fine step, offset, fine, coarse, half
            schuifKnoppen.Add(Knop(x, S(124), "½ werktuig", "arrow-big-left_licht_64", () => mf.trk.NudgeTrack(-half))); x += S(124) + gap;
            schuifKnoppen.Add(Knop(x, S(96), grof, "chevrons-left_licht_64", () => mf.trk.NudgeTrack(-stap))); x += S(96) + gap;
            schuifKnoppen.Add(Knop(x, S(84), klein, "chevron-left_licht_64", () => mf.trk.NudgeTrack(-fijn))); x += S(84) + gap;

            lblOffset = new Label
            {
                Bounds = new Rectangle(x, S(14), S(130), Height - S(28)),
                Font = JohanTheme.Font(18F, FontStyle.Bold),
                ForeColor = JohanTheme.Text,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            Controls.Add(lblOffset); x += S(130) + gap;

            schuifKnoppen.Add(Knop(x, S(84), klein, "chevron-right_licht_64", () => mf.trk.NudgeTrack(fijn))); x += S(84) + gap;
            schuifKnoppen.Add(Knop(x, S(96), grof, "chevrons-right_licht_64", () => mf.trk.NudgeTrack(stap))); x += S(96) + gap;
            schuifKnoppen.Add(Knop(x, S(124), "½ werktuig", "arrow-big-right_licht_64", () => mf.trk.NudgeTrack(half))); x += S(124) + 2 * gap;

            schuifKnoppen.Add(Knop(x, S(136), "Naar trekker", "focus-2_licht_64", () => mf.trk.SnapToPivot())); x += S(136) + gap;
            schuifKnoppen.Add(Knop(x, S(92), "Op nul", "refresh_licht_64", () => mf.trk.NudgeDistanceReset())); x += S(92) + gap;

            btnSlot = Knop(Width - S(14) - S(96) - S(10) - S(96), S(96), "Slot", "adjustments-horizontal_licht_64", () =>
            {
                // locking and unlocking a line needs the code (once per session)
                if (!mf.JohanCodeVragen(this, btnSlot)) return;
                bool nu = !mf.JohanIsTrackLocked(mf.trk.idx);
                mf.JohanSetTrackLocked(mf.trk.idx, nu);
                mf.TimedMessageBox(1500, "Lijn", nu ? "Lijn vergrendeld: verschuiven uit" : "Lijn vrijgegeven");
            });
            var klaar = Knop(Width - S(14) - S(96), S(96), "Klaar", "check_licht_64", () => Close());
            klaar.BackColor = JohanTheme.Green;
            klaar.ForeColor = Color.White;
        }

        private void UpdateLabel()
        {
            if (mf.trk.idx < 0 || mf.trk.idx >= mf.trk.gArr.Count) { lblOffset.Text = "geen lijn"; return; }
            double d = mf.trk.gArr[mf.trk.idx].nudgeDistance * 100.0;
            int cm = (int)Math.Round(Math.Abs(d));
            lblOffset.Text = cm == 0 ? "0 cm" : d < 0 ? "◀ " + cm + " cm" : cm + " cm ▶";
            bool slot = mf.JohanIsTrackLocked(mf.trk.idx);
            if (btnSlot != null)
            {
                btnSlot.Text = slot ? "Vast" : "Slot";
                btnSlot.Image = JohanSkin.Icon(slot ? "hand-stop_licht_64" : "adjustments-horizontal_licht_64", 26);
                btnSlot.BackColor = slot ? JohanTheme.Amber : JohanTheme.Panel;
            }
            foreach (var b in schuifKnoppen) b.Enabled = !slot;
        }
    }
}
