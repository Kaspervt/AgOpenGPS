using AgOpenGPS.Forms;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace AgOpenGPS
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        private static readonly Mutex Mutex = new Mutex(true, "{516-0AC5-B9A1-55fd-A8CE-72F04E6BDE8F}");

        public static readonly string Version = Assembly.GetEntryAssembly().GetName().Version.ToString(3); // Major.Minor.Patch
        public static readonly string SemVer = Application.ProductVersion.Split('+').First();
        public static readonly bool IsPreRelease = Application.ProductVersion.Contains('-');
        public static readonly bool IsDevelopVersion = Application.ProductVersion == "1.0.0.0";

        // Johan fork: a restart from inside the program (theme switch) must hand the single-instance
        // mutex back first, otherwise the new instance sees "already running" and quits
        public static void Restart()
        {
            try { Mutex.ReleaseMutex(); Mutex.Dispose(); } catch { }
            // not Application.Restart(): that starts the new instance while this one still holds the
            // UDP loopback port and the field files. A hidden shell waits a moment, then starts it.
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe",
                    "/c ping -n 4 127.0.0.1 >nul & start \"\" \"" + Application.ExecutablePath + "\"")
                { CreateNoWindow = true, UseShellExecute = false, WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden };
                System.Diagnostics.Process.Start(psi);
            }
            catch { Application.Restart(); return; }
            Application.Exit();
        }

        [STAThread]
        private static void Main()
        {
            if (Mutex.WaitOne(TimeSpan.Zero, true))
            {
                RegistrySettings.Load();
                Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo(RegistrySettings.culture);
                Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(RegistrySettings.culture);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new FormGPS());
            }
            else
            {
                FormDialog.Show(
                    "Warning",
                    "AgOpenGPS is Already Running",
                    DialogSeverity.Warning);
            }
        }
    }
}