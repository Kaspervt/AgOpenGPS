using System;
using AgLibrary.Johan;
using System.Drawing;
using System.Windows.Forms;
using AgOpenGPS.Properties;

namespace AgOpenGPS
{
    // Johan fork: chapter "Machine en relais" of the settings page (canvas D, 8-11).
    // Everything here ends up on the machine board (PGN 238 for the lift, PGN 236 for the pins).
    // The lift values are written into ToolSettings exactly as ConfigModule.SaveSettingsMachine does;
    // Toepassen() then runs mf.SendSettings(), which fills and sends PGN 238 from those settings,
    // so no separate send call is needed. The pin assignment itself stays in the original window.
    public partial class FormInstellingen
    {
        // bit 0 of setArdMac_setting0 = invert relays, bit 1 = hydraulic lift enabled (ConfigModule)
        private const int MacBitOmkeren = 1;
        private const int MacBitHef = 2;

        private static bool MacBit(int bit) => (ToolSettings.Default.setArdMac_setting0 & bit) == bit;

        private static void ZetMacBit(int bit, bool aan)
        {
            int sett = ToolSettings.Default.setArdMac_setting0;
            if (aan)
            {
                sett |= bit;
            }
            else
            {
                sett &= ~bit;
            }
            ToolSettings.Default.setArdMac_setting0 = (byte)sett;
        }

        private void HoofdstukMachineRelais()
        {
            var k = Hoofdstuk("Machine en relais", "Hef, extra uitgangen en rijpaden. Alleen met een machineprint.", true);

            // ---- hydraulic lift (PGN 238) ------------------------------------------------------
            RijAanUit(k, "Hydraulische hef",
                () => MacBit(MacBitHef),
                v =>
                {
                    ZetMacBit(MacBitHef, v);
                    // the separate byte is what CPGN_EE reads at start-up; keep both in step
                    ToolSettings.Default.setArdMac_isHydEnabled = (byte)(v ? 1 : 0);
                    Toepassen();
                },
                "De print zet de hef omhoog en omlaag op de kopakker");

            RijGetal(k, "Heftijd", "s",
                () => ToolSettings.Default.setArdMac_hydRaiseTime,
                v => { ToolSettings.Default.setArdMac_hydRaiseTime = (byte)v; Toepassen(); },
                1, 20, 1, 0, "Hoe lang het ventiel omhoog bekrachtigd blijft");

            RijGetal(k, "Zaktijd", "s",
                () => ToolSettings.Default.setArdMac_hydLowerTime,
                v => { ToolSettings.Default.setArdMac_hydLowerTime = (byte)v; Toepassen(); },
                1, 20, 1, 0, "Hoe lang het ventiel omlaag bekrachtigd blijft");

            RijGetal(k, "Vooruitkijken hef", "s",
                () => ToolSettings.Default.setVehicle_hydraulicLiftLookAhead,
                v => { ToolSettings.Default.setVehicle_hydraulicLiftLookAhead = v; Toepassen(); },
                0, 10, 0.1, 1, "Hoe vroeg de hef begint vóór de kopakker");

            // user bytes 1-4 go to the machine board as-is; what they mean depends on the firmware
            RijGetal(k, "Gebruiker 1", "",
                () => ToolSettings.Default.setArdMac_user1,
                v => { ToolSettings.Default.setArdMac_user1 = (byte)v; Toepassen(); },
                0, 255, 1, 0, "Vrije waarde voor de machineprint");
            RijGetal(k, "Gebruiker 2", "",
                () => ToolSettings.Default.setArdMac_user2,
                v => { ToolSettings.Default.setArdMac_user2 = (byte)v; Toepassen(); },
                0, 255, 1, 0, "Vrije waarde voor de machineprint");
            RijGetal(k, "Gebruiker 3", "",
                () => ToolSettings.Default.setArdMac_user3,
                v => { ToolSettings.Default.setArdMac_user3 = (byte)v; Toepassen(); },
                0, 255, 1, 0, "Vrije waarde voor de machineprint");
            RijGetal(k, "Gebruiker 4", "",
                () => ToolSettings.Default.setArdMac_user4,
                v => { ToolSettings.Default.setArdMac_user4 = (byte)v; Toepassen(); },
                0, 255, 1, 0, "Vrije waarde voor de machineprint");

            RijAanUit(k, "Relais omkeren",
                () => MacBit(MacBitOmkeren),
                v => { ZetMacBit(MacBitOmkeren, v); Toepassen(); },
                "Aan als de relais andersom schakelen dan bedoeld");

            // ---- pins (PGN 236): assign in the original window, reset to default here ---------
            Action pinnenStandaard = () =>
            {
                // same default as the "default" button in ConfigModule: pin 1-3 = sectie 1-3, rest leeg
                ToolSettings.Default.setRelay_pinConfig = "1,2,3,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0";
                Toepassen();
                mf.SendRelaySettingsToMachineModule();
                mf.TimedMessageBox(2000, "Machine en relais", "Pinnen teruggezet: 1 t/m 3 zijn sectie 1 t/m 3");
            };
            RijKnoppen(k, "Pinnen 1 t/m 24", "Welke uitgang wat doet; toewijzen gaat in het originele venster",
                ("Toewijzen in Configuratie", () => mf.BeheerOpenConfig()),
                ("Standaard", pinnenStandaard));

            // ---- tramlines ----------------------------------------------------------------------
            RijAanUit(k, "Rijpaden (spuitpaden)",
                () => ToolSettings.Default.setTool_isDisplayTramControl,
                v => { ToolSettings.Default.setTool_isDisplayTramControl = v; Toepassen(werktuig: true); },
                "Toon de knoppen voor rijpaden op het scherm");

            RijGetal(k, "Spoorbreedte rijpaden", "m",
                () => Settings.Default.setTram_tramWidth,
                v =>
                {
                    Settings.Default.setTram_tramWidth = v;
                    // the tram object is not rebuilt by LoadSettings; same two calls as ConfigModule
                    mf.tram.tramWidth = v;
                    mf.tram.IsTramOuterOrInner();
                    Toepassen();
                },
                1, 60, 0.5, 2, "Afstand tussen twee rijpaden, meestal de spuitbreedte");

            RijAanUit(k, "Rijpaden omkeren",
                () => ToolSettings.Default.setTool_isTramOuterInverted,
                v =>
                {
                    ToolSettings.Default.setTool_isTramOuterInverted = v;
                    mf.tram.IsTramOuterOrInner();
                    Toepassen();
                },
                "Als het eerste rijpad aan de verkeerde kant ligt");
        }
    }
}
