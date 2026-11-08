using System;
using System.Drawing;
using System.Windows.Forms;
using AgLibrary.Johan;
using AgLibrary.Logging;

namespace AgOpenGPS
{
    // Johan fork: "Grens rijden" as one bar above the bottom buttons. Same flags and the same
    // boundary building as FormBoundaryPlayer; the points are added in the position loop.
    public class FormGrens : Form
    {
        private readonly FormGPS mf;
        private readonly Timer tick = new Timer { Interval = 500 };
        private Label lblStatus;
        private JohanRoundButton btnStart, btnKant, btnKlaar;
        private bool opnemen;

        private static int S(int px) => JohanTheme.S(px);

        public FormGrens(FormGPS formGPS)
        {
            mf = formGPS;
            Name = "FormGrens";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            BackColor = JohanTheme.Bar;
            ForeColor = JohanTheme.Text;
            Font = JohanTheme.Font(12F);
            ShowInTaskbar = false;

            var scr = Screen.FromControl(mf).Bounds;
            int h = S(96), w = Math.Min(S(1200), scr.Width - S(32));
            Bounds = new Rectangle(scr.Left + (scr.Width - w) / 2, scr.Bottom - S(132) - S(12) - h, w, h);

            BuildLayout();
            tick.Tick += (s, e) => UpdateStatus();
            Load += (s, e) =>
            {
                // recording at the outer edge of the tool, nothing added until Start
                mf.bnd.isDrawAtPivot = false;
                mf.bnd.createBndOffset = mf.tool.width * 0.5;
                mf.bnd.isBndBeingMade = true;
                mf.bnd.isOkToAddPoints = false;
                mf.bnd.bndBeingMadePts.Clear();
                UpdateStatus();
                tick.Start();
            };
            FormClosed += (s, e) => tick.Stop();
        }

        private JohanRoundButton Knop(int x, int wdt, string tekst, string icon, Action actie)
        {
            var b = new JohanRoundButton
            {
                Bounds = new Rectangle(x, S(14), wdt, Height - S(28)),
                Text = tekst,
                BackColor = JohanTheme.Panel,
                ForeColor = JohanTheme.Text,
                Font = JohanTheme.Font(11F, FontStyle.Bold),
                Image = icon == null ? null : JohanSkin.Icon(icon, 26),
                TextImageRelation = TextImageRelation.ImageBeforeText
            };
            b.Click += (s, e) => { actie(); UpdateStatus(); mf.Activate(); };
            Controls.Add(b);
            return b;
        }

        private void BuildLayout()
        {
            int x = S(14), gap = S(10);
            lblStatus = new Label
            {
                Bounds = new Rectangle(x, S(14), S(330), Height - S(28)),
                Font = JohanTheme.Font(13F),
                ForeColor = JohanTheme.Text,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            Controls.Add(lblStatus); x += S(330) + gap;

            btnKant = Knop(x, S(170), "Rechterkant", "arrow-right_licht_64", () =>
            {
                mf.bnd.isDrawRightSide = !mf.bnd.isDrawRightSide;
            }); x += S(170) + gap;

            btnStart = Knop(x, S(150), "Start", "player-record_licht_64", () =>
            {
                opnemen = !opnemen;
                mf.bnd.isOkToAddPoints = opnemen;
            }); x += S(150) + gap;

            Knop(x, S(150), "Opnieuw", "refresh_licht_64", () =>
            {
                opnemen = false;
                mf.bnd.isOkToAddPoints = false;
                mf.bnd.bndBeingMadePts.Clear();
            }); x += S(150) + 2 * gap;

            Knop(x, S(130), "Annuleer", "x_licht_64", () =>
            {
                Stop();
                Close();
            }); x += S(130) + gap;

            btnKlaar = Knop(Width - S(14) - S(140), S(140), "Klaar", "check_licht_64", Klaar);
            btnKlaar.BackColor = JohanTheme.Green;
            btnKlaar.ForeColor = Color.White;
        }

        private void UpdateStatus()
        {
            int n = mf.bnd.bndBeingMadePts.Count;
            double ha = 0;
            if (n > 2)
            {
                double a = 0;
                var p = mf.bnd.bndBeingMadePts;
                for (int i = 0, j = n - 1; i < n; j = i++) a += (p[j].easting + p[i].easting) * (p[j].northing - p[i].northing);
                ha = Math.Abs(a) / 2 / 10000;
            }
            lblStatus.Text = opnemen
                ? "Grens wordt opgenomen: " + n + " punten, " + ha.ToString("0.00") + " ha"
                : n == 0 ? "Rij naar de rand van het perceel en druk op Start" : "Pauze: " + n + " punten, " + ha.ToString("0.00") + " ha";
            btnStart.Text = opnemen ? "Pauze" : (n == 0 ? "Start" : "Verder");
            btnStart.BackColor = opnemen ? JohanTheme.Amber : JohanTheme.Panel;
            btnStart.ForeColor = opnemen ? Color.White : JohanTheme.Text;
            btnKant.Text = mf.bnd.isDrawRightSide ? "Rechterkant" : "Linkerkant";
            btnKant.Image = JohanSkin.Icon(mf.bnd.isDrawRightSide ? "arrow-right_licht_64" : "arrow-left_licht_64", 26);
            btnKlaar.Enabled = n > 2;
        }

        private void Stop()
        {
            opnemen = false;
            mf.bnd.isOkToAddPoints = false;
            mf.bnd.isBndBeingMade = false;
            mf.bnd.bndBeingMadePts.Clear();
        }

        private void Klaar()
        {
            if (mf.bnd.bndBeingMadePts.Count <= 2) return;
            try
            {
                var nieuw = new CBoundaryList();
                foreach (var p in mf.bnd.bndBeingMadePts) nieuw.fenceLine.Add(p);
                nieuw.CalculateFenceArea(mf.bnd.bndList.Count);
                nieuw.FixFenceLine(mf.bnd.bndList.Count);
                mf.bnd.bndList.Add(nieuw);
                mf.fd.UpdateFieldBoundaryGUIAreas();
                mf.CalculateMinMax();
                mf.FileSaveBoundary();
                mf.bnd.BuildTurnLines();
                Log.EventWriter("Johan flow: driven boundary saved, " + mf.bnd.bndBeingMadePts.Count + " points");
                mf.TimedMessageBox(2000, "Grens", "Grens bewaard");
            }
            catch (Exception ex)
            {
                Log.EventWriter("Johan flow: boundary failed: " + ex.Message);
                mf.TimedMessageBox(3000, "Grens", "Grens kon niet worden bewaard");
            }
            Stop();
            Close();
        }
    }
}
