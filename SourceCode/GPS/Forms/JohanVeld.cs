using System;
using AgLibrary.Johan;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AgLibrary.Logging;
using AgOpenGPS.Core.Models;
using AgOpenGPS.Properties;

namespace AgOpenGPS
{
    // Johan fork: the field flow of the canvas. A new field is a name and a drive: the boundary
    // is recorded while driving (FormGrens), the first line is set with two points (FormLijn).
    // Everything here calls the same AgOpenGPS code as FormFieldDir, FormBoundaryPlayer and FormQuickAB.
    public partial class FormGPS
    {
        // Creates and opens an empty field at the current position. False when the name is taken or something failed.
        public bool JohanMaakPerceel(string naam)
        {
            naam = naam.Trim();
            if (naam.Length == 0) return false;
            foreach (char c in Path.GetInvalidFileNameChars()) naam = naam.Replace(c, ' ');

            var dir = new DirectoryInfo(Path.Combine(RegistrySettings.fieldsDirectory, naam));
            if (dir.Exists)
            {
                TimedMessageBox(2500, "Perceel", "Er is al een perceel met die naam");
                return false;
            }

            try
            {
                currentFieldDirectory = naam;
                JobNew();
                pn.DefineLocalPlane(AppModel.CurrentLatLon, false);
                dir.Create();
                FileCreateField();
                FileCreateSections();
                FileCreateRecPath();
                FileCreateContour();
                FileCreateElevation();
                FileSaveFlags();
                FileCreateBoundary();
                Settings.Default.setF_CurrentDir = currentFieldDirectory;
                Settings.Default.Save();
                Log.EventWriter("** Opened **  " + naam + " (new, Johan flow)");
                return true;
            }
            catch (Exception ex)
            {
                Log.EventWriter("Johan flow: new field failed: " + ex.Message);
                TimedMessageBox(3000, "Perceel", "Kon het perceel niet aanmaken");
                currentFieldDirectory = "";
                return false;
            }
        }

        // Theme switch without a restart: settings, shared theme and skin, map colours, own screens
        public bool johanReopenStart;
        public void JohanThemeLive(bool light)
        {
            Settings.Default.setDisplay_isJohanLight = light;
            Settings.Default.Save();
            // own non-modal bars are simply closed; they come back in the new colours when opened
            foreach (string n in new[] { "FormVerschuif", "FormGrens", "FormLijn", "FormKopakker" })
            {
                var f = System.Windows.Forms.Application.OpenForms[n];
                if (f != null) { try { f.Close(); } catch { } }
            }
            JohanTheme.SwitchLive(light);
            JohanLock.ApplyColours();
            frameDayColor = Settings.Default.setDisplay_colorDayFrame.CheckColorFor255();
            frameNightColor = Settings.Default.setDisplay_colorNightFrame.CheckColorFor255();
            fieldColorDay = Settings.Default.setDisplay_colorFieldDay.CheckColorFor255();
            fieldColorNight = Settings.Default.setDisplay_colorFieldNight.CheckColorFor255();
            textColorDay = Settings.Default.setDisplay_colorTextDay.CheckColorFor255();
            textColorNight = Settings.Default.setDisplay_colorTextNight.CheckColorFor255();
            BackColor = JohanTheme.Bg;
            RebuildJohanBars();
            Log.EventWriter("Theme switched live, light = " + light);
        }

        // ---- headland: one width, the outer boundary moved inwards (what FormHeadAche does per segment)

        // Current headland width in metres, read back from the saved line (0 = none)
        public double JohanKopakkerBreedte()
        {
            if (!isJobStarted || bnd.bndList.Count == 0 || bnd.bndList[0].hdLine.Count == 0) return 0;
            var f = bnd.bndList[0].fenceLine;
            var h = bnd.bndList[0].hdLine;
            if (f.Count == 0) return 0;
            var ds = new List<double>();
            int step = Math.Max(1, h.Count / 24);
            for (int i = 0; i < h.Count; i += step)
            {
                double best = double.MaxValue;
                for (int k = 0; k < f.Count; k++)
                {
                    double d = glm.Distance(h[i], f[k]);
                    if (d < best) best = d;
                }
                ds.Add(best);
            }
            ds.Sort();
            return Math.Round(ds[ds.Count / 2] * 2) / 2;
        }

        // Builds and saves the headland line at the given width; 0 removes it. Returns false when it cannot be made.
        public bool JohanBouwKopakker(double breedte)
        {
            if (!isJobStarted || bnd.bndList.Count == 0 || bnd.bndList[0].fenceLine.Count < 3) return false;
            var b = bnd.bndList[0];
            var fence = b.fenceLine;
            if (breedte < 0.25)
            {
                b.hdLine.Clear();
                bnd.isHeadlandOn = false;
                FileSaveHeadland();
                return true;
            }

            // which way is inside: try a short step from the first point on both sides
            vec3 p0 = fence[0];
            double sx0 = Math.Sin(glm.PIBy2 + p0.heading), cx0 = Math.Cos(glm.PIBy2 + p0.heading);
            double sign = b.fenceLineEar.IsPointInPolygon(new vec2(p0.easting - sx0 * 0.5, p0.northing - cx0 * 0.5)) ? 1 : -1;

            var res = new List<vec3>();
            double distSq = breedte * breedte - 0.01;
            for (int i = 0; i < fence.Count; i++)
            {
                var pt = new vec3(
                    fence[i].easting - sign * Math.Sin(glm.PIBy2 + fence[i].heading) * breedte,
                    fence[i].northing - sign * Math.Cos(glm.PIBy2 + fence[i].heading) * breedte,
                    fence[i].heading);
                bool ok = true;
                for (int t = 0; t < fence.Count; t++)
                {
                    double dx = pt.easting - fence[t].easting, dy = pt.northing - fence[t].northing;
                    if (dx * dx + dy * dy < distSq) { ok = false; break; }
                }
                if (!ok) continue;
                if (res.Count > 0 && glm.Distance(pt, res[res.Count - 1]) < 1.0) continue;
                res.Add(pt);
            }
            if (res.Count < 3)
            {
                TimedMessageBox(2500, "Kopakker", "Te breed voor dit perceel");
                return false;
            }
            // headings along the new line, as the headland builder does
            for (int i = 0; i < res.Count; i++)
            {
                vec3 a = res[i], c = res[(i + 1) % res.Count];
                double hd = Math.Atan2(c.easting - a.easting, c.northing - a.northing);
                if (hd < 0) hd += glm.twoPI;
                res[i] = new vec3(a.easting, a.northing, hd);
            }
            b.hdLine.Clear();
            b.hdLine.AddRange(res);
            bnd.isHeadlandOn = true;
            FileSaveHeadland();
            Log.EventWriter("Johan flow: headland built, width " + breedte.ToString("0.0") + " m, " + res.Count + " points");
            return true;
        }

        // ---- shutdown from Beheer or the start screen: our own question, then the normal AgOpenGPS close
        public int johanAfsluitKeuze = -1;   // 0 exit, 2 shut the computer down, -1 ask the AgOpenGPS way
        public void JohanAfsluiten(bool computerUit)
        {
            johanAfsluitKeuze = computerUit ? 2 : 0;
        }

        // Called once the dialog that asked has closed (the main form refuses to close while it owns an open window)
        public void JohanAfsluitenUitvoeren()
        {
            if (johanAfsluitKeuze < 0) return;
            BeginInvoke(new Action(() => { try { Close(); } catch { } }));
        }

        // Beheer: the full AgOpenGPS field menu once (import of KML, ISO-XML, AgShare, templates)
        public bool johanUpstreamJobMenu;
        public void JohanImportMenu()
        {
            johanUpstreamJobMenu = true;
            try { btnJobMenu_Click(btnJobMenu, EventArgs.Empty); }
            finally { johanUpstreamJobMenu = false; }
        }

        // Tap on the tractor: next symbol (arrow, dot, tractor), remembered in the settings
        public void JohanSymboolWissel()
        {
            int s = (Settings.Default.setDisplay_johanSymbool + 1) % 3;
            Settings.Default.setDisplay_johanSymbool = s;
            Settings.Default.Save();
            JohanLook.Symbol = s;
            TimedMessageBox(1200, "Symbool", s == 0 ? "Pijl" : s == 1 ? "Stip" : "Trekker");
        }

        // Locked lines per field: a plain list of track names in Vergrendeld.txt next to TrackLines.txt
        private string JohanLockFile => Path.Combine(RegistrySettings.fieldsDirectory, currentFieldDirectory, "Vergrendeld.txt");

        public HashSet<string> JohanLockedTracks()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (isJobStarted && File.Exists(JohanLockFile))
                    foreach (string l in File.ReadAllLines(JohanLockFile)) if (l.Trim().Length > 0) set.Add(l.Trim());
            }
            catch { }
            return set;
        }

        public bool JohanIsTrackLocked(int idx)
        {
            if (idx < 0 || idx >= trk.gArr.Count) return false;
            return JohanLockedTracks().Contains(trk.gArr[idx].name ?? "");
        }

        public void JohanSetTrackLocked(int idx, bool locked)
        {
            if (idx < 0 || idx >= trk.gArr.Count || !isJobStarted) return;
            var set = JohanLockedTracks();
            string name = trk.gArr[idx].name ?? "";
            if (locked) set.Add(name); else set.Remove(name);
            try { File.WriteAllLines(JohanLockFile, set.ToArray()); }
            catch (Exception ex) { Log.EventWriter("Johan flow: lock file: " + ex.Message); }
        }
    }
}
