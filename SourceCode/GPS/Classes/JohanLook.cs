using AgOpenGPS.Core.DrawLib;
using AgOpenGPS.Core.Models;
using OpenTK.Graphics.OpenGL;

namespace AgOpenGPS
{
    // Johan fork: the map in the canvas look. One flat ground, a yellow guidance line
    // with a dark halo, a light field boundary, a blue arrow for the vehicle.
    // Switched on together with the driver screen (JohanLock.ApplyColours); the drawing
    // classes consult it and fall back to the upstream colours when it is off.
    public static class JohanLook
    {
        public static bool On;

        public static readonly ColorRgba Line = new ColorRgba(0.992f, 0.878f, 0.278f);          // #fde047
        public static readonly ColorRgba LineHalo = new ColorRgba(0.059f, 0.090f, 0.165f, 0.85f); // #0f172a
        public static readonly ColorRgba Guide = new ColorRgba(1.0f, 1.0f, 1.0f, 0.30f);          // side guide lines
        public static readonly ColorRgba Boundary = new ColorRgba(0.886f, 0.910f, 0.941f);       // #e2e8f0
        public static readonly ColorRgba BoundaryHalo = new ColorRgba(0.059f, 0.090f, 0.165f, 0.6f);
        public static readonly ColorRgba OutOfBounds = new ColorRgba(0.863f, 0.149f, 0.149f);    // #dc2626
        public static readonly ColorRgba Vehicle = new ColorRgba(0.114f, 0.306f, 0.847f);        // #1d4ed8
        public static readonly ColorRgba VehicleEdge = new ColorRgba(0.973f, 0.980f, 0.988f);    // #f8fafc

        // Navigation arrow around the pivot: tip ahead of the front axle, notch at the back.
        // Vehicle space: x to the right, y forward, pivot at the origin, wheelbase along y.
        public static void DrawVehicleArrow(double wheelbase, double opacity)
        {
            double len = System.Math.Max(5.0, wheelbase * 1.8);
            double tipY = len * 0.75, backY = -len * 0.35, notchY = -len * 0.12, half = len * 0.42;

            GL.Color4(Vehicle.Red / 255f, Vehicle.Green / 255f, Vehicle.Blue / 255f, (float)opacity);
            GL.Begin(PrimitiveType.TriangleFan);
            GL.Vertex3(0, notchY, 0);
            GL.Vertex3(-half, backY, 0);
            GL.Vertex3(0, tipY, 0);
            GL.Vertex3(half, backY, 0);
            GL.End();

            GLW.SetLineWidth(2.5f);
            GLW.SetColor(VehicleEdge);
            GL.Begin(PrimitiveType.LineLoop);
            GL.Vertex3(0, notchY, 0);
            GL.Vertex3(-half, backY, 0);
            GL.Vertex3(0, tipY, 0);
            GL.Vertex3(half, backY, 0);
            GL.End();
            GLW.SetLineWidth(1.0f);
        }
    }
}
