using System;
using System.Drawing;
using AgLibrary.Johan;

namespace AgOpenGPS
{
    // Johan fork: chapter "Verbindingen" of the settings page. Status only, nothing is written here.
    // The colours and texts follow the start screen (FormStart.UpdateStatus) so both say the same thing.
    // Connection settings themselves (ports, NTRIP) live in AgIO; the buttons open that window.
    public partial class FormInstellingen
    {
        private void HoofdstukVerbindingen()
        {
            var k = Hoofdstuk("Verbindingen", "Wat nu aan de tablet hangt. Dit was het AgIO-venster.");

            // Same sources as the start screen: fix quality, NMEA sentence counter, steer board counter
            Func<int> fq = () => mf.pn.fixQuality;
            Func<bool> gpsOk = () => fq() > 0 && mf.sentenceCounter < 20;
            Func<bool> printOk = () => gpsOk() && mf.steerModuleConnectedCounter < 30;

            // GPS receiver
            RijStatus(k, "GPS-ontvanger",
                () => gpsOk() ? JohanTheme.Green : JohanTheme.Red,
                () => gpsOk() ? mf.pn.satellitesTracked + " satellieten" : "Geen positie",
                "Poort en snelheid stel je in AgIO in",
                ("AgIO openen", () => mf.BeheerOpenAgIO()));

            // Steer board; the counter only runs inside the position loop, so without GPS it says nothing
            RijStatus(k, "Stuurprint",
                () => printOk() ? JohanTheme.Green : JohanTheme.Red,
                () => printOk() ? "Verbonden" : gpsOk() ? "Geen verbinding met de stuurprint" : "Onbekend zonder GPS",
                "Netwerk of USB",
                ("AgIO openen", () => mf.BeheerOpenAgIO()));

            // RTK corrections: fix / float / none, with the age of the last correction
            RijStatus(k, "RTK-correcties",
                () =>
                {
                    int q = fq();
                    return q == 4 || q == 8 ? JohanTheme.Green : (q == 5 ? JohanTheme.Amber : JohanTheme.Red);
                },
                () =>
                {
                    int q = fq();
                    if (q == 4) { return "RTK fix, correctie " + mf.pn.age.ToString("0") + " s oud"; }
                    if (q == 5) { return "RTK float, nog niet nauwkeurig"; }
                    if (q == 8) { return "Simulator"; }
                    return "Geen correcties";
                },
                "Komen via NTRIP binnen in AgIO");

            // Base station: NTRIP caster, mountpoint and login live in AgIO's own settings
            RijKnoppen(k, "Basisstation", "Adres, poort, mountpoint en inlog staan in AgIO",
                ("Instellen in AgIO", () => mf.BeheerOpenAgIO()));

            // Tractor: the steer angle coming back from the board is the sign the tractor is there
            RijStatus(k, "Trekker (CAN)",
                () => printOk() && Math.Abs(mf.mc.actualSteerAngleDegrees) < 60 ? JohanTheme.Green : JohanTheme.Amber,
                () => printOk() && Math.Abs(mf.mc.actualSteerAngleDegrees) < 60
                    ? "Stuurhoek " + Komma(mf.mc.actualSteerAngleDegrees, 1) + " graden"
                    : "Wacht op de trekker",
                "Stuurhoek die van de trekker terugkomt");

            // Tilt sensor: 99999 is AgOpenGPS's "no IMU heading" marker, 88888 the "no roll" marker
            RijStatus(k, "Hellingsensor (IMU)",
                () => mf.ahrs.imuHeading != 99999 ? JohanTheme.Green : JohanTheme.Grey,
                () => mf.ahrs.imuHeading != 99999
                    ? (mf.ahrs.imuRoll != 88888 ? "Op de stuurprint, helling " + Komma(mf.ahrs.imuRoll, 1) + " graden" : "Op de stuurprint, geen helling")
                    : "Geen hellingsensor gezien",
                "Zit meestal op de stuurprint");

            // Current position, 5 decimals, N/Z and O/W
            RijTekst(k, "Positie nu", () =>
            {
                if (!gpsOk() || mf.AppModel == null) { return "Nog geen positie"; }
                double lat = mf.AppModel.CurrentLatLon.Latitude;
                double lon = mf.AppModel.CurrentLatLon.Longitude;
                return Komma(Math.Abs(lat), 5) + " " + (lat >= 0 ? "N" : "Z") + "   " + Komma(Math.Abs(lon), 5) + " " + (lon >= 0 ? "O" : "W");
            });

            // The original connections window, full screen
            RijKnoppen(k, "AgIO-venster", "Het originele verbindingenvenster, beeldvullend",
                ("Openen", () => mf.BeheerOpenAgIO()));
        }
    }
}
