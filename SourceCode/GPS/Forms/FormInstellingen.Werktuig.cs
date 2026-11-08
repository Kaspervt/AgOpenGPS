using System;
using AgLibrary.Johan;
using AgLibrary.Settings;
using System.Windows.Forms;
using AgOpenGPS.Properties;

namespace AgOpenGPS
{
    // Johan fork: chapter "Werktuig" of the settings page (canvas proposal D, card 1).
    // Everything is written into ToolSettings (and Settings for the work switch), then
    // applied with Toepassen(werktuig: true) so the tool is rebuilt the way FormConfig does it.
    public partial class FormInstellingen
    {
        private static ToolSettings Ts => ToolSettings.Default;

        private void HoofdstukWerktuig()
        {
            var k = Hoofdstuk("Werktuig", "Wat achter de trekker hangt en hoe het werkt.");

            // current tool profile: name and width, button opens the tool picker (choose, new, pencil to edit)
            Action werktuigKiezen = () =>
            {
                using (var f = new FormWerktuig(mf))
                {
                    f.ShowDialog(this);
                }
            };
            RijStatus(k, "Huidig werktuig",
                () => JohanTheme.Green,
                () => RegistrySettings.toolProfileName + "   " + Komma(Ts.setVehicle_toolWidth, 2) + " m",
                "Kiezen, nieuw maken of bewerken",
                ("Kiezen of bewerken", werktuigKiezen));

            // width, overlap and offset; width and section count share the section layout
            RijGetal(k, "Werkbreedte", "m",
                () => Ts.setVehicle_toolWidth,
                v => { Ts.setVehicle_toolWidth = v; ZetSecties(v, Ts.setVehicle_numSections); Toepassen(werktuig: true); },
                0.5, 60, 0.1, 2);

            RijGetal(k, "Overlap", "cm",
                () => Math.Round(Ts.setVehicle_toolOverlap * 100),
                v => { Ts.setVehicle_toolOverlap = v / 100.0; Toepassen(werktuig: true); },
                0, 200, 1, 0, "Overlap tussen twee werkgangen");

            RijGetal(k, "Zijwaartse verschuiving", "cm",
                () => Math.Round(Ts.setVehicle_toolOffset * 100),
                v => { Ts.setVehicle_toolOffset = v / 100.0; Toepassen(werktuig: true); },
                -500, 500, 1, 0, "Plus is rechts, voor een maaier die opzij staat");

            RijGetal(k, "Secties", "",
                () => Ts.setVehicle_numSections,
                v => { ZetSecties(Ts.setVehicle_toolWidth, (int)Math.Round(v)); Toepassen(werktuig: true); },
                1, 16, 1, 0, "Gelijke breedte; ongelijke secties of zones via de knop hieronder");

            RijKnoppen(k, "Ongelijke secties of zones", "Opent het AgOpenGPS-venster, tabblad Secties",
                ("Ongelijk…", () => mf.BeheerOpenConfig()));

            // attachment: trailing, fixed rear or front. The tank-between option stays in FormConfig.
            RijKeuze(k, "Aanbouw", new[] { "Getrokken", "Vast achter", "Voorop" },
                () => Ts.setTool_isToolFront ? 2 : Ts.setTool_isToolRearFixed ? 1 : 0,
                i =>
                {
                    Ts.setTool_isToolTrailing = i == 0;
                    Ts.setTool_isToolRearFixed = i == 1;
                    Ts.setTool_isToolFront = i == 2;
                    Ts.setTool_isToolTBT = false;
                    // the sign of the hitch length follows the attachment: front is forward (+), rear is back (-)
                    Ts.setVehicle_hitchLength = i == 2 ? Math.Abs(Ts.setVehicle_hitchLength) : -Math.Abs(Ts.setVehicle_hitchLength);
                    Toepassen(werktuig: true);
                });

            // trailing: drawbar to the tool; stored negative (behind the tractor)
            RijGetal(k, "Trekhaak tot werktuig", "m",
                () => Math.Abs(Ts.setVehicle_toolTrailingHitchLength),
                v => { Ts.setVehicle_toolTrailingHitchLength = -Math.Abs(v); Toepassen(werktuig: true); },
                0.1, 20, 0.1, 2, "Alleen bij getrokken: van de trekhaak tot het midden van het werktuig");

            // rear axle to the hitch point (trailing) or to the tool (fixed); sign follows the attachment
            RijGetal(k, "Achteras tot werktuig", "m",
                () => Math.Abs(Ts.setVehicle_hitchLength),
                v => { Ts.setVehicle_hitchLength = Ts.setTool_isToolFront ? Math.Abs(v) : -Math.Abs(v); Toepassen(werktuig: true); },
                0, 10, 0.1, 2, "Vast achter of voorop: tot het werktuig. Getrokken: tot de trekhaak");

            RijGetal(k, "Draaipunt werktuig", "m",
                () => Ts.setTool_trailingToolToPivotLength,
                v => { Ts.setTool_trailingToolToPivotLength = v; Toepassen(werktuig: true); },
                -5, 5, 0.1, 2, "Voor (plus) of achter (min) het wiel van het getrokken werktuig");

            // section timing
            RijGetal(k, "Vooruitkijken aan", "s",
                () => Ts.setVehicle_toolLookAheadOn,
                v => { Ts.setVehicle_toolLookAheadOn = v; Toepassen(werktuig: true); },
                0, 10, 0.1, 1, "Hoe vroeg een sectie aangaat");

            RijGetal(k, "Vooruitkijken uit", "s",
                () => Ts.setVehicle_toolLookAheadOff,
                v => { Ts.setVehicle_toolLookAheadOff = v; Toepassen(werktuig: true); },
                0, 10, 0.1, 1, "Hoe vroeg een sectie uitgaat");

            RijGetal(k, "Uitschakelvertraging", "s",
                () => Ts.setVehicle_toolOffDelay,
                v => { Ts.setVehicle_toolOffDelay = v; Toepassen(werktuig: true); },
                0, 10, 0.1, 1);

            RijAanUit(k, "Secties uit buiten de grens",
                () => Ts.setTool_isSectionOffWhenOut,
                b => { Ts.setTool_isSectionOffWhenOut = b; Toepassen(werktuig: true); });

            RijGetal(k, "Minimale dekking voor uitschakelen", "%",
                () => Ts.setVehicle_minCoverage,
                v => { Ts.setVehicle_minCoverage = Math.Round(v); Toepassen(werktuig: true); },
                0, 100, 5, 0, "Een sectie gaat pas uit als dit deel al gedaan is");

            RijGetal(k, "Secties uit onder", "km/u",
                () => Ts.setVehicle_slowSpeedCutoff,
                v => { Ts.setVehicle_slowSpeedCutoff = v; Toepassen(werktuig: true); },
                0, 30, 0.1, 1, "Langzamer dan dit en de secties gaan uit");

            // switches on the steer board (Settings holds the work switch, ToolSettings the steer switch)
            RijKeuze(k, "Werkschakelaar op de print", new[] { "Uit", "Handmatig", "Auto" },
                () => !Settings.Default.setF_isWorkSwitchEnabled ? 0 : Settings.Default.setF_isWorkSwitchManualSections ? 1 : 2,
                i =>
                {
                    Settings.Default.setF_isWorkSwitchEnabled = i > 0;
                    Settings.Default.setF_isWorkSwitchManualSections = i == 1;
                    Toepassen(werktuig: true);
                },
                "Alleen als er een schakelaar is aangesloten");

            RijAanUit(k, "Stuurschakelaar bedient secties",
                () => Ts.setF_isSteerWorkSwitchEnabled,
                b => { Ts.setF_isSteerWorkSwitchEnabled = b; Toepassen(werktuig: true); });
        }

        // Equal sections over the working width, the same layout FormWerktuigBewerken.Vul writes:
        // positions 1..n+1 from the left edge, the rest 0, zones off.
        private static void ZetSecties(double breedte, int secties)
        {
            secties = Math.Max(1, Math.Min(16, secties));
            Ts.setTool_isSectionsNotZones = true;
            Ts.setVehicle_numSections = secties;
            Ts.setTool_defaultSectionWidth = breedte / secties;
            decimal pos = (decimal)(-breedte * 0.5), sw = (decimal)(breedte / secties);
            for (int i = 1; i <= 17; i++)
            {
                var f = typeof(ToolSettings).GetField("setSection_position" + i);
                if (f == null)
                {
                    continue;
                }
                f.SetValue(Ts, i <= secties + 1 ? Math.Round(pos, 3) : 0M);
                pos += sw;
            }
        }
    }
}
