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

namespace AgLibrary.Johan
{
    // Johan fork: icon skin shared by AgOpenGPS and AgIO.
    // Swaps a generated Resources class' ResourceManager for one that answers known
    // image names with Tabler-based PNGs from <exe>\johan-icons\ (scaled to the original
    // size so no layout changes) and restyles every other small picture to the same
    // single-colour line look. Textures and large illustrations stay as they are.
    public static class JohanSkinCore
    {
        // Dark strokes of the original picture become theme-text strokes, light fills a faint tint:
        // the same icon, but in the single-colour line look of the rest of the theme
        public static Bitmap Mono(Image src, Color ink)
        {
            var bmp = new Bitmap(src.Width, src.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var tmp = new Bitmap(src.Width, src.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(tmp)) g.DrawImageUnscaled(src, 0, 0);
                var rect = new Rectangle(0, 0, tmp.Width, tmp.Height);
                var din = tmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                var dout = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    int n = Math.Abs(din.Stride) * tmp.Height;
                    var px = new byte[n];
                    System.Runtime.InteropServices.Marshal.Copy(din.Scan0, px, 0, n);
                    var outPx = new byte[n];
                    for (int i = 0; i < n; i += 4)
                    {
                        int b = px[i], gch = px[i + 1], r = px[i + 2], a = px[i + 3];
                        if (a < 8) continue;
                        double lum = (0.299 * r + 0.587 * gch + 0.114 * b) / 255.0;
                        double sat = (Math.Max(r, Math.Max(gch, b)) - Math.Min(r, Math.Min(gch, b))) / 255.0;
                        // dark or strongly coloured = ink, near-white = faint
                        double w = Math.Max(1.0 - lum, sat * 0.8);
                        int na = (int)(a * (0.22 + 0.78 * Math.Min(1.0, w)));
                        // premultiplied? no: Format32bppArgb is straight alpha
                        outPx[i] = ink.B; outPx[i + 1] = ink.G; outPx[i + 2] = ink.R; outPx[i + 3] = (byte)na;
                    }
                    System.Runtime.InteropServices.Marshal.Copy(outPx, 0, dout.Scan0, n);
                }
                finally { tmp.UnlockBits(din); bmp.UnlockBits(dout); }
            }
            return bmp;
        }

        public static bool IsInstalled { get; private set; }
        private static SkinResourceManager current;

        // Theme switch: every picture that came from here is looked up again (light <-> dark icons)
        public static void ReskinAll(IEnumerable<Form> forms)
        {
            var oldIcons = new Dictionary<Bitmap, string>();
            lock (iconCache)
            {
                foreach (var kv in iconCache) oldIcons[kv.Value] = kv.Key;
                iconCache.Clear();
            }
            var oldRes = current?.SnapshotAndClear() ?? new Dictionary<Bitmap, string>();

            Image Nieuw(Image img)
            {
                var b = img as Bitmap;
                if (b == null) return null;
                if (oldIcons.TryGetValue(b, out string key))
                {
                    int at = key.LastIndexOf('@');
                    return Icon(key.Substring(0, at), int.Parse(key.Substring(at + 1)));
                }
                if (oldRes.TryGetValue(b, out string name) && current != null) return current.GetObject(name, null) as Image;
                return null;
            }

            void Walk(Control c)
            {
                if (c == null || c.IsDisposed) return;
                try
                {
                    if (c is ButtonBase bb && bb.Image != null) { var n = Nieuw(bb.Image); if (n != null) bb.Image = n; }
                    else if (c is PictureBox pb && pb.Image != null) { var n = Nieuw(pb.Image); if (n != null) pb.Image = n; }
                    else if (c is Label lb && lb.Image != null) { var n = Nieuw(lb.Image); if (n != null) lb.Image = n; }
                    else if (c is ToolStrip ts)
                        foreach (ToolStripItem it in ts.Items) WalkItem(it);
                }
                catch { }
                foreach (Control child in c.Controls) Walk(child);
            }

            void WalkItem(ToolStripItem it)
            {
                try { if (it.Image != null) { var n = Nieuw(it.Image); if (n != null) it.Image = n; } } catch { }
                if (it is ToolStripDropDownItem dd) foreach (ToolStripItem sub in dd.DropDownItems) WalkItem(sub);
            }

            foreach (var f in forms) Walk(f);
            // the old bitmaps stay alive for anything not reached (GL textures hold their own copies)
        }

        private static readonly Dictionary<string, Bitmap> iconCache = new Dictionary<string, Bitmap>();

        // A Tabler icon from johan-icons (file name without .png) scaled to a square of the given size,
        // for the fork's own buttons. Null when the file is missing, so callers fall back to text only.
        // In the light theme the white ("licht") icons become the dark ones
        public static string Resolve(string file) => JohanTheme.IsLight ? file.Replace("_licht_", "_donker_") : file;

        public static Bitmap Icon(string file, int size)
        {
            size = JohanTheme.S(size);   // callers ask in design pixels, the bitmap follows the screen scale
            string key = file + "@" + size;   // the requested name, so a theme switch can resolve it again
            file = Resolve(file);
            lock (iconCache)
            {
                if (iconCache.TryGetValue(key, out Bitmap hit)) return hit;
                string path = Path.Combine(Application.StartupPath, "johan-icons", file + ".png");
                if (!File.Exists(path)) return null;
                try
                {
                    using (var src = new Bitmap(path))
                    {
                        var bmp = new Bitmap(size, size);
                        using (var g = Graphics.FromImage(bmp))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.SmoothingMode = SmoothingMode.AntiAlias;
                            g.Clear(Color.Transparent);
                            g.DrawImage(src, 0, 0, size, size);
                        }
                        iconCache[key] = bmp;
                        return bmp;
                    }
                }
                catch { return null; }
            }
        }

        // resourcesType: the generated Properties.Resources class of the host program;
        // map: resource name -> icon file; keepOriginal: pictures that must not be restyled
        public static void Install(Type resourcesType, IDictionary<string, string> map, Func<string, Image, bool> keepOriginal)
        {
            if (IsInstalled) return;
            try
            {
                string dir = Path.Combine(Application.StartupPath, "johan-icons");
                if (!Directory.Exists(dir)) { Log.EventWriter("Johan skin: no johan-icons folder, skipped"); return; }

                var field = resourcesType.GetField("resourceMan", BindingFlags.Static | BindingFlags.NonPublic);
                if (field == null) { Log.EventWriter("Johan skin: resourceMan field not found, skipped"); return; }

                current = new SkinResourceManager(resourcesType, dir, map, keepOriginal);
                field.SetValue(null, current);
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
            private readonly IDictionary<string, string> map;
            private readonly Func<string, Image, bool> keepOriginal;
            private readonly Dictionary<string, Bitmap> cache = new Dictionary<string, Bitmap>();

            public SkinResourceManager(Type resourcesType, string iconDir, IDictionary<string, string> iconMap, Func<string, Image, bool> keep)
                : base(resourcesType.FullName, resourcesType.Assembly)
            {
                dir = iconDir;
                map = iconMap ?? new Dictionary<string, string>();
                keepOriginal = keep ?? ((n, img) => false);
            }

            // names that are not images (strings, icons, textures we never touch) skip the lookup
            private readonly HashSet<string> passThrough = new HashSet<string>();

            // for a theme switch: hand out what was cached (bitmap -> name) and start afresh
            public Dictionary<Bitmap, string> SnapshotAndClear()
            {
                var snap = new Dictionary<Bitmap, string>();
                lock (cache)
                {
                    foreach (var kv in cache) snap[kv.Value] = kv.Key;
                    cache.Clear();
                }
                return snap;
            }

            public override object GetObject(string name, CultureInfo culture)
            {
                // Hot path: upstream reads Resources.X inside timers and paint code, and every
                // base.GetObject deserialises a fresh Bitmap. Answer from the cache first.
                lock (cache)
                {
                    if (cache.TryGetValue(name, out Bitmap hit)) return hit;
                    if (passThrough.Contains(name)) return base.GetObject(name, culture);
                }

                object original = base.GetObject(name, culture);
                if (!(original is Image origImg))
                {
                    lock (cache) passThrough.Add(name);
                    return original;
                }

                Bitmap result = null;
                if (!map.TryGetValue(name, out string file))
                {
                    // no hand-picked icon: keep textures and illustrations, restyle small button pictures
                    if (keepOriginal(name, origImg)) result = origImg as Bitmap ?? new Bitmap(origImg);
                    else
                    {
                        try { result = Mono(origImg, JohanTheme.Text); }
                        catch { result = origImg as Bitmap ?? new Bitmap(origImg); }
                    }
                }
                else
                {
                    string path = Path.Combine(dir, Resolve(file) + ".png");
                    if (File.Exists(path))
                    {
                        var size = origImg.Size;
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
                                result = bmp;
                            }
                        }
                        catch { result = null; }
                    }
                    if (result == null) result = origImg as Bitmap ?? new Bitmap(origImg);
                }

                // the deserialised original is not handed out, so free it
                if (!ReferenceEquals(result, origImg)) origImg.Dispose();

                lock (cache)
                {
                    if (cache.TryGetValue(name, out Bitmap raced)) { if (!ReferenceEquals(raced, result)) result.Dispose(); return raced; }
                    cache[name] = result;
                }
                return result;
            }
        }
    }
}
