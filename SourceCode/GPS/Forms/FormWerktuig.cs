using System;
using AgLibrary.Johan;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AgLibrary.Logging;
using AgLibrary.Settings;
using AgOpenGPS.Properties;

namespace AgOpenGPS
{
    // Johan fork: implement picker, one list of tool profiles; tap to switch.
    // Used from the driver top bar; the start screen shares LoadTool.
    public class FormWerktuig : Form
    {
        private readonly FormGPS mf;

        public FormWerktuig(FormGPS formGPS)
        {
            mf = formGPS;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = Screen.FromControl(mf).Bounds;
            BackColor = JohanTheme.Bg;
            ForeColor = JohanTheme.Text;
            Font = JohanTheme.Font(14F);
            KeyPreview = true;
            ShowInTaskbar = false;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
            BuildLayout();
        }

        private void BuildLayout()
        {
            int w = Width, h = Height, x = 56;
            int listW = w - 2 * x;

            Controls.Add(new Label { Text = "Welk werktuig?", Font = JohanTheme.Font(28F, FontStyle.Bold), AutoSize = true, Location = new Point(x, 36) });
            Controls.Add(new Label { Text = "Het laatst gekozen werktuig blijft staan tot je wisselt", Font = JohanTheme.Font(11F), ForeColor = JohanTheme.Muted, AutoSize = true, Location = new Point(x + 2, 92) });

            var lijst = new FlowLayoutPanel
            {
                Bounds = new Rectangle(x, 150, listW, h - 150 - 130),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = JohanTheme.Bg
            };
            Controls.Add(lijst);

            if (Directory.Exists(RegistrySettings.toolsDirectory))
            {
                var files = Directory.GetFiles(RegistrySettings.toolsDirectory, "*.xml")
                    .Where(f => CSettingsMigration.IsSettingsType(f, "ToolSettings"))
                    .OrderBy(f => f)
                    .ToArray();

                foreach (string f in files)
                {
                    string name = Path.GetFileNameWithoutExtension(f);
                    bool selected = name == RegistrySettings.toolProfileName;
                    var b = new JohanRoundButton
                    {
                        Text = name + "      " + Breedte(f, selected ? mf.tool.width : double.NaN) + (selected ? "   (nu achter de trekker)" : ""),
                        Size = new Size(listW - 24, 72),
                        Margin = new Padding(0, 0, 0, 10),
                        BackColor = selected ? (JohanTheme.IsLight ? Color.FromArgb(191, 219, 254) : Color.FromArgb(30, 58, 95)) : JohanTheme.Panel,
                        ForeColor = JohanTheme.Text,
                        FlatStyle = FlatStyle.Flat,
                        Font = JohanTheme.Font(16F, FontStyle.Bold),
                        TextAlign = ContentAlignment.MiddleLeft,
                        Padding = new Padding(20, 0, 0, 0),
                        Tag = name
                    };
                    b.FlatAppearance.BorderSize = selected ? 2 : 0;
                    b.FlatAppearance.BorderColor = JohanTheme.Green;
                    b.Click += (s, e) => { LoadTool(mf, (string)((Button)s).Tag); Close(); };
                    lijst.Controls.Add(b);
                }
            }

            var terug = new JohanRoundButton
            {
                Text = "Terug",
                Bounds = new Rectangle(x, h - 100, 200, 64),
                BackColor = JohanTheme.Panel,
                ForeColor = JohanTheme.Text,
                FlatStyle = FlatStyle.Flat,
                Font = JohanTheme.Font(14F, FontStyle.Bold)
            };
            terug.FlatAppearance.BorderSize = 0;
            terug.Click += (s, e) => Close();
            Controls.Add(terug);

            // new tool: name and working width, copied from the current one, then loaded
            var plus = new JohanRoundButton
            {
                Text = "Nieuw werktuig",
                Image = JohanSkin.Icon("plus_licht_64", 28),
                Bounds = new Rectangle(w - x - 280, h - 100, 280, 64),
                BackColor = JohanTheme.Green,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = JohanTheme.Font(14F, FontStyle.Bold)
            };
            plus.FlatAppearance.BorderSize = 0;
            plus.Click += (s, e) => NieuwWerktuig();
            Controls.Add(plus);
        }

        private void NieuwWerktuig()
        {
            string name = AgOpenGPS.Forms.FormInputDialog.ShowInput("Nieuw werktuig", "Naam van het werktuig", mf);
            if (string.IsNullOrWhiteSpace(name)) return;
            name = name.Trim();
            string path = Path.Combine(RegistrySettings.toolsDirectory, name + ".xml");
            if (File.Exists(path)) { mf.TimedMessageBox(2500, "Werktuig", "Er is al een werktuig met die naam"); return; }

            double width = mf.tool.width;
            using (var num = new FormNumeric(0.5, 60, width))
            {
                if (num.ShowDialog(this) == DialogResult.OK) width = num.ReturnValue;
            }

            // start from the current tool, set the width, save under the new name and load it
            double oldWidth = ToolSettings.Default.setVehicle_toolWidth;
            ToolSettings.Default.setVehicle_toolWidth = width;
            XmlSettingsHandler.SaveXMLFile(path, ToolSettings.Default);
            ToolSettings.Default.setVehicle_toolWidth = oldWidth;
            Log.EventWriter("Tool picker: new tool " + name + " (" + width.ToString("0.0") + " m)");
            LoadTool(mf, name);
            Close();
        }

        // Working width straight from the profile file, so every tool in the list shows it
        private static string Breedte(string file, double loadedWidth)
        {
            if (!double.IsNaN(loadedWidth)) return loadedWidth.ToString("0.0") + " m";
            try
            {
                var doc = System.Xml.Linq.XDocument.Load(file);
                foreach (var e in doc.Descendants("setting"))
                    if ((string)e.Attribute("name") == "setVehicle_toolWidth")
                    {
                        double w = double.Parse(e.Element("value").Value, System.Globalization.CultureInfo.InvariantCulture);
                        return w.ToString("0.0") + " m";
                    }
            }
            catch { }
            return "";
        }

        // Same sequence as FormLoadVehicleTool, tool only. Returns true when switched.
        public static bool LoadTool(FormGPS mf, string name)
        {
            if (name == RegistrySettings.toolProfileName) return false;

            var result = ToolSettings.Default.Load(name);
            if (result != LoadResult.Ok)
            {
                Log.EventWriter($"Tool picker: error loading tool {name} ({result})");
                mf.TimedMessageBox(3000, "Werktuig", "Kon " + name + " niet laden");
                return false;
            }
            RegistrySettings.Save(RegKeys.toolProfileName, name);
            mf.tool = new CTool(mf);
            mf.LoadSettings();
            mf.SetVehicleTextures();
            mf.SendSettings();
            Log.EventWriter("Tool picker: tool loaded " + name);
            return true;
        }
    }
}
