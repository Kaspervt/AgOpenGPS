using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AgLibrary.Johan;
using AgLibrary.Logging;
using AgLibrary.Settings;
using AgOpenGPS.Properties;

namespace AgOpenGPS
{
    // Johan fork: chapter "Trekker" of the settings page (canvas proposal D).
    // Dimensions of the tractor and the antenna, plus the vehicle profile itself.
    // Everything is written into VehicleSettings/ToolSettings and applied with Toepassen().
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

            // Profile: which vehicle file is in use, switch to another one, or save a copy under a new name
            RijKnoppen(k, "Trekkerprofiel", null,
                ("Ander profiel", KiesTrekkerprofiel),
                ("Opslaan als…", TrekkerprofielOpslaanAls));
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

        // Simple full-screen list of the vehicle profiles on disk; tapping one loads it
        private void KiesTrekkerprofiel()
        {
            if (mf.isJobStarted)
            {
                mf.TimedMessageBox(2500, "Trekker", "Sluit eerst het perceel, dan kun je van trekker wisselen");
                return;
            }
            if (!Directory.Exists(RegistrySettings.vehiclesDirectory))
            {
                mf.TimedMessageBox(2500, "Trekker", "Er zijn nog geen trekkerprofielen");
                return;
            }

            string[] namen = Directory.GetFiles(RegistrySettings.vehiclesDirectory, "*.xml")
                .Where(f => CSettingsMigration.IsSettingsType(f, "VehicleSettings"))
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(n => n)
                .ToArray();
            if (namen.Length == 0)
            {
                mf.TimedMessageBox(2500, "Trekker", "Er zijn nog geen trekkerprofielen");
                return;
            }

            using (var f = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Bounds = Bounds,
                BackColor = JohanTheme.Bg,
                ForeColor = JohanTheme.Text,
                ShowInTaskbar = false,
                KeyPreview = true
            })
            {
                f.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) f.Close(); };
                int x = S(56);
                int listW = Math.Min(f.Width - 2 * x, S(900));
                f.Controls.Add(new Label { Text = "Welke trekker?", Font = JohanTheme.Font(28F, FontStyle.Bold), AutoSize = true, Location = new Point(x, S(36)) });
                f.Controls.Add(new Label { Text = "Het gekozen profiel blijft staan tot je wisselt", Font = JohanTheme.Font(11F), ForeColor = JohanTheme.Muted, AutoSize = true, Location = new Point(x + S(2), S(92)) });

                var lijst = new FlowLayoutPanel
                {
                    Bounds = new Rectangle(x, S(150), listW, f.Height - S(150) - S(130)),
                    FlowDirection = FlowDirection.TopDown,
                    WrapContents = false,
                    AutoScroll = true,
                    BackColor = JohanTheme.Bg
                };
                f.Controls.Add(lijst);

                foreach (string naam in namen)
                {
                    bool huidig = naam == RegistrySettings.vehicleProfileName;
                    var b = new JohanRoundButton
                    {
                        Text = naam + (huidig ? "   (nu in gebruik)" : ""),
                        Size = new Size(listW - S(24), S(72)),
                        Margin = new Padding(0, 0, 0, S(10)),
                        BackColor = huidig ? (JohanTheme.IsLight ? Color.FromArgb(191, 219, 254) : Color.FromArgb(30, 58, 95)) : JohanTheme.Panel,
                        ForeColor = JohanTheme.Text,
                        FlatStyle = FlatStyle.Flat,
                        Font = JohanTheme.Font(16F, FontStyle.Bold),
                        TextAlign = ContentAlignment.MiddleLeft,
                        Padding = new Padding(S(20), 0, 0, 0),
                        Tag = naam
                    };
                    b.FlatAppearance.BorderSize = huidig ? S(2) : 0;
                    b.FlatAppearance.BorderColor = JohanTheme.Green;
                    b.Click += (s, e) => { LaadTrekkerprofiel((string)((Button)s).Tag); f.Close(); };
                    lijst.Controls.Add(b);
                }

                var terug = new JohanRoundButton
                {
                    Text = "Terug",
                    Bounds = new Rectangle(f.Width - x - S(200), f.Height - S(100), S(200), S(64)),
                    BackColor = JohanTheme.PanelLight,
                    ForeColor = JohanTheme.Text,
                    FlatStyle = FlatStyle.Flat,
                    Font = JohanTheme.Font(14F, FontStyle.Bold)
                };
                terug.FlatAppearance.BorderSize = 0;
                terug.Click += (s, e) => f.Close();
                f.Controls.Add(terug);

                f.ShowDialog(this);
            }
        }

        // Same order as FormLoadVehicleTool: save the current one, load the new one, remember it, apply
        private void LaadTrekkerprofiel(string naam)
        {
            if (string.IsNullOrEmpty(naam) || naam == RegistrySettings.vehicleProfileName)
            {
                return;
            }
            try
            {
                VehicleSettings.Default.Save();   // keep unsaved changes of the current profile
                var result = VehicleSettings.Default.Load(naam);
                if (result != LoadResult.Ok)
                {
                    Log.EventWriter($"Instellingen: error loading vehicle {naam}.xml ({result})");
                    mf.TimedMessageBox(3000, "Trekker", "Kon " + naam + " niet laden");
                    return;
                }
                RegistrySettings.Save(RegKeys.vehicleProfileName, naam);
                // the vehicle only reads its settings in the constructor, so rebuild it (as FormLoadVehicleTool does)
                mf.vehicle = new CVehicle(mf);
                mf.LoadSettings();
                mf.SetVehicleTextures();
                mf.SendSettings();
                Log.EventWriter("Instellingen: vehicle loaded " + naam);
            }
            catch (Exception ex)
            {
                Log.EventWriter("Instellingen: vehicle load failed: " + ex.Message);
                mf.TimedMessageBox(3000, "Trekker", "Wisselen mislukt: " + ex.Message);
            }
        }

        // Write the current vehicle settings under a new name and switch to that profile
        private void TrekkerprofielOpslaanAls()
        {
            string naam = AgOpenGPS.Forms.FormInputDialog.ShowInput("Trekker", "Naam van het nieuwe profiel", mf, RegistrySettings.vehicleProfileName);
            if (string.IsNullOrWhiteSpace(naam))
            {
                return;
            }
            naam = naam.Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                naam = naam.Replace(c, ' ');
            }
            if (naam == RegistrySettings.vehicleProfileName)
            {
                VehicleSettings.Default.Save();
                mf.TimedMessageBox(2000, "Trekker", "Profiel opgeslagen");
                return;
            }
            try
            {
                Directory.CreateDirectory(RegistrySettings.vehiclesDirectory);
                string path = Path.Combine(RegistrySettings.vehiclesDirectory, naam + ".xml");
                if (File.Exists(path))
                {
                    mf.TimedMessageBox(2500, "Trekker", "Er is al een profiel met die naam");
                    return;
                }
                VehicleSettings.Default.Save();   // the current profile keeps what it has
                XmlSettingsHandler.SaveXMLFile(path, VehicleSettings.Default);
                RegistrySettings.Save(RegKeys.vehicleProfileName, naam);
                RegistrySettings.vehicleProfileName = naam;
                mf.LoadSettings();
                mf.SendSettings();
                Log.EventWriter("Instellingen: vehicle saved as " + naam);
                mf.TimedMessageBox(2000, "Trekker", "Opgeslagen als " + naam);
            }
            catch (Exception ex)
            {
                Log.EventWriter("Instellingen: save vehicle as failed: " + ex.Message);
                mf.TimedMessageBox(3000, "Trekker", "Opslaan mislukt: " + ex.Message);
            }
        }
    }
}
