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
        public static readonly ColorRgba Grid = new ColorRgba(0.33f, 0.37f, 0.31f);                // orientation grid: a shade lighter than the ground
        public static readonly ColorRgba Corridor = new ColorRgba(0.992f, 0.878f, 0.278f, 0.18f); // soft band around the active line
        public static readonly ColorRgba SectionOn = new ColorRgba(0.133f, 0.773f, 0.369f);      // #22c55e
        public static readonly ColorRgba SectionOff = new ColorRgba(0.392f, 0.455f, 0.545f);     // #64748b
        public static readonly ColorRgba SectionWait = new ColorRgba(0.961f, 0.620f, 0.043f);    // amber: on, not mapping yet
        public static int Symbol;   // 0 arrow, 1 dot, 2 tractor
        public static readonly ColorRgba Boundary = new ColorRgba(0.886f, 0.910f, 0.941f);       // #e2e8f0
        public static readonly ColorRgba BoundaryHalo = new ColorRgba(0.059f, 0.090f, 0.165f, 0.6f);
        public static readonly ColorRgba OutOfBounds = new ColorRgba(0.863f, 0.149f, 0.149f);    // #dc2626
        public static readonly ColorRgba Vehicle = new ColorRgba(0.114f, 0.306f, 0.847f);        // #1d4ed8
        public static readonly ColorRgba VehicleEdge = new ColorRgba(0.973f, 0.980f, 0.988f);    // #f8fafc

        // Navigation arrow around the pivot: tip ahead of the front axle, notch at the back.
        // Vehicle space: x to the right, y forward, pivot at the origin, wheelbase along y.
        public static void DrawVehicleArrow(double wheelbase, double opacity)
        {
            if (Symbol == 1) { DrawDot(wheelbase, opacity); return; }
            if (Symbol == 2) { DrawTractor(wheelbase, opacity); return; }
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

        // Dot with a small heading triangle
        private static void DrawDot(double wheelbase, double opacity)
        {
            double r = System.Math.Max(2.0, wheelbase * 0.7);
            GL.Color4(Vehicle.Red / 255f, Vehicle.Green / 255f, Vehicle.Blue / 255f, (float)opacity);
            GL.Begin(PrimitiveType.TriangleFan);
            GL.Vertex3(0, 0, 0);
            for (int i = 0; i <= 32; i++) { double a = i / 32.0 * 2 * System.Math.PI; GL.Vertex3(r * System.Math.Sin(a), r * System.Math.Cos(a), 0); }
            GL.End();
            GLW.SetLineWidth(2.5f);
            GLW.SetColor(VehicleEdge);
            GL.Begin(PrimitiveType.LineLoop);
            for (int i = 0; i < 32; i++) { double a = i / 32.0 * 2 * System.Math.PI; GL.Vertex3(r * System.Math.Sin(a), r * System.Math.Cos(a), 0); }
            GL.End();
            GL.Begin(PrimitiveType.Triangles);
            GL.Vertex3(0, r * 1.5, 0); GL.Vertex3(-r * 0.45, r * 0.7, 0); GL.Vertex3(r * 0.45, r * 0.7, 0);
            GL.End();
            GLW.SetLineWidth(1.0f);
        }

        // Simple tractor from above: body, four wheels, cab
        private static void DrawTractor(double wheelbase, double opacity)
        {
            double wb = System.Math.Max(2.5, wheelbase), hw = wb * 0.28;
            GL.Color4(Vehicle.Red / 255f, Vehicle.Green / 255f, Vehicle.Blue / 255f, (float)opacity);
            Quad(-hw, -wb * 0.35, hw, wb * 1.15);
            GL.Color4(0.06f, 0.09f, 0.16f, (float)opacity);
            Quad(-hw - wb * 0.18, -wb * 0.3, -hw - wb * 0.02, wb * 0.25);   // rear wheels, big
            Quad(hw + wb * 0.02, -wb * 0.3, hw + wb * 0.18, wb * 0.25);
            Quad(-hw - wb * 0.14, wb * 0.75, -hw - wb * 0.02, wb * 1.05);   // front wheels, small
            Quad(hw + wb * 0.02, wb * 0.75, hw + wb * 0.14, wb * 1.05);
            GLW.SetColor(VehicleEdge);
            Quad(-hw * 0.7, wb * 0.05, hw * 0.7, wb * 0.5);                 // cab
            GLW.SetLineWidth(2.0f);
            GL.Begin(PrimitiveType.LineLoop);
            GL.Vertex3(-hw, -wb * 0.35, 0); GL.Vertex3(hw, -wb * 0.35, 0); GL.Vertex3(hw, wb * 1.15, 0); GL.Vertex3(-hw, wb * 1.15, 0);
            GL.End();
            GLW.SetLineWidth(1.0f);
        }

        private static void Quad(double x0, double y0, double x1, double y1)
        {
            GL.Begin(PrimitiveType.Quads);
            GL.Vertex3(x0, y0, 0); GL.Vertex3(x1, y0, 0); GL.Vertex3(x1, y1, 0); GL.Vertex3(x0, y1, 0);
            GL.End();
        }
    }
}
