using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using AgLibrary.Johan;
using AgOpenGPS.Properties;

namespace AgOpenGPS
{
    // Johan fork: chapter "Verbindingen instellen" (advanced, behind the code). This is what the
    // AgIO window used to do: GPS port and speed, steer board over network or USB, the NTRIP base
    // station. The values live in the AgIO profile that carries the tractor's name (JohanAgIO); every
    // change is written to that file at once, and "Nu verbinden" restarts AgIO so it picks them up.
    public partial class FormInstellingen
    {
        private bool verbindingGewijzigd;

        private void HoofdstukVerbindingenInstellen()
        {
            var k = Hoofdstuk("Verbindingen instellen", "GPS-poort, stuurprint en basisstation. Horen bij de trekker.", true);

            string profiel = string.IsNullOrEmpty(RegistrySettings.vehicleProfileName) ? JohanAgIO.ActiefProfiel : RegistrySettings.vehicleProfileName;
            JohanAgIO.ZorgProfiel(profiel);
            var d = JohanAgIO.Lees(profiel);
            Action<string, string> zet = (naam, waarde) =>
            {
                d[naam] = waarde;
                JohanAgIO.Schrijf(profiel, new Dictionary<string, string> { { naam, waarde } });
                verbindingGewijzigd = true;
            };

            RijTekst(k, "Profiel", () => "Trekker " + profiel + ". Wisselen van trekker wisselt deze instellingen mee.");

            // Serial ports present now; the saved name stays in the list even when the cable is out
            string[] poorten;
            try { poorten = System.IO.Ports.SerialPort.GetPortNames().OrderBy(p => p.Length).ThenBy(p => p).ToArray(); } catch { poorten = new string[0]; }
            Func<string, string[]> keuzes = huidig =>
            {
                var l = new List<string> { "Geen" };
                l.AddRange(poorten);
                if (!string.IsNullOrEmpty(huidig) && huidig.StartsWith("COM") && !l.Contains(huidig)) l.Add(huidig);
                return l.ToArray();
            };

            // Steer board (and GPS on it) over the network, or everything over USB
            RijAanUit(k, "Stuurprint via netwerk",
                () => JohanAgIO.Ja(d, "setUDP_isOn"),
                v => zet("setUDP_isOn", v ? "True" : "False"),
                "Netwerkprint (UDP). Uit: de print zit aan een USB-kabel");

            string gpsNu = JohanAgIO.Tekst(d, "setPort_portNameGPS");
            string[] gpsKeuzes = keuzes(gpsNu);
            RijKeuze(k, "GPS-ontvanger via USB", gpsKeuzes,
                () => Math.Max(0, Array.IndexOf(gpsKeuzes, JohanAgIO.Ja(d, "setPort_wasGPSConnected") ? JohanAgIO.Tekst(d, "setPort_portNameGPS") : "Geen")),
                i =>
                {
                    bool aan = i > 0;
                    zet("setPort_wasGPSConnected", aan ? "True" : "False");
                    if (aan) zet("setPort_portNameGPS", gpsKeuzes[i]);
                },
                "Geen: de positie komt via het netwerk");

            int[] snelheden = { 9600, 19200, 38400, 57600, 115200, 230400, 460800 };
            RijKeuze(k, "GPS-snelheid", snelheden.Select(s => s.ToString()).ToArray(),
                () => Math.Max(0, Array.IndexOf(snelheden, JohanAgIO.Getal(d, "setPort_baudRateGPS", 9600))),
                i => zet("setPort_baudRateGPS", snelheden[i].ToString()),
                "Baud, moet gelijk zijn aan de ontvanger");

            string stuurNu = JohanAgIO.Tekst(d, "setPort_portNameSteer");
            string[] stuurKeuzes = keuzes(stuurNu);
            RijKeuze(k, "Stuurprint via USB", stuurKeuzes,
                () => Math.Max(0, Array.IndexOf(stuurKeuzes, JohanAgIO.Ja(d, "setPort_wasSteerModuleConnected") ? JohanAgIO.Tekst(d, "setPort_portNameSteer") : "Geen")),
                i =>
                {
                    bool aan = i > 0;
                    zet("setPort_wasSteerModuleConnected", aan ? "True" : "False");
                    if (aan) zet("setPort_portNameSteer", stuurKeuzes[i]);
                },
                "Alleen als de print niet via het netwerk gaat");

            // Base station
            RijAanUit(k, "Basisstation (NTRIP)",
                () => JohanAgIO.Ja(d, "setNTRIP_isOn"),
                v => zet("setNTRIP_isOn", v ? "True" : "False"),
                "RTK-correcties via internet");
            RijTekstInvoer(k, "Adres", () => JohanAgIO.Tekst(d, "setNTRIP_casterURL"), v => zet("setNTRIP_casterURL", v), "Naam of IP-adres van de caster");
            RijGetal(k, "Poort", "", () => JohanAgIO.Getal(d, "setNTRIP_casterPort", 2101), v => zet("setNTRIP_casterPort", ((int)v).ToString()), 1, 65535, 1, 0);
            RijTekstInvoer(k, "Mountpoint", () => JohanAgIO.Tekst(d, "setNTRIP_mount"), v => zet("setNTRIP_mount", v));
            RijTekstInvoer(k, "Gebruikersnaam", () => JohanAgIO.Tekst(d, "setNTRIP_userName"), v => zet("setNTRIP_userName", v));
            RijTekstInvoer(k, "Wachtwoord", () => JohanAgIO.Tekst(d, "setNTRIP_userPassword"), v => zet("setNTRIP_userPassword", v), null, true);
            RijGetal(k, "Positie melden elke", "s", () => JohanAgIO.Getal(d, "setNTRIP_sendGGAInterval", 10), v => zet("setNTRIP_sendGGAInterval", ((int)v).ToString()), 0, 120, 5, 0,
                "Het basisstation wil weten waar je bent. 0 is nooit");

            // Apply: AgIO reads its profile at start, so restart it
            var vak = Rij(k, "Opnieuw verbinden", "AgIO start opnieuw met deze instellingen");
            var knop = Knop(vak, 0, S(200), "Nu verbinden", () =>
            {
                JohanAgIO.KoppelAanTrekker(profiel, herstart: true);
                verbindingGewijzigd = false;
                mf.TimedMessageBox(2500, "Verbindingen", "AgIO is opnieuw gestart");
            });
            verversers.Add(() =>
            {
                knop.BackColor = verbindingGewijzigd ? JohanTheme.Blue : JohanTheme.PanelLight;
                knop.ForeColor = verbindingGewijzigd ? Color.White : JohanTheme.Text;
                knop.Text = verbindingGewijzigd ? "Nu verbinden" : (JohanAgIO.Draait ? "Opnieuw verbinden" : "AgIO starten");
            });
        }
    }
}
