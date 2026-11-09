using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using AgLibrary.Johan;
using AgLibrary.Logging;
using AgLibrary.Settings;
using AgOpenGPS.Properties;

namespace AgOpenGPS
{
    // Johan fork: choosing and loading a tractor profile. Used by the start screen (so a tablet can
    // move from one tractor to another) and by the Trekker chapter of the settings page.
    // A tractor profile carries the vehicle settings and, with the same name, the AgIO connection
    // profile (see JohanAgIO), so one choice switches dimensions, steering, board and base station.
    public partial class FormGPS
    {
        // Vehicle profiles on disk, the AgOpenGPS default left out
        public static string[] JohanTrekkerprofielen()
        {
            if (!Directory.Exists(RegistrySettings.vehiclesDirectory)) return new string[0];
            return Directory.GetFiles(RegistrySettings.vehiclesDirectory, "*.xml")
                .Where(f => CSettingsMigration.IsSettingsType(f, "VehicleSettings"))
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => n != "DefaultVehicle")
                .OrderBy(n => n)
                .ToArray();
        }

        // Full-screen list "Welke trekker?"; returns true when another profile was loaded
        public bool JohanKiesTrekkerprofiel(IWin32Window owner)
        {
            string[] namen = JohanTrekkerprofielen();
            if (namen.Length == 0)
            {
                TimedMessageBox(2500, "Trekker", "Er zijn nog geen trekkerprofielen");
                return false;
            }

            bool gewisseld = false;
            var scr = Screen.FromControl(this).Bounds;
            using (var f = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Bounds = scr,
                BackColor = JohanTheme.Bg,
                ForeColor = JohanTheme.Text,
                ShowInTaskbar = false,
                KeyPreview = true
            })
            {
                f.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) f.Close(); };
                int x = JohanTheme.S(56);
                int listW = Math.Min(f.Width - 2 * x, JohanTheme.S(900));
                f.Controls.Add(new Label { Text = "Welke trekker?", Font = JohanTheme.Font(28F, FontStyle.Bold), AutoSize = true, Location = new Point(x, JohanTheme.S(36)) });
                f.Controls.Add(new Label { Text = "Maten, sturen en verbindingen horen bij de trekker. De keuze blijft staan tot je wisselt.", Font = JohanTheme.Font(11F), ForeColor = JohanTheme.Muted, AutoSize = true, Location = new Point(x + JohanTheme.S(2), JohanTheme.S(92)) });

                var lijst = new FlowLayoutPanel
                {
                    Bounds = new Rectangle(x, JohanTheme.S(150), listW, f.Height - JohanTheme.S(150) - JohanTheme.S(130)),
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
                        Size = new Size(listW - JohanTheme.S(24), JohanTheme.S(72)),
                        Margin = new Padding(0, 0, 0, JohanTheme.S(10)),
                        BackColor = huidig ? (JohanTheme.IsLight ? Color.FromArgb(191, 219, 254) : Color.FromArgb(30, 58, 95)) : JohanTheme.Panel,
                        ForeColor = JohanTheme.Text,
                        FlatStyle = FlatStyle.Flat,
                        Font = JohanTheme.Font(16F, FontStyle.Bold),
                        TextAlign = ContentAlignment.MiddleLeft,
                        Padding = new Padding(JohanTheme.S(20), 0, 0, 0),
                        Tag = naam
                    };
                    b.FlatAppearance.BorderSize = huidig ? JohanTheme.S(2) : 0;
                    b.FlatAppearance.BorderColor = JohanTheme.Green;
                    b.Click += async (s, e) =>
                    {
                        string gekozen = (string)((Button)s).Tag;
                        foreach (Control c in lijst.Controls) c.Enabled = false;   // one tap, no double switch
                        gewisseld = await JohanLaadTrekkerprofiel(gekozen);
                        f.Close();
                    };
                    lijst.Controls.Add(b);
                }

                // New tractor: copy of the current profile under a new name, behind the code
                var nieuw = new JohanRoundButton
                {
                    Text = "+  Nieuwe trekker",
                    Bounds = new Rectangle(x, f.Height - JohanTheme.S(100), JohanTheme.S(260), JohanTheme.S(64)),
                    BackColor = JohanTheme.Panel,
                    ForeColor = JohanTheme.Text,
                    FlatStyle = FlatStyle.Flat,
                    Font = JohanTheme.Font(14F, FontStyle.Bold)
                };
                nieuw.FlatAppearance.BorderSize = 0;
                nieuw.Click += async (s, e) =>
                {
                    if (!JohanCodeVragen(f, nieuw)) return;
                    string naam = FormTekst.Vraag(f, "Naam van de nieuwe trekker", "", 40);
                    if (naam == null) return;
                    foreach (Control c in lijst.Controls) c.Enabled = false;
                    gewisseld = await JohanNieuweTrekker(naam);
                    f.Close();
                };
                f.Controls.Add(nieuw);

                var terug = new JohanRoundButton
                {
                    Text = "Terug",
                    Bounds = new Rectangle(f.Width - x - JohanTheme.S(200), f.Height - JohanTheme.S(100), JohanTheme.S(200), JohanTheme.S(64)),
                    BackColor = JohanTheme.PanelLight,
                    ForeColor = JohanTheme.Text,
                    FlatStyle = FlatStyle.Flat,
                    Font = JohanTheme.Font(14F, FontStyle.Bold)
                };
                terug.FlatAppearance.BorderSize = 0;
                terug.Click += (s, e) => f.Close();
                f.Controls.Add(terug);

                f.ShowDialog(owner);
            }
            return gewisseld;
        }

        // A new tractor profile: the current vehicle settings and the current connection profile are
        // copied under the new name, then that profile is loaded. The dimensions are then changed in
        // the Trekker chapter of the settings page (advanced, behind the code).
        public async Task<bool> JohanNieuweTrekker(string naam)
        {
            naam = naam.Trim();
            foreach (char c in Path.GetInvalidFileNameChars()) naam = naam.Replace(c, ' ');
            if (naam.Length == 0) return false;
            try
            {
                Directory.CreateDirectory(RegistrySettings.vehiclesDirectory);
                string pad = Path.Combine(RegistrySettings.vehiclesDirectory, naam + ".xml");
                if (File.Exists(pad))
                {
                    TimedMessageBox(2500, "Trekker", "Er is al een trekker met die naam");
                    return false;
                }
                VehicleSettings.Default.Save();   // the current profile keeps what it has
                XmlSettingsHandler.SaveXMLFile(pad, VehicleSettings.Default);
                JohanAgIO.ZorgProfiel(naam);      // connections: a copy of the current ones
                Log.EventWriter("Trekker: new profile " + naam);
            }
            catch (Exception ex)
            {
                Log.EventWriter("Trekker: new profile failed: " + ex.Message);
                TimedMessageBox(3000, "Trekker", "Aanmaken mislukt: " + ex.Message);
                return false;
            }
            return await JohanLaadTrekkerprofiel(naam);
        }

        // Same order as FormLoadVehicleTool: save the current one, load the new one, remember it, apply.
        // Then AgIO follows: its profile of the same name becomes active and it restarts.
        // An open field (the kiosk resumes the last one before the start screen) is closed and saved
        // first and opened again afterwards, so the driver does not notice.
        public async Task<bool> JohanLaadTrekkerprofiel(string naam)
        {
            if (string.IsNullOrEmpty(naam) || naam == RegistrySettings.vehicleProfileName) return false;
            bool veldOpen = isJobStarted;
            try
            {
                if (veldOpen)
                {
                    await FileSaveEverythingBeforeClosingField();
                }
                VehicleSettings.Default.Save();   // keep unsaved changes of the current profile
                var result = VehicleSettings.Default.Load(naam);
                if (result != LoadResult.Ok)
                {
                    Log.EventWriter($"Trekker: error loading vehicle {naam}.xml ({result})");
                    TimedMessageBox(3000, "Trekker", "Kon " + naam + " niet laden");
                    return false;
                }
                RegistrySettings.Save(RegKeys.vehicleProfileName, naam);
                // the vehicle only reads its settings in the constructor, so rebuild it (as FormLoadVehicleTool does)
                vehicle = new CVehicle(this);
                LoadSettings();
                SetVehicleTextures();
                SendSettings();
                Log.EventWriter("Trekker: vehicle loaded " + naam);

                JohanAgIO.KoppelAanTrekker(naam, herstart: true);
                if (veldOpen)
                {
                    await FileOpenField("Resume");   // the same field again, now under the new tractor
                }
                TimedMessageBox(2500, "Trekker", naam + " geladen, verbindingen worden opnieuw gemaakt");
                return true;
            }
            catch (Exception ex)
            {
                Log.EventWriter("Trekker: vehicle load failed: " + ex.Message);
                TimedMessageBox(3000, "Trekker", "Wisselen mislukt: " + ex.Message);
                return false;
            }
        }
    }
}
