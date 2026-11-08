using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AgOpenGPS
{
    // Johan fork: touch gestures on the map. Windows sends WM_GESTURE to the OpenGL control;
    // a two-finger pan (the message carries the finger distance) tilts the camera, a pinch zooms.
    // One-finger taps still arrive as ordinary mouse clicks, so nothing else changes.
    internal class JohanGestureWindow : NativeWindow
    {
        private const int WM_GESTURE = 0x0119;
        private const uint GID_ZOOM = 3, GID_PAN = 4;
        private const uint GF_BEGIN = 0x1;

        [StructLayout(LayoutKind.Sequential)]
        private struct GESTUREINFO
        {
            public uint cbSize;
            public uint dwFlags;
            public uint dwID;
            public IntPtr hwndTarget;
            public short x;
            public short y;
            public uint dwInstanceID;
            public uint dwSequenceID;
            public ulong ullArguments;
            public uint cbExtraArgs;
        }

        [DllImport("user32.dll")] private static extern bool GetGestureInfo(IntPtr hGestureInfo, ref GESTUREINFO pGestureInfo);
        [DllImport("user32.dll")] private static extern bool CloseGestureInfoHandle(IntPtr hGestureInfo);

        private readonly FormGPS mf;
        private int lastX, lastY;
        private ulong lastZoom;

        public JohanGestureWindow(FormGPS formGPS) { mf = formGPS; }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_GESTURE && mf.JohanSchermActief)
            {
                var gi = new GESTUREINFO { cbSize = (uint)Marshal.SizeOf(typeof(GESTUREINFO)) };
                if (GetGestureInfo(m.LParam, ref gi))
                {
                    bool begin = (gi.dwFlags & GF_BEGIN) != 0;
                    uint dist = (uint)(gi.ullArguments & 0xFFFFFFFF);
                    if (gi.dwID == GID_PAN)
                    {
                        // dist > 0: two fingers, drag up = tilt further, drag down = back towards 2D.
                        // dist == 0: one finger, the map pans with the finger.
                        if (!begin && dist > 0) mf.JohanZetHoek(mf.camera.PitchInDegrees + (gi.y - lastY) * 0.25);
                        else if (!begin) mf.JohanPan(gi.x - lastX, gi.y - lastY);
                        lastX = gi.x;
                        lastY = gi.y;
                        CloseGestureInfoHandle(m.LParam);
                        m.Result = IntPtr.Zero;
                        return;
                    }
                    if (gi.dwID == GID_ZOOM)
                    {
                        if (!begin && lastZoom > 0 && dist > 0)
                        {
                            if (dist > lastZoom + 3) mf.JohanZoomStap(true);
                            else if (dist + 3 < lastZoom) mf.JohanZoomStap(false);
                        }
                        lastZoom = dist;
                        CloseGestureInfoHandle(m.LParam);
                        m.Result = IntPtr.Zero;
                        return;
                    }
                }
            }
            base.WndProc(ref m);
        }
    }
}
