using System;
using System.Windows.Forms;
using AgLibrary.Logging;
using AgOpenGPS.Properties;

namespace AgOpenGPS
{
    // Johan fork: driver mode.
    // Hides the setup menus (tools, settings) and buttons a driver never needs,
    // without touching the guidance core. A long press (3 s) on the main menu
    // button unlocks the hidden menus until the next start.
    public partial class FormGPS
    {
        public bool isDriverMode = false;
        private bool isDriverUnlocked = false;
        private Timer driverPressTimer;
        private const int driverLongPressMs = 3000;

        // Called once from FormGPS_Load.
        // The main menu opens its dropdown on MouseDown, so MouseUp never reaches the item;
        // a timer that fires while the button is still held is the reliable way to detect a long press.
        private void InitDriverMode()
        {
            driverPressTimer = new Timer { Interval = driverLongPressMs };
            driverPressTimer.Tick += DriverPressTimer_Tick;
            fileToolStripMenuItem.MouseDown += (s, e) => { if (isDriverMode) driverPressTimer.Start(); };
            fileToolStripMenuItem.MouseUp += (s, e) => driverPressTimer.Stop();
            fileToolStripMenuItem.MouseLeave += (s, e) => driverPressTimer.Stop();

            // Debug aid: "--snapshot-main <png>" writes the main form to a PNG after 10 s
            // (the OpenGL map stays black in such a capture, the buttons do show)
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
                        using (var bmp = new System.Drawing.Bitmap(Width, Height))
                        {
                            DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, Width, Height));
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
            isDriverUnlocked = !isDriverUnlocked;
            ApplyDriverMode();
            TimedMessageBox(2000, "Chauffeursmodus",
                isDriverUnlocked ? "Beheer tijdelijk zichtbaar" : "Beheer weer verborgen");
            Log.EventWriter("Driver mode unlock: " + isDriverUnlocked);
        }

        // Apply visibility. Safe to call repeatedly; upstream code may re-show
        // some buttons when a field opens, so this runs again from FieldMenuButtonEnableDisable.
        public void ApplyDriverMode()
        {
            bool hide = isDriverMode && !isDriverUnlocked;

            // Left column: tools menu and settings menu
            statusStripLeft.Visible = !hide;
            statusStrip2.Visible = !hide;
            btnNavigationSettings.Visible = !hide;
            btnStartAgIO.Visible = !hide && Settings.Default.setFeatures.isAgIOOn;

            // Bottom row: things a driver on a mower, tedder or slurry tanker does not use
            if (hide)
            {
                btnFlag.Visible = false;
                btnHydLift.Visible = false;
                btnTramDisplayMode.Visible = false;
                btnResetToolHeading.Visible = false;
            }

            // Driver mode: let AgOpenGPS pick the nearest line itself when a field is open
            // (upstream resets isAutoTrack when a field opens; this runs after that)
            if (hide && isJobStarted && trk.gArr.Count > 0 && !trk.isAutoTrack)
            {
                trk.isAutoTrack = true;
                btnAutoTrack.Image = Resources.AutoTrack;
            }

            // Right column: section control and automatic U-turn (Johan turns by hand on grassland)
            if (hide)
            {
                btnSectionMasterManual.Visible = false;
                btnSectionMasterAuto.Visible = false;
                btnIsobusSectionControl.Visible = false;
                btnAutoYouTurn.Visible = false;
                btnYouSkipEnable.Visible = false;
                cboxpRowWidth.Visible = false;
            }
            else
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
