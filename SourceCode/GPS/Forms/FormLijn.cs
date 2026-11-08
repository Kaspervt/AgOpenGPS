using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using AgLibrary.Johan;
using AgLibrary.Logging;

namespace AgOpenGPS
{
    // Johan fork: "Lijn maken" as one bar: drive, tap A, drive on, tap B. Same track record as FormQuickAB.
    public class FormLijn : Form
    {
        private readonly FormGPS mf;
        private Label lblStatus;
        private JohanRoundButton btnA, btnB;
        private vec2 ptA;
        private bool aGezet;

        private static int S(int px) => JohanTheme.S(px);

        public FormLijn(FormGPS formGPS)
        {
            mf = formGPS;
            Name = "FormLijn";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            BackColor = JohanTheme.Bar;
            ForeColor = JohanTheme.Text;
            Font = JohanTheme.Font(12F);
            ShowInTaskbar = false;

            var scr = Screen.FromControl(mf).Bounds;
            int h = S(96), w = Math.Min(S(1000), scr.Width - S(32));
            Bounds = new Rectangle(scr.Left + (scr.Width - w) / 2, scr.Bottom - S(132) - S(12) - h, w, h);
            BuildLayout();
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
            b.Click += (s, e) => { actie(); mf.Activate(); };
            Controls.Add(b);
            return b;
        }

        private void BuildLayout()
        {
            int x = S(14), gap = S(10);
            lblStatus = new Label
            {
                Bounds = new Rectangle(x, S(14), S(380), Height - S(28)),
                Font = JohanTheme.Font(13F),
                ForeColor = JohanTheme.Text,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Text = "Rij het begin van de werkgang en zet punt A"
            };
            Controls.Add(lblStatus); x += S(380) + gap;

            btnA = Knop(x, S(150), "Punt A", "circle-letter-a_licht_64", () =>
            {
                ptA = new vec2(mf.pivotAxlePos.easting, mf.pivotAxlePos.northing);
                aGezet = true;
                btnA.BackColor = JohanTheme.Green; btnA.ForeColor = Color.White;
                btnB.Enabled = true;
                lblStatus.Text = "Rij minstens 20 m rechtdoor en zet punt B";
            }); x += S(150) + gap;

            btnB = Knop(x, S(150), "Punt B", "circle-letter-b_licht_64", MaakLijn);
            btnB.Enabled = false; x += S(150) + 2 * gap;

            var annuleer = Knop(Width - S(14) - S(130), S(130), "Annuleer", "x_licht_64", Close);
        }

        private void MaakLijn()
        {
            if (!aGezet) return;
            var ptB = new vec2(mf.pivotAxlePos.easting, mf.pivotAxlePos.northing);
            double afstand = Math.Sqrt((ptB.easting - ptA.easting) * (ptB.easting - ptA.easting) + (ptB.northing - ptA.northing) * (ptB.northing - ptA.northing));
            if (afstand < 5)
            {
                lblStatus.Text = "Te dicht bij A, rij eerst verder";
                return;
            }
            double heading = Math.Atan2(ptB.easting - ptA.easting, ptB.northing - ptA.northing);
            if (heading < 0) heading += glm.twoPI;

            var track = new CTrk
            {
                ptA = ptA,
                ptB = ptB,
                mode = TrackMode.AB,
                heading = heading,
                isVisible = true,
                name = "AB " + Math.Round(glm.toDegrees(heading), 1).ToString(CultureInfo.InvariantCulture) + "°"
            };
            mf.trk.gArr.Add(track);
            mf.trk.idx = mf.trk.gArr.Count - 1;
            // AgOpenGPS treats the reference line as the edge between passes: shift it half a
            // working width, so the driven track becomes the centre of the first pass (as FormQuickAB does)
            mf.trk.NudgeRefABLine((mf.tool.width - mf.tool.overlap) * -0.5 + mf.tool.offset);
            mf.ABLine.isABValid = false;
            mf.ABLine.isMakingABLine = false;
            mf.FileSaveTracks();
            Log.EventWriter("Johan flow: AB line made: " + track.name);
            mf.TimedMessageBox(2000, "Lijn", "Lijn " + track.name + " staat");
            Close();
        }
    }
}
