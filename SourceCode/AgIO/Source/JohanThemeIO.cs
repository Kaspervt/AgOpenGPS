using System;
using System.Collections.Generic;
using System.Drawing;
using AgLibrary.Johan;

namespace AgIO
{
    // Johan fork: AgIO in the same theme as AgOpenGPS. Light or dark comes from the registry
    // (AgOpenGPS writes it at start), the icons from the shared johan-icons folder next to the exe.
    internal static class JohanThemeIO
    {
        public static bool IsOn { get; private set; }

        private static readonly Dictionary<string, string> map = new Dictionary<string, string>
        {
            { "Cancel64", "x_rood_64" },
            { "OK64", "check_groen_64" },
            { "WindowClose", "x_licht_64" },
            { "WindowMinimize", "minus_licht_64" },
            { "MinimizeIcon", "minus_licht_64" },
            { "back_button", "arrow-left_licht_64" },
            { "ArrowLeft", "chevron-left_licht_64" },
            { "ArrowRight", "chevron-right_licht_64" },
            { "ArrowGrnLeft", "chevron-left_groen_64" },
            { "ArrowGrnRight", "chevron-right_groen_64" },
            { "UpArrow64", "chevron-up_licht_64" },
            { "DnArrow64", "chevron-down_licht_64" },
            { "Settings48", "settings_licht_64" },
            { "Help", "circle-dot_licht_64" },
            { "Trash", "trash_rood_64" },
            { "Edit", "pencil_licht_64" },
            { "AddNew", "plus_licht_64" },
            { "FileSaveAs", "device-floppy_licht_64" },
            { "VehFileSave", "device-floppy_licht_64" },
            { "satellite", "satellite_licht_64" },
            { "www", "world_licht_64" },
            { "USB_Connect", "plug_groen_64" },
            { "USB_Disconnect", "plug_rood_64" },
            { "EthernetSetup", "cloud-upload_licht_64" },
            { "EthernetOff", "power_rood_64" },
            { "ScanNetwork", "target_licht_64" },
            { "ScanPorts", "target_licht_64" },
            { "DeviceManager", "device-desktop_licht_64" },
            { "SerialMonitor", "list_licht_64" },
            { "LogViewer", "list_licht_64" },
            { "LogNMEA", "list_licht_64" },
            { "Nmea", "list_licht_64" },
            { "Sort", "arrows-sort_licht_64" },
            { "B_Autosteer", "steering-wheel_licht_64" },
            { "B_GPS", "satellite_licht_64" },
            { "B_IMU", "compass_licht_64" },
            { "B_Machine", "engine_licht_64" },
            { "B_UDP", "cloud-upload_licht_64" },
            { "NTRIP_Client", "antenna_licht_64" },
            { "NtripSettings", "antenna_licht_64" },
        };

        // large pictures (the splash, the module drawings) keep their own look
        private static bool KeepOriginal(string name, Image img) => img.Width > 160 || img.Height > 160 || name == "AgIO_First";

        public static void Install()
        {
            try
            {
                JohanTheme.Init(JohanTheme.ReadLightFromRegistry());
                JohanSkinCore.Install(typeof(Properties.Resources), map, KeepOriginal);
                JohanTheme.Install();
                IsOn = true;
                // AgOpenGPS writes the light/dark choice to the registry; follow it while running
                bool last = JohanTheme.IsLight;
                var volg = new System.Windows.Forms.Timer { Interval = 2000 };
                volg.Tick += (s, e) =>
                {
                    bool nu = JohanTheme.ReadLightFromRegistry();
                    if (nu != last) { last = nu; JohanTheme.SwitchLive(nu); }
                };
                volg.Start();
            }
            catch (Exception ex)
            {
                AgLibrary.Logging.Log.EventWriter("Johan theme (AgIO) failed: " + ex.Message);
            }
        }
    }
}
