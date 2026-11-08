using System;
using AgLibrary.Johan;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using AgLibrary.Logging;
using AgLibrary.Settings;
using AgOpenGPS.Properties;

namespace AgOpenGPS
{
    // Johan fork: one full-screen page for a tool, used for a new tool and behind the pencil.
    // Name, how it is attached, working width, overlap, sideways offset, number of (equal)
    // sections and the hitch distance, as rows under each other. Saved into the tool profile
    // and applied at once, the same calls as the configuration screen uses.
    public class FormWerktuigBewerken : Form
    {
        private readonly FormGPS mf;
        private readonly bool nieuw;
        private string naam;
        private double breedte, overlap, offset, trekhaak, hef;
        private int secties;
        private int aanbouw;   // 0 trailing, 1 fixed rear, 2 front
        private Button btnNaam, btnAfstand;
        private Label lblAfstand, lblAfstandUitleg;
        private readonly Button[] btnAanbouw = new Button[3];

        // Design pixels (96 dpi) to the current DPI scale
        private static int S(int px) => JohanTheme.S(px);

        // toolName null = new tool (values start from the current one); file = the profile xml of an existing tool
        public FormWerktuigBewerken(FormGPS formGPS, string toolName, string file)
        {
            mf = formGPS;
            nieuw = toolName == null;
            naam = toolName ?? "";
            bool current = !nieuw && toolName == RegistrySettings.toolProfileName;
            if (nieuw || current)
            {
                breedte = mf.tool.width; overlap = mf.tool.overlap; offset = mf.tool.offset;
                secties = mf.tool.numOfSections;
                aanbouw = mf.tool.isToolFrontFixed ? 2 : mf.tool.isToolRearFixed ? 1 : 0;
                trekhaak = Math.Abs(mf.tool.trailingHitchLength);
                hef = Math.Abs(mf.tool.hitchLength);
            }
            else
            {
                breedte = FormWerktuig.Lees(file, "setVehicle_toolWidth", 3.0);
                overlap = FormWerktuig.Lees(file, "setVehicle_toolOverlap", 0.0);
                offset = FormWerktuig.Lees(file, "setVehicle_toolOffset", 0.0);
                secties = (int)FormWerktuig.Lees(file, "setVehicle_numSections", 1);
                bool front = FormWerktuig.LeesBool(file, "setTool_isToolFront", false), rear = FormWerktuig.LeesBool(file, "setTool_isToolRearFixed", false);
                aanbouw = front ? 2 : rear ? 1 : 0;
                trekhaak = Math.Abs(FormWerktuig.Lees(file, "setVehicle_toolTrailingHitchLength", 2.5));
                hef = Math.Abs(FormWerktuig.Lees(file, "setVehicle_hitchLength", 1.0));
            }
            secties = Math.Max(1, Math.Min(16, secties));

            Name = "FormWerktuigBewerken";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            BackColor = JohanTheme.Bg;
            ForeColor = JohanTheme.Text;
            Font = JohanTheme.Font(12F);
            ShowInTaskbar = false;
            Bounds = Screen.FromControl(mf).Bounds;
            BuildLayout();
        }

        private static string Komma(double v, string f) => v.ToString(f).Replace('.', ',');

        // A number through the AgOpenGPS keypad, with a title that says what is being entered
        private Control laatsteVeld;
        private double Vraag(string titel, string eenheid, double min, double max, double nu)
            => FormGetal.Vraag(this, titel, eenheid, min, max, nu, eenheid == "m" ? 2 : 0, laatsteVeld);

        private Button Rij(int y, string label, string eenheid, Func<string> waarde, Action tik, string uitleg = null)
        {
            int x = S(56);
            Controls.Add(new Label { Text = label, Font = JohanTheme.Font(15F), ForeColor = JohanTheme.Text, AutoSize = false, Bounds = new Rectangle(x, y, S(420), uitleg == null ? S(64) : S(38)), TextAlign = ContentAlignment.MiddleLeft });
            if (uitleg != null)
                Controls.Add(new Label { Text = uitleg, Font = JohanTheme.Font(10.5F), ForeColor = JohanTheme.Muted, AutoSize = false, Bounds = new Rectangle(x, y + S(36), S(420), S(26)), TextAlign = ContentAlignment.TopLeft });
            var b = new JohanRoundButton
            {
                Bounds = new Rectangle(x + S(440), y, S(240), S(64)),
                BackColor = JohanTheme.Panel,
                ForeColor = JohanTheme.Text,
                FlatStyle = FlatStyle.Flat,
                Font = JohanTheme.Font(18F, FontStyle.Bold),
                Text = waarde()
            };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (s, e) => { laatsteVeld = b; tik(); b.Text = waarde(); };
            Controls.Add(b);
            Controls.Add(new Label { Text = eenheid, Font = JohanTheme.Font(13F), ForeColor = JohanTheme.Muted, AutoSize = false, Bounds = new Rectangle(x + S(690), y, S(90), S(64)), TextAlign = ContentAlignment.MiddleLeft });
            return b;
        }

        private void BuildLayout()
        {
            int x = S(56);
            Controls.Add(new Label { Text = nieuw ? "Nieuw werktuig" : "Werktuig", Font = JohanTheme.Font(28F, FontStyle.Bold), AutoSize = true, Location = new Point(x, S(36)) });
            Controls.Add(new Label { Text = nieuw ? "Begint met de waarden van het huidige werktuig. Secties krijgen allemaal dezelfde breedte." : "Secties krijgen allemaal dezelfde breedte. Zeldzame instellingen: Beheer, Alles (AgOpenGPS).", Font = JohanTheme.Font(11F), ForeColor = JohanTheme.Muted, AutoSize = true, Location = new Point(x + S(2), S(92)) });

            int y = S(150), stap = S(76);
            btnNaam = Rij(y, "Naam", "", () => naam == "" ? "Kies een naam" : naam, () =>
            {
                string n = AgOpenGPS.Forms.FormInputDialog.ShowInput("Werktuig", "Naam van het werktuig", mf, naam);
                if (!string.IsNullOrWhiteSpace(n)) naam = n.Trim();
            });
            if (!nieuw) btnNaam.Enabled = false;   // renaming a profile file is a separate matter
            btnNaam.Font = JohanTheme.Font(16F, FontStyle.Bold);
            y += stap;

            // attachment: three buttons, one lit
            Controls.Add(new Label { Text = "Aanbouw", Font = JohanTheme.Font(15F), ForeColor = JohanTheme.Text, AutoSize = false, Bounds = new Rectangle(x, y, S(420), S(64)), TextAlign = ContentAlignment.MiddleLeft });
            string[] namen = { "Getrokken", "Vast achter", "Voorop" };
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                var b = new JohanRoundButton
                {
                    Bounds = new Rectangle(x + S(440) + i * S(176), y, S(164), S(64)),
                    Text = namen[i],
                    FlatStyle = FlatStyle.Flat,
                    Font = JohanTheme.Font(13F, FontStyle.Bold)
                };
                b.FlatAppearance.BorderSize = 0;
                b.Click += (s, e) => { aanbouw = k; ZetAanbouw(); };
                Controls.Add(b);
                btnAanbouw[i] = b;
            }
            y += stap;

            Rij(y, "Werkbreedte", "m", () => Komma(breedte, "0.00"), () => breedte = Vraag("Werkbreedte", "m", 0.5, 60, breedte)); y += stap;
            Rij(y, "Overlap tussen werkgangen", "cm", () => Math.Round(overlap * 100).ToString("0"), () => overlap = Vraag("Overlap", "cm", 0, 200, Math.Round(overlap * 100)) / 100.0); y += stap;
            Rij(y, "Zijwaartse verschuiving", "cm", () => Math.Round(offset * 100).ToString("0"), () => offset = Vraag("Zijwaartse verschuiving, plus is rechts", "cm", -500, 500, Math.Round(offset * 100)) / 100.0, "Plus is rechts van de trekker, bijvoorbeeld een maaier die opzij hangt"); y += stap;
            Rij(y, "Aantal secties", "", () => secties.ToString(), () => secties = (int)Math.Round(Vraag("Aantal secties", "", 1, 16, secties)), "1 als het werktuig geen secties heeft"); y += stap;

            lblAfstand = new Label { Font = JohanTheme.Font(15F), ForeColor = JohanTheme.Text, AutoSize = false, Bounds = new Rectangle(x, y, S(420), S(38)), TextAlign = ContentAlignment.MiddleLeft };
            lblAfstandUitleg = new Label { Font = JohanTheme.Font(10.5F), ForeColor = JohanTheme.Muted, AutoSize = false, Bounds = new Rectangle(x, y + S(36), S(420), S(26)) };
            Controls.Add(lblAfstand); Controls.Add(lblAfstandUitleg);
            btnAfstand = new JohanRoundButton { Bounds = new Rectangle(x + S(440), y, S(240), S(64)), BackColor = JohanTheme.Panel, ForeColor = JohanTheme.Text, FlatStyle = FlatStyle.Flat, Font = JohanTheme.Font(18F, FontStyle.Bold) };
            btnAfstand.FlatAppearance.BorderSize = 0;
            btnAfstand.Click += (s, e) =>
            {
                laatsteVeld = btnAfstand;
                if (aanbouw == 0) trekhaak = Vraag("Trekhaak tot het werktuig", "m", 0.1, 20, trekhaak);
                else hef = Vraag("Achteras tot het werktuig", "m", 0.0, 10, hef);
                ZetAanbouw();
            };
            Controls.Add(btnAfstand);
            Controls.Add(new Label { Text = "m", Font = JohanTheme.Font(13F), ForeColor = JohanTheme.Muted, AutoSize = false, Bounds = new Rectangle(x + S(690), y, S(90), S(64)), TextAlign = ContentAlignment.MiddleLeft });
            ZetAanbouw();

            var terug = new JohanRoundButton
            {
                Text = "Annuleren",
                Bounds = new Rectangle(x, Height - S(100), S(200), S(64)),
                BackColor = JohanTheme.Panel,
                ForeColor = JohanTheme.Text,
                FlatStyle = FlatStyle.Flat,
                Font = JohanTheme.Font(14F, FontStyle.Bold)
            };
            terug.FlatAppearance.BorderSize = 0;
            terug.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(terug);

            var opslaan = new JohanRoundButton
            {
                Text = nieuw ? "Aanmaken en gebruiken" : "Opslaan",
                Image = JohanSkin.Icon("check_licht_64", 28),
                Bounds = new Rectangle(Width - x - S(300), Height - S(100), S(300), S(64)),
                BackColor = JohanTheme.Green,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = JohanTheme.Font(14F, FontStyle.Bold)
            };
            opslaan.FlatAppearance.BorderSize = 0;
            opslaan.Click += (s, e) => { if (Opslaan()) { DialogResult = DialogResult.OK; Close(); } };
            Controls.Add(opslaan);
        }

        private void ZetAanbouw()
        {
            for (int i = 0; i < 3; i++)
            {
                bool aan = i == aanbouw;
                btnAanbouw[i].BackColor = aan ? JohanTheme.Blue : JohanTheme.Panel;
                btnAanbouw[i].ForeColor = aan ? Color.White : JohanTheme.Text;
            }
            if (aanbouw == 0)
            {
                lblAfstand.Text = "Trekhaak tot het werktuig";
                lblAfstandUitleg.Text = "Van de trekhaak tot het midden van het getrokken werktuig";
                btnAfstand.Text = Komma(trekhaak, "0.00");
            }
            else
            {
                lblAfstand.Text = "Achteras tot het werktuig";
                lblAfstandUitleg.Text = aanbouw == 1 ? "Van de achteras naar achteren tot het werktuig" : "Van de achteras naar voren tot het werktuig";
                btnAfstand.Text = Komma(hef, "0.00");
            }
        }

        // Writes the values into a ToolSettings object (the loaded profile, or a copy for a new one)
        private void Vul(ToolSettings ts)
        {
            ts.setVehicle_toolWidth = breedte;
            ts.setVehicle_toolOverlap = overlap;
            ts.setVehicle_toolOffset = offset;
            ts.setTool_isSectionsNotZones = true;
            ts.setVehicle_numSections = secties;
            ts.setTool_defaultSectionWidth = breedte / secties;
            decimal pos = (decimal)(-breedte * 0.5), sw = (decimal)(breedte / secties);
            for (int i = 1; i <= 17; i++)
            {
                var f = typeof(ToolSettings).GetField("setSection_position" + i);
                if (f == null) continue;
                f.SetValue(ts, i <= secties + 1 ? Math.Round(pos, 3) : 0M);
                pos += sw;
            }
            ts.setTool_isToolTrailing = aanbouw == 0;
            ts.setTool_isToolRearFixed = aanbouw == 1;
            ts.setTool_isToolFront = aanbouw == 2;
            ts.setTool_isToolTBT = false;
            if (aanbouw == 0) ts.setVehicle_toolTrailingHitchLength = -Math.Abs(trekhaak);
            else ts.setVehicle_hitchLength = aanbouw == 1 ? -Math.Abs(hef) : Math.Abs(hef);
        }

        private bool Opslaan()
        {
            if (nieuw)
            {
                if (naam.Length == 0) { mf.TimedMessageBox(2500, "Werktuig", "Geef het werktuig eerst een naam"); return false; }
                foreach (char c in Path.GetInvalidFileNameChars()) naam = naam.Replace(c, ' ');
                string path = Path.Combine(RegistrySettings.toolsDirectory, naam + ".xml");
                if (File.Exists(path)) { mf.TimedMessageBox(2500, "Werktuig", "Er is al een werktuig met die naam"); return false; }
                try
                {
                    // a copy of the current profile with the new values, saved under the new name, then loaded
                    var ts = ToolSettings.Default;
                    string huidig = Path.Combine(RegistrySettings.toolsDirectory, RegistrySettings.toolProfileName + ".xml");
                    XmlSettingsHandler.SaveXMLFile(huidig, ts);   // keep the current one as it is
                    Vul(ts);
                    XmlSettingsHandler.SaveXMLFile(path, ts);
                    ts.Load(RegistrySettings.toolProfileName);    // restore the current profile in memory
                    Log.EventWriter("Tool page: new tool " + naam + " (" + breedte.ToString("0.00") + " m, " + secties + " sections)");
                    FormWerktuig.LoadTool(mf, naam);
                    return true;
                }
                catch (Exception ex)
                {
                    Log.EventWriter("Tool page: new tool failed: " + ex.Message);
                    mf.TimedMessageBox(3000, "Werktuig", "Aanmaken mislukt: " + ex.Message);
                    return false;
                }
            }

            // existing: load it first when it is not the current one, then write and apply
            if (naam != RegistrySettings.toolProfileName && !FormWerktuig.LoadTool(mf, naam)) return false;
            try
            {
                Vul(ToolSettings.Default);
                ToolSettings.Default.Save();
                mf.tool = new CTool(mf);
                mf.LoadSettings();
                mf.SetVehicleTextures();
                mf.SendSettings();
                Log.EventWriter("Tool page: edited " + naam + " (" + breedte.ToString("0.00") + " m, " + secties + " sections)");
                return true;
            }
            catch (Exception ex)
            {
                Log.EventWriter("Tool page: edit failed: " + ex.Message);
                mf.TimedMessageBox(3000, "Werktuig", "Opslaan mislukt: " + ex.Message);
                return false;
            }
        }
    }
}
