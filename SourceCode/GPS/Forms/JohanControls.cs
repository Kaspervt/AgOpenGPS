using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AgLibrary.Johan;

namespace AgOpenGPS
{
    // Johan fork: the rounded controls of the canvas (buttons with 14 px corners, pills with a status dot).
    // Plain WinForms buttons are square; these paint themselves and are otherwise ordinary buttons/labels,
    // so clicks, Enabled and Text work as before.

    public class JohanRoundButton : Button
    {
        public int Radius { get; set; } = 14;
        // colour behind the corners; null = the parent's colour (over the map: the ground colour)
        public Color? Corner { get; set; }
        private bool hover, pressed;

        public JohanRoundButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }

        public static GraphicsPath Rounded(Rectangle r, int radius)
        {
            int d = Math.Max(1, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            // the parent's colour behind the corners
            using (var back = new SolidBrush(Corner ?? Parent?.BackColor ?? JohanTheme.Bar)) g.FillRectangle(back, ClientRectangle);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color fill = BackColor;
            if (Enabled && pressed) fill = JohanTheme.Blue;
            else if (Enabled && hover && !IsSignal(fill)) fill = JohanTheme.PanelLight;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Rounded(r, Radius))
            using (var b = new SolidBrush(fill)) g.FillPath(b, path);

            // disabled: muted grey on panels, pale grey on coloured (grey/green) buttons
            bool onPanel = fill.ToArgb() == JohanTheme.Panel.ToArgb() || fill.ToArgb() == JohanTheme.PanelLight.ToArgb() || fill.ToArgb() == JohanTheme.Bg.ToArgb();
            Color fore = Enabled ? ForeColor : (onPanel ? JohanTheme.Muted : Color.FromArgb(203, 213, 225));
            Image img = Image;
            Size ts = string.IsNullOrEmpty(Text) ? Size.Empty : TextRenderer.MeasureText(g, Text, Font, Size.Empty, TextFormatFlags.NoPadding);
            int iw = img?.Width ?? 0, ih = img?.Height ?? 0;

            if (img != null && ts.IsEmpty)
            {
                int ix = ImageAlign == ContentAlignment.MiddleLeft ? Padding.Left : ImageAlign == ContentAlignment.MiddleRight ? Width - Padding.Right - iw : (Width - iw) / 2;
                g.DrawImage(img, ix, (Height - ih) / 2, iw, ih);
            }
            else if (TextImageRelation == TextImageRelation.ImageAboveText && img != null)
            {
                int gap = 8, total = ih + gap + ts.Height, y = (Height - total) / 2;
                g.DrawImage(img, (Width - iw) / 2, y, iw, ih);
                TextRenderer.DrawText(g, Text, Font, new Rectangle(0, y + ih + gap, Width, ts.Height), fore, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
            }
            else
            {
                int gap = img == null ? 0 : 12, total = iw + gap + ts.Width;
                bool left = TextAlign == ContentAlignment.MiddleLeft;
                int x = left ? Padding.Left + 8 : Math.Max(4, (Width - total) / 2);
                if (img != null) g.DrawImage(img, x, (Height - ih) / 2, iw, ih);
                int tw = left ? Math.Max(10, Width - x - iw - gap - 8) : Math.Min(ts.Width + 4, Width - x - iw - gap - 2);
                TextRenderer.DrawText(g, Text, Font, new Rectangle(x + iw + gap, 0, tw, Height), fore,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            }
        }

        // green/red/blue buttons keep their colour under the finger
        private static bool IsSignal(Color c) => !JohanTheme.IsNeutral(c);
    }

    // A rounded label: dark pill over the map, or a status pill with a coloured dot in front of the text
    public class JohanPill : Label
    {
        public int Radius { get; set; } = 12;
        public Color? Dot { get; set; }
        public Color Fill { get; set; } = JohanTheme.Bar;
        public Color? Corner { get; set; }

        public JohanPill()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            TextAlign = ContentAlignment.MiddleCenter;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var back = new SolidBrush(Corner ?? Parent?.BackColor ?? JohanTheme.Bar)) g.FillRectangle(back, ClientRectangle);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Fill.A > 0)
                using (var path = JohanRoundButton.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), Radius))
                using (var b = new SolidBrush(Fill)) g.FillPath(b, path);

            int x = 14;
            if (Dot.HasValue)
            {
                using (var d = new SolidBrush(Dot.Value)) g.FillEllipse(d, x, (Height - 14) / 2, 14, 14);
                x += 24;
            }
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis
                        | (Dot.HasValue || TextAlign == ContentAlignment.MiddleLeft ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter);
            var rect = Dot.HasValue || TextAlign == ContentAlignment.MiddleLeft ? new Rectangle(x, 0, Width - x - 8, Height) : new Rectangle(0, 0, Width, Height);
            TextRenderer.DrawText(g, Text, Font, rect, ForeColor, flags);
        }
    }
}
