using System;
using System.IO;
using System.Linq;
using AgLibrary.Johan;
using AgOpenGPS.Properties;

namespace AgOpenGPS
{
    // Johan fork: chapter "Scherm en geluid" of the settings page (canvas proposal D).
    // Theme switches live through FormGPS.JohanThemeLive, the same way the theme button in the
    // top bar does; the other rows are plain display/sound flags in Settings.
    public partial class FormInstellingen
    {
        private void HoofdstukSchermGeluid()
        {
            var k = Hoofdstuk("Scherm en geluid", "Hoe het eruitziet en wat je hoort.");

            // Theme: index 0 = dark, 1 = light. JohanThemeLive saves the setting and recolours
            // the running screens itself. This page only recolours its own background; the cards
            // and rows keep the old colours until the page is opened again, hence the message.
            RijKeuze(k, "Thema", new[] { "Donker", "Licht", "Op de klok" },
                () => Settings.Default.setDisplay_johanThemaKlok ? 2 : Settings.Default.setDisplay_isJohanLight ? 1 : 0,
                i =>
                {
                    // on the clock: light from 7 to 19 h, dark otherwise (DriverMode checks every tick)
                    Settings.Default.setDisplay_johanThemaKlok = i == 2;
                    bool licht = i == 2 ? (DateTime.Now.Hour >= 7 && DateTime.Now.Hour < 19) : i == 1;
                    Toepassen();
                    if (licht == Settings.Default.setDisplay_isJohanLight)
                    {
                        return;
                    }
                    mf.JohanThemeLive(licht);
                    BackColor = JohanTheme.Bg;
                    ForeColor = JohanTheme.Text;
                    mf.TimedMessageBox(2500, "Scherm", "Het thema is gewisseld. Sluit en open de instellingen om alles in de nieuwe kleuren te zien.");
                },
                "Donker is rustiger in het donker, licht is beter te lezen in de zon; op de klok wisselt vanzelf om 7 en 19 uur.");

            // CSound reads the settings only in its constructor and LoadSettings does not reread them,
            // so the running flags in mf.sounds are set as well, the way ConfigData does.
            RijAanUit(k, "Geluid bij autosturen aan / uit",
                () => Settings.Default.setSound_isAutoSteerOn,
                v =>
                {
                    Settings.Default.setSound_isAutoSteerOn = v;
                    if (mf.sounds != null)
                    {
                        mf.sounds.isSteerSoundOn = v;
                    }
                    Toepassen();
                },
                "Een piep als het autosturen aan- of uitgaat.");

            // Sounds from the steer/machine board: U-turn and hydraulic lift share one switch here
            RijAanUit(k, "Geluid bij meldingen van de print",
                () => Settings.Default.setSound_isUturnOn || Settings.Default.setSound_isHydLiftOn,
                v =>
                {
                    Settings.Default.setSound_isUturnOn = v;
                    Settings.Default.setSound_isHydLiftOn = v;
                    if (mf.sounds != null)
                    {
                        mf.sounds.isTurnSoundOn = v;
                        mf.sounds.isHydLiftSoundOn = v;
                    }
                    Toepassen();
                },
                "Piep bij een kopakkerbocht en bij de hef.");

            RijTekst(k, "Startfoto's", StartfotoTekst,
                "Foto's in de map Environment, submap startfotos. Elke start een andere.");

            RijAanUit(k, "Schermtoetsenbord",
                () => Settings.Default.setDisplay_isKeyboardOn,
                v => { Settings.Default.setDisplay_isKeyboardOn = v; Toepassen(); },
                "Toetsenbord op het scherm als je iets moet intypen.");

            RijAanUit(k, "Helderheid regelen vanuit het programma",
                () => Settings.Default.setDisplay_isBrightnessOn,
                v => { Settings.Default.setDisplay_isBrightnessOn = v; Toepassen(); },
                "Alleen als het scherm dat toelaat.");

            RijTekst(k, "Eenheden", () => "Metrisch, vast");
        }

        // Number of start photos in Environment\startfotos (jpg, jpeg, png), as FormStart picks them
        private static string StartfotoTekst()
        {
            try
            {
                string dir = Path.Combine(RegistrySettings.environmentDirectory, "startfotos");
                int n = Directory.Exists(dir)
                    ? Directory.GetFiles(dir).Count(f =>
                        f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    : 0;
                if (n == 0)
                {
                    bool los = File.Exists(Path.Combine(RegistrySettings.environmentDirectory, "startfoto.jpg"));
                    return los ? "1 foto (startfoto.jpg)" : "Geen foto's";
                }
                return n == 1 ? "1 foto" : n + " foto's, wisselend";
            }
            catch
            {
                return "Onbekend";
            }
        }
    }
}
