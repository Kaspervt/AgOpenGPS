using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AgLibrary.Logging;
using AgLibrary.Settings;
using AgOpenGPS.Properties;

namespace AgOpenGPS
{
    // Johan fork: start screen shown on top of the main form in kiosk mode.
    // Photo, four status checks, implement choice and one big "Rijden" button.
    // Built in code (no designer file) so it is easy to keep apart from upstream.
    public class FormStart : Form
    {
        private readonly FormGPS mf;
        private readonly Timer tick = new Timer { Interval = 500 };

        private readonly Color bg = Color.FromArgb(15, 23, 42);
        private readonly Color panel = Color.FromArgb(30, 41, 59);
        private readonly Color text = Color.FromArgb(248, 250, 252);
        private readonly Color muted = Color.FromArgb(148, 163, 184);
        private readonly Color green = Color.FromArgb(22, 163, 74);
        private readonly Color amber = Color.FromArgb(245, 158, 11);
        private readonly Color red = Color.FromArgb(220, 38, 38);

        private readonly StatusRow[] rows = new StatusRow[4];
        private FlowLayoutPanel toolRow;
        private Label lblField;

        public FormStart(FormGPS formGPS)
        {
            mf = formGPS;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = Screen.FromControl(mf).Bounds;
            BackColor = bg;
            ForeColor = text;
            Font = new Font("Segoe UI", 14F, FontStyle.Regular, GraphicsUnit.Point);
            KeyPreview = true;
            ShowInTaskbar = false;

            BuildLayout();

            tick.Tick += (s, e) => UpdateStatus();
            Load += (s, e) => { UpdateStatus(); tick.Start(); };
            FormClosed += (s, e) => tick.Stop();
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        }

        private void BuildLayout()
        {
            int w = Width, h = Height;
            int leftW = w / 2;

            // Left half: photo or placeholder, with name overlay
            var photo = new PictureBox
            {
                Bounds = new Rectangle(0, 0, leftW, h),
                BackColor = panel,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            string photoPath = Path.Combine(RegistrySettings.environmentDirectory, "startfoto.jpg");
            if (File.Exists(photoPath))
            {
                try { photo.Image = Image.FromFile(photoPath); } catch { }
            }
            Controls.Add(photo);

            var lblSub = new Label
            {
                Text = "AgOpenGPS",
                ForeColor = muted,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(40, h - 150)
            };
            var lblTitle = new Label
            {
                Text = RegistrySettings.vehicleProfileName,
                ForeColor = text,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 32F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(36, h - 120)
            };
            // Labels inside the picture box so they draw over the photo
            photo.Controls.Add(lblSub);
            photo.Controls.Add(lblTitle);
            if (photo.Image == null)
            {
                photo.Controls.Add(new Label
                {
                    Text = "Zet een foto als startfoto.jpg in de map Environment",
                    ForeColor = muted,
                    BackColor = Color.Transparent,
                    AutoSize = true,
                    Location = new Point(40, h / 2)
                });
            }

            // Right half
            int x = leftW + 48;
            int rightW = w - x - 48;
            int y = 40;

            Controls.Add(new Label { Text = "Klaar om te rijden?", ForeColor = muted, AutoSize = true, Location = new Point(x, y) });
            y += 40;

            string[] names = { "GPS", "RTK-correcties", "Stuurprint", "Trekker" };
            for (int i = 0; i < 4; i++)
            {
                rows[i] = new StatusRow(names[i], panel, text, muted)
                {
                    Bounds = new Rectangle(x, y, rightW, 64)
                };
                Controls.Add(rows[i]);
                y += 74;
            }

            y += 10;
            Controls.Add(new Label { Text = "Werktuig", ForeColor = muted, AutoSize = true, Location = new Point(x, y) });
            y += 32;
            toolRow = new FlowLayoutPanel
            {
                Bounds = new Rectangle(x, y, rightW, 84),
                BackColor = bg,
                WrapContents = false,
                AutoScroll = false
            };
            Controls.Add(toolRow);
            FillTools();
            y += 100;

            var btnGo = new Button
            {
                Text = "Rijden",
                Bounds = new Rectangle(x, h - 170, rightW, 90),
                BackColor = green,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 24F, FontStyle.Bold)
            };
            btnGo.FlatAppearance.BorderSize = 0;
            btnGo.Click += (s, e) => Close();
            Controls.Add(btnGo);

            lblField = new Label
            {
                ForeColor = muted,
                Font = new Font("Segoe UI", 10F),
                AutoSize = true,
                Location = new Point(x, h - 64)
            };
            Controls.Add(lblField);

            var lblBeheer = new Label
            {
                Text = "Beheer: houd het menu 3 s ingedrukt",
                ForeColor = muted,
                Font = new Font("Segoe UI", 10F),
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleRight
            };
            lblBeheer.Location = new Point(w - 48 - 300, h - 64);
            Controls.Add(lblBeheer);
        }

        private void FillTools()
        {
            toolRow.Controls.Clear();
            if (!Directory.Exists(RegistrySettings.toolsDirectory)) return;

            var files = Directory.GetFiles(RegistrySettings.toolsDirectory, "*.xml")
                .Where(f => CSettingsMigration.IsSettingsType(f, "ToolSettings"))
                .OrderBy(f => f)
                .ToArray();

            foreach (string f in files)
            {
                string name = Path.GetFileNameWithoutExtension(f);
                bool selected = name == RegistrySettings.toolProfileName;
                var b = new Button
                {
                    Text = name + (selected ? "\n" + mf.tool.width.ToString("0.0") + " m" : ""),
                    Size = new Size(150, 76),
                    Margin = new Padding(0, 0, 10, 0),
                    BackColor = panel,
                    ForeColor = text,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                    Tag = name
                };
                b.FlatAppearance.BorderSize = 2;
                b.FlatAppearance.BorderColor = selected ? green : panel;
                b.Click += ToolButton_Click;
                toolRow.Controls.Add(b);
            }
        }

        private void ToolButton_Click(object sender, EventArgs e)
        {
            string name = (string)((Button)sender).Tag;
            if (name == RegistrySettings.toolProfileName) return;

            // Same sequence as FormLoadVehicleTool, tool only
            var result = ToolSettings.Default.Load(name);
            if (result != LoadResult.Ok)
            {
                Log.EventWriter($"Start screen: error loading tool {name} ({result})");
                mf.TimedMessageBox(3000, "Werktuig", "Kon " + name + " niet laden");
                return;
            }
            RegistrySettings.Save(RegKeys.toolProfileName, name);
            mf.tool = new CTool(mf);
            mf.LoadSettings();
            mf.SetVehicleTextures();
            mf.SendSettings();
            Log.EventWriter("Start screen: tool loaded " + name);
            FillTools();
        }

        private void UpdateStatus()
        {
            // GPS
            int fq = mf.pn.fixQuality;
            bool gpsOk = fq > 0 && mf.sentenceCounter < 20;
            rows[0].Set(gpsOk ? green : red, gpsOk ? mf.pn.satellitesTracked + " satellieten" : "Geen positie");

            // RTK
            Color rtkColor = fq == 4 || fq == 8 ? green : (fq == 5 ? amber : red);
            string rtkText = fq == 4 ? "RTK fix, correctie " + mf.pn.age.ToString("0") + " s oud"
                           : fq == 5 ? "RTK float, nog niet nauwkeurig"
                           : fq == 8 ? "Simulator"
                           : "Geen correcties";
            rows[1].Set(rtkColor, rtkText);

            // Steer board
            bool boardOk = mf.steerModuleConnectedCounter < 30;
            rows[2].Set(boardOk ? green : red, boardOk ? "Verbonden" : "Geen verbinding met de stuurprint");

            // Tractor: steer angle comes back from the board
            bool tractorOk = boardOk && Math.Abs(mf.mc.actualSteerAngleDegrees) < 60;
            rows[3].Set(tractorOk ? green : amber,
                tractorOk ? "Stuurhoek " + mf.mc.actualSteerAngleDegrees.ToString("0.0") + " graden" : "Wacht op de trekker");

            lblField.Text = string.IsNullOrEmpty(mf.currentFieldDirectory)
                ? "Nog geen perceel"
                : "Laatste perceel: " + mf.currentFieldDirectory;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(bg);
        }

        // One status line: colored dot, name, detail
        private class StatusRow : Panel
        {
            private readonly Label lblName, lblDetail;
            private Color dot = Color.Gray;

            public StatusRow(string name, Color back, Color fore, Color mutedColor)
            {
                BackColor = back;
                lblName = new Label { Text = name, ForeColor = fore, Font = new Font("Segoe UI", 14F, FontStyle.Bold), AutoSize = true, Location = new Point(64, 8), BackColor = Color.Transparent };
                lblDetail = new Label { Text = "", ForeColor = mutedColor, Font = new Font("Segoe UI", 10F), AutoSize = true, Location = new Point(64, 36), BackColor = Color.Transparent };
                Controls.Add(lblName);
                Controls.Add(lblDetail);
            }

            public void Set(Color c, string detail)
            {
                if (dot != c) { dot = c; Invalidate(); }
                if (lblDetail.Text != detail) lblDetail.Text = detail;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(dot)) e.Graphics.FillEllipse(b, 18, 18, 28, 28);
                if (dot.G > 120 && dot.R < 100)
                {
                    using (var p = new Pen(Color.White, 3f))
                        e.Graphics.DrawLines(p, new[] { new Point(25, 32), new Point(30, 37), new Point(39, 26) });
                }
            }
        }
    }
}
