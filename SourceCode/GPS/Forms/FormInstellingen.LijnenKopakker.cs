using System;
using AgLibrary.Johan;
using System.Drawing;
using System.Windows.Forms;
using AgOpenGPS.Properties;

namespace AgOpenGPS
{
    // Johan fork: chapter "Lijnen en kopakker" of the settings page (canvas proposal D).
    // Everyday behaviour of the guidance lines and the headland. No code needed.
    public partial class FormInstellingen
    {
        // Nudge step options in cm, as the canvas shows them (ToolSettings.setAS_snapDistance is in cm)
        private static readonly double[] VerschuifStappen = { 5, 10, 25 };

        private void HoofdstukLijnenKopakker()
        {
            var k = Hoofdstuk("Lijnen en kopakker", "Hoe de lijnen en de kopakker zich gedragen tijdens het rijden.");

            // Nudge step: ToolSettings.setAS_snapDistance, double in centimetres (FormVerschuif reads it * 0.01).
            // When the saved value is none of the three, no button lights up until one is chosen.
            RijKeuze(k, "Stap bij Lijn verschuiven", new[] { "5 cm", "10 cm", "25 cm" },
                () => Array.FindIndex(VerschuifStappen, s => Math.Abs(s - ToolSettings.Default.setAS_snapDistance) < 0.01),
                i => { ToolSettings.Default.setAS_snapDistance = VerschuifStappen[i]; Toepassen(); },
                "Hoeveel de lijn per tik opzij gaat");

            // Headland default: how many working passes the headland bar proposes for a field without one
            RijGetal(k, "Kopakker standaard", "werkgangen",
                () => Settings.Default.setDisplay_johanKopakkerGangen,
                v => { Settings.Default.setDisplay_johanKopakkerGangen = (int)Math.Round(v); Toepassen(); },
                1, 10, 1, 0, "Wat Kopakker instellen voorstelt bij een perceel zonder kopakker");

            // Beep when the tool crosses into the headland, optionally some metres early
            RijAanUit(k, "Piep bij de kopakker",
                () => Settings.Default.setDisplay_johanKopakkerPiep,
                aan => { Settings.Default.setDisplay_johanKopakkerPiep = aan; Toepassen(); },
                "Eén toon zodra het werktuig de kopakkerlijn overrijdt");
            RijGetal(k, "Piep zoveel meter eerder", "m",
                () => Settings.Default.setDisplay_johanKopakkerPiepAfstand,
                v => { Settings.Default.setDisplay_johanKopakkerPiepAfstand = v; Toepassen(); },
                0, 30, 0.5, 1, "0 = precies als het werktuig de lijn raakt; meer = eerder, gerekend vanaf het werktuig");

            // Sections off on the headland: the saved setting plus the live flag, as FormKopakker does
            RijAanUit(k, "Secties uit op de kopakker",
                () => ToolSettings.Default.setHeadland_isSectionControlled,
                aan =>
                {
                    ToolSettings.Default.setHeadland_isSectionControlled = aan;
                    mf.bnd.isSectionControlledByHeadland = aan;
                    Toepassen();
                },
                "De secties gaan vanzelf dicht op de kopakker");

            // Hydraulic lift: enabling it on the machine board is PGN 238 (setArdMac_isHydEnabled), so it is
            // only shown here. mf.vehicle.isHydLiftOn is the live state and resets with the headland.
            RijStatus(k, "Hef volgt de kopakker",
                () => mf.vehicle.isHydLiftOn ? JohanTheme.Green : JohanTheme.Grey,
                () =>
                {
                    if (ToolSettings.Default.setArdMac_isHydEnabled == 0) return "Hef op de print staat uit";
                    return mf.vehicle.isHydLiftOn ? "Aan" : "Uit";
                },
                "Alleen met de hef op de print",
                ("Heftijden…", () => mf.BeheerOpenConfig()));

            // Look ahead time for picking the next line, seconds with one decimal
            RijGetal(k, "Zoektijd volgende lijn", "s",
                () => Settings.Default.setAS_guidanceLookAheadTime,
                v => { Settings.Default.setAS_guidanceLookAheadTime = v; Toepassen(); },
                0.5, 5.0, 0.1, 1,
                "Hoe ver vooruit het programma de volgende lijn zoekt");

            // Neighbouring lines on each side of the active one. JohanLock.ApplyColours clamps values above 4
            // to 3 at every start, so 0..3 survives a restart. CABLine only reads the setting in its constructor,
            // hence the live copy is updated too (FormConfig does the same the other way round).
            RijKeuze(k, "Hulplijnen naast de actieve lijn", new[] { "Geen", "1", "2", "3" },
                () => Math.Max(0, Math.Min(3, Settings.Default.setAS_numGuideLines)),
                i =>
                {
                    Settings.Default.setAS_numGuideLines = i;
                    mf.ABLine.numGuideLines = i;
                    Toepassen();
                },
                "Lichte lijnen een werkbreedte verderop");

            // U-turn: JohanLock.Enforce switches it off at every start, so it cannot be set here.
            // The radius and boundary distance live in the original configuration screen.
            RijStatus(k, "U-bocht",
                () => Settings.Default.setFeatures.isUTurnOn ? JohanTheme.Green : JohanTheme.Grey,
                () => Settings.Default.setFeatures.isUTurnOn ? "Aan" : "Uit",
                "Staat uit; Johan keert zelf aan het eind van de lijn",
                ("Draaicirkel, afstand tot grens…", () => mf.BeheerOpenConfig()));
        }
    }
}
