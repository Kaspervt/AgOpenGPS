using System;
using AgLibrary.Johan;
using System.Drawing;
using System.Windows.Forms;
using AgLibrary.Logging;
using AgOpenGPS.Properties;

namespace AgOpenGPS
{
    // Johan fork: the few implement settings a driver changes, behind the pencil in the tool list.
    // Working width, overlap, sideways offset and the number of (equal) sections. Saved into the
    // tool profile and applied at once, the same calls as the configuration screen uses.
    public class FormWerktuigBewerken : Form
    {
        private readonly FormGPS mf;
        private readonly string naam;
        private double breedte, overlap, offset;
        private int secties;
        private Button btnBreedte, btnOverlap, btnOffset, btnSecties;

        // Design pixels (96 dpi) to the current DPI scale
        private static int S(int px) => JohanTheme.S(px);

        public FormWerktuigBewerken(FormGPS formGPS, string toolName, double width, double toolOverlap, double toolOffset, int numSections)
        {
            mf = formGPS;
            naam = toolName;
            breedte = width; overlap = toolOverlap; offset = toolOffset; secties = Math.Max(1, Math.Min(16, numSections));
            Name = "FormWerktuigBewerken";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            BackColor = JohanTheme.Bg;
            ForeColor = JohanTheme.Text;
            Font = JohanTheme.Font(12F);
            ShowInTaskbar = false;
            var scr = Screen.FromControl(mf).Bounds;
            int w = S(760), h = S(600);
            Bounds = new Rectangle(scr.Left + (scr.Width - w) / 2, scr.Top + (scr.Height - h) / 2, w, h);
            BuildLayout();
        }

        private Button Rij(int y, string label, string eenheid, Func<string> waarde, Action tik)
        {
            Controls.Add(new Label { Text = label, Font = JohanTheme.Font(14F), ForeColor = JohanTheme.Text, AutoSize = false, Bounds = new Rectangle(S(48), y, S(330), S(64)), TextAlign = ContentAlignment.MiddleLeft });
            var b = new JohanRoundButton
            {
                Bounds = new Rectangle(S(400), y, S(220), S(64)),
                BackColor = JohanTheme.Panel,
                ForeColor = JohanTheme.Text,
                FlatStyle = FlatStyle.Flat,
                Font = JohanTheme.Font(18F, FontStyle.Bold),
                Text = waarde()
            };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (s, e) => { tik(); b.Text = waarde(); };
            Controls.Add(b);
            Controls.Add(new Label { Text = eenheid, Font = JohanTheme.Font(12F), ForeColor = JohanTheme.Muted, AutoSize = false, Bounds = new Rectangle(S(630), y, S(90), S(64)), TextAlign = ContentAlignment.MiddleLeft });
            return b;
        }

        private double Vraag(double min, double max, double nu)
        {
            using (var num = new FormNumeric(min, max, nu))
            {
                return num.ShowDialog(this) == DialogResult.OK ? num.ReturnValue : nu;
            }
        }

        private void BuildLayout()
        {
            Controls.Add(new Label { Text = naam, Font = JohanTheme.Font(24F, FontStyle.Bold), AutoSize = true, Location = new Point(S(48), S(36)) });
            Controls.Add(new Label { Text = "Secties krijgen allemaal dezelfde breedte. Andere instellingen: Beheer, Configuratie.", Font = JohanTheme.Font(11F), ForeColor = JohanTheme.Muted, AutoSize = true, Location = new Point(S(50), S(84)) });

            int y = S(130), stap = S(76);
            btnBreedte = Rij(y, "Werkbreedte", "m", () => breedte.ToString("0.00").Replace('.', ','), () => breedte = Vraag(0.5, 60, breedte)); y += stap;
            btnOverlap = Rij(y, "Overlap tussen werkgangen", "cm", () => Math.Round(overlap * 100).ToString("0"), () => overlap = Vraag(0, 200, Math.Round(overlap * 100)) / 100.0); y += stap;
            btnOffset = Rij(y, "Zijwaartse verschuiving (+ rechts)", "cm", () => Math.Round(offset * 100).ToString("0"), () => offset = Vraag(-500, 500, Math.Round(offset * 100)) / 100.0); y += stap;
            btnSecties = Rij(y, "Aantal secties", "", () => secties.ToString(), () => secties = (int)Math.Round(Vraag(1, 16, secties))); y += stap;

            var terug = new JohanRoundButton
            {
                Text = "Annuleren",
                Bounds = new Rectangle(S(48), Height - S(96), S(200), S(64)),
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
                Text = "Opslaan",
                Image = JohanSkin.Icon("check_licht_64", 28),
                Bounds = new Rectangle(Width - S(48) - S(240), Height - S(96), S(240), S(64)),
                BackColor = JohanTheme.Green,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = JohanTheme.Font(14F, FontStyle.Bold)
            };
            opslaan.FlatAppearance.BorderSize = 0;
            opslaan.Click += (s, e) => { if (Opslaan()) { DialogResult = DialogResult.OK; Close(); } };
            Controls.Add(opslaan);
        }

        // Into the loaded profile: the tool is loaded first when it is not the current one
        private bool Opslaan()
        {
            if (naam != RegistrySettings.toolProfileName && !FormWerktuig.LoadTool(mf, naam)) return false;
            try
            {
                var ts = ToolSettings.Default;
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
                ts.Save();
                mf.tool = new CTool(mf);
                mf.LoadSettings();
                mf.SetVehicleTextures();
                mf.SendSettings();
                Log.EventWriter("Tool picker: edited " + naam + " (" + breedte.ToString("0.00") + " m, " + secties + " sections)");
                return true;
            }
            catch (Exception ex)
            {
                Log.EventWriter("Tool picker: edit failed: " + ex.Message);
                mf.TimedMessageBox(3000, "Werktuig", "Opslaan mislukt: " + ex.Message);
                return false;
            }
        }
    }
}
