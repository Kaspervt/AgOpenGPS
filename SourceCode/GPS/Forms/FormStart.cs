using System;
using AgLibrary.Johan;
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

        // palette follows the theme (light or dark)
        private readonly Color bg = JohanTheme.Bg;
        private readonly Color panel = JohanTheme.Panel;
        private readonly Color text = JohanTheme.Text;
        private readonly Color muted = JohanTheme.Muted;
        private readonly Color green = Color.FromArgb(22, 163, 74);
        private readonly Color amber = Color.FromArgb(245, 158, 11);
        private readonly Color red = Color.FromArgb(220, 38, 38);

        private readonly StatusRow[] rows = new StatusRow[4];
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
            Load += (s, e) => { UpdateStatus(); tick.Start(); ArmSnapshot(); };
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

            // light/dark switch, same as on the driving screen: saved in the settings, so it is remembered
            var btnThema = new JohanRoundButton
            {
                Bounds = new Rectangle(w - 48 - 44, y - 8, 44, 44),
                BackColor = panel,
                FlatStyle = FlatStyle.Flat,
                Image = JohanSkin.Icon(JohanTheme.IsLight ? "moon_licht_64" : "sun_licht_64", 24),
                ImageAlign = ContentAlignment.MiddleCenter
            };
            btnThema.FlatAppearance.BorderSize = 0;
            btnThema.Click += (s, e) =>
            {
                Settings.Default.setDisplay_isJohanLight = !Settings.Default.setDisplay_isJohanLight;
                Settings.Default.Save();
                Log.EventWriter("Theme switch on start screen, light = " + Settings.Default.setDisplay_isJohanLight + ", restarting");
                mf.johanRestartPending = true;
                Close();
            };
            Controls.Add(btnThema);
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

            // no tool choice here: the last tool and field load by themselves, switching is one tap on the driving screen

            var btnGo = new JohanRoundButton
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
                Location = new Point(x, h - 78)
            };
            Controls.Add(lblField);

            var lblBeheer = new Label
            {
                Text = "Beheer: houd de snelheid rechtsboven 3 s ingedrukt",
                ForeColor = muted,
                Font = new Font("Segoe UI", 10F),
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleRight
            };
            lblBeheer.Location = new Point(w - 48 - 300, h - 64);
            Controls.Add(lblBeheer);
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
            // the counter only runs inside the position loop, so without GPS it says nothing
            bool boardOk = gpsOk && mf.steerModuleConnectedCounter < 30;
            rows[2].Set(boardOk ? green : red, boardOk ? "Verbonden" : gpsOk ? "Geen verbinding met de stuurprint" : "Onbekend zonder GPS");

            // Tractor: steer angle comes back from the board
            bool tractorOk = boardOk && Math.Abs(mf.mc.actualSteerAngleDegrees) < 60;
            rows[3].Set(tractorOk ? green : amber,
                tractorOk ? "Stuurhoek " + mf.mc.actualSteerAngleDegrees.ToString("0.0") + " graden" : "Wacht op de trekker");

            lblField.Text = (string.IsNullOrEmpty(mf.currentFieldDirectory)
                ? "Nog geen perceel"
                : "Laatste perceel: " + mf.currentFieldDirectory)
                + "\nWerktuig: " + RegistrySettings.toolProfileName + " " + mf.tool.width.ToString("0.0") + " m";
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(bg);
        }

        // Debug aid: "AgOpenGPS.exe --snapshot C:\pad\start.png" writes the start screen to a PNG
        // two seconds after it opens, so it can be checked without looking at the screen.
        private void ArmSnapshot()
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "--snapshot");
            if (i < 0 || i + 1 >= args.Length) return;
            string path = args[i + 1];
            var t = new Timer { Interval = 2000 };
            t.Tick += (s, e) =>
            {
                t.Stop();
                try
                {
                    using (var bmp = new Bitmap(Width, Height))
                    {
                        DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
                        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    Log.EventWriter("Start screen snapshot written: " + path);
                }
                catch (Exception ex) { Log.EventWriter("Start screen snapshot failed: " + ex.Message); }
            };
            t.Start();
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
