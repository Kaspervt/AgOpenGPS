using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Windows.Forms;
using AgLibrary.Logging;

namespace AgOpenGPS
{
    // Johan fork: icon skin.
    // Swaps the generated Resources' ResourceManager for one that answers known
    // button images with Tabler-based PNGs from <exe>\johan-icons\, scaled to the
    // size of the original image so no layout changes. Everything else falls through
    // to the embedded resources, so upstream code and designer files stay untouched.
    public static class JohanSkin
    {
        // resource name -> icon file (without .png) in johan-icons
        private static readonly Dictionary<string, string> map = new Dictionary<string, string>
        {
            { "AutoSteerOff", "steering-wheel_donker_64" },
            { "AutoSteerOn", "steering-wheel_groen_64" },
            { "AutoSteerOffSnapToPivot", "steering-wheel_donker_64" },
            { "AutoSteerOnSnapToPivot", "steering-wheel_groen_64" },
            { "YouTurnNo", "corner-up-right_donker_64" },
            { "Youturn80", "corner-up-right_groen_64" },
            { "HeadlandOff", "box-margin_donker_64" },
            { "HeadlandOn", "box-margin_groen_64" },
            { "TrackOn", "line_donker_64" },
            { "ABSnapNudgeMenu", "arrows-horizontal_donker_64" },
            { "ContourOff", "vector-spline_donker_64" },
            { "ContourOn", "vector-spline_groen_64" },
            { "AutoTrackOff", "route_donker_64" },
            { "AutoTrackOn", "route_groen_64" },
            { "JobActive", "polygon_donker_64" },
            { "FieldStats", "list_donker_64" },
            { "Settings48", "settings_donker_64" },
            { "SpecialFunctions", "tool_donker_64" },
            { "NavigationSettings", "map-2_donker_64" },
            { "GPSQuality", "satellite_donker_64" },
            { "ZoomIn48", "zoom-in_donker_64" },
            { "ZoomOut48", "zoom-out_donker_64" },
            { "WindowClose", "power_rood_64" },
        };

        public static bool IsInstalled { get; private set; }

        public static void Install()
        {
            if (IsInstalled) return;
            try
            {
                string dir = Path.Combine(Application.StartupPath, "johan-icons");
                if (!Directory.Exists(dir)) { Log.EventWriter("Johan skin: no johan-icons folder, skipped"); return; }

                var field = typeof(Properties.Resources).GetField("resourceMan", BindingFlags.Static | BindingFlags.NonPublic);
                if (field == null) { Log.EventWriter("Johan skin: resourceMan field not found, skipped"); return; }

                field.SetValue(null, new SkinResourceManager(dir));
                IsInstalled = true;
                Log.EventWriter("Johan skin installed from " + dir);
            }
            catch (Exception ex)
            {
                Log.EventWriter("Johan skin failed: " + ex.Message);
            }
        }

        private class SkinResourceManager : ResourceManager
        {
            private readonly string dir;
            private readonly Dictionary<string, Bitmap> cache = new Dictionary<string, Bitmap>();

            public SkinResourceManager(string iconDir)
                : base("AgOpenGPS.Properties.Resources", typeof(Properties.Resources).Assembly)
            {
                dir = iconDir;
            }

            public override object GetObject(string name, CultureInfo culture)
            {
                object original = base.GetObject(name, culture);
                if (!map.TryGetValue(name, out string file)) return original;

                lock (cache)
                {
                    if (cache.TryGetValue(name, out Bitmap cached)) return cached;

                    string path = Path.Combine(dir, file + ".png");
                    if (!File.Exists(path)) return original;

                    var size = (original as Image)?.Size ?? new Size(64, 64);
                    try
                    {
                        using (var src = new Bitmap(path))
                        {
                            var bmp = new Bitmap(size.Width, size.Height);
                            using (var g = Graphics.FromImage(bmp))
                            {
                                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                g.SmoothingMode = SmoothingMode.AntiAlias;
                                g.Clear(Color.Transparent);
                                // keep the icon square and centered inside non-square originals
                                int s = Math.Min(size.Width, size.Height);
                                g.DrawImage(src, (size.Width - s) / 2, (size.Height - s) / 2, s, s);
                            }
                            cache[name] = bmp;
                            return bmp;
                        }
                    }
                    catch
                    {
                        return original;
                    }
                }
            }
        }
    }
}
