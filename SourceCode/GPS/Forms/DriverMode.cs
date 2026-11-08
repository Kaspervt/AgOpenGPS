using System;
using AgLibrary.Johan;
using System.Collections.Generic;
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
        private Label lblJLijn, lblJSpeed;
        private JohanPill lblJRtk, lblJTrekker, lblJBewerkt, lblJAfstand;
        private JohanRoundButton btnJZoomIn, btnJZoomUit, btnJ3D, btnJMeet, btnJTerug, btnJNoord;
        private JohanPill lblJMeet;
        // measuring tape: point A and whether it is running (drawn in OpenGL.Designer.cs)
        public bool johanMeetActief;
        public vec3 johanMeetA;
        private Button btnJInstellingen, btnJUit;
        private Timer johanTick;
        private Control[] johanSectionButtons;
        private bool johanLayoutBusy;
        // design sizes in 96-dpi pixels; S() scales them to the screen (see JohanTheme.Schaal)
        private static int JohanTopH => JohanTheme.S(68);
        private static int JohanBottomH => JohanTheme.S(132);
        private static int S(int px) => JohanTheme.S(px);

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
            johanTick.Tick += (s, e) => { try { UpdateJohanBars(); JohanThemaKlok(); } catch (Exception ex) { Log.EventWriter("Driver bars: " + ex.Message); } };
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
            JohanAfsluitenUitvoeren();
        }

        // Called from FormBeheer; the handlers themselves are private to FormGPS
        // Both settings windows open full screen from Beheer (same construction as the upstream
        // button handlers in Controls.Designer.cs, only the window geometry is changed afterwards).
        public void BeheerOpenConfig()
        {
            using (var form = new FormConfig(this))
            {
                JohanBeeldvullend(form);
                form.ShowDialog(this);
            }
        }

        public void BeheerOpenSteer()
        {
            Form fc = Application.OpenForms["FormSteer"];
            if (fc != null) { fc.Focus(); fc.Close(); return; }
            var form = new FormSteer(this);
            // FormSteer_Load restores the saved location, so the geometry is applied after it
            form.Load += (s, e) => JohanBeeldvullend((Form)s);
            form.Show(this);
            this.Activate();
        }

        // Full screen without border on the screen the main window is on; no saved location,
        // no size limits. The upstream Designer files stay untouched: the layout is adjusted here
        // after the form has been built.
        private void JohanBeeldvullend(Form f)
        {
            f.StartPosition = FormStartPosition.Manual;
            f.FormBorderStyle = FormBorderStyle.None;
            f.MaximumSize = Size.Empty;
            f.MinimumSize = Size.Empty;
            f.Bounds = Screen.FromControl(this).Bounds;

            if (f is FormConfig)
            {
                // Designer: tab1 Dock Top (fixed height) above panelBottom Dock Bottom; let the pages take the rest
                if (f.Controls["tab1"] is TabControl tab) tab.Dock = DockStyle.Fill;
            }
            else if (f is FormSteer)
            {
                // The window is wider than the expanded state (918 px), so the right tab strip is
                // visible anyway; the expand arrow would only shrink it again.
                foreach (Control c in f.Controls.Find("btnExpand", true)) c.Visible = false;
                // controls are placed absolutely for the 898 x 628 layout: centre that block on the screen
                var union = Rectangle.Empty;
                foreach (Control c in f.Controls)
                    if (c.Visible) union = union.IsEmpty ? c.Bounds : Rectangle.Union(union, c.Bounds);
                if (!union.IsEmpty)
                {
                    int dx = Math.Max(0, (f.ClientSize.Width - union.Width) / 2 - union.X);
                    int dy = Math.Max(0, (f.ClientSize.Height - union.Height) / 2 - union.Y);
                    if (dx != 0 || dy != 0)
                        foreach (Control c in f.Controls) c.Location = new Point(c.Left + dx, c.Top + dy);
                }
            }
        }
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
            var b = new JohanRoundButton
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

        // Theme switch: throw the bars away and build them again in the new colours
        public void RebuildJohanBars()
        {
            if (panelJohanTop == null) return;
            foreach (Control c in new Control[] { panelJohanTop, panelJohanBottom, lblJBewerkt, lblJAfstand, btnJZoomIn, btnJZoomUit, btnJ3D, btnJMeet, lblJMeet, btnJTerug, btnJNoord })
            {
                if (c == null) continue;
                Controls.Remove(c);
                c.Dispose();
            }
            BuildJohanBars();
            johanLayoutBusy = false;
            LayoutJohan();
            UpdateJohanBars();
        }

        private void BuildJohanBars()
        {
            // ---- top bar
            panelJohanTop = new Panel { BackColor = JohanTheme.Bar, Visible = false };

            btnJPerceel = JohanButton("Kies perceel", "polygon_licht_64", 24, 14F, false);
            btnJPerceel.TextAlign = ContentAlignment.MiddleLeft;
            btnJPerceel.Padding = new Padding(S(8), 0, S(8), 0);
            btnJPerceel.Click += (s, e) => btnJobMenu_Click(btnJobMenu, EventArgs.Empty);

            lblJLijn = JohanLabel("", 14F, FontStyle.Regular, JohanTheme.Text);

            btnJWerktuig = JohanButton("Werktuig", "tool_licht_64", 24, 14F, false);
            btnJWerktuig.TextAlign = ContentAlignment.MiddleLeft;
            btnJWerktuig.Padding = new Padding(S(8), 0, S(8), 0);
            btnJWerktuig.Click += (s, e) => { using (var f = new FormWerktuig(this)) f.ShowDialog(this); };

            lblJRtk = new JohanPill { Text = "RTK", Font = JohanTheme.Font(14F, FontStyle.Bold), ForeColor = JohanTheme.Text, Fill = Color.Transparent, Dot = JohanTheme.Red };
            lblJTrekker = new JohanPill { Text = "Trekker", Font = JohanTheme.Font(14F, FontStyle.Bold), ForeColor = JohanTheme.Text, Fill = Color.Transparent, Dot = JohanTheme.Red };
            lblJSpeed = JohanLabel("0,0 km/u", 22F, FontStyle.Bold, JohanTheme.Text);
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
            btnJThema.Click += (s, e) => JohanThemeLive(!Settings.Default.setDisplay_isJohanLight);

            // settings in plain sight (the long press on the speed still works too)
            btnJInstellingen = JohanButton("", "settings_licht_64", 24, 10F, false);
            // gear = the settings page; Beheer stays behind the long press on the speed and under Onderhoud
            btnJInstellingen.Click += (s, e) => { using (var f = new FormInstellingen(this)) f.ShowDialog(this); JohanAfsluitenUitvoeren(); };

            // off: the same question as in Beheer and on the start screen
            btnJUit = JohanButton("", "power_licht_64", 24, 10F, false);
            btnJUit.BackColor = JohanTheme.Red;   // red, as in Beheer: this one switches off
            btnJUit.Click += (s, e) => { using (var f = new FormAfsluiten(this)) f.ShowDialog(this); JohanAfsluitenUitvoeren(); };
            panelJohanTop.Controls.AddRange(new Control[] { btnJPerceel, lblJLijn, btnJWerktuig, lblJRtk, lblJTrekker, lblJSpeed, btnJLuchtfoto, btnJThema, btnJInstellingen, btnJUit });
            Controls.Add(panelJohanTop);

            // ---- bottom bar
            panelJohanBottom = new Panel { BackColor = JohanTheme.Bar, Visible = false };

            btnJAndereLijn = JohanButton("Andere lijn", "line_licht_64", 32, 12F, true);
            btnJAndereLijn.Click += (s, e) =>
            {
                if (isJobStarted && trk.gArr.Count == 0) { JohanOpenLijn(); return; }
                JohanAndereLijn();
                // with more than one line also show the list, so the right one is one tap away
                int zichtbaar = 0;
                foreach (var t in trk.gArr) if (t.isVisible) zichtbaar++;
                if (!isJobStarted || zichtbaar < 2) return;
                var open = Application.OpenForms["FormLijnKeuze"] as FormLijnKeuze;
                if (open != null) { open.Wakker(); return; }
                new FormLijnKeuze(this).Show(this);
            };

            btnJVerschuif = JohanButton("Lijn verschuiven", "arrows-horizontal_licht_64", 32, 12F, true);
            btnJVerschuif.Click += (s, e) =>
            {
                if (trk.idx < 0) { TimedMessageBox(1500, "Lijn verschuiven", "Eerst een lijn kiezen"); return; }
                Form open = Application.OpenForms["FormVerschuif"];
                if (open != null) { open.Focus(); return; }
                new FormVerschuif(this).Show(this);
            };

            btnJKopakker = JohanButton("Kopakker", "kopakker_licht_64", 32, 12F, true);
            btnJKopakker.Click += (s, e) =>
            {
                if (!isJobStarted) return;
                if (bnd.bndList.Count == 0) { JohanOpenGrens(); return; }
                Form open = Application.OpenForms["FormKopakker"];
                if (open != null) { open.Focus(); return; }
                new FormKopakker(this).Show(this);
            };

            btnJAuto = JohanButton("Autosturen uit", "steering-wheel_licht_64", 40, 18F, false);
            btnJAuto.BackColor = JohanTheme.Grey;
            btnJAuto.Click += (s, e) => btnAutoSteer_Click(btnAutoSteer, EventArgs.Empty);

            panelJohanBottom.Controls.AddRange(new Control[] { btnJAndereLijn, btnJVerschuif, btnJKopakker, btnJAuto });
            Controls.Add(panelJohanBottom);

            // ---- overlays over the map (separate windows above the OpenGL view, as in the canvas)
            lblJBewerkt = new JohanPill { Font = JohanTheme.Font(13F), ForeColor = JohanTheme.Text, Fill = JohanTheme.Bg, Corner = JohanTheme.FieldGround, Visible = false };
            Controls.Add(lblJBewerkt);
            lblJAfstand = new JohanPill { Font = JohanTheme.Font(20F, FontStyle.Bold), ForeColor = JohanTheme.Text, Fill = JohanTheme.Bg, Corner = JohanTheme.FieldGround, Visible = false };
            Controls.Add(lblJAfstand);
            btnJZoomIn = (JohanRoundButton)JohanButton("", "zoom-in_licht_64", 28, 10F, false);
            btnJZoomIn.BackColor = JohanTheme.Bg; btnJZoomIn.Radius = S(12); btnJZoomIn.Visible = false; btnJZoomIn.Corner = JohanTheme.FieldGround;
            btnJZoomIn.Click += (s, e) => { camera.ZoomIn(); SetZoom(); };
            btnJZoomUit = (JohanRoundButton)JohanButton("", "zoom-out_licht_64", 28, 10F, false);
            btnJZoomUit.BackColor = JohanTheme.Bg; btnJZoomUit.Radius = S(12); btnJZoomUit.Visible = false; btnJZoomUit.Corner = JohanTheme.FieldGround;
            btnJZoomUit.Click += (s, e) => { camera.ZoomOut(); SetZoom(); };
            Controls.Add(btnJZoomIn);
            Controls.Add(btnJZoomUit);
            // 2D flat and north up, or 3D tilted behind the tractor (the AgOpenGPS camera modes)
            btnJ3D = (JohanRoundButton)JohanButton("", "cube_licht_64", 28, 10F, false);
            btnJ3D.BackColor = JohanTheme.Bg; btnJ3D.Radius = S(12); btnJ3D.Visible = false; btnJ3D.Corner = JohanTheme.FieldGround;
            btnJ3D.Click += (s, e) =>
            {
                // one switch: 2D from above <-> 3D behind the tractor (two fingers tilt further)
                double volgende = camera.PitchInDegrees > -20 ? -55 : 0;
                JohanZetHoek(volgende);
                TimedMessageBox(1200, "Weergave", volgende == 0 ? "2D, van boven" : "3D, achter de trekker");
            };
            Controls.Add(btnJ3D);

            // measuring tape: tap = point A here, the pill shows the straight-line distance; tap again = stop
            btnJMeet = (JohanRoundButton)JohanButton("", "ruler-2_licht_64", 28, 10F, false);
            btnJMeet.BackColor = JohanTheme.Bg; btnJMeet.Radius = S(12); btnJMeet.Visible = false; btnJMeet.Corner = JohanTheme.FieldGround;
            btnJMeet.Click += (s, e) =>
            {
                if (!johanMeetActief)
                {
                    johanMeetA = pivotAxlePos;
                    johanMeetActief = true;
                    TimedMessageBox(1500, "Meten", "Punt A staat hier; rij weg en lees de afstand af");
                }
                else johanMeetActief = false;
                btnJMeet.BackColor = johanMeetActief ? JohanTheme.Amber : JohanTheme.Bg;
                UpdateJohanBars();
            };
            Controls.Add(btnJMeet);

            // compass: pin the 2D map north up (off = the map turns with the tractor)
            btnJNoord = (JohanRoundButton)JohanButton("", "compass_licht_64", 28, 10F, false);
            btnJNoord.Radius = S(12); btnJNoord.Visible = false; btnJNoord.Corner = JohanTheme.FieldGround;
            btnJNoord.BackColor = Settings.Default.setDisplay_johanNoordBoven ? JohanTheme.Blue : JohanTheme.Bg;
            btnJNoord.Click += (s, e) =>
            {
                Settings.Default.setDisplay_johanNoordBoven = !Settings.Default.setDisplay_johanNoordBoven;
                Settings.Default.Save();
                btnJNoord.BackColor = Settings.Default.setDisplay_johanNoordBoven ? JohanTheme.Blue : JohanTheme.Bg;
                JohanZetHoek(camera.PitchInDegrees);
                TimedMessageBox(1200, "Weergave", Settings.Default.setDisplay_johanNoordBoven ? "Noorden boven" : "Rijrichting boven");
            };
            Controls.Add(btnJNoord);
            lblJMeet = new JohanPill { Font = JohanTheme.Font(16F, FontStyle.Bold), ForeColor = JohanTheme.Text, Fill = JohanTheme.Bg, Corner = JohanTheme.FieldGround, Visible = false, Dot = JohanTheme.Amber };
            Controls.Add(lblJMeet);

            // back to the tractor after panning the map
            btnJTerug = (JohanRoundButton)JohanButton("Naar de trekker", "navigation_licht_64", 24, 11F, false);
            btnJTerug.BackColor = JohanTheme.Blue; btnJTerug.ForeColor = Color.White; btnJTerug.Radius = S(12); btnJTerug.Visible = false; btnJTerug.Corner = JohanTheme.FieldGround;
            btnJTerug.Click += (s, e) => { camera.PanX = 0; camera.PanY = 0; btnJTerug.Visible = false; };
            Controls.Add(btnJTerug);
            JohanGestures();
        }

        // Theme on the clock: light between 7 and 19 h, dark otherwise; checked every tick, switched live once
        private int johanKlokMinuut = -1;
        private void JohanThemaKlok()
        {
            if (!Settings.Default.setDisplay_johanThemaKlok) return;
            int minuut = DateTime.Now.Hour * 60 + DateTime.Now.Minute;
            if (minuut == johanKlokMinuut) return;
            johanKlokMinuut = minuut;
            bool licht = DateTime.Now.Hour >= 7 && DateTime.Now.Hour < 19;
            if (licht != Settings.Default.setDisplay_isJohanLight && Application.OpenForms.Count <= 1) JohanThemeLive(licht);
        }

        // Pan the map by a number of screen pixels (one finger or a mouse drag); the button to come back appears
        public void JohanPan(int dx, int dy)
        {
            if (dx == 0 && dy == 0) return;
            double perPixel = 2.0 * camera.DistanceToLookAt * Math.Tan(fovy * 0.5) / Math.Max(1, oglMain.Height);
            camera.PanX += dx * perPixel;
            camera.PanY -= dy * perPixel;
            if (btnJTerug != null && !btnJTerug.Visible && JohanSchermActief) { btnJTerug.Visible = true; btnJTerug.BringToFront(); }
        }

        // Camera tilt: 0 is straight down, -75 almost along the ground. Icon follows: cube = go 3D, map = back to 2D.
        public void JohanZetHoek(double pitch)
        {
            pitch = Math.Max(-70, Math.Min(0, pitch));
            if (pitch > -4) pitch = 0;
            // 3D always follows the driving direction; 2D can be pinned north up (compass button)
            camera.FollowDirectionHint = pitch <= -20 || !Settings.Default.setDisplay_johanNoordBoven;
            camera.PitchInDegrees = pitch;
            if (btnJ3D != null) btnJ3D.Image = JohanSkin.Icon(pitch > -20 ? "cube_licht_64" : "map-2_licht_64", 28);
            if (btnJNoord != null) btnJNoord.Visible = JohanSchermActief && pitch > -20;
            SetZoom();
        }

        public bool JohanSchermActief => isDriverMode && !isDriverUnlocked;
        public void JohanZoomStap(bool dichterbij) { if (dichterbij) camera.ZoomInSmallStep(); else camera.ZoomOutSmallStep(); SetZoom(); }

        // Tilt by dragging on the map: two fingers on a touch screen (WM_GESTURE pan with a finger distance),
        // or a plain mouse drag on a laptop. A pinch zooms. Taps keep working as before.
        private JohanGestureWindow johanGest;
        private int johanDragX, johanDragY = -1;
        private bool johanDragging;
        private void JohanGestures()
        {
            if (johanGest != null) return;
            johanGest = new JohanGestureWindow(this);
            if (oglMain.IsHandleCreated) johanGest.AssignHandle(oglMain.Handle);
            oglMain.HandleCreated += (s, e) => johanGest.AssignHandle(oglMain.Handle);
            oglMain.HandleDestroyed += (s, e) => johanGest.ReleaseHandle();
            oglMain.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { johanDragX = e.X; johanDragY = e.Y; johanDragging = false; } };
            oglMain.MouseUp += (s, e) => johanDragY = -1;
            oglMain.MouseMove += (s, e) =>
            {
                if (e.Button != MouseButtons.Left || johanDragY < 0 || !JohanSchermActief) return;
                int dx = e.X - johanDragX, dy = e.Y - johanDragY;
                if (!johanDragging && Math.Abs(dx) + Math.Abs(dy) < S(12)) return;
                johanDragging = true;
                // a mouse drag pans the map (one finger on the tablet does the same); tilt: 3D button or two fingers
                JohanPan(dx, dy);
                johanDragX = e.X;
                johanDragY = e.Y;
            };
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
            // no photo yet: fetch the tiles for this field in the background (JohanLuchtfoto.cs), no map window
            JohanLuchtfotoOphalen();
        }


        public void JohanOpenGrens()
        {
            if (!isJobStarted) { TimedMessageBox(1500, "Grens", "Eerst een perceel openen"); return; }
            if (Application.OpenForms["FormGrens"] != null) return;
            new FormGrens(this).Show(this);
        }

        public void JohanOpenLijn()
        {
            if (!isJobStarted) { TimedMessageBox(1500, "Lijn", "Eerst een perceel openen"); return; }
            if (Application.OpenForms["FormLijn"] != null) return;
            new FormLijn(this).Show(this);
        }

        // Select a line by index, the same side effects as the AgOpenGPS cycle button
        public void JohanKiesLijn(int idx)
        {
            if (!isJobStarted || idx < 0 || idx >= trk.gArr.Count || !trk.gArr[idx].isVisible) return;
            if (idx == trk.idx) return;   // tapping the active line must not switch autosteer off
            trk.isAutoTrack = false;
            btnAutoTrack.Image = Resources.AutoTrackOff;
            trk.idx = idx;
            guideLineCounter = 20;
            lblGuidanceLine.Visible = true;
            lblGuidanceLine.Text = trk.gArr[idx].name;
            if (isBtnAutoSteerOn) { btnAutoSteer.PerformClick(); TimedMessageBox(2000, "Autosturen uit", "Lijn gewisseld"); }
            if (yt.isYouTurnBtnOn) btnAutoYouTurn.PerformClick();
            lblNumCu.Text = (trk.idx + 1).ToString() + "/" + trk.gArr.Count.ToString();

            // same as btnCycleLines_Click: force a rebuild of the guidance line
            twoSecondCounter = 100;

            ABLine.isABValid = false;
            curve.isCurveValid = false;
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

                // top bar contents (design pixels through S())
                int y = S(12), bh = JohanTopH - S(24), x = S(16);
                int speedW = S(210), trekkerW = S(150), rtkW = S(210), g8 = S(8), g12 = S(12), g16 = S(16);
                // right block first (speed, trekker, RTK, four square buttons), the left block gets what is left
                int rightW = g16 + speedW + trekkerW + rtkW + g12 + 4 * (bh + g8);
                int leftW = Math.Max(S(420), w - rightW - S(32));
                int perceelW = Math.Min(S(300), (int)(leftW * 0.38)), werktuigW = Math.Min(S(260), (int)(leftW * 0.34));
                int lijnW = Math.Max(S(100), leftW - perceelW - werktuigW - S(24));
                btnJPerceel.Bounds = new Rectangle(x, y, perceelW, bh); x += perceelW + g12;
                lblJLijn.Bounds = new Rectangle(x, y, lijnW, bh); x += lijnW + g12;
                btnJWerktuig.Bounds = new Rectangle(x, y, werktuigW, bh);

                lblJSpeed.Bounds = new Rectangle(w - g16 - speedW, g8, speedW, JohanTopH - g16);
                lblJTrekker.Bounds = new Rectangle(w - g16 - speedW - trekkerW, y, trekkerW, bh);
                lblJRtk.Bounds = new Rectangle(w - g16 - speedW - trekkerW - rtkW, y, rtkW, bh);
                btnJUit.Bounds = new Rectangle(w - g16 - speedW - trekkerW - rtkW - g12 - bh, y, bh, bh);
                btnJInstellingen.Bounds = new Rectangle(btnJUit.Left - g8 - bh, y, bh, bh);
                btnJThema.Bounds = new Rectangle(btnJInstellingen.Left - g8 - bh, y, bh, bh);
                btnJLuchtfoto.Bounds = new Rectangle(btnJThema.Left - g8 - bh, y, bh, bh);

                // bottom bar: 1fr 1fr 1fr 2fr, 12 px gaps, 14/16 px padding
                int gap = S(12), pad = S(16), top = S(14), bhh = JohanBottomH - S(28);
                int unit = (w - 2 * pad - 3 * gap) / 5;
                int bx = pad;
                btnJAndereLijn.Bounds = new Rectangle(bx, top, unit, bhh); bx += unit + gap;
                btnJVerschuif.Bounds = new Rectangle(bx, top, unit, bhh); bx += unit + gap;
                btnJKopakker.Bounds = new Rectangle(bx, top, unit, bhh); bx += unit + gap;
                btnJAuto.Bounds = new Rectangle(bx, top, w - pad - bx, bhh);

                int z = S(56), z10 = S(10), m20 = S(20);
                lblJBewerkt.Bounds = new Rectangle(w - m20 - S(260), h - JohanBottomH - g16 - S(40), S(260), S(40));
                lblJAfstand.Bounds = new Rectangle((w - S(560)) / 2, JohanTopH + m20, S(560), z);
                btnJZoomIn.Bounds = new Rectangle(w - m20 - z, JohanTopH + m20, z, z);
                btnJZoomUit.Bounds = new Rectangle(w - m20 - z, JohanTopH + m20 + z + z10, z, z);
                btnJ3D.Bounds = new Rectangle(w - m20 - z, JohanTopH + m20 + 2 * (z + z10) + g12, z, z);
                btnJMeet.Bounds = new Rectangle(w - m20 - z, JohanTopH + m20 + 3 * (z + z10) + 2 * g12, z, z);
                btnJNoord.Bounds = new Rectangle(w - m20 - z, JohanTopH + m20 + 4 * (z + z10) + 2 * g12, z, z);
                lblJMeet.Bounds = new Rectangle((w - S(420)) / 2, JohanTopH + m20 + z + z10, S(420), S(48));
                btnJTerug.Bounds = new Rectangle((w - S(260)) / 2, h - JohanBottomH - g16 - S(48), S(260), S(48));

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
                btnJZoomIn.Visible = true;
                btnJZoomUit.Visible = true;
                btnJ3D.Visible = true;
                btnJMeet.Visible = true;
                btnJNoord.Visible = camera.PitchInDegrees > -20;
                camera.FollowDirectionHint = camera.PitchInDegrees <= -20 || !Settings.Default.setDisplay_johanNoordBoven;
                panelJohanTop.BringToFront();
                panelJohanBottom.BringToFront();
                lblJBewerkt.BringToFront();
                lblJAfstand.BringToFront();
                btnJZoomIn.BringToFront();
                btnJZoomUit.BringToFront();
                btnJ3D.BringToFront();
                btnJMeet.BringToFront();
                btnJNoord.BringToFront();
                lblJMeet.BringToFront();
                btnJTerug.BringToFront();
            }
            finally { johanLayoutBusy = false; }
        }

        private void RestoreOriginalLayout()
        {
            if (panelJohanTop == null) return;
            panelJohanTop.Visible = false;
            panelJohanBottom.Visible = false;
            lblJBewerkt.Visible = false;
            lblJAfstand.Visible = false;
            btnJZoomIn.Visible = false;
            btnJZoomUit.Visible = false;
            btnJ3D.Visible = false;
            btnJMeet.Visible = false;
            btnJNoord.Visible = false;
            lblJMeet.Visible = false;
            btnJTerug.Visible = false;
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

            // upstream lines the section buttons up again after a field opens; keep them away.
            // HideSections() walks the whole control tree 16 times, so look the buttons up once.
            if (johanSectionButtons == null)
            {
                var lijst = new List<Control>();
                for (int i = 1; i <= 16; i++) lijst.AddRange(Controls.Find("btnSection" + i + "Man", true));
                for (int i = 1; i <= 8; i++) lijst.AddRange(Controls.Find("btnZone" + i, true));
                johanSectionButtons = lijst.ToArray();
            }
            foreach (Control c in johanSectionButtons) if (c.Visible) c.Visible = false;

            btnJWerktuig.Text = RegistrySettings.toolProfileName + "   " + tool.width.ToString("0.0") + " m";

            int fq = pn.fixQuality;
            bool gpsAlive = fq > 0 && sentenceCounter < 20;
            Color rtkKleur; string rtkTekst;
            if (!gpsAlive) { rtkKleur = JohanTheme.Red; rtkTekst = "Geen GPS"; }
            else if (fq == 4) { rtkKleur = JohanTheme.Green; rtkTekst = "RTK " + pn.age.ToString("0") + " s"; }
            else if (fq == 5) { rtkKleur = JohanTheme.Amber; rtkTekst = "RTK float"; }
            else if (fq == 8) { rtkKleur = JohanTheme.Green; rtkTekst = "Simulator"; }
            else { rtkKleur = JohanTheme.Amber; rtkTekst = "GPS zonder RTK"; }
            if (lblJRtk.Dot != rtkKleur || lblJRtk.Text != rtkTekst) { lblJRtk.Dot = rtkKleur; lblJRtk.Text = rtkTekst; lblJRtk.Invalidate(); }

            // the counter only runs inside the position loop, so without GPS it says nothing
            bool steerOk = gpsAlive && steerModuleConnectedCounter < 30;
            Color trekkerKleur = steerOk ? JohanTheme.Green : JohanTheme.Red;
            if (lblJTrekker.Dot != trekkerKleur) { lblJTrekker.Dot = trekkerKleur; lblJTrekker.Invalidate(); }

            // distance to the line, as the pill in the canvas (avgPivDistance is the smoothed cross-track in mm)
            bool lijnActief = isJobStarted && (trk.idx > -1 || ct.isContourBtnOn);
            if (!isFirstHeadingSet && headingFromSource != "Dual")
            {
                // the arrow points anywhere until the first metres are driven; say so instead of the question mark
                lijnActief = true;
                const string wacht = "Rijrichting onbekend, rij vooruit";
                if (lblJAfstand.Text != wacht) lblJAfstand.Text = wacht;
            }
            else if (lijnActief)
            {
                double cmAf = avgPivDistance * 0.1;
                int cmAbs = (int)Math.Round(Math.Abs(cmAf));
                string afst = cmAbs == 0 ? "op de lijn" : cmAbs + " cm " + (cmAf > 0 ? "rechts van de lijn" : "links van de lijn");
                if (cmAbs > 999) afst = "ver van de lijn";
                if (lblJAfstand.Text != afst) lblJAfstand.Text = afst;
            }
            if (lblJAfstand.Visible != lijnActief) lblJAfstand.Visible = lijnActief;

            // measuring tape: straight-line distance from point A to the tractor
            if (johanMeetActief)
            {
                double m = glm.Distance(johanMeetA, pivotAxlePos);
                string t = "Afstand tot punt A: " + (m < 100 ? m.ToString("0.0") : m.ToString("0")).Replace('.', ',') + " m";
                if (lblJMeet.Text != t) lblJMeet.Text = t;
                if (!lblJMeet.Visible) { lblJMeet.Visible = true; lblJMeet.BringToFront(); }
            }
            else if (lblJMeet.Visible) lblJMeet.Visible = false;

            lblJSpeed.Text = SpeedKPH.Replace('.', ',') + " km/u";

            if (isJobStarted && ha > 0)
                lblJBewerkt.Text = "Bewerkt " + (fd.workedAreaTotal / 10000.0).ToString("0.0") + " van " + ha.ToString("0.0") + " ha";
            else if (isJobStarted)
                lblJBewerkt.Text = "Bewerkt " + (fd.workedAreaTotal / 10000.0).ToString("0.0") + " ha";
            lblJBewerkt.Visible = isJobStarted;

            // bottom buttons: without lines or boundary the buttons become the makers
            bool geenLijnen = isJobStarted && trk.gArr.Count == 0, geenGrens = isJobStarted && bnd.bndList.Count == 0;
            btnJAndereLijn.Enabled = isJobStarted;
            string lijnTekst = geenLijnen ? "Lijn maken" : "Andere lijn";
            if (btnJAndereLijn.Text != lijnTekst) { btnJAndereLijn.Text = lijnTekst; btnJAndereLijn.Image = JohanSkin.Icon(geenLijnen ? "plus_licht_64" : "line_licht_64", 32); }
            btnJVerschuif.Enabled = isJobStarted && trk.idx > -1;

            bool hasHeadland = isJobStarted && bnd.bndList.Count > 0 && bnd.bndList[0].hdLine.Count > 0;
            btnJKopakker.Enabled = isJobStarted;
            string kopTekst = geenGrens ? "Grens rijden" : !hasHeadland ? "Kopakker instellen" : bnd.isHeadlandOn ? "Kopakker aan" : "Kopakker uit";
            if (btnJKopakker.Text != kopTekst) { btnJKopakker.Text = kopTekst; btnJKopakker.Image = JohanSkin.Icon(geenGrens ? "polygon_licht_64" : "kopakker_licht_64", 32); }
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
            JohanLook.On = hide;   // map look follows the driver screen, also after unlock and back

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
