using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using AgLibrary.Logging;
using AgOpenGPS.Properties;
using Microsoft.Win32;

namespace AgOpenGPS
{
    // Johan fork: the connection settings (GPS port, steer board network, NTRIP base station) live in
    // AgIO's own profile (AgIO\<name>.XML, active name in the registry key ProfileName). This class
    // ties that profile to the vehicle profile: the AgIO profile has the same name as the tractor, so
    // switching tractor switches cable, board and base station with it. AgOpenGPS edits the XML
    // directly and restarts AgIO, which only reads its settings at start.
    public static class JohanAgIO
    {
        private const string RegPad = @"SOFTWARE\AgOpenGPS";
        private const string RegSleutel = "ProfileName";

        public static string Map => Path.Combine(RegistrySettings.baseDirectory, "AgIO");
        public static string Pad(string naam) => Path.Combine(Map, naam + ".XML");

        // The AgIO profile in use (registry), "" when none
        public static string ActiefProfiel
        {
            get
            {
                try
                {
                    using (var k = Registry.CurrentUser.OpenSubKey(RegPad))
                    {
                        return k?.GetValue(RegSleutel)?.ToString() ?? "";
                    }
                }
                catch { return ""; }
            }
            set
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RegPad))
                {
                    k.SetValue(RegSleutel, value ?? "");
                }
            }
        }

        // Make sure an AgIO profile with this name exists: copy the active one (or Johan.XML), else start empty
        public static void ZorgProfiel(string naam)
        {
            if (string.IsNullOrEmpty(naam)) return;
            Directory.CreateDirectory(Map);
            string doel = Pad(naam);
            if (File.Exists(doel)) return;
            string bron = Pad(ActiefProfiel);
            if (string.IsNullOrEmpty(ActiefProfiel) || !File.Exists(bron)) bron = Pad("Johan");
            if (File.Exists(bron))
            {
                File.Copy(bron, doel);
                Log.EventWriter("AgIO profile " + naam + " created from " + Path.GetFileName(bron));
            }
            else
            {
                Schrijf(naam, new Dictionary<string, string>());
                Log.EventWriter("AgIO profile " + naam + " created empty");
            }
        }

        // Couple AgIO to this tractor: profile exists, registry points to it, AgIO restarted when asked
        public static void KoppelAanTrekker(string naam, bool herstart)
        {
            if (string.IsNullOrEmpty(naam)) return;
            try
            {
                if (herstart) Stop();   // AgIO saves into its current profile on closing, so stop it first
                ZorgProfiel(naam);
                if (ActiefProfiel != naam)
                {
                    ActiefProfiel = naam;
                    Log.EventWriter("AgIO profile set to " + naam + " (tractor profile)");
                }
                if (herstart) Start();
            }
            catch (Exception ex)
            {
                Log.EventWriter("AgIO couple to tractor failed: " + ex.Message);
            }
        }

        // ---- the XML ---------------------------------------------------------------------------

        // All <setting name="x"><value>y</value></setting> pairs of a profile
        public static Dictionary<string, string> Lees(string naam)
        {
            var d = new Dictionary<string, string>();
            string pad = Pad(naam);
            if (!File.Exists(pad)) return d;
            var doc = new XmlDocument();
            doc.Load(pad);
            foreach (XmlNode n in doc.SelectNodes("//setting"))
            {
                string k = n.Attributes?["name"]?.Value;
                var v = n.SelectSingleNode("value");
                if (!string.IsNullOrEmpty(k) && v != null) d[k] = v.InnerText;
            }
            return d;
        }

        // Write these pairs into the profile; other settings in the file stay as they are
        public static void Schrijf(string naam, Dictionary<string, string> waarden)
        {
            Directory.CreateDirectory(Map);
            string pad = Pad(naam);
            var doc = new XmlDocument();
            if (File.Exists(pad))
            {
                doc.Load(pad);
            }
            else
            {
                doc.LoadXml("<?xml version=\"1.0\" encoding=\"utf-8\"?><configuration><userSettings><AgIO.Properties.Settings /></userSettings></configuration>");
            }
            XmlNode groep = doc.SelectSingleNode("//userSettings/*") ?? doc.DocumentElement;
            foreach (var kv in waarden)
            {
                XmlNode s = doc.SelectSingleNode("//setting[@name='" + kv.Key + "']");
                if (s == null)
                {
                    var e = doc.CreateElement("setting");
                    e.SetAttribute("name", kv.Key);
                    e.SetAttribute("serializeAs", "String");
                    e.AppendChild(doc.CreateElement("value"));
                    groep.AppendChild(e);
                    s = e;
                }
                XmlNode v = s.SelectSingleNode("value") ?? s.AppendChild(doc.CreateElement("value"));
                v.InnerText = kv.Value ?? "";
            }
            doc.Save(pad);
        }

        public static string Tekst(Dictionary<string, string> d, string k, string anders = "") => d.TryGetValue(k, out var v) ? v : anders;
        public static bool Ja(Dictionary<string, string> d, string k, bool anders = false) => d.TryGetValue(k, out var v) ? v.Trim().Equals("True", StringComparison.OrdinalIgnoreCase) : anders;
        public static int Getal(Dictionary<string, string> d, string k, int anders = 0) => d.TryGetValue(k, out var v) && int.TryParse(v.Trim(), out int i) ? i : anders;

        // ---- the AgIO process -------------------------------------------------------------------

        public static bool Draait => Process.GetProcessesByName("AgIO").Length > 0;

        // Ask AgIO to close (it saves its settings) and wait for it; kill it when it does not listen
        public static void Stop()
        {
            foreach (var p in Process.GetProcessesByName("AgIO"))
            {
                try
                {
                    p.CloseMainWindow();
                    if (!p.WaitForExit(5000)) { p.Kill(); p.WaitForExit(2000); }
                }
                catch { }
            }
        }

        public static void Start()
        {
            if (Draait) return;
            string pad = Path.Combine(Application.StartupPath, "AgIO.exe");
            if (!File.Exists(pad)) { Log.EventWriter("AgIO.exe not found: " + pad); return; }
            Process.Start(new ProcessStartInfo { FileName = pad, WorkingDirectory = Path.GetDirectoryName(pad) });
            Log.EventWriter("AgIO started (" + ActiefProfiel + ")");
        }

        public static void Herstart()
        {
            Stop();
            Start();
        }
    }
}
