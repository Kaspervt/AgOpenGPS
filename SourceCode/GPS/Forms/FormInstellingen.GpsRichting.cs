using System;
using AgLibrary.Johan;
using System.Drawing;
using System.Windows.Forms;
using AgOpenGPS.Properties;

namespace AgOpenGPS
{
    // Johan fork: chapter "GPS en richting" of the settings page.
    // Rows come from the FormConfig tabs dHead (heading source, fusion, reverse, step, RTK, fix jump)
    // and dRoll (roll zero, invert, filter). Values go into VehicleSettings / Settings, after which
    // Toepassen() saves and lets FormGPS.LoadSettings() rebuild ahrs and reload the heading source.
    public partial class FormInstellingen
    {
        // IMU reports 88888 for roll when no roll sensor is present (same check as FormConfig)
        private const double GeenRol = 88888;

        private void HoofdstukGpsRichting()
        {
            var k = Hoofdstuk("GPS en richting", "Waar de positie en de rijrichting vandaan komen.", true);

            // Heading source: "Fix" (one antenna, heading from movement) or "Dual" (two antennas).
            // These are the exact strings FormConfig stores (the radio button texts).
            RijKeuze(k, "Antenne", new[] { "Eén antenne", "Twee antennes" },
                () => VehicleSettings.Default.setGPS_headingFromWhichSource == "Dual" ? 1 : 0,
                i =>
                {
                    VehicleSettings.Default.setGPS_headingFromWhichSource = i == 1 ? "Dual" : "Fix";
                    Toepassen();
                });

            // Dual heading offset, degrees (stored in degrees)
            RijGetal(k, "Richting-offset twee antennes", "°",
                () => VehicleSettings.Default.setGPS_dualHeadingOffset,
                v =>
                {
                    VehicleSettings.Default.setGPS_dualHeadingOffset = v;
                    Toepassen();
                },
                -180, 180, 1, 0, "Alleen bij twee antennes");

            // Fusion weight: FormConfig slider 5..60 shows weight * 500 as the GPS share in percent
            RijGetal(k, "Fusie hellingsensor en GPS", "%",
                () => Math.Round(VehicleSettings.Default.setIMU_fusionWeight2 * 500.0),
                v =>
                {
                    VehicleSettings.Default.setIMU_fusionWeight2 = v * 0.002;
                    Toepassen();
                },
                5, 60, 1, 0, "Hoeveel de GPS meetelt voor de richting, de rest komt van de hellingsensor");

            RijAanUit(k, "Achteruitrijden herkennen",
                () => Settings.Default.setIMU_isReverseOn,
                b =>
                {
                    Settings.Default.setIMU_isReverseOn = b;
                    Toepassen();
                });

            // Minimum GPS step, stored in meters, shown in cm. FormConfig couples the heading step
            // distance to it (10 x the step: 0,05 -> 0,5 and 0,10 -> 1,0) and resets the first heading.
            RijGetal(k, "Minimale GPS-stap", "cm",
                () => Math.Round(VehicleSettings.Default.setGPS_minimumStepLimit * 100.0),
                v =>
                {
                    VehicleSettings.Default.setGPS_minimumStepLimit = v / 100.0;
                    Settings.Default.setF_minHeadingStepDistance = v / 10.0;
                    mf.isFirstHeadingSet = false;
                    Toepassen();
                },
                1, 50, 1, 0, "Kleinste verplaatsing die als beweging telt");

            RijAanUit(k, "Waarschuwen zonder RTK",
                () => Settings.Default.setGPS_isRTK,
                b =>
                {
                    Settings.Default.setGPS_isRTK = b;
                    Toepassen();
                },
                "Geluid en rode melding zodra de RTK-fix wegvalt");

            RijAanUit(k, "Autosturen uit zonder RTK",
                () => Settings.Default.setGPS_isRTK_KillAutoSteer,
                b =>
                {
                    Settings.Default.setGPS_isRTK_KillAutoSteer = b;
                    Toepassen();
                });

            // setGPS_jumpFixAlarmDistance (fix jump alarm) is not read anywhere at runtime
            // (GUI.Designer.cs has the only use commented out), so the row is left out.

            // Roll zero: same as FormConfig btnZeroRoll / btnRemoveZeroOffset. The live roll is
            // shown so Johan sees what he is zeroing; the dot is grey when there is no roll sensor.
            Action rolNulstellen = () =>
            {
                if (mf.ahrs.imuRoll == GeenRol)
                {
                    mf.TimedMessageBox(2000, "Rol nulstellen", "Er komt geen rol van de hellingsensor");
                    return;
                }
                // FormConfig: add the old zero back to the raw roll and take that as the new zero
                mf.ahrs.imuRoll += mf.ahrs.rollZero;
                mf.ahrs.rollZero = mf.ahrs.imuRoll;
                VehicleSettings.Default.setIMU_rollZero = mf.ahrs.rollZero;
                Toepassen();
            };
            Action rolNulpuntWeg = () =>
            {
                mf.ahrs.rollZero = 0;
                VehicleSettings.Default.setIMU_rollZero = 0;
                Toepassen();
            };
            RijStatus(k, "Rol nulstellen",
                () => mf.ahrs.imuRoll == GeenRol ? JohanTheme.Grey : JohanTheme.Green,
                () =>
                {
                    if (mf.ahrs.imuRoll == GeenRol)
                    {
                        return "Geen hellingsensor";
                    }
                    return "Nu " + Komma(mf.ahrs.imuRoll, 1) + "°, nulpunt " + Komma(VehicleSettings.Default.setIMU_rollZero, 1) + "°";
                },
                "Op vlakke grond, trekker stil",
                ("Nulstellen", rolNulstellen),
                ("!Nulpunt weghalen", rolNulpuntWeg));

            RijAanUit(k, "Rol omkeren",
                () => VehicleSettings.Default.setIMU_invertRoll,
                b =>
                {
                    VehicleSettings.Default.setIMU_invertRoll = b;
                    Toepassen();
                },
                "Als de trekker de verkeerde kant op helt op het scherm");

            // Roll filter: FormConfig slider 0..98, stored as value * 0,01
            RijGetal(k, "Rolfilter", "%",
                () => Math.Round(VehicleSettings.Default.setIMU_rollFilter * 100.0),
                v =>
                {
                    VehicleSettings.Default.setIMU_rollFilter = v / 100.0;
                    Toepassen();
                },
                0, 98, 1, 0, "Hoger is rustiger, maar trager");
        }
    }
}
