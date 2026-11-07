using System;
using System.Drawing;
using System.Windows.Forms;
using AgLibrary.Logging;
using AgOpenGPS.Core.Streamers;
using AgOpenGPS.Properties;

namespace AgOpenGPS
{
    // Johan fork: driver mode.
    // Lays the canvas design over the main form: a top bar (field, line, implement,
    // RTK, tractor, speed), the OpenGL map, and a bottom bar with four big buttons.
    // The original AgOpenGPS panels stay in place but hidden, and every button here
    // calls the existing click handlers, so the guidance core is untouched.
    // A long press (3 s) on the speed block brings the original layout back
    // (themed) until the next long press or restart.
    public partial class FormGPS
    {
        public bool isDriverMode = false;
        private bool isDriverUnlocked = false;
        private Timer driverPressTimer;
        private const int driverLongPressMs = 3000;

        private Panel panelJohanTop, panelJohanBottom;
        private Button btnJPerceel, btnJWerktuig, btnJAndereLijn, btnJVerschuif, btnJKopakker, btnJAuto, btnJLuchtfoto, btnJThema;
        private Label lblJLijn, lblJRtk, lblJTrekker, lblJSpeed, lblJBewerkt;
        private Timer johanTick;
        private bool johanLayoutBusy;
        private const int JohanTopH = 68, JohanBottomH = 132;

        // Called once from FormGPS_Load.
        private void InitDriverMode()
        {
            driverPressTimer = new Timer { Interval = driverLongPressMs };
            driverPressTimer.Tick += DriverPressTimer_Tick;
            // the hamburger menu keeps working as unlock target when the original layout is visible
            fileToolStripMenuItem.MouseDown += (s, e) => { if (isDriverMode) driverPressTimer.Start(); };
            fileToolStripMenuItem.MouseUp += (s, e) => driverPressTimer.Stop();
            fileToolStripMenuItem.MouseLeave += (s, e) => driverPressTimer.Stop();

            BuildJohanBars();

            // upstream re-shows its panels and resizes the map when a field opens or the window changes
            SizeChanged += (s, e) => LayoutJohan();
            oglMain.SizeChanged += (s, e) => LayoutJohan();
            panelRight.VisibleChanged += (s, e) => LayoutJohan();
            panelBottom.VisibleChanged += (s, e) => LayoutJohan();
            panelLeft.VisibleChanged += (s, e) => LayoutJohan();

            johanTick = new Timer { Interval = 500 };
            johanTick.Tick += (s, e) => { try { UpdateJohanBars(); } catch (Exception ex) { Log.EventWriter("Driver bars: " + ex.Message); } };
            johanTick.Start();

            // LoadSettings ran before the bars existed; lay them out now
            ApplyDriverMode();

            // Debug aid: "--snapshot-main <png>" writes the main form to a PNG after 10 s
            // (the OpenGL map stays black in such a capture, the bars do show)
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "--snapshot-main");
            if (i >= 0 && i + 1 < args.Length)
            {
                string path = args[i + 1];
                var t = new Timer { Interval = 10000 };
                t.Tick += (s, e) =>
                {
                    t.Stop();
                    try
                    {
                        using (var bmp = new Bitmap(Width, Height))
                        {
                            DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
                            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                        }
                        Log.EventWriter("Main form snapshot written: " + path);
                    }
                    catch (Exception ex) { Log.EventWriter("Main form snapshot failed: " + ex.Message); }
                };
                t.Start();
            }
        }

        private void DriverPressTimer_Tick(object sender, EventArgs e)
        {
            driverPressTimer.Stop();
            if (!isDriverMode) return;

            fileToolStripMenuItem.HideDropDown();
            if (isDriverUnlocked)
            {
                // long press on the hamburger menu in the original layout: back to the driver screen
                isDriverUnlocked = false;
                ApplyDriverMode();
                TimedMessageBox(2000, "Chauffeursmodus", "Rijscherm weer actief");
                Log.EventWriter("Driver mode: back to driver layout");
                return;
            }
            // long press on the speed block: the Beheer page from the canvas
            using (var f = new FormBeheer(this)) f.ShowDialog(this);
        }

        // Called from FormBeheer; the handlers themselves are private to FormGPS
        public void BeheerOpenConfig() => btnConfig_Click(btnJobMenu, EventArgs.Empty);
        public void BeheerOpenSteer() => btnAutoSteerConfig_Click(btnAutoSteerConfig, EventArgs.Empty);
        public void BeheerOpenAgIO() => btnStartAgIO_Click(btnStartAgIO, EventArgs.Empty);
        public void BeheerOpenLog()
        {
            Form f = Application.OpenForms["FormEventViewer"];
            if (f != null) { f.Focus(); return; }
            new FormEventViewer(System.IO.Path.Combine(RegistrySettings.logsDirectory, "AgOpenGPS_Events_Log.txt")).Show(this);
        }
        public void BeheerShowOriginal()
        {
            isDriverUnlocked = true;
            ApplyDriverMode();
            TimedMessageBox(3000, "Originele indeling", "Terug naar het rijscherm: houd het menu linksboven 3 s ingedrukt");
            Log.EventWriter("Driver mode: original layout shown");
        }

        #region Bars

        private Button JohanButton(string text, string icon, int iconSize, float fontSize, bool iconAbove)
        {
            var b = new Button
            {
                Text = text,
                BackColor = JohanTheme.Panel,
                ForeColor = JohanTheme.Text,
                FlatStyle = FlatStyle.Flat,
                Font = JohanTheme.Font(fontSize, FontStyle.Bold),
                TextImageRelation = iconAbove ? TextImageRelation.ImageAboveText : TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleCenter,
                TextAlign = ContentAlignment.MiddleCenter,
                UseVisualStyleBackColor = false,
                Image = icon == null ? null : JohanSkin.Icon(icon, iconSize)
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = JohanTheme.PanelLight;
            b.FlatAppearance.MouseDownBackColor = JohanTheme.Blue;
            return b;
        }

        private Label JohanLabel(string text, float size, FontStyle style, Color color)
        {
            return new Label
            {
                Text = text,
                Font = JohanTheme.Font(size, style),
                ForeColor = color,
                BackColor = Color.Transparent,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private void BuildJohanBars()
        {
            // ---- top bar
            panelJohanTop = new Panel { BackColor = JohanTheme.Bar, Visible = false };

            btnJPerceel = JohanButton("Kies perceel", "polygon_licht_64", 22, 12F, false);
            btnJPerceel.TextAlign = ContentAlignment.MiddleLeft;
            btnJPerceel.Padding = new Padding(8, 0, 8, 0);
            btnJPerceel.Click += (s, e) => btnJobMenu_Click(btnJobMenu, EventArgs.Empty);

            lblJLijn = JohanLabel("", 12F, FontStyle.Regular, JohanTheme.Muted);

            btnJWerktuig = JohanButton("Werktuig", "tool_licht_64", 22, 12F, false);
            btnJWerktuig.TextAlign = ContentAlignment.MiddleLeft;
            btnJWerktuig.Padding = new Padding(8, 0, 8, 0);
            btnJWerktuig.Click += (s, e) => { using (var f = new FormWerktuig(this)) f.ShowDialog(this); };

            lblJRtk = JohanLabel("● RTK", 12F, FontStyle.Regular, JohanTheme.Red);
            lblJTrekker = JohanLabel("● Trekker", 12F, FontStyle.Regular, JohanTheme.Red);
            lblJSpeed = JohanLabel("0,0 km/u", 20F, FontStyle.Bold, JohanTheme.Text);
            lblJSpeed.TextAlign = ContentAlignment.MiddleRight;

            // long press on the speed block = unlock
            foreach (Control c in new Control[] { lblJSpeed, lblJRtk, lblJTrekker })
            {
                c.MouseDown += (s, e) => { if (isDriverMode) driverPressTimer.Start(); };
                c.MouseUp += (s, e) => driverPressTimer.Stop();
                c.MouseLeave += (s, e) => driverPressTimer.Stop();
            }

            // aerial photo on/off (per field, AgOpenGPS keeps it as BackPic in the field folder)
            btnJLuchtfoto = JohanButton("", "photo_licht_64", 24, 10F, false);
            btnJLuchtfoto.Click += (s, e) => ToggleLuchtfoto();

            // dark/light theme; the switch restarts the program so every window follows
            btnJThema = JohanButton("", JohanTheme.IsLight ? "moon_licht_64" : "sun_licht_64", 24, 10F, false);
            btnJThema.Click += (s, e) =>
            {
                Settings.Default.setDisplay_isJohanLight = !Settings.Default.setDisplay_isJohanLight;
                Settings.Default.Save();
                Log.EventWriter("Theme switch, light = " + Settings.Default.setDisplay_isJohanLight + ", restarting");
                if (isJobStarted) { try { FileSaveEverythingBeforeClosingField().Wait(5000); } catch { } }
                Application.Restart();
            };

            panelJohanTop.Controls.AddRange(new Control[] { btnJPerceel, lblJLijn, btnJWerktuig, lblJRtk, lblJTrekker, lblJSpeed, btnJLuchtfoto, btnJThema });
            Controls.Add(panelJohanTop);

            // ---- bottom bar
            panelJohanBottom = new Panel { BackColor = JohanTheme.Bar, Visible = false };

            btnJAndereLijn = JohanButton("Andere lijn", "line_licht_64", 32, 12F, true);
            btnJAndereLijn.Click += (s, e) => JohanAndereLijn();

            btnJVerschuif = JohanButton("Lijn verschuiven", "arrows-horizontal_licht_64", 32, 12F, true);
            btnJVerschuif.Click += (s, e) =>
            {
                if (trk.idx < 0) { TimedMessageBox(1500, "Lijn verschuiven", "Eerst een lijn kiezen"); return; }
                Form open = Application.OpenForms["FormVerschuif"];
                if (open != null) { open.Focus(); return; }
                new FormVerschuif(this).Show(this);
            };

            btnJKopakker = JohanButton("Kopakker", "box-margin_licht_64", 32, 12F, true);
            btnJKopakker.Click += (s, e) => btnHeadlandOnOff_Click(btnHeadlandOnOff, EventArgs.Empty);

            btnJAuto = JohanButton("Autosturen uit", "steering-wheel_licht_64", 40, 18F, false);
            btnJAuto.BackColor = JohanTheme.Grey;
            btnJAuto.Click += (s, e) => btnAutoSteer_Click(btnAutoSteer, EventArgs.Empty);

            panelJohanBottom.Controls.AddRange(new Control[] { btnJAndereLijn, btnJVerschuif, btnJKopakker, btnJAuto });
            Controls.Add(panelJohanBottom);

            // ---- small overlay bottom right of the map
            lblJBewerkt = JohanLabel("", 10F, FontStyle.Regular, JohanTheme.Text);
            lblJBewerkt.BackColor = JohanTheme.Bg;
            lblJBewerkt.TextAlign = ContentAlignment.MiddleCenter;
            lblJBewerkt.Visible = false;
            Controls.Add(lblJBewerkt);
        }

        // Aerial photo: off = keep the loaded map aside; on = restore it, load it from the field folder,
        // or open the AgOpenGPS map window to fetch one (needs internet)
        private void ToggleLuchtfoto()
        {
            if (!isJobStarted) { TimedMessageBox(1500, "Luchtfoto", "Eerst een perceel openen"); return; }
            if (worldGrid.HasBingMap)
            {
                // the photo stays on disk (BackPic in the field folder); switching on reads it again
                worldGrid.BingMap = null;

                return;
            }
            var dirInfo = new System.IO.DirectoryInfo(System.IO.Path.Combine(RegistrySettings.fieldsDirectory, currentFieldDirectory));
            var fromDisk = new BingMapStreamer().TryRead(dirInfo);
            if (fromDisk != null) { worldGrid.BingMap = fromDisk; return; }
            Form open = Application.OpenForms["FormMap"];
            if (open != null) { open.Focus(); return; }
            new FormMap(this).Show(this);
        }


        private void JohanAndereLijn()
        {
            if (!isJobStarted) return;
            int visible = 0;
            foreach (var t in trk.gArr) if (t.isVisible) visible++;
            if (trk.idx == -1 && trk.gArr.Count > 0)
            {
                btnTrack_Click(btnTrack, EventArgs.Empty);
                return;
            }
            if (visible > 1)
            {
                btnCycleLines_Click(btnCycleLines, EventArgs.Empty);
            }
            else
            {
                TimedMessageBox(2000, "Lijnen", trk.gArr.Count == 0 ? "Dit perceel heeft nog geen lijnen" : "Dit perceel heeft maar één lijn");
            }
        }

        // Positions bars and map; hides the original chrome. Re-entrant safe.
        private void LayoutJohan()
        {
            if (johanLayoutBusy || panelJohanTop == null) return;
            bool johan = isDriverMode && !isDriverUnlocked;
            if (!johan) return;
            johanLayoutBusy = true;
            try
            {
                int w = ClientSize.Width, h = ClientSize.Height;

                panelJohanTop.Bounds = new Rectangle(0, 0, w, JohanTopH);
                panelJohanBottom.Bounds = new Rectangle(0, h - JohanBottomH, w, JohanBottomH);

                var ogl = new Rectangle(0, JohanTopH, w, h - JohanTopH - JohanBottomH);
                if (oglMain.Bounds != ogl) oglMain.Bounds = ogl;

                // top bar contents
                int y = 12, bh = JohanTopH - 24, x = 16;
                btnJPerceel.Bounds = new Rectangle(x, y, 300, bh); x += 312;
                lblJLijn.Bounds = new Rectangle(x, y, 220, bh); x += 232;
                btnJWerktuig.Bounds = new Rectangle(x, y, 260, bh);

                lblJSpeed.Bounds = new Rectangle(w - 16 - 200, 8, 200, JohanTopH - 16);
                lblJTrekker.Bounds = new Rectangle(w - 16 - 200 - 130, y, 130, bh);
                lblJRtk.Bounds = new Rectangle(w - 16 - 200 - 130 - 150, y, 150, bh);
                btnJThema.Bounds = new Rectangle(w - 16 - 200 - 130 - 150 - 12 - bh, y, bh, bh);
                btnJLuchtfoto.Bounds = new Rectangle(btnJThema.Left - 8 - bh, y, bh, bh);

                // bottom bar: 1fr 1fr 1fr 2fr, 12 px gaps, 14/16 px padding
                int gap = 12, pad = 16, top = 14, bhh = JohanBottomH - 28;
                int unit = (w - 2 * pad - 3 * gap) / 5;
                int bx = pad;
                btnJAndereLijn.Bounds = new Rectangle(bx, top, unit, bhh); bx += unit + gap;
                btnJVerschuif.Bounds = new Rectangle(bx, top, unit, bhh); bx += unit + gap;
                btnJKopakker.Bounds = new Rectangle(bx, top, unit, bhh); bx += unit + gap;
                btnJAuto.Bounds = new Rectangle(bx, top, w - pad - bx, bhh);

                lblJBewerkt.Bounds = new Rectangle(w - 20 - 260, h - JohanBottomH - 16 - 34, 260, 34);

                // original chrome away
                panelLeft.Visible = false;
                panelRight.Visible = false;
                panelBottom.Visible = false;
                panelControlBox.Visible = false;
                panelNavigation.Visible = false;
                panelSim.Visible = false;
                menuStrip1.Visible = false;
                flp1.Visible = false;
                oglZoom.Visible = false;
                lblCurrentField.Visible = false;
                lblFix.Visible = false;
                HideSections();
                HideZones();

                panelJohanTop.Visible = true;
                panelJohanBottom.Visible = true;
                lblJBewerkt.Visible = isJobStarted;
                panelJohanTop.BringToFront();
                panelJohanBottom.BringToFront();
                lblJBewerkt.BringToFront();
            }
            finally { johanLayoutBusy = false; }
        }

        private void RestoreOriginalLayout()
        {
            if (panelJohanTop == null) return;
            panelJohanTop.Visible = false;
            panelJohanBottom.Visible = false;
            lblJBewerkt.Visible = false;
            menuStrip1.Visible = true;
            panelControlBox.Visible = true;
            panelLeft.Visible = true;
            oglZoom.Visible = true;
            lblCurrentField.Visible = true;
            panelSim.Visible = timerSim.Enabled;
            PanelsAndOGLSize();
            PanelUpdateRightAndBottom();
        }

        private void UpdateJohanBars()
        {
            if (panelJohanTop == null || !isDriverMode || isDriverUnlocked || !panelJohanTop.Visible) return;

            double ha = isJobStarted ? fd.areaOuterBoundary / 10000.0 : 0;
            btnJPerceel.Text = isJobStarted
                ? currentFieldDirectory + (ha > 0 ? "   " + ha.ToString("0.0") + " ha" : "")
                : "Kies perceel";

            string lijn;
            if (!isJobStarted) lijn = "";
            else if (ct.isContourBtnOn) lijn = "Contour";
            else if (trk.idx > -1 && trk.idx < trk.gArr.Count) lijn = trk.gArr[trk.idx].name;
            else lijn = trk.gArr.Count == 0 ? "Geen lijnen" : "Geen lijn gekozen";
            lblJLijn.Text = lijn == "" ? "" : "Lijn:  " + lijn;
            lblJLijn.Visible = isJobStarted;

            // upstream lines the section buttons up again after a field opens; keep them away
            HideSections();
            HideZones();

            btnJWerktuig.Text = RegistrySettings.toolProfileName + "   " + tool.width.ToString("0.0") + " m";

            int fq = pn.fixQuality;
            bool gpsAlive = fq > 0 && sentenceCounter < 20;
            if (!gpsAlive) { lblJRtk.ForeColor = JohanTheme.Red; lblJRtk.Text = "●  Geen GPS"; }
            else if (fq == 4) { lblJRtk.ForeColor = JohanTheme.Green; lblJRtk.Text = "●  RTK " + pn.age.ToString("0") + " s"; }
            else if (fq == 5) { lblJRtk.ForeColor = JohanTheme.Amber; lblJRtk.Text = "●  RTK float"; }
            else if (fq == 8) { lblJRtk.ForeColor = JohanTheme.Green; lblJRtk.Text = "●  Simulator"; }
            else { lblJRtk.ForeColor = JohanTheme.Amber; lblJRtk.Text = "●  GPS zonder RTK"; }

            // the counter only runs inside the position loop, so without GPS it says nothing
            bool steerOk = gpsAlive && steerModuleConnectedCounter < 30;
            lblJTrekker.ForeColor = steerOk ? JohanTheme.Green : JohanTheme.Red;
            lblJTrekker.Text = "●  Trekker";

            lblJSpeed.Text = SpeedKPH.Replace('.', ',') + " km/u";

            if (isJobStarted && ha > 0)
                lblJBewerkt.Text = "Bewerkt " + (fd.workedAreaTotal / 10000.0).ToString("0.0") + " van " + ha.ToString("0.0") + " ha";
            else if (isJobStarted)
                lblJBewerkt.Text = "Bewerkt " + (fd.workedAreaTotal / 10000.0).ToString("0.0") + " ha";
            lblJBewerkt.Visible = isJobStarted;

            // bottom buttons
            btnJAndereLijn.Enabled = isJobStarted && trk.gArr.Count > 0;
            btnJVerschuif.Enabled = isJobStarted && trk.idx > -1;

            bool hasHeadland = isJobStarted && bnd.bndList.Count > 0 && bnd.bndList[0].hdLine.Count > 0;
            btnJKopakker.Enabled = hasHeadland;
            btnJKopakker.Text = !hasHeadland ? "Geen kopakker" : bnd.isHeadlandOn ? "Kopakker aan" : "Kopakker uit";
            btnJKopakker.BackColor = hasHeadland && bnd.isHeadlandOn ? JohanTheme.Green : JohanTheme.Panel;

            bool canSteer = isJobStarted && (trk.idx > -1 || ct.isContourBtnOn);
            if (isBtnAutoSteerOn && !canSteer)
            {
                // steer safe: upstream does this via a PerformClick on a button we hide
                btnAutoSteer_Click(btnAutoSteer, EventArgs.Empty);
            }
            btnJAuto.Enabled = canSteer;
            btnJAuto.Text = isBtnAutoSteerOn ? "Autosturen AAN" : "Autosturen uit";
            btnJAuto.BackColor = isBtnAutoSteerOn ? JohanTheme.Green : JohanTheme.Grey;

            foreach (Button b in new[] { btnJAndereLijn, btnJVerschuif, btnJKopakker })
                b.ForeColor = b.Enabled ? JohanTheme.Text : JohanTheme.Muted;
            // the big button is always on a dark green or grey ground, in both themes
            btnJAuto.ForeColor = btnJAuto.Enabled ? Color.White : Color.FromArgb(203, 213, 225);
            btnJKopakker.ForeColor = hasHeadland && bnd.isHeadlandOn ? Color.White : btnJKopakker.ForeColor;

            btnJLuchtfoto.BackColor = isJobStarted && worldGrid.HasBingMap ? JohanTheme.Green : JohanTheme.Panel;
        }

        #endregion

        // Apply visibility. Safe to call repeatedly; upstream code re-shows
        // some buttons when a field opens, so this runs again from FieldMenuButtonEnableDisable.
        public void ApplyDriverMode()
        {
            bool hide = isDriverMode && !isDriverUnlocked;

            // Left column: tools menu and settings menu
            statusStripLeft.Visible = !hide;
            statusStrip2.Visible = !hide;
            btnNavigationSettings.Visible = !hide;
            btnStartAgIO.Visible = !hide && Settings.Default.setFeatures.isAgIOOn;

            // Driver mode: let AgOpenGPS pick the nearest line itself when a field is open
            // (upstream resets isAutoTrack when a field opens; this runs after that)
            if (hide && isJobStarted && trk.gArr.Count > 0 && !trk.isAutoTrack)
            {
                trk.isAutoTrack = true;
                btnAutoTrack.Image = Resources.AutoTrack;
            }

            if (hide)
            {
                LayoutJohan();
                UpdateJohanBars();
            }
            else if (panelJohanTop != null && panelJohanTop.Visible)
            {
                RestoreOriginalLayout();
            }

            if (!hide)
            {
                btnSectionMasterManual.Visible = Settings.Default.setFeatures.isManualSectionOn;
                btnSectionMasterAuto.Visible = Settings.Default.setFeatures.isAutoSectionOn;
                btnAutoYouTurn.Visible = Settings.Default.setFeatures.isYouTurnOn;
                btnYouSkipEnable.Visible = Settings.Default.setFeatures.isYouTurnOn;
                cboxpRowWidth.Visible = Settings.Default.setFeatures.isYouTurnOn;
            }
        }
    }
}
