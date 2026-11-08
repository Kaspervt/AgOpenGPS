using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AgLibrary.Logging;

namespace AgLibrary.Johan
{
    // Johan fork: one colour theme over every window AgOpenGPS or AgIO opens.
    // Nothing in the upstream forms is rewritten: a sweeper runs whenever a
    // window appears (modal or not) and recolours it. Neutral colours (greys,
    // whites, the lavender frame) become the dark canvas palette; signal
    // colours (green on, red off, yellow warning) are left alone.
    public static class JohanTheme
    {
        // palette from the canvas; Init() switches to the light set
        public static Color Bg = Color.FromArgb(15, 23, 42);
        public static Color Bar = Color.FromArgb(17, 28, 51);
        public static Color Panel = Color.FromArgb(30, 41, 59);
        public static Color PanelLight = Color.FromArgb(51, 65, 85);
        public static Color Text = Color.FromArgb(248, 250, 252);
        public static Color Muted = Color.FromArgb(148, 163, 184);
        public static bool IsLight { get; private set; }

        // Called once at start, before any form is built; a switch at runtime restarts the program
        public static void Init(bool light)
        {
            IsLight = light;
            if (light)
            {
                Bg = Color.FromArgb(241, 245, 249);
                Bar = Color.White;
                Panel = Color.FromArgb(226, 232, 240);
                PanelLight = Color.FromArgb(203, 213, 225);
                Text = Color.FromArgb(15, 23, 42);
                Muted = Color.FromArgb(71, 85, 105);
            }
            else
            {
                Bg = Color.FromArgb(15, 23, 42);
                Bar = Color.FromArgb(17, 28, 51);
                Panel = Color.FromArgb(30, 41, 59);
                PanelLight = Color.FromArgb(51, 65, 85);
                Text = Color.FromArgb(248, 250, 252);
                Muted = Color.FromArgb(148, 163, 184);
            }
        }

        // Switch while running: new palette, every open window recoloured and its pictures re-resolved.
        // The host rebuilds its own screens afterwards (they are skipped here, as always).
        public static void SwitchLive(bool light)
        {
            Init(light);
            WriteLightToRegistry(light);
            var open = new List<Form>();
            try { foreach (Form f in Application.OpenForms) open.Add(f); } catch { }
            lock (tabIcons) { foreach (var b in tabIcons.Values) b.Dispose(); tabIcons.Clear(); }
            JohanSkinCore.ReskinAll(open);
            foreach (Form f in open)
            {
                if (f.IsDisposed || IsOwn(f)) continue;
                try { Apply(f); if (f.FormBorderStyle != FormBorderStyle.None) DarkTitleBar(f); f.Invalidate(true); }
                catch (Exception ex) { Log.EventWriter("Johan theme switch: " + f.Name + ": " + ex.Message); }
            }
            Log.EventWriter("Johan theme switched live, light = " + light);
        }
        public static readonly Color Green = Color.FromArgb(22, 163, 74);
        public static readonly Color Red = Color.FromArgb(220, 38, 38);
        public static readonly Color Amber = Color.FromArgb(245, 158, 11);
        public static readonly Color Blue = Color.FromArgb(29, 78, 216);
        public static readonly Color Grey = Color.FromArgb(71, 85, 105);
        public static readonly Color FieldGround = Color.FromArgb(63, 74, 58);

        public static Font Font(float size, FontStyle style = FontStyle.Regular)
            => new Font("Segoe UI", size, style, GraphicsUnit.Point);

        public static bool IsInstalled { get; private set; }

        private static readonly HashSet<Form> themed = new HashSet<Form>();
        private static readonly HashSet<Control> watched = new HashSet<Control>();
        private static Timer sweeper;

        // Forms built for this fork already draw in the theme; the host program says which
        public static Func<Form, bool> IsOwnForm = f => false;
        private static bool IsOwn(Form f) => IsOwnForm != null && IsOwnForm(f);

        // Light or dark is chosen in AgOpenGPS and shared with AgIO through the registry key both already use
        private const string RegPath = @"SOFTWARE\AgOpenGPS", RegLight = "JohanLight";

        public static bool ReadLightFromRegistry()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegPath))
                    return key != null && string.Equals(key.GetValue(RegLight) as string, "1");
            }
            catch { return false; }
        }

        public static void WriteLightToRegistry(bool light)
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RegPath))
                    key.SetValue(RegLight, light ? "1" : "0");
            }
            catch { }
        }

        // Windows 10/11: dark title bar and frame for the dialogs that keep a border
        [System.Runtime.InteropServices.DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        private static void DarkTitleBar(Form f)
        {
            try
            {
                if (!f.IsHandleCreated) return;
                int on = 1;
                if (DwmSetWindowAttribute(f.Handle, 20, ref on, 4) != 0) DwmSetWindowAttribute(f.Handle, 19, ref on, 4);
                int caption = ColorTranslator.ToWin32(Bar);     // DWMWA_CAPTION_COLOR, Windows 11
                DwmSetWindowAttribute(f.Handle, 35, ref caption, 4);
                int text = ColorTranslator.ToWin32(Text);       // DWMWA_TEXT_COLOR
                DwmSetWindowAttribute(f.Handle, 36, ref text, 4);
            }
            catch { }
        }

        public static void Install()
        {
            if (IsInstalled) return;
            IsInstalled = true;

            // A window gets its colours in the CBT hook, before Windows paints it the first time,
            // so there is no flash of the original grey. The sweeper is the safety net.
            try
            {
                hookRef = CbtProc;
                hook = SetWindowsHookEx(WH_CBT, hookRef, IntPtr.Zero, GetCurrentThreadId());
            }
            catch (Exception ex) { Log.EventWriter("Johan theme: CBT hook failed: " + ex.Message); }

            // No Application.Idle handler: that fires after every message (GPS data arrives
            // ten times a second), the CBT hook already catches new windows. The timer is the net.
            Application.EnterThreadModal += (s, e) => Sweep();
            sweeper = new Timer { Interval = 500 };
            sweeper.Tick += (s, e) => Sweep();
            sweeper.Start();
            Log.EventWriter("Johan theme installed" + (hook == IntPtr.Zero ? " (no hook)" : ""));
        }

        private static void Sweep()
        {
            // nothing new: no list, no work
            try { if (Application.OpenForms.Count == themed.Count) return; }
            catch { return; }
            var open = new List<Form>();
            try { foreach (Form f in Application.OpenForms) open.Add(f); }
            catch { return; }
            foreach (Form f in open) ThemeForm(f);
        }

        private static void ThemeForm(Form f)
        {
            if (f == null || f.IsDisposed || themed.Contains(f)) return;
            themed.Add(f);
            // FormTimedMessage disposes before it closes, so FormClosed never fires there
            f.FormClosed += (s, e) => themed.Remove((Form)s);
            f.Disposed += (s, e) => themed.Remove((Form)s);
            if (IsOwn(f)) return;
            try { Apply(f); }
            catch (Exception ex) { Log.EventWriter("Johan theme: " + f.Name + ": " + ex.Message); }
        }

        #region CBT hook

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
        private const int WH_CBT = 5, HCBT_ACTIVATE = 5, HCBT_CREATEWND = 3;
        private static HookProc hookRef;
        private static IntPtr hook;

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        private static IntPtr CbtProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode == HCBT_ACTIVATE)
            {
                try { ThemeForm(Control.FromHandle(wParam) as Form); } catch { }
            }
            return CallNextHookEx(hook, nCode, wParam, lParam);
        }

        #endregion

        // Greys, whites, near-blacks and the pale AgOpenGPS frame colours
        public static bool IsNeutral(Color c)
        {
            if (c.A == 0) return false;
            int max = Math.Max(c.R, Math.Max(c.G, c.B));
            int min = Math.Min(c.R, Math.Min(c.G, c.B));
            return max - min <= 48;
        }

        public static void Apply(Control c)
        {
            if (c == null || c.IsDisposed) return;

            // the OpenGL views draw themselves
            string type = c.GetType().Name;
            if (type.Contains("GLControl")) return;

            if (c is Form form)
            {
                form.BackColor = Bg;
                form.ForeColor = Text;
                if (form.FormBorderStyle != FormBorderStyle.None)
                {
                    if (form.IsHandleCreated) DarkTitleBar(form);
                    else form.HandleCreated += (s, e) => DarkTitleBar((Form)s);
                }
            }
            else if (c is ToolStrip strip)
            {
                strip.BackColor = Bar;
                strip.ForeColor = Text;
                strip.Renderer = new ToolStripProfessionalRenderer(new JohanColorTable());
                foreach (ToolStripItem item in strip.Items) ApplyItem(item);
            }
            else if (c is Button b)
            {
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderSize = 0;
                b.FlatAppearance.MouseOverBackColor = PanelLight;
                b.FlatAppearance.MouseDownBackColor = Blue;
                if (IsNeutral(b.BackColor)) b.BackColor = Panel;
                if (IsNeutral(b.ForeColor)) b.ForeColor = Text;
                b.UseVisualStyleBackColor = false;
            }
            else if (c is CheckBox || c is RadioButton)
            {
                var bb = (ButtonBase)c;
                if (IsNeutral(bb.ForeColor)) bb.ForeColor = Text;
                if (bb is CheckBox cb && cb.Appearance == Appearance.Button || bb is RadioButton rb && rb.Appearance == Appearance.Button)
                {
                    bb.FlatStyle = FlatStyle.Flat;
                    bb.FlatAppearance.BorderSize = 0;
                    if (IsNeutral(bb.BackColor)) bb.BackColor = Panel;
                    bb.UseVisualStyleBackColor = false;
                }
                else if (IsNeutral(bb.BackColor)) bb.BackColor = Color.Transparent;
            }
            else if (c is TextBoxBase || c is NumericUpDown || c is ComboBox || c is ListBox || c is ListView
                     || c is TreeView || c is DataGridView || c is DateTimePicker || c is PropertyGrid)
            {
                if (IsNeutral(c.BackColor)) c.BackColor = Panel;
                if (IsNeutral(c.ForeColor)) c.ForeColor = Text;
                if (c is TextBoxBase tb) tb.BorderStyle = BorderStyle.FixedSingle;
                if (c is DataGridView g)
                {
                    g.BackgroundColor = Bg;
                    g.DefaultCellStyle.BackColor = Panel;
                    g.DefaultCellStyle.ForeColor = Text;
                    g.ColumnHeadersDefaultCellStyle.BackColor = Bar;
                    g.ColumnHeadersDefaultCellStyle.ForeColor = Text;
                    g.EnableHeadersVisualStyles = false;
                }
            }
            else if (c is Label || c is LinkLabel)
            {
                if (IsNeutral(c.BackColor)) c.BackColor = Color.Transparent;
                if (IsNeutral(c.ForeColor)) c.ForeColor = Text;
                if (c is LinkLabel ll) { ll.LinkColor = Muted; ll.ActiveLinkColor = Text; }
            }
            else if (c is TabControl tabs)
            {
                InstallTabDrawing(tabs);
            }
            else if (c is TrackBar || c is ProgressBar || c is ScrollBar)
            {
                if (IsNeutral(c.BackColor)) c.BackColor = Bg;
            }
            else
            {
                // panels, group boxes, tab pages, split containers, user controls
                if (IsNeutral(c.BackColor)) c.BackColor = (c is TabPage) ? Panel : Bg;
                if (IsNeutral(c.ForeColor)) c.ForeColor = Text;
            }

            // one stubborn control must not leave the rest of the window unthemed
            foreach (Control child in c.Controls)
            {
                try { Apply(child); }
                catch (Exception ex) { Log.EventWriter("Johan theme: " + child.GetType().Name + " " + child.Name + ": " + ex.Message); }
            }

            // controls added later (dynamic buttons, lists) get themed too
            if (c.HasChildren || c is Form || c is Panel || c is TabPage || c is GroupBox)
            {
                if (watched.Add(c))
                {
                    c.ControlAdded += (s, e) => { try { Apply(e.Control); } catch { } };
                    c.Disposed += (s, e) => watched.Remove((Control)s);
                }
            }
        }

        private static void ApplyItem(ToolStripItem item)
        {
            item.ForeColor = Text;
            if (IsNeutral(item.BackColor)) item.BackColor = Bar;
            if (item is ToolStripDropDownItem dd)
            {
                dd.DropDown.BackColor = Bar;
                dd.DropDown.ForeColor = Text;
                if (dd.DropDown is ToolStrip ds) ds.Renderer = new ToolStripProfessionalRenderer(new JohanColorTable());
                foreach (ToolStripItem sub in dd.DropDownItems) ApplyItem(sub);
            }
        }

        private static void InstallTabDrawing(TabControl tabs)
        {
            tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabs.SizeMode = TabSizeMode.Fixed;
            if (tabs.ItemSize.Height < 36) tabs.ItemSize = new Size(Math.Max(tabs.ItemSize.Width, 110), 36);
            tabs.DrawItem += (s, e) =>
            {
                var tc = (TabControl)s;
                bool sel = e.Index == tc.SelectedIndex;
                // the first item paints the whole strip, so the native white never shows between tabs
                if (e.Index == 0)
                    using (var strip = new SolidBrush(Bg))
                        e.Graphics.FillRectangle(strip, new Rectangle(0, 0, tc.Width, Math.Max(0, tc.DisplayRectangle.Top)));
                using (var back = new SolidBrush(sel ? Panel : Bg))
                    e.Graphics.FillRectangle(back, e.Bounds);
                var page = tc.TabPages[e.Index];
                Image img = null;
                if (tc.ImageList != null)
                {
                    if (page.ImageIndex >= 0 && page.ImageIndex < tc.ImageList.Images.Count) img = tc.ImageList.Images[page.ImageIndex];
                    else if (!string.IsNullOrEmpty(page.ImageKey) && tc.ImageList.Images.ContainsKey(page.ImageKey)) img = tc.ImageList.Images[page.ImageKey];
                }
                // tab pictures come from the form's own image list, not from Resources: restyle them here
                if (img != null) img = MonoTab(tc, e.Index, img);
                if (img != null)
                {
                    int sz = Math.Min(img.Width, Math.Min(e.Bounds.Width, e.Bounds.Height) - 6);
                    var r = new Rectangle(e.Bounds.X + (e.Bounds.Width - sz) / 2, e.Bounds.Y + (e.Bounds.Height - sz) / 2, sz, sz);
                    if (!string.IsNullOrEmpty(page.Text)) r.Offset(0, -8);
                    e.Graphics.DrawImage(img, r);
                    if (!string.IsNullOrEmpty(page.Text))
                        TextRenderer.DrawText(e.Graphics, page.Text, tc.Font, new Rectangle(e.Bounds.X, e.Bounds.Bottom - 20, e.Bounds.Width, 18),
                            sel ? Text : Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                else
                {
                    TextRenderer.DrawText(e.Graphics, page.Text, tc.Font, e.Bounds,
                        sel ? Text : Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            };
            // the native control paints the strip behind the tabs white; paint over it after each WM_PAINT
            if (tabs.IsHandleCreated) new TabStripPainter(tabs);
            else tabs.HandleCreated += (s, e) => new TabStripPainter((TabControl)s);
        }

        private static readonly Dictionary<string, Bitmap> tabIcons = new Dictionary<string, Bitmap>();

        private static Image MonoTab(TabControl tc, int index, Image img)
        {
            string key = tc.Name + "#" + tc.GetHashCode() + "#" + index;
            lock (tabIcons)
            {
                if (tabIcons.TryGetValue(key, out Bitmap hit)) return hit;
                try
                {
                    var mono = JohanSkinCore.Mono(img, Text);
                    tabIcons[key] = mono;
                    return mono;
                }
                catch { return img; }
            }
        }

        private class TabStripPainter : NativeWindow
        {
            private const int WM_PAINT = 0x000F, WM_ERASEBKGND = 0x0014;
            private readonly TabControl tabs;

            public TabStripPainter(TabControl t)
            {
                tabs = t;
                AssignHandle(t.Handle);
                t.HandleDestroyed += (s, e) => ReleaseHandle();
            }

            private const int WM_PRINT = 0x0317, WM_PRINTCLIENT = 0x0318;

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);
                if (tabs.IsDisposed) return;
                bool print = m.Msg == WM_PRINT || m.Msg == WM_PRINTCLIENT;   // DrawToBitmap path
                if (m.Msg != WM_PAINT && !print) return;
                try
                {
                    using (var g = print ? Graphics.FromHdc(m.WParam) : Graphics.FromHwnd(tabs.Handle))
                    using (var back = new SolidBrush(Bg))
                    {
                        var strip = new Region(new Rectangle(0, 0, tabs.Width, Math.Max(0, tabs.DisplayRectangle.Top)));
                        for (int i = 0; i < tabs.TabCount; i++) strip.Exclude(tabs.GetTabRect(i));
                        g.FillRegion(back, strip);
                        // the page border the native control draws around the display area
                        var d = tabs.DisplayRectangle;
                        using (var pen = new Pen(Bg, 4)) g.DrawRectangle(pen, d.X - 2, d.Y - 2, d.Width + 3, d.Height + 3);
                    }
                }
                catch { }
            }
        }

        private class JohanColorTable : ProfessionalColorTable
        {
            public override Color MenuStripGradientBegin => Bar;
            public override Color MenuStripGradientEnd => Bar;
            public override Color MenuItemSelected => PanelLight;
            public override Color MenuItemSelectedGradientBegin => PanelLight;
            public override Color MenuItemSelectedGradientEnd => PanelLight;
            public override Color MenuItemPressedGradientBegin => Panel;
            public override Color MenuItemPressedGradientEnd => Panel;
            public override Color MenuItemBorder => PanelLight;
            public override Color MenuBorder => PanelLight;
            public override Color ToolStripDropDownBackground => Bar;
            public override Color ImageMarginGradientBegin => Bar;
            public override Color ImageMarginGradientMiddle => Bar;
            public override Color ImageMarginGradientEnd => Bar;
            public override Color SeparatorDark => PanelLight;
            public override Color SeparatorLight => Bar;
            public override Color ToolStripGradientBegin => Bar;
            public override Color ToolStripGradientMiddle => Bar;
            public override Color ToolStripGradientEnd => Bar;
            public override Color StatusStripGradientBegin => Bar;
            public override Color StatusStripGradientEnd => Bar;
            public override Color ToolStripBorder => Bar;
            public override Color ButtonSelectedHighlight => PanelLight;
            public override Color ButtonSelectedGradientBegin => PanelLight;
            public override Color ButtonSelectedGradientEnd => PanelLight;
            public override Color ButtonPressedGradientBegin => Panel;
            public override Color ButtonPressedGradientEnd => Panel;
        }
    }
}
