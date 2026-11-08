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
            { "AutoSteerOff", "steering-wheel_licht_64" },
            { "AutoSteerOn", "steering-wheel_groen_64" },
            { "AutoSteerOffSnapToPivot", "steering-wheel_licht_64" },
            { "AutoSteerOnSnapToPivot", "steering-wheel_groen_64" },
            { "YouTurnNo", "corner-up-right_licht_64" },
            { "Youturn80", "corner-up-right_groen_64" },
            { "HeadlandOff", "box-margin_licht_64" },
            { "HeadlandOn", "box-margin_groen_64" },
            { "TrackOn", "line_licht_64" },
            { "ABSnapNudgeMenu", "arrows-horizontal_licht_64" },
            { "ContourOff", "vector-spline_licht_64" },
            { "ContourOn", "vector-spline_groen_64" },
            { "AutoTrackOff", "route_licht_64" },
            { "AutoTrackOn", "route_groen_64" },
            { "JobActive", "polygon_licht_64" },
            { "FieldStats", "list_licht_64" },
            { "Settings48", "settings_licht_64" },
            { "SpecialFunctions", "tool_licht_64" },
            { "NavigationSettings", "map-2_licht_64" },
            { "GPSQuality", "satellite_licht_64" },
            { "ZoomIn48", "zoom-in_licht_64" },
            { "ZoomOut48", "zoom-out_licht_64" },
            { "WindowClose", "power_rood_64" },

            // dialogs and menus the driver can reach
            { "Cancel64", "x_rood_64" },
            { "OK64", "check_groen_64" },
            { "back_button", "arrow-left_licht_64" },
            { "SwitchOff", "power_rood_64" },
            { "SwitchOn", "power_groen_64" },
            { "Trash", "trash_rood_64" },
            { "TrashApplied", "trash_rood_64" },
            { "TrashContourRef", "trash_rood_64" },
            { "UpArrow64", "chevron-up_licht_64" },
            { "DnArrow64", "chevron-down_licht_64" },
            { "ArrowLeft", "arrow-left_licht_64" },
            { "ArrowRight", "arrow-right_licht_64" },
            { "Next", "chevron-right_licht_64" },
            { "Previous", "chevron-left_licht_64" },
            { "BackSpace", "backspace_licht_64" },
            { "FileSave", "device-floppy_licht_64" },
            { "FileNew", "folder-plus_licht_64" },
            { "FileOpen", "folder-open_licht_64" },
            { "FileExisting", "copy_licht_64" },
            { "FileClose", "folder-off_licht_64" },
            { "FilePrevious", "history_licht_64" },
            { "FileCopy", "copy_licht_64" },
            { "FileEditName", "pencil_licht_64" },
            { "FileExplorerWindows", "folder-open_licht_64" },
            { "ISOXML", "file-import_licht_64" },
            { "BoundaryLoadFromGE", "world_licht_64" },
            { "BoundaryLoadMultiFromGE", "world_licht_64" },
            { "GoogleEarth", "world_licht_64" },
            { "bing1", "world_licht_64" },
            { "AutoManualIsAuto", "tractor_licht_64" },
            { "AgShare", "cloud-upload_licht_64" },
            { "JobNameCalendar", "calendar_licht_64" },
            { "JobNameTime", "clock_licht_64" },
            { "SnapLeft", "arrow-big-left_licht_64" },
            { "SnapRight", "arrow-big-right_licht_64" },
            { "SnapLeftHalf", "chevron-left_licht_64" },
            { "SnapRightHalf", "chevron-right_licht_64" },
            { "SnapToPivot", "focus-2_licht_64" },
            { "SteerZero", "circle-dot_licht_64" },
            { "Sort", "arrows-sort_licht_64" },
            { "FlagRed", "flag_rood_64" },
            { "FlagGrn", "flag_groen_64" },
            { "FlagYel", "flag_licht_64" },
            { "FlagDelete", "trash_rood_64" },
            { "Con_VehicleMenu", "tractor_licht_64" },
            { "Con_ImplementMenu", "tool_licht_64" },
            { "Con_SourcesMenu", "antenna_licht_64" },
            { "Con_UTurnMenu", "corner-up-right_licht_64" },
            { "Con_ModulesMenu", "cpu_licht_64" },
            { "Con_TramMenu", "line-dashed_licht_64" },
            { "Con_Display", "device-desktop_licht_64" },
            { "Con_FeatureMenu", "adjustments_licht_64" },
            { "Con_RightMenuEdit", "layout-sidebar-right_licht_64" },
            { "AgIO", "plug_licht_64" },
            { "ModePurePursuit", "route_licht_64" },
            { "ModeStanley", "route-2_licht_64" },
            { "WizardWand", "wand_licht_64" },
            { "Reset_Default", "refresh_licht_64" },
            { "ResetTool", "refresh_licht_64" },
            { "ToolAcceptChange", "check_groen_64" },
            { "Webcam", "camera_licht_64" },
            { "ScreenShot", "camera_licht_64" },
            { "WindowMinimize", "minus_licht_64" },
            { "WindowMaximize", "maximize_licht_64" },
            { "WindowNightMode", "moon_licht_64" },
            { "WindowDayMode", "sun_licht_64" },
            { "BrightnessUp", "sun_licht_64" },
            { "BrightnessDn", "sun-low_licht_64" },
            { "Camera2D64", "map_licht_64" },
            { "Camera3D64", "cube_licht_64" },
            { "CameraNorth2D", "compass_licht_64" },
            { "TiltUp", "chevron-up_licht_64" },
            { "TiltDown", "chevron-down_licht_64" },
            { "fileMenu", "menu-2_licht_64" },
            { "FieldTools", "tools_licht_64" },
            { "AutoSteerConf", "adjustments-horizontal_licht_64" },
            { "Boundary", "polygon_licht_64" },
            { "HeadlandBuild", "box-margin_licht_64" },
            { "HeadlandMenu", "box-margin_licht_64" },
            { "AddNew", "plus_licht_64" },
            { "PointAdd", "plus_licht_64" },
            { "PointDelete", "minus_licht_64" },
            { "IncrementPlus", "plus_licht_64" },
            { "IncrementMinus", "minus_licht_64" },
            { "ABTrackAB", "line_licht_64" },
            { "ABTrackCurve", "vector-spline_licht_64" },
            { "TrackLine", "line_licht_64" },
            { "TrackCurve", "vector-spline_licht_64" },
            { "TrackPivot", "rotate_licht_64" },
            { "ABLineCycle", "arrow-right_licht_64" },
            { "ABLineCycleBk", "arrow-left_licht_64" },
            { "ABSwapPoints", "arrows-left-right_licht_64" },
            { "TrackVisible", "eye_licht_64" },
            { "TracksInvisible", "eye-off_licht_64" },
            { "ABLinesHideShow", "eye_licht_64" },
            { "BoundaryRecord", "player-record_rood_64" },
            { "boundaryPause", "player-pause_licht_64" },
            { "boundaryPlay", "player-play_groen_64" },
            { "boundaryStop", "player-stop_rood_64" },
            { "BoundaryLeft", "arrow-bar-to-left_licht_64" },
            { "BoundaryRight", "arrow-bar-to-right_licht_64" },
            { "SectionMasterOff", "secties_licht_64" },
            { "SectionMasterOn", "secties_groen_64" },
            { "HydraulicLiftOff", "arrows-vertical_licht_64" },
            { "HydraulicLiftOn", "arrows-vertical_groen_64" },
            { "ZoomOGL", "zoom-in_licht_64" },
            { "LetterABlue", "circle-letter-a_licht_64" },
            { "LetterBBlue", "circle-letter-b_licht_64" },
            { "SteerDriveOn", "steering-wheel_groen_64" },
            { "SteerDriveOff", "steering-wheel_licht_64" },
            { "SteerLeft", "arrow-left_licht_64" },
            { "SteerRight", "arrow-right_licht_64" },
            { "ColourPick", "palette_licht_64" },
            { "ColorUnlocked", "palette_licht_64" },
            { "Chart", "chart-line_licht_64" },
            { "HardwareMessage", "alert-triangle_licht_64" },
            { "debug", "bug_licht_64" },
            { "RecPath", "route_licht_64" },
            { "YouTurnReverse", "corner-down-left_licht_64" },
            { "TramAll", "line-dashed_licht_64" },
            { "TramLines", "line-dashed_licht_64" },
            { "TramOuter", "line-dashed_licht_64" },
            { "TramOff", "line-dashed_licht_64" },
            { "TramMulti", "line-dashed_licht_64" },
            { "ConD_KeyBoard", "keyboard_licht_64" },
            { "Headache", "ruler-2_licht_64" },
            { "ConF_SteerSound", "volume_licht_64" },
            { "ConF_TurnSound", "volume_licht_64" },
            { "ConF_SoundSections", "volume_licht_64" },
            { "ConF_HydLiftSound", "volume_licht_64" },
            { "ConD_Speedometer", "gauge_licht_64" },
            { "ConD_FullScreenBegin", "arrows-maximize_licht_64" },
            { "AutoStop", "hand-stop_rood_64" },
            { "ManualOff", "hand-stop_licht_64" },
            { "ManualOn", "hand-stop_groen_64" },
            { "pathResumeStart", "player-play_groen_64" },
            { "ABDraw", "pencil_licht_64" },
            { "ABTracks", "line_licht_64" },
            { "ABSmooth", "vector-spline_licht_64" },
            { "GridRotate", "rotate_licht_64" },
            { "SectionMapping", "map_licht_64" },
            { "ChargeIndicator", "plug_groen_64" },
            { "ChargingNo", "plug_rood_64" },
            { "ConS_SourcesHeading", "compass_licht_64" },
            { "ConS_SourcesRoll", "arrows-left-right_licht_64" },
            { "ABSnapNudgeMenuRef", "arrows-horizontal_licht_64" },
            { "MenuHideShow", "dots_licht_64" },
            { "Pan", "map-pin_licht_64" },
        };

        // GL textures and large illustrations keep their original image;
        // small button pictures without an explicit icon get the mono line style
        private static bool KeepOriginal(string name, Image img)
        {
            if (name.StartsWith("z_") || name.StartsWith("QR") || name.StartsWith("vehiclePage") || name.Contains("HitchPage") || name.StartsWith("Antenna") || name.StartsWith("RadiusWheelBase")) return true;
            return img.Width > 160 || img.Height > 160;
        }

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

        private static readonly Dictionary<string, Bitmap> iconCache = new Dictionary<string, Bitmap>();

        // A Tabler icon from johan-icons (file name without .png) scaled to a square of the given size,
        // for the fork's own buttons. Null when the file is missing, so callers fall back to text only.
        // In the light theme the white ("licht") icons become the dark ones
        private static string Resolve(string file) => JohanTheme.IsLight ? file.Replace("_licht_", "_donker_") : file;

        public static Bitmap Icon(string file, int size)
        {
            file = Resolve(file);
            string key = file + "@" + size;
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

            // names that are not images (strings, icons, textures we never touch) skip the lookup
            private readonly HashSet<string> passThrough = new HashSet<string>();

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
                    if (KeepOriginal(name, origImg)) result = origImg as Bitmap ?? new Bitmap(origImg);
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
