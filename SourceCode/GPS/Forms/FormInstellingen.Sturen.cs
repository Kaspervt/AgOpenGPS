using System;
using AgLibrary.Johan;
using AgLibrary.Logging;
using System.Drawing;
using System.Windows.Forms;
using AgOpenGPS.Properties;

namespace AgOpenGPS
{
    // Johan fork: chapter "Sturen" of the settings page (canvas proposal D, 8-11).
    // Everything FormSteer sends with PGN 252 (gain, PWM, counts per degree, WAS offset, Ackermann)
    // is written into VehicleSettings/ToolSettings and applied with Toepassen(), which calls
    // mf.SendSettings() and so fills and sends PGN 252 itself. Whatever needs PGN 251 (steer enable,
    // motor driver, A2D, inversions, Danfoss, sensor) is only shown here, with a button that opens
    // the original FormSteer window, so the steering board config is never changed by accident.
    public partial class FormInstellingen
    {
        // Preset values for "Reactie op de lijn": look-ahead hold (s) and Kp, around the defaults 3,0 / 50
        private static readonly double[] ReactieHold = { 4.0, 3.0, 2.2 };
        private static readonly byte[] ReactieKp = { 40, 50, 65 };

        private void HoofdstukSturen()
        {
            var k = Hoofdstuk("Sturen", "Hoe de stuurprint de wielen stuurt. Wijzigingen gaan direct naar de print.", true);

            // Max steer angle, look-ahead hold and the integral gain live in CVehicle and are only read
            // from the settings in its constructor (mf.LoadSettings() does not touch them). FormSteer
            // rebuilds the vehicle the same way after its reset, so these rows do that before Toepassen().
            Action toepassenVoertuig = () =>
            {
                mf.vehicle = new CVehicle(mf);
                Toepassen();
            };

            // One choice that sets look-ahead hold and Kp together; the index shown is the preset
            // closest to the current look-ahead hold (the loose rows below may have moved it)
            RijKeuze(k, "Reactie op de lijn", new[] { "Rustig", "Normaal", "Snel" },
                () =>
                {
                    double nu = ToolSettings.Default.setVehicle_goalPointLookAheadHold;
                    int beste = 0;
                    for (int i = 1; i < ReactieHold.Length; i++)
                    {
                        if (Math.Abs(ReactieHold[i] - nu) < Math.Abs(ReactieHold[beste] - nu))
                        {
                            beste = i;
                        }
                    }
                    return beste;
                },
                i =>
                {
                    ToolSettings.Default.setVehicle_goalPointLookAheadHold = ReactieHold[i];
                    VehicleSettings.Default.setAS_Kp = ReactieKp[i];
                    toepassenVoertuig();
                },
                "Eén keuze; de losse waarden staan hieronder");

            // Speed window in which autosteer is allowed (tabAlarm in FormSteer). CVehicle reads
            // maxSteerSpeed/minSteerSpeed in its constructor only, so the vehicle is rebuilt too.
            RijGetal(k, "Autosturen tot", "km/u",
                () => VehicleSettings.Default.setAS_maxSteerSpeed,
                v => { VehicleSettings.Default.setAS_maxSteerSpeed = v; toepassenVoertuig(); },
                1, 40, 1, 0, "Daarboven schakelt het vanzelf uit");

            RijGetal(k, "Autosturen vanaf", "km/u",
                () => VehicleSettings.Default.setAS_minSteerSpeed,
                v => { VehicleSettings.Default.setAS_minSteerSpeed = v; toepassenVoertuig(); },
                0, 10, 0.5, 1, "Daaronder stuurt het niet");

            // Maximum steer angle the board may ask for (tabSteer)
            RijGetal(k, "Maximale stuurhoek", "°",
                () => VehicleSettings.Default.setVehicle_maxSteerAngle,
                v => { VehicleSettings.Default.setVehicle_maxSteerAngle = v; toepassenVoertuig(); },
                10, 80, 1, 0);

            // Zero the wheel angle sensor: same arithmetic as FormSteer btnZeroWAS_Click
            Action wielhoekNulstellen = () =>
            {
                int cpd = VehicleSettings.Default.setAS_countsPerDegree;
                int correctie = (int)(cpd * -mf.mc.actualSteerAngleDegrees);
                int offset = correctie + VehicleSettings.Default.setAS_wasOffset;
                if (Math.Abs(offset) > 3900)
                {
                    Log.EventWriter("Instellingen: excessive steer angle, no WAS zero " + offset);
                    mf.TimedMessageBox(3000, "Wielhoek nulstellen", "De wielen staan te ver gedraaid. Zet ze eerst recht.");
                }
                else
                {
                    VehicleSettings.Default.setAS_wasOffset += correctie;
                    Toepassen();
                    mf.TimedMessageBox(2000, "Wielhoek nulstellen", "Deze stand is nu recht vooruit");
                }
            };
            RijStatus(k, "Wielhoek nulstellen",
                () => Math.Abs(mf.mc.actualSteerAngleDegrees) < 0.5 ? JohanTheme.Green : JohanTheme.Grey,
                () => "Nu " + Komma(mf.mc.actualSteerAngleDegrees, 1) + "°",
                "Wielen recht, dan Nulstellen",
                ("Nulstellen", wielhoekNulstellen));

            // Sensor counts per degree of wheel angle (byte on the board)
            RijGetal(k, "Counts per graad", "",
                () => VehicleSettings.Default.setAS_countsPerDegree,
                v => { VehicleSettings.Default.setAS_countsPerDegree = (byte)v; Toepassen(); },
                1, 255, 1, 0, "Meten gaat met de stuur-wizard hieronder");

            RijGetal(k, "Ackermann", "%",
                () => VehicleSettings.Default.setAS_ackerman,
                v => { VehicleSettings.Default.setAS_ackerman = (byte)v; Toepassen(); },
                1, 200, 1, 0, "Verschil tussen linker- en rechterbocht");

            RijGetal(k, "Versterking (Kp)", "",
                () => VehicleSettings.Default.setAS_Kp,
                v => { VehicleSettings.Default.setAS_Kp = (byte)v; Toepassen(); },
                1, 200, 1, 0, "Hoe hard de motor reageert op een afwijking");

            // High PWM also sets the low PWM to a third, the way FormSteer does
            RijGetal(k, "Maximale motorkracht", "",
                () => VehicleSettings.Default.setAS_highSteerPWM,
                v =>
                {
                    VehicleSettings.Default.setAS_highSteerPWM = (byte)v;
                    VehicleSettings.Default.setAS_lowSteerPWM = (byte)((int)v / 3);
                    Toepassen();
                },
                1, 255, 5, 0, "Kracht bij een grote afwijking");

            RijGetal(k, "Minimale motorkracht", "",
                () => VehicleSettings.Default.setAS_minSteerPWM,
                v => { VehicleSettings.Default.setAS_minSteerPWM = (byte)v; Toepassen(); },
                0, 200, 1, 0, "Net genoeg om de wielen in beweging te krijgen");

            // Pure pursuit: look-ahead hold (s) and integral gain (tabPP)
            RijGetal(k, "Stuurrespons", "s",
                () => ToolSettings.Default.setVehicle_goalPointLookAheadHold,
                v => { ToolSettings.Default.setVehicle_goalPointLookAheadHold = v; toepassenVoertuig(); },
                1.0, 7.0, 0.1, 1, "Hoe ver vooruit op de lijn wordt gekeken; kleiner is sneller");

            RijGetal(k, "Integraal", "",
                () => ToolSettings.Default.purePursuitIntegralGainAB,
                v => { ToolSettings.Default.purePursuitIntegralGainAB = v; toepassenVoertuig(); },
                0, 1.0, 0.05, 2, "Trekt een blijvende afwijking naast de lijn weg; 0 is uit");

            RijAanUit(k, "Sturen achteruit",
                () => VehicleSettings.Default.setAS_isSteerInReverse,
                v => { VehicleSettings.Default.setAS_isSteerInReverse = v; Toepassen(); },
                "Autosturen blijft aan bij achteruitrijden");

            // Side hill compensation, degrees of steer per degree of roll (stored as a factor)
            RijGetal(k, "Hellingcompensatie", "°",
                () => VehicleSettings.Default.setAS_sideHillComp,
                v => { VehicleSettings.Default.setAS_sideHillComp = v; Toepassen(); },
                0, 1.0, 0.05, 2, "Extra stuurhoek per graad zijhelling; 0 is uit");

            // Steer enable source lives in setArdSteer_setting0 (bits 32 = switch, 64 = button) and
            // goes to the board with PGN 251, so it is only shown here; changing it is done in FormSteer
            RijStatus(k, "Autosturen inschakelen met",
                () => JohanTheme.Blue,
                () =>
                {
                    int sett = VehicleSettings.Default.setArdSteer_setting0;
                    if ((sett & 32) == 32)
                    {
                        return "Schakelaar";
                    }
                    if ((sett & 64) == 64)
                    {
                        return "Knop";
                    }
                    return "Geen (alleen op het scherm)";
                },
                "Knop of schakelaar op de stuurprint; wijzigen in het autostuur-venster",
                ("Wijzigen", () => mf.BeheerOpenSteer()));

            // Motor driver, A2D, inversions, Danfoss, sensor: PGN 251, original window only
            RijKnoppen(k, "Motor en bedrading", "Motor driver, A2D, omkeren, Danfoss, draaisensor; alleen bij het inrichten",
                ("Autostuur-venster", () => mf.BeheerOpenSteer()));

            // Guided measurement of all steer values, same window FormSteer opens
            Action wizardStarten = () =>
            {
                Form open = Application.OpenForms["FormSteer"];
                if (open != null)
                {
                    open.Close();
                }
                Form wiz = new FormSteerWiz(mf);
                wiz.Show(mf);
            };
            RijKnoppen(k, "Stuur-wizard", "Begeleide meting van alle stuurwaarden",
                ("Start", wizardStarten));

            // Factory values: same list as FormSteer btnVehicleReset_Click, after a question
            Action fabriekswaarden = () =>
            {
                DialogResult antwoord = AgOpenGPS.Forms.FormDialog.ShowQuestion(
                    "Terug naar fabriekswaarden",
                    "Alle stuurwaarden gaan terug naar de standaard. Weet je het zeker?");
                if (antwoord != DialogResult.OK)
                {
                    return;
                }
                Log.EventWriter("Instellingen: steer settings set to default");

                VehicleSettings.Default.setVehicle_maxSteerAngle = 45;
                VehicleSettings.Default.setAS_countsPerDegree = 110;
                VehicleSettings.Default.setAS_ackerman = 100;
                VehicleSettings.Default.setAS_wasOffset = 3;
                VehicleSettings.Default.setAS_highSteerPWM = 180;
                VehicleSettings.Default.setAS_lowSteerPWM = 60;
                VehicleSettings.Default.setAS_Kp = 50;
                VehicleSettings.Default.setAS_minSteerPWM = 25;
                VehicleSettings.Default.setArdSteer_setting0 = 56;
                VehicleSettings.Default.setArdSteer_setting1 = 0;
                VehicleSettings.Default.setArdMac_isDanfoss = false;
                VehicleSettings.Default.setArdSteer_maxPulseCounts = 3;

                ToolSettings.Default.setVehicle_goalPointAcquireFactor = 0.85;
                ToolSettings.Default.setVehicle_goalPointLookAheadHold = 3;
                ToolSettings.Default.setVehicle_goalPointLookAheadMult = 1.5;
                ToolSettings.Default.stanleyHeadingErrorGain = 1;
                ToolSettings.Default.stanleyDistanceErrorGain = 1;
                ToolSettings.Default.stanleyIntegralGainAB = 0;
                ToolSettings.Default.purePursuitIntegralGainAB = 0;
                ToolSettings.Default.setAS_uTurnCompensation = 1;
                ToolSettings.Default.setAS_snapDistance = 20;
                ToolSettings.Default.setVehicle_isStanleyUsed = false;

                VehicleSettings.Default.setAS_sideHillComp = 0;
                VehicleSettings.Default.setIMU_invertRoll = false;
                VehicleSettings.Default.setIMU_rollZero = 0;
                VehicleSettings.Default.setAS_minSteerSpeed = 0;
                VehicleSettings.Default.setAS_maxSteerSpeed = 15;
                VehicleSettings.Default.setAS_functionSpeedLimit = 12;
                VehicleSettings.Default.setAS_isSteerInReverse = false;

                Settings.Default.setDisplay_lightbarCmPerPixel = 5;
                Settings.Default.setDisplay_lineWidth = 2;
                Settings.Default.setAS_guidanceLookAheadTime = 1.5;

                toepassenVoertuig();
                mf.TimedMessageBox(2000, "Fabriekswaarden", "Alle stuurwaarden staan weer op de standaard");
            };
            RijKnoppen(k, "Terug naar fabriekswaarden", "Zet alle stuurwaarden terug",
                ("!Reset", fabriekswaarden));
        }
    }
}
