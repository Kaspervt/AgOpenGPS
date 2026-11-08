using System;
using AgLibrary.Johan;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using AgOpenGPS.Properties;

namespace AgOpenGPS
{
    // Johan fork: the "Beheer" page from the canvas. Opened by the long press.
    // Four cards: start-up (what the lock pins), remote (not set up yet), straight into
    // AgOpenGPS (the real dialogs, themed), and status. Nothing here changes the core.
    public class FormBeheer : Form
    {
        private readonly FormGPS mf;
        private readonly Timer tick = new Timer { Interval = 1000 };
        private Label lblStatus;
        private Label[] statusValues;

        public FormBeheer(FormGPS formGPS)
        {
            mf = formGPS;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = Screen.FromControl(mf).Bounds;
            BackColor = JohanTheme.Bg;
            ForeColor = JohanTheme.Text;
            Font = JohanTheme.Font(12F);
            KeyPreview = true;
            ShowInTaskbar = false;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
            BuildLayout();
            tick.Tick += (s, e) => UpdateStatus();
            Load += (s, e) => { UpdateStatus(); tick.Start(); };
            FormClosed += (s, e) => tick.Stop();
        }

        private Panel Card(Rectangle r, string titel)
        {
            var p = new Panel { Bounds = r, BackColor = JohanTheme.Panel, Padding = new Padding(24) };
            p.Controls.Add(new Label { Text = titel, Font = JohanTheme.Font(16F, FontStyle.Bold), ForeColor = JohanTheme.Text, AutoSize = true, Location = new Point(24, 20) });
            Controls.Add(p);
            return p;
        }

        private void Row(Panel card, int y, string tekst, bool aan, string sub = null)
        {
            card.Controls.Add(new Label { Text = tekst, Font = JohanTheme.Font(12.5F), ForeColor = JohanTheme.Text, AutoSize = true, Location = new Point(24, y) });
            var dot = new Label
            {
                Text = aan ? "aan" : "uit",
                Font = JohanTheme.Font(10F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = aan ? JohanTheme.Green : JohanTheme.Grey,
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds = new Rectangle(card.Width - 24 - 56, y, 56, 26)
            };
            card.Controls.Add(dot);
            if (sub != null)
                card.Controls.Add(new Label { Text = sub, Font = JohanTheme.Font(9.5F), ForeColor = JohanTheme.Muted, AutoSize = true, Location = new Point(24, y + 26) });
        }

        private Button Knop(Panel card, Rectangle r, string tekst, string icon, Action actie)
        {
            var b = new JohanRoundButton
            {
                Bounds = r,
                Text = "  " + tekst,
                BackColor = JohanTheme.Bg,
                ForeColor = JohanTheme.Text,
                FlatStyle = FlatStyle.Flat,
                Font = JohanTheme.Font(12F, FontStyle.Bold),
                Image = JohanSkin.Icon(icon, 26),
                ImageAlign = ContentAlignment.MiddleLeft,
                TextAlign = ContentAlignment.MiddleLeft,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                Padding = new Padding(14, 0, 0, 0)
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = JohanTheme.PanelLight;
            b.Click += (s, e) => { try { actie(); } catch (Exception ex) { mf.TimedMessageBox(3000, "Beheer", ex.Message); } };
            card.Controls.Add(b);
            return b;
        }

        private void BuildLayout()
        {
            int w = Width, h = Height, x = 56, top = 40;
            Controls.Add(new Label { Text = "Beheer", Font = JohanTheme.Font(26F, FontStyle.Bold), AutoSize = true, Location = new Point(x, top) });
            Controls.Add(new Label { Text = "Alleen voor Kasper. Bestaande AgOpenGPS-instellingen, hier bij elkaar gezet.", Font = JohanTheme.Font(11F), ForeColor = JohanTheme.Muted, AutoSize = true, Location = new Point(x + 2, top + 52) });

            int gridTop = top + 100, gap = 20;
            int cardW = (w - 2 * x - gap) / 2;
            int cardH = (h - gridTop - 120 - gap) / 2;

            // Opstarten
            var c1 = Card(new Rectangle(x, gridTop, cardW, cardH), "Opstarten");
            bool locked = JohanLock.IsLocked;
            Row(c1, 70, "Kioskstand: geen voorwaarden, AgIO mee, laatste perceel open", Settings.Default.setWindow_isKioskMode);
            Row(c1, 118, "Chauffeursmodus: alleen de rijknoppen", Settings.Default.setDisplay_isDriverMode);
            Row(c1, 166, "Startscherm met foto en vinkjes", Settings.Default.setDisplay_isStartScreen);
            Row(c1, 214, "Thema donker, iconen", Settings.Default.setDisplay_isJohanIcons,
                locked ? "Vastgezet door johan.lock naast het programma" : "Geen johan.lock: instellingen kunnen terugvallen");

            // Op afstand
            var c2 = Card(new Rectangle(x + cardW + gap, gridTop, cardW, cardH), "Op afstand");
            Row(c2, 70, "Tailscale aan (Kasper mag meekijken)", false, "Nog niet ingericht op deze tablet");
            Row(c2, 140, "Percelen synchroniseren met Lijnbeheer", false, "Nog niet ingericht; nu via ISO-XML-bestand");

            // Simulator (only on a laptop without GPS): drive, stop, steer, so the field flow can be tried indoors
            if (mf.timerSim.Enabled)
            {
                c2.Controls.Add(new Label { Text = "Simulator (geen GPS aangesloten)", Font = JohanTheme.Font(12F, FontStyle.Bold), ForeColor = JohanTheme.Text, AutoSize = true, Location = new Point(24, 200) });
                int sw = (cardW - 48 - 3 * 10) / 4, sy = 232;
                Knop(c2, new Rectangle(24, sy, sw, 52), "Rijden", "player-play_licht_64", () => { mf.sim.stepDistance = 0.15; });
                Knop(c2, new Rectangle(24 + sw + 10, sy, sw, 52), "Stop", "player-stop_licht_64", () => { mf.sim.stepDistance = 0; });
                Knop(c2, new Rectangle(24 + 2 * (sw + 10), sy, sw, 52), "Links", "arrow-left_licht_64", () => { mf.sim.steerAngleScrollBar = Math.Max(-30, mf.sim.steerAngleScrollBar - 8); });
                Knop(c2, new Rectangle(24 + 3 * (sw + 10), sy, sw, 52), "Rechts", "arrow-right_licht_64", () => { mf.sim.steerAngleScrollBar = Math.Min(30, mf.sim.steerAngleScrollBar + 8); });
                Knop(c2, new Rectangle(24, sy + 60, cardW - 48, 40), "Rechtuit", "arrow-bar-to-right_licht_64", () => { mf.sim.steerAngleScrollBar = 0; });
            }

            // Rechtstreeks naar AgOpenGPS
            var c3 = Card(new Rectangle(x, gridTop + cardH + gap, cardW, cardH), "Rechtstreeks naar AgOpenGPS");
            c3.Controls.Add(new Label { Text = "De volledige menu's, voor afstellen en storingen.", Font = JohanTheme.Font(10F), ForeColor = JohanTheme.Muted, AutoSize = true, Location = new Point(24, 54) });
            int bw = (cardW - 48 - 12) / 2, bh = 56, by = 92;
            Knop(c3, new Rectangle(24, by, bw, bh), "Configuratie", "settings_licht_64", () => mf.BeheerOpenConfig());
            Knop(c3, new Rectangle(24 + bw + 12, by, bw, bh), "Autostuur", "steering-wheel_licht_64", () => mf.BeheerOpenSteer());
            Knop(c3, new Rectangle(24, by + bh + 12, bw, bh), "AgIO", "plug_licht_64", () => mf.BeheerOpenAgIO());
            Knop(c3, new Rectangle(24 + bw + 12, by + bh + 12, bw, bh), "Logboek", "list_licht_64", () => mf.BeheerOpenLog());
            Knop(c3, new Rectangle(24, by + 2 * (bh + 12), bw, bh), "Originele indeling tonen", "layout-sidebar-right_licht_64", () => { mf.BeheerShowOriginal(); Close(); });
            Knop(c3, new Rectangle(24 + bw + 12, by + 2 * (bh + 12), bw, bh), "Perceel importeren (KML, ISO-XML)", "file-import_licht_64", () => { Close(); mf.BeginInvoke(new Action(() => mf.JohanImportMenu())); });

            // Status
            var c4 = Card(new Rectangle(x + cardW + gap, gridTop + cardH + gap, cardW, cardH), "Status");
            lblStatus = new Label { Font = JohanTheme.Font(12F), ForeColor = JohanTheme.Text, AutoSize = false, Bounds = new Rectangle(24, 64, cardW - 48, cardH - 80) };
            c4.Controls.Add(lblStatus);

            var terug = new JohanRoundButton
            {
                Text = "Terug naar het rijscherm",
                Bounds = new Rectangle(x, h - 96, 360, 64),
                BackColor = JohanTheme.Green,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = JohanTheme.Font(14F, FontStyle.Bold)
            };
            terug.FlatAppearance.BorderSize = 0;
            terug.Click += (s, e) => Close();
            Controls.Add(terug);

            // the only way out of the program in the Johan screen (the title bar is hidden)
            var afsluiten = new JohanRoundButton
            {
                Text = "Afsluiten",
                Image = JohanSkin.Icon("power_licht_64", 28),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                Bounds = new Rectangle(w - x - 240, h - 96, 240, 64),
                BackColor = Color.FromArgb(220, 38, 38),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = JohanTheme.Font(14F, FontStyle.Bold)
            };
            afsluiten.FlatAppearance.BorderSize = 0;
            afsluiten.Click += (s, e) => { using (var f = new FormAfsluiten(mf)) f.ShowDialog(this); if (mf.johanAfsluitKeuze >= 0) Close(); };
            Controls.Add(afsluiten);
        }

        private void UpdateStatus()
        {
            string pv = Application.ProductVersion;
            int plus = pv.IndexOf('+');
            if (plus > 0 && pv.Length > plus + 8) pv = pv.Substring(0, plus + 8);
            string versie = "AgOpenGPS " + pv + " (johan)";
            string trekker = RegistrySettings.vehicleProfileName + ", wielbasis " + VehicleSettings.Default.setVehicle_wheelbase.ToString("0.00") + " m";
            string werktuig = RegistrySettings.toolProfileName + ", " + mf.tool.width.ToString("0.0") + " m";
            int fq = mf.pn.fixQuality;
            bool gps = fq > 0 && mf.sentenceCounter < 20;
            string rtk = !gps ? "geen GPS" : fq == 4 ? "RTK fix, correctie " + mf.pn.age.ToString("0") + " s" : fq == 5 ? "RTK float" : fq == 8 ? "simulator" : "GPS zonder RTK";
            string print = gps && mf.steerModuleConnectedCounter < 30 ? "verbonden" : "niet verbonden";
            string perceel = mf.isJobStarted ? mf.currentFieldDirectory : "geen perceel open";
            string data = RegistrySettings.fieldsDirectory;
            // two columns: a Label has no tab stops
            var rows = new[]
            {
                new[] { "Versie", versie }, new[] { "Trekker", trekker }, new[] { "Werktuig", werktuig },
                new[] { "GPS", rtk }, new[] { "Stuurprint", print }, new[] { "Perceel", perceel }, new[] { "Percelenmap", data },
            };
            if (lblStatus.Tag == null)
            {
                lblStatus.Tag = rows.Length;
                var card = lblStatus.Parent;
                int y = lblStatus.Top;
                statusValues = new Label[rows.Length];
                for (int i = 0; i < rows.Length; i++)
                {
                    card.Controls.Add(new Label { Text = rows[i][0], Font = JohanTheme.Font(11.5F), ForeColor = JohanTheme.Muted, AutoSize = false, Bounds = new Rectangle(lblStatus.Left, y, 130, 30) });
                    statusValues[i] = new Label { Font = JohanTheme.Font(11.5F), ForeColor = JohanTheme.Text, AutoSize = false, Bounds = new Rectangle(lblStatus.Left + 136, y, lblStatus.Width - 136, 30), AutoEllipsis = true };
                    card.Controls.Add(statusValues[i]);
                    y += 34;
                }
                lblStatus.Visible = false;
            }
            for (int i = 0; i < rows.Length; i++) statusValues[i].Text = rows[i][1];
        }
    }
}
