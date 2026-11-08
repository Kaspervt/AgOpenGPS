using System;
using AgLibrary.Johan;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AgLibrary.Logging;
using AgOpenGPS.Properties;

namespace AgOpenGPS
{
    // Johan fork: the live steering test and the wiring of the steer board, as one page in the
    // canvas look. Replaces the AgOpenGPS autosteer window (FormSteer) for Johan: wheel angle wanted
    // and actual, free steering left/right, zero the wheel angle, measure the steer angle by driving
    // a circle, and the board choices (sensor, motor driver, inversions, Danfoss, how autosteer is
    // switched on). The board choices are sent with the same PGN 251 as FormSteer does.
    public class FormStuurtest : Form
    {
        private readonly FormGPS mf;
        private readonly Timer tick = new Timer { Interval = 100 };
        private Panel meter;
        private Label lblGewenst, lblWerkelijk, lblVerschil, lblPwm, lblMeten;
        private JohanRoundButton btnVrij, btnLinks, btnRechts, btnMeten;

        // steer angle measurement by driving a circle (as FormSteer does)
        private bool meten;
        private vec3 startFix;
        private double diameter;
        private int stil;

        // board choices, edited here and sent together
        private int sensor;        // 0 none, 1 encoder, 2 pressure, 3 current
        private int drempel;       // 0..255 max pulse counts / threshold
        private bool cytron, enkel, wasOm, motorOm, relaisOm, danfoss;
        private int inschakelen;   // 0 button, 1 switch, 2 none
        private bool gewijzigd;
        private readonly System.Collections.Generic.List<Action> verversers = new System.Collections.Generic.List<Action>();

        private static int S(int px) => JohanTheme.S(px);

        public FormStuurtest(FormGPS formGPS)
        {
            mf = formGPS;
            Name = "FormStuurtest";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            BackColor = JohanTheme.Bg;
            ForeColor = JohanTheme.Text;
            Font = JohanTheme.Font(12F);
            ShowInTaskbar = false;
            KeyPreview = true;
            Bounds = Screen.FromControl(mf).Bounds;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
            LeesBord();
            BuildLayout();
            tick.Tick += (s, e) => { try { Tik(); } catch (Exception ex) { Log.EventWriter("Stuurtest: " + ex.Message); } };
            Load += (s, e) => tick.Start();
            FormClosed += (s, e) =>
            {
                tick.Stop(); tick.Dispose();
                // never leave the tractor in free steering
                mf.vehicle.isInFreeDriveMode = false;
                mf.vehicle.driveFreeSteerAngle = 0;
            };
        }

        // ---- layout ------------------------------------------------------------------------------

        private Panel Kaart(Rectangle r, string titel, string uitleg)
        {
            var k = new Panel { Bounds = r, BackColor = JohanTheme.Panel };
            k.Paint += (s, e) =>
            {
                using (var path = JohanRoundButton.Rounded(new Rectangle(0, 0, k.Width - 1, k.Height - 1), S(16)))
                using (var b = new SolidBrush(JohanTheme.Panel))
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.Clear(JohanTheme.Bg);
                    e.Graphics.FillPath(b, path);
                }
            };
            k.Controls.Add(new Label { Text = titel, Font = JohanTheme.Font(18F, FontStyle.Bold), AutoSize = true, Location = new Point(S(20), S(16)), BackColor = Color.Transparent });
            k.Controls.Add(new Label { Text = uitleg, Font = JohanTheme.Font(10.5F), ForeColor = JohanTheme.Muted, AutoSize = true, Location = new Point(S(22), S(52)), BackColor = Color.Transparent });
            Controls.Add(k);
            return k;
        }

        private JohanRoundButton Knop(Control ouder, Rectangle r, string tekst, Action actie, bool groot = false)
        {
            var b = new JohanRoundButton
            {
                Bounds = r,
                Text = tekst,
                BackColor = JohanTheme.PanelLight,
                ForeColor = JohanTheme.Text,
                FlatStyle = FlatStyle.Flat,
                Font = JohanTheme.Font(groot ? 16F : 13F, FontStyle.Bold),
                Radius = S(10)
            };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (s, e) => { actie(); Ververs(); };
            ouder.Controls.Add(b);
            return b;
        }

        private Label Waarde(Control ouder, int x, int y, int w, string kop)
        {
            ouder.Controls.Add(new Label { Text = kop, Font = JohanTheme.Font(11F), ForeColor = JohanTheme.Muted, AutoSize = false, Bounds = new Rectangle(x, y, w, S(24)), TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent });
            var l = new Label { Text = "–", Font = JohanTheme.Font(26F, FontStyle.Bold), ForeColor = JohanTheme.Text, AutoSize = false, Bounds = new Rectangle(x, y + S(24), w, S(48)), TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent };
            ouder.Controls.Add(l);
            return l;
        }

        // one row with a label left and a choice or on/off right, inside the board card
        private void Rij(Panel k, ref int y, string label, string[] opties, Func<int> lees, Action<int> schrijf, string hint = null)
        {
            k.Controls.Add(new Label { Text = label, Font = JohanTheme.Font(14F), AutoSize = false, Bounds = new Rectangle(S(20), y, S(300), hint == null ? S(52) : S(30)), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent });
            if (hint != null) k.Controls.Add(new Label { Text = hint, Font = JohanTheme.Font(10F), ForeColor = JohanTheme.Muted, AutoSize = false, Bounds = new Rectangle(S(20), y + S(28), S(300), S(22)), BackColor = Color.Transparent });
            int x = S(330);
            var knoppen = new System.Collections.Generic.List<JohanRoundButton>();
            for (int i = 0; i < opties.Length; i++)
            {
                int n = i;
                int w = TextRenderer.MeasureText(opties[i], JohanTheme.Font(13F, FontStyle.Bold)).Width + S(28);
                knoppen.Add(Knop(k, new Rectangle(x, y + S(2), w, S(48)), opties[i], () => { schrijf(n); gewijzigd = true; }));
                x += w + S(8);
            }
            verversers.Add(() => { int cur = lees(); for (int i = 0; i < knoppen.Count; i++) { bool aan = i == cur; knoppen[i].BackColor = aan ? JohanTheme.Blue : JohanTheme.PanelLight; knoppen[i].ForeColor = aan ? Color.White : JohanTheme.Text; } });
            y += S(62);
        }

        private void BuildLayout()
        {
            int x = S(40);
            Controls.Add(new Label { Text = "Stuurtest en bedrading", Font = JohanTheme.Font(28F, FontStyle.Bold), AutoSize = true, Location = new Point(x, S(28)) });
            Controls.Add(new Label { Text = "Links: kijken en proberen terwijl de trekker stilstaat of rustig rijdt. Rechts: hoe de stuurprint is aangesloten; verstuur na een wijziging.", Font = JohanTheme.Font(11F), ForeColor = JohanTheme.Muted, AutoSize = true, Location = new Point(x + S(2), S(80)) });

            int top = S(120), h = Height - top - S(110);
            int linksW = (Width - 2 * x - S(20)) / 2;
            var kt = Kaart(new Rectangle(x, top, linksW, h), "Stuurtest", "Gewenste en werkelijke wielhoek, live van de stuurprint");
            var kb = Kaart(new Rectangle(x + linksW + S(20), top, linksW, h), "Stuurprint", "Sensor, motor en omkeringen; gaat met één knop naar de print");

            // --- meter: a bar from the centre to the actual angle, a marker for the wanted angle
            meter = new Panel { Bounds = new Rectangle(S(20), S(92), kt.Width - S(40), S(64)), BackColor = JohanTheme.Bg };
            meter.Paint += MeterPaint;
            kt.Controls.Add(meter);
            kt.Controls.Add(new Label { Text = "links  40°", Font = JohanTheme.Font(10F), ForeColor = JohanTheme.Muted, AutoSize = true, Location = new Point(S(20), S(160)), BackColor = Color.Transparent });
            var r = new Label { Text = "40°  rechts", Font = JohanTheme.Font(10F), ForeColor = JohanTheme.Muted, AutoSize = true, BackColor = Color.Transparent };
            r.Location = new Point(kt.Width - S(20) - r.PreferredWidth, S(160));
            kt.Controls.Add(r);

            int vw = (kt.Width - S(40)) / 4, vy = S(196);
            lblGewenst = Waarde(kt, S(20), vy, vw, "Gewenst");
            lblWerkelijk = Waarde(kt, S(20) + vw, vy, vw, "Werkelijk");
            lblVerschil = Waarde(kt, S(20) + 2 * vw, vy, vw, "Verschil");
            lblPwm = Waarde(kt, S(20) + 3 * vw, vy, vw, "Motor (PWM)");

            // --- free steering: on/off, left, straight, right
            int by = S(300), bh = S(64);
            kt.Controls.Add(new Label { Text = "Vrij sturen: de print stuurt naar de hoek die je hier kiest, zonder lijn. Alleen stilstaand of heel rustig.", Font = JohanTheme.Font(10.5F), ForeColor = JohanTheme.Muted, AutoSize = false, Bounds = new Rectangle(S(20), by - S(28), kt.Width - S(40), S(24)), BackColor = Color.Transparent });
            btnVrij = Knop(kt, new Rectangle(S(20), by, S(170), bh), "Vrij sturen", () =>
            {
                bool aan = !mf.vehicle.isInFreeDriveMode;
                mf.vehicle.isInFreeDriveMode = aan;
                mf.vehicle.driveFreeSteerAngle = 0;
            });
            btnLinks = Knop(kt, new Rectangle(S(20) + S(182), by, S(110), bh), "←  5°", () => { if (mf.vehicle.isInFreeDriveMode) mf.vehicle.driveFreeSteerAngle = Math.Max(-40, mf.vehicle.driveFreeSteerAngle - 5); }, true);
            var recht = Knop(kt, new Rectangle(S(20) + S(300), by, S(110), bh), "Recht", () => { if (mf.vehicle.isInFreeDriveMode) mf.vehicle.driveFreeSteerAngle = 0; });
            btnRechts = Knop(kt, new Rectangle(S(20) + S(418), by, S(110), bh), "5°  →", () => { if (mf.vehicle.isInFreeDriveMode) mf.vehicle.driveFreeSteerAngle = Math.Min(40, mf.vehicle.driveFreeSteerAngle + 5); }, true);

            // --- zero the wheel angle sensor, as FormSteer btnZeroWAS does
            int zy = by + bh + S(40);
            kt.Controls.Add(new Label { Text = "Wielen recht zetten en dan nulstellen: die stand is voortaan recht vooruit.", Font = JohanTheme.Font(10.5F), ForeColor = JohanTheme.Muted, AutoSize = false, Bounds = new Rectangle(S(20), zy - S(28), kt.Width - S(40), S(24)), BackColor = Color.Transparent });
            Knop(kt, new Rectangle(S(20), zy, S(260), bh), "Wielhoek nulstellen", () =>
            {
                var v = VehicleSettings.Default;
                int offset = (int)(v.setAS_countsPerDegree * -mf.mc.actualSteerAngleDegrees + v.setAS_wasOffset);
                if (Math.Abs(offset) > 3900) { mf.TimedMessageBox(3000, "Wielhoek nulstellen", "De wielen staan te ver gedraaid. Zet ze eerst recht."); return; }
                v.setAS_wasOffset += (int)(v.setAS_countsPerDegree * -mf.mc.actualSteerAngleDegrees);
                v.Save();
                mf.LoadSettings();
                mf.SendSettings();
                mf.TimedMessageBox(2000, "Wielhoek nulstellen", "Deze stand is nu recht vooruit");
            });

            // --- measure the steer angle: drive a full circle at a fixed wheel angle
            int my = zy + bh + S(40);
            kt.Controls.Add(new Label { Text = "Stuurhoek meten: stuur vol in, rij een hele cirkel; de diameter geeft de echte hoek.", Font = JohanTheme.Font(10.5F), ForeColor = JohanTheme.Muted, AutoSize = false, Bounds = new Rectangle(S(20), my - S(28), kt.Width - S(40), S(24)), BackColor = Color.Transparent });
            btnMeten = Knop(kt, new Rectangle(S(20), my, S(200), bh), "Start meting", () =>
            {
                meten = !meten;
                if (meten) { startFix = mf.pivotAxlePos; diameter = 0; stil = 0; }
            });
            lblMeten = new Label { Text = "", Font = JohanTheme.Font(14F, FontStyle.Bold), AutoSize = false, Bounds = new Rectangle(S(20) + S(212), my, kt.Width - S(40) - S(212), bh), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent };
            kt.Controls.Add(lblMeten);

            // --- board card
            int y = S(92);
            Rij(kb, ref y, "Draaisensor stuurwiel", new[] { "Geen", "Encoder", "Druk", "Stroom" }, () => sensor, i => sensor = i, "Merkt dat Johan het stuur vastpakt");
            Rij(kb, ref y, "Drempel draaisensor", new[] { "−", "+" }, () => -1, i => drempel = Math.Max(0, Math.Min(255, drempel + (i == 0 ? -13 : 13))), "In procenten, hoger is minder gevoelig");
            var lblDrempel = new Label { Font = JohanTheme.Font(14F, FontStyle.Bold), AutoSize = false, Bounds = new Rectangle(S(330) + S(120), y - S(62) + S(2), S(90), S(48)), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent };
            kb.Controls.Add(lblDrempel);
            verversers.Add(() => lblDrempel.Text = ((int)(drempel * 0.3921568627)).ToString() + " %");
            Rij(kb, ref y, "Motor driver", new[] { "Cytron", "IBT2" }, () => cytron ? 0 : 1, i => cytron = i == 0);
            Rij(kb, ref y, "Autosturen inschakelen met", new[] { "Knop", "Schakelaar", "Geen" }, () => inschakelen, i => inschakelen = i, "De knop op de joystick of een schakelaar op de print");
            Rij(kb, ref y, "Wielhoeksensor", new[] { "Differentieel", "Enkel" }, () => enkel ? 1 : 0, i => enkel = i == 1, "Hoe de sensor op de print zit (A2D)");
            Rij(kb, ref y, "Wielhoeksensor omkeren", new[] { "Uit", "Aan" }, () => wasOm ? 1 : 0, i => wasOm = i == 1, "Als links en rechts verwisseld zijn");
            Rij(kb, ref y, "Motor omkeren", new[] { "Uit", "Aan" }, () => motorOm ? 1 : 0, i => motorOm = i == 1, "Als de motor de verkeerde kant op draait");
            Rij(kb, ref y, "Relais omkeren", new[] { "Uit", "Aan" }, () => relaisOm ? 1 : 0, i => relaisOm = i == 1);
            Rij(kb, ref y, "Danfoss-ventiel", new[] { "Uit", "Aan" }, () => danfoss ? 1 : 0, i => danfoss = i == 1, "Alleen bij een hydraulisch Danfoss-ventiel");

            var verstuur = Knop(kb, new Rectangle(S(20), kb.Height - S(20) - S(64), kb.Width - S(40), S(64)), "Verstuur naar de stuurprint en bewaar", VerstuurBord, true);
            verstuur.BackColor = JohanTheme.Green; verstuur.ForeColor = Color.White;
            verversers.Add(() => { verstuur.BackColor = gewijzigd ? JohanTheme.Green : JohanTheme.PanelLight; verstuur.ForeColor = gewijzigd ? Color.White : JohanTheme.Text; });

            var klaar = new JohanRoundButton
            {
                Text = "Klaar",
                Image = JohanSkin.Icon("check_licht_64", 28),
                Bounds = new Rectangle(Width - x - S(200), Height - S(88), S(200), S(64)),
                BackColor = JohanTheme.Green,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = JohanTheme.Font(14F, FontStyle.Bold)
            };
            klaar.FlatAppearance.BorderSize = 0;
            klaar.Click += (s, e) =>
            {
                if (gewijzigd) { mf.TimedMessageBox(2500, "Stuurprint", "De printinstellingen zijn nog niet verstuurd"); gewijzigd = false; Ververs(); return; }
                Close();
            };
            Controls.Add(klaar);
            Ververs();
        }

        private void MeterPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(JohanTheme.Panel);
            using (var path = JohanRoundButton.Rounded(new Rectangle(0, 0, meter.Width - 1, meter.Height - 1), S(12)))
            using (var b = new SolidBrush(JohanTheme.Bg)) g.FillPath(b, path);
            int mid = meter.Width / 2, h = meter.Height;
            double schaal = (meter.Width / 2.0 - S(8)) / 40.0;
            double act = Math.Max(-40, Math.Min(40, mf.mc.actualSteerAngleDegrees));
            int ax = (int)(act * schaal);
            using (var b = new SolidBrush(JohanTheme.Blue))
                g.FillRectangle(b, ax < 0 ? mid + ax : mid, S(10), Math.Max(2, Math.Abs(ax)), h - S(20));
            using (var p = new Pen(JohanTheme.Text, 2)) g.DrawLine(p, mid, S(4), mid, h - S(4));
            double set = Math.Max(-40, Math.Min(40, mf.guidanceLineSteerAngle * 0.01));
            int sx = mid + (int)(set * schaal);
            using (var p = new Pen(JohanTheme.Amber, 3)) g.DrawLine(p, sx, S(2), sx, h - S(2));
        }

        private void Tik()
        {
            meter.Invalidate();
            double act = mf.mc.actualSteerAngleDegrees, set = mf.guidanceLineSteerAngle * 0.01;
            lblGewenst.Text = Komma(set, 1) + "°";
            lblWerkelijk.Text = Komma(act, 1) + "°";
            double err = act - set;
            lblVerschil.Text = Komma(Math.Abs(err), 1) + "°";
            lblVerschil.ForeColor = Math.Abs(err) < 1 ? JohanTheme.Green : Math.Abs(err) < 3 ? JohanTheme.Amber : JohanTheme.Red;
            lblPwm.Text = mf.mc.pwmDisplay.ToString();

            bool vrij = mf.vehicle.isInFreeDriveMode;
            btnVrij.BackColor = vrij ? JohanTheme.Green : JohanTheme.PanelLight;
            btnVrij.ForeColor = vrij ? Color.White : JohanTheme.Text;
            btnLinks.Enabled = vrij; btnRechts.Enabled = vrij;

            if (meten)
            {
                double d = glm.Distance(startFix, mf.pivotAxlePos);
                if (d > diameter) { diameter = d; stil = 0; } else stil++;
                btnMeten.Text = "Stop meting";
                btnMeten.BackColor = JohanTheme.Amber; btnMeten.ForeColor = Color.White;
                if (stil > 90 && diameter > 1)
                {
                    // half a circle is done: the diameter stopped growing (same maths as FormSteer)
                    double hoek = glm.toDegrees(Math.Atan(mf.vehicle.VehicleConfig.Wheelbase / ((diameter - mf.vehicle.VehicleConfig.TrackWidth * 0.5) / 2)));
                    lblMeten.Text = "Diameter " + Komma(diameter, 1) + " m, binnenwiel " + Komma(hoek, 1) + "°";
                    meten = false;
                }
                else lblMeten.Text = "Diameter tot nu " + Komma(diameter, 1) + " m, rij door";
            }
            else
            {
                btnMeten.Text = "Start meting";
                btnMeten.BackColor = JohanTheme.PanelLight; btnMeten.ForeColor = JohanTheme.Text;
            }
        }

        private static string Komma(double v, int dec) => v.ToString("F" + dec, System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');

        private void Ververs() { foreach (var v in verversers) { try { v(); } catch { } } }

        // ---- board settings, the same bits as FormSteer reads and writes -------------------------

        private void LeesBord()
        {
            int s0 = VehicleSettings.Default.setArdSteer_setting0, s1 = VehicleSettings.Default.setArdSteer_setting1;
            wasOm = (s0 & 1) != 0; relaisOm = (s0 & 2) != 0; motorOm = (s0 & 4) != 0; enkel = (s0 & 8) != 0; cytron = (s0 & 16) != 0;
            inschakelen = (s0 & 32) == 32 ? 1 : (s0 & 64) == 64 ? 0 : 2;
            bool encoder = (s0 & 128) != 0, druk = (s1 & 2) != 0, stroom = (s1 & 4) != 0;
            sensor = encoder ? 1 : druk ? 2 : stroom ? 3 : 0;
            danfoss = (s1 & 1) != 0;
            drempel = VehicleSettings.Default.setArdSteer_maxPulseCounts;
            gewijzigd = false;
        }

        private void VerstuurBord()
        {
            int s0 = 0;
            if (wasOm) s0 |= 1;
            if (relaisOm) s0 |= 2;
            if (motorOm) s0 |= 4;
            if (enkel) s0 |= 8;
            if (cytron) s0 |= 16;
            if (inschakelen == 1) s0 |= 32;
            if (inschakelen == 0) s0 |= 64;
            if (sensor == 1) s0 |= 128;
            int s1 = 0;
            if (danfoss) s1 |= 1;
            if (sensor == 2) s1 |= 2;
            if (sensor == 3) s1 |= 4;
            s1 |= VehicleSettings.Default.setArdSteer_setting1 & 8;   // IMU axis bit stays as it is

            var v = VehicleSettings.Default;
            v.setArdSteer_setting0 = (byte)s0;
            v.setArdSteer_setting1 = (byte)s1;
            v.setArdSteer_maxPulseCounts = (byte)drempel;
            v.setArdMac_isDanfoss = danfoss;
            v.Save();

            mf.p_251.pgn[mf.p_251.set0] = v.setArdSteer_setting0;
            mf.p_251.pgn[mf.p_251.set1] = v.setArdSteer_setting1;
            mf.p_251.pgn[mf.p_251.maxPulse] = v.setArdSteer_maxPulseCounts;
            mf.p_251.pgn[mf.p_251.minSpeed] = unchecked((byte)(v.setAS_minSteerSpeed * 10));
            mf.p_251.pgn[mf.p_251.angVel] = Settings.Default.setAS_isConstantContourOn ? (byte)1 : (byte)0;
            mf.SendPgnToLoop(mf.p_251.pgn);
            mf.LoadSettings();
            gewijzigd = false;
            Log.EventWriter("Stuurtest: steer board settings sent (PGN 251)");
            mf.TimedMessageBox(2000, "Stuurprint", "Instellingen verstuurd en bewaard");
        }
    }
}
