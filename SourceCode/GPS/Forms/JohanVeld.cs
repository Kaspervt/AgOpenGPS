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
            foreach (string n in new[] { "FormVerschuif", "FormGrens", "FormLijn" })
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
