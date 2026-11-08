using System.Drawing;
using System.IO;
using AgLibrary.Johan;
using System.Windows.Forms;
using AgLibrary.Logging;
using AgOpenGPS.Properties;

namespace AgOpenGPS
{
    // Johan fork: a file "johan.lock" next to the exe pins the program to the
    // driver setup on every start (kiosk, driver mode, start screen, icons, theme),
    // whatever the saved environment says. Remove the file to get a normal AgOpenGPS.
    public static class JohanLock
    {
        public static string LockPath => Path.Combine(Application.StartupPath, "johan.lock");

        public static bool IsLocked => File.Exists(LockPath);

        // Called first thing in the FormGPS constructor, before the designer runs
        public static void Enforce()
        {
            if (!IsLocked) return;
            var s = Settings.Default;
            s.setWindow_isKioskMode = true;
            s.setDisplay_isTermsAccepted = true;
            s.setDisplay_isAutoStartAgIO = true;
            s.setDisplay_isAutoResumeField = true;
            s.setDisplay_isDriverMode = true;
            s.setDisplay_isStartScreen = true;
            s.setDisplay_isJohanIcons = true;
            // Johan turns himself: no U-turn and sideways-skip arrows on the map
            var f = s.setFeatures; f.isUTurnOn = false; f.isLateralOn = false; f.isYouTurnOn = false; s.setFeatures = f;
            Log.EventWriter("Johan lock: driver setup enforced (" + LockPath + ")");
        }

        // Map and frame colours that match the theme; applied whenever driver mode is on,
        // before LoadSettings reads them
        public static void ApplyColours()
        {
            var s = Settings.Default;
            s.setDisplay_isDayMode = false;
            s.setDisplay_colorNightFrame = JohanTheme.Bg;
            s.setDisplay_colorDayFrame = JohanTheme.Bg;
            s.setDisplay_colorTextNight = JohanTheme.Text;
            s.setDisplay_colorTextDay = JohanTheme.Text;
            s.setDisplay_colorFieldNight = JohanTheme.FieldGround;
            s.setDisplay_colorFieldDay = JohanTheme.FieldGround;
            s.setMenu_isGridOn = true;    // faint grid for orientation on the flat ground
            s.setMenu_isSideGuideLines = true;   // neighbouring lines a tool width apart
            if (s.setAS_numGuideLines > 4) s.setAS_numGuideLines = 3;
            JohanLook.Symbol = s.setDisplay_johanSymbool;
            s.setDisplay_isTextureOn = false;                                   // flat ground, no grain
            s.setDisplay_colorSectionsDay = System.Drawing.Color.FromArgb(22, 163, 74);   // worked area green
            s.setDisplay_colorVehicle = JohanTheme.Blue;
            JohanLook.On = true;   // ApplyDriverMode keeps it in step afterwards
        }
    }
}
