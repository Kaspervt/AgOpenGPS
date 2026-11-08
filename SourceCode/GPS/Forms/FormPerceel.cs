using System;
using AgLibrary.Johan;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AgLibrary.Logging;
using AgOpenGPS.Core.Models;
using AgOpenGPS.IO;
using AgOpenGPS.Properties;

namespace AgOpenGPS
{
    // Johan fork: field picker for driver mode.
    // One list, nearest field first, with area and number of lines; tap to open.
    // "Nieuw perceel" hands over to the normal AgOpenGPS field menu.
    public class FormPerceel : Form
    {
        private readonly FormGPS mf;

        private readonly Color bg = Color.FromArgb(15, 23, 42);
        private readonly Color panel = Color.FromArgb(30, 41, 59);
        private readonly Color selected = Color.FromArgb(30, 58, 95);
        private readonly Color text = Color.FromArgb(248, 250, 252);
        private readonly Color muted = Color.FromArgb(148, 163, 184);

        public bool WantsNewField { get; private set; }

        private class Rij
        {
            public string Dir, Naam, Lijnen;
            public double Ha, Km;
            public bool Huidig;
        }

        public FormPerceel(FormGPS formGPS)
        {
            mf = formGPS;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = Screen.FromControl(mf).Bounds;
            BackColor = bg;
            ForeColor = text;
            Font = new Font("Segoe UI", 14F);
            KeyPreview = true;
            ShowInTaskbar = false;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
            BuildLayout();
            Load += (s, e) => ArmSnapshot();
        }

        private void BuildLayout()
        {
            int w = Width, h = Height, x = 56;
            int listW = w - 2 * x;

            Controls.Add(new Label { Text = "Welk perceel?", Font = new Font("Segoe UI", 28F, FontStyle.Bold), ForeColor = text, AutoSize = true, Location = new Point(x, 36) });
            Controls.Add(new Label { Text = "Dichtstbijzijnde bovenaan", Font = new Font("Segoe UI", 11F), ForeColor = muted, AutoSize = true, Location = new Point(x + 2, 92) });

            var kop = new Panel { Bounds = new Rectangle(x, 128, listW, 28), BackColor = bg };
            kop.Controls.Add(new Label { Text = "PERCEEL", ForeColor = muted, Font = new Font("Segoe UI", 9F, FontStyle.Bold), AutoSize = true, Location = new Point(24, 4) });
            kop.Controls.Add(new Label { Text = "OPPERVLAK", ForeColor = muted, Font = new Font("Segoe UI", 9F, FontStyle.Bold), AutoSize = true, Location = new Point(listW - 440, 4) });
            kop.Controls.Add(new Label { Text = "AFSTAND", ForeColor = muted, Font = new Font("Segoe UI", 9F, FontStyle.Bold), AutoSize = true, Location = new Point(listW - 300, 4) });
            kop.Controls.Add(new Label { Text = "LIJNEN", ForeColor = muted, Font = new Font("Segoe UI", 9F, FontStyle.Bold), AutoSize = true, Location = new Point(listW - 170, 4) });
            Controls.Add(kop);

            var lijst = new FlowLayoutPanel
            {
                Bounds = new Rectangle(x, 160, listW, h - 160 - 130),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = bg
            };
            Controls.Add(lijst);

            foreach (var rij in LeesPercelen())
            {
                var b = new JohanRoundButton
                {
                    Size = new Size(listW - 24, 72),
                    Margin = new Padding(0, 0, 0, 10),
                    BackColor = rij.Huidig ? selected : panel,
                    ForeColor = text,
                    FlatStyle = FlatStyle.Flat,
                    Tag = rij.Dir,
                    Text = ""
                };
                b.FlatAppearance.BorderSize = 0;
                string dir = rij.Dir;
                b.Click += (s, e) => OpenField(dir);

                // Labels inside the button draw over it; forward their clicks
                b.Controls.Add(Lbl(rij.Naam, 24, 20, text, 16F, FontStyle.Bold, dir));
                b.Controls.Add(Lbl(rij.Ha > 0 ? rij.Ha.ToString("0.0") + " ha" : "geen grens", listW - 440, 22, text, 14F, FontStyle.Regular, dir));
                b.Controls.Add(Lbl(rij.Km < 0 ? "" : rij.Km < 0.05 ? "hier" : rij.Km.ToString("0.0") + " km", listW - 300, 22, muted, 14F, FontStyle.Regular, dir));
                b.Controls.Add(Lbl(rij.Lijnen, listW - 170, 22, muted, 12F, FontStyle.Regular, dir));
                lijst.Controls.Add(b);
            }

            var terug = new JohanRoundButton
            {
                Text = "Terug",
                Bounds = new Rectangle(x, h - 100, 200, 64),
                BackColor = panel,
                ForeColor = text,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold)
            };
            terug.FlatAppearance.BorderSize = 0;
            terug.Click += (s, e) => Close();
            Controls.Add(terug);

            var nieuw = new JohanRoundButton
            {
                Text = "Nieuw perceel of gewoon rijden",
                Bounds = new Rectangle(w - x - 420, h - 100, 420, 64),
                BackColor = bg,
                ForeColor = text,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold)
            };
            nieuw.FlatAppearance.BorderSize = 2;
            nieuw.FlatAppearance.BorderColor = muted;
            nieuw.Click += (s, e) => { WantsNewField = true; Close(); };
            Controls.Add(nieuw);
        }

        private Label Lbl(string t, int x, int y, Color c, float size, FontStyle style, string dir)
        {
            var l = new Label { Text = t, ForeColor = c, BackColor = Color.Transparent, Font = new Font("Segoe UI", size, style), AutoSize = true, Location = new Point(x, y) };
            l.Click += (s, e) => OpenField(dir);
            return l;
        }

        private List<Rij> LeesPercelen()
        {
            var rijen = new List<Rij>();
            if (!Directory.Exists(RegistrySettings.fieldsDirectory)) return rijen;

            Wgs84 hier = mf.AppModel.CurrentLatLon;
            bool heeftPositie = mf.pn.fixQuality > 0;

            foreach (string dir in Directory.GetDirectories(RegistrySettings.fieldsDirectory))
            {
                if (!File.Exists(Path.Combine(dir, "Field.txt"))) continue;
                var rij = new Rij { Dir = dir, Naam = Path.GetFileName(dir), Km = -1, Lijnen = "" };
                rij.Huidig = string.Equals(rij.Naam, mf.currentFieldDirectory, StringComparison.OrdinalIgnoreCase);
                try
                {
                    Wgs84 origin = FieldPlaneFiles.LoadOrigin(dir);
                    if (heeftPositie) rij.Km = hier.DistanceInKiloMeters(origin);
                }
                catch (Exception ex) { Log.EventWriter("Field picker: origin of " + rij.Naam + ": " + ex.Message); }
                try
                {
                    var bnd = BoundaryFiles.Load(dir);
                    if (bnd.Count > 0 && bnd[0].fenceLine.Count > 2) rij.Ha = Hectares(bnd[0].fenceLine);
                }
                catch (Exception ex) { Log.EventWriter("Field picker: boundary of " + rij.Naam + ": " + ex.Message); }
                try
                {
                    int n = TrackFiles.Load(dir).Count;
                    rij.Lijnen = n == 0 ? "geen lijnen" : n == 1 ? "1 lijn" : n + " lijnen";
                }
                catch { rij.Lijnen = ""; }
                rijen.Add(rij);
            }
            return rijen.OrderBy(r => r.Km < 0 ? double.MaxValue : r.Km).ThenBy(r => r.Naam).ToList();
        }

        private static double Hectares(List<vec3> ring)
        {
            double a = 0;
            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
                a += (ring[j].easting + ring[i].easting) * (ring[j].northing - ring[i].northing);
            return Math.Abs(a) / 2.0 / 10000.0;
        }

        private async void OpenField(string dir)
        {
            Enabled = false;
            try
            {
                await mf.FileOpenField(Path.Combine(dir, "Field.txt"));
                if (mf.isJobStarted)
                {
                    Settings.Default.setF_CurrentDir = mf.currentFieldDirectory;
                    Settings.Default.Save();
                    Log.EventWriter("** Opened **  " + mf.currentFieldDirectory + " (field picker)");
                }
            }
            catch (Exception ex)
            {
                Log.EventWriter("Field picker: open failed: " + ex.Message);
                mf.TimedMessageBox(3000, "Perceel", "Kon het perceel niet openen");
            }
            Close();
        }

        // Debug aid: "--snapshot-perceel <png>" writes this screen to a PNG two seconds after it opens
        private void ArmSnapshot()
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "--snapshot-perceel");
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
                    Log.EventWriter("Field picker snapshot written: " + path);
                }
                catch (Exception ex) { Log.EventWriter("Field picker snapshot failed: " + ex.Message); }
            };
            t.Start();
        }
    }
}
