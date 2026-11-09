using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using AgLibrary.Logging;
using AgLibrary.Settings;
using AgOpenGPS.Properties;

namespace AgOpenGPS
{
    // Johan fork: chapter "Trekker" of the settings page (canvas proposal D).
    // Dimensions of the tractor and the antenna, plus the vehicle profile itself.
    // Everything is written into VehicleSettings/ToolSettings and applied with Toepassen().
    // Choosing and loading a profile is shared with the start screen (FormGPS.JohanKiesTrekkerprofiel).
    public partial class FormInstellingen
    {
        private void HoofdstukTrekker()
        {
            var k = Hoofdstuk("Trekker", "Maten van deze trekker. Eén keer per trekker.", true);

            // Wheelbase, track width, antenna position and vehicle type are only read from the
            // settings in the CVehicle constructor (mf.LoadSettings() does not touch them), so the
            // vehicle is rebuilt from the saved settings before Toepassen(), the way FormSteer and
            // FormLoadVehicleTool do. Nothing is written into mf.vehicle directly.
            Action toepassenTrekker = () =>
            {
                mf.vehicle = new CVehicle(mf);
                Toepassen();
            };
            Action toepassenTrekkerEnWerktuig = () =>
            {
                mf.vehicle = new CVehicle(mf);
                Toepassen(true);
            };

            // Profile: which vehicle file is in use, switch to another one, or save a copy under a new name.
            // Switching loads other connection settings too, so the page is rebuilt afterwards.
            Action anderProfiel = () =>
            {
                // rebuild after the click has finished: the button that was tapped lives in a card that goes away
                if (mf.JohanKiesTrekkerprofiel(this)) BeginInvoke(new Action(Herbouw));
            };
            Action nieuweTrekker = async () =>
            {
                string naam = FormTekst.Vraag(this, "Naam van de nieuwe trekker", "", 40);
                if (naam == null) return;
                if (await mf.JohanNieuweTrekker(naam)) Herbouw();
            };
            RijKnoppen(k, "Trekkerprofiel", "Nieuw = kopie van deze trekker, daarna hier de maten aanpassen",
                ("Andere trekker", anderProfiel),
                ("+ Nieuwe trekker", nieuweTrekker));
            verversers.Add(() =>
            {
                // the label of the row shows the current profile name
                foreach (Control rij in k.Controls)
                {
                    if (rij.Tag is string t && t == "rij" && rij.Controls.Count > 0 && rij.Controls[0] is Label l && l.Text.StartsWith("Trekkerprofiel"))
                    {
                        string naam = string.IsNullOrEmpty(RegistrySettings.vehicleProfileName) ? "geen" : RegistrySettings.vehicleProfileName;
                        l.Text = "Trekkerprofiel: " + naam;
                    }
                }
            });

            RijKeuze(k, "Type", new[] { "Trekker", "Maaidorser", "Knikstuur" },
                () => Math.Max(0, Math.Min(2, VehicleSettings.Default.setVehicle_vehicleType)),
                v => { VehicleSettings.Default.setVehicle_vehicleType = v; toepassenTrekkerEnWerktuig(); });

            RijGetal(k, "Wielbasis", "m",
                () => VehicleSettings.Default.setVehicle_wheelbase,
                v => { VehicleSettings.Default.setVehicle_wheelbase = v; toepassenTrekker(); },
                0.5, 10, 0.01, 2);

            RijGetal(k, "Spoorbreedte", "m",
                () => VehicleSettings.Default.setVehicle_trackWidth,
                v => { VehicleSettings.Default.setVehicle_trackWidth = v; toepassenTrekker(); },
                0.5, 6, 0.01, 2);

            RijGetal(k, "Antenne hoogte", "m",
                () => VehicleSettings.Default.setVehicle_antennaHeight,
                v => { VehicleSettings.Default.setVehicle_antennaHeight = v; toepassenTrekker(); },
                0, 6, 0.01, 2);

            RijGetal(k, "Antenne voor de achteras", "m",
                () => VehicleSettings.Default.setVehicle_antennaPivot,
                v => { VehicleSettings.Default.setVehicle_antennaPivot = v; toepassenTrekker(); },
                -5, 10, 0.01, 2);

            RijGetal(k, "Antenne zijwaarts", "m",
                () => VehicleSettings.Default.setVehicle_antennaOffset,
                v => { VehicleSettings.Default.setVehicle_antennaOffset = v; toepassenTrekker(); },
                -3, 3, 0.01, 2, "Plus is rechts");

            // Hitch length lives in ToolSettings. Internally negative = behind the rear axle,
            // positive = in front (front tool). Show the distance, keep the sign.
            RijGetal(k, "Heflengte", "m",
                () => Math.Abs(ToolSettings.Default.setVehicle_hitchLength),
                v =>
                {
                    bool voorop = ToolSettings.Default.setTool_isToolFront;
                    ToolSettings.Default.setVehicle_hitchLength = voorop ? Math.Abs(v) : -Math.Abs(v);
                    Toepassen(true);
                },
                0, 10, 0.01, 2, "Achteras tot de hef");
        }

    }
}
