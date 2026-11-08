using System;
using System.Collections.Generic;
using AgOpenGPS.Core.DrawLib;
using AgOpenGPS.Core.Models;
using OpenTK.Graphics.OpenGL;

namespace AgOpenGPS
{
    // Johan fork: the map in the canvas look. Striped ground, a lighter field inside the boundary,
    // a cyan guidance line, a light field boundary, a blue arrow (or dot, or tractor) for the vehicle.
    // Switched on together with the driver screen (JohanLock.ApplyColours); the drawing
    // classes consult it and fall back to the upstream colours when it is off.
    public static class JohanLook
    {
        public static bool On;
        // set once per frame from the camera pitch: tilted views get raised (3D) vehicle symbols
        public static bool ThreeD;

        // Kasper koos 8-11 lijnstijl B: dun cyaan met donkere rand, buurlijnen cyaan, geen corridor
        public static readonly ColorRgba Line = new ColorRgba(0.133f, 0.827f, 0.933f);          // #22d3ee
        public static readonly ColorRgba LineHalo = new ColorRgba(0.059f, 0.090f, 0.165f, 0.85f); // #0f172a
        public static readonly ColorRgba Guide = new ColorRgba(0.133f, 0.827f, 0.933f, 0.30f);  // side guide lines, cyan 30%
        public static readonly ColorRgba Grid = new ColorRgba(0.33f, 0.37f, 0.31f);                // orientation grid: a shade lighter than the ground
        public static readonly ColorRgba Corridor = new ColorRgba(0.133f, 0.827f, 0.933f, 0.0f);  // corridor off in style B (alpha 0 = not drawn)
        public static readonly ColorRgba SectionOn = new ColorRgba(0.133f, 0.773f, 0.369f);      // #22c55e
        public static readonly ColorRgba SectionOff = new ColorRgba(0.392f, 0.455f, 0.545f);     // #64748b
        public static readonly ColorRgba SectionWait = new ColorRgba(0.961f, 0.620f, 0.043f);    // amber: on, not mapping yet
        public static readonly ColorRgba ToolBar = new ColorRgba(0.059f, 0.090f, 0.165f);        // #0f172a, the bar behind the sections
        public static readonly ColorRgba FieldFill = new ColorRgba(0.376f, 0.502f, 0.227f);      // #608039, the field inside the boundary
        public static int Symbol;   // 0 arrow, 1 dot, 2 tractor
        public static readonly ColorRgba Boundary = new ColorRgba(0.886f, 0.910f, 0.941f);       // #e2e8f0
        public static readonly ColorRgba BoundaryHalo = new ColorRgba(0.059f, 0.090f, 0.165f, 0.6f);
        public static readonly ColorRgba OutOfBounds = new ColorRgba(0.863f, 0.149f, 0.149f);    // #dc2626
        public static readonly ColorRgba Vehicle = new ColorRgba(0.114f, 0.306f, 0.847f);        // #1d4ed8
        public static readonly ColorRgba VehicleEdge = new ColorRgba(0.973f, 0.980f, 0.988f);    // #f8fafc

        // ---------------------------------------------------------------- measuring tape

        // Point A as a ring, a dashed line from A to the tractor (straight-line distance shown in the pill)
        public static void DrawMeet(vec3 a, vec3 pos, double zoom)
        {
            double r = Math.Max(0.6, zoom * 0.06);
            GL.PushMatrix();
            GL.Translate(0, 0, 0.02);
            GL.LineWidth(4);
            GL.Enable(EnableCap.LineStipple);
            GL.LineStipple(3, 0x0F0F);
            GL.Color4(0.961f, 0.620f, 0.043f, 0.95f);
            GL.Begin(PrimitiveType.Lines);
            GL.Vertex3(a.easting, a.northing, 0);
            GL.Vertex3(pos.easting, pos.northing, 0);
            GL.End();
            GL.Disable(EnableCap.LineStipple);
            GL.LineWidth(3);
            GL.Begin(PrimitiveType.LineLoop);
            for (int i = 0; i < 24; i++)
            {
                double t = i * Math.PI * 2 / 24;
                GL.Vertex3(a.easting + Math.Cos(t) * r, a.northing + Math.Sin(t) * r, 0);
            }
            GL.End();
            GL.PointSize(8);
            GL.Begin(PrimitiveType.Points);
            GL.Vertex3(a.easting, a.northing, 0);
            GL.End();
            GL.PopMatrix();
        }

        // ---------------------------------------------------------------- ground

        // Diagonal bands over the whole ground, a shade lighter than the ground colour (the canvas
        // background). Band width grows with the zoom so the pattern stays calm at every scale.
        public static void DrawStripes(double eMin, double eMax, double nMin, double nMax, ColorRgba ground, double zoom)
        {
            double cx = (eMin + eMax) * 0.5, cy = (nMin + nMax) * 0.5;
            double half = Math.Max(eMax - eMin, nMax - nMin) * 0.75;   // covers the rotated square
            double band = Math.Max(2.0, zoom * 0.18);
            GL.Color4(Math.Min(1f, ground.Red / 255f * 1.09f + 0.01f), Math.Min(1f, ground.Green / 255f * 1.09f + 0.01f), Math.Min(1f, ground.Blue / 255f * 1.09f + 0.01f), 1f);
            GL.PushMatrix();
            GL.Translate(cx, cy, -0.095);
            GL.Rotate(25.0, 0, 0, 1);
            GL.Begin(PrimitiveType.Quads);
            int n = (int)(half / band);
            for (int i = -n; i <= n; i += 2)
            {
                double x0 = i * band, x1 = x0 + band;
                GL.Vertex3(x0, -half, 0); GL.Vertex3(x1, -half, 0); GL.Vertex3(x1, half, 0); GL.Vertex3(x0, half, 0);
            }
            GL.End();
            GL.PopMatrix();
        }

        // The field inside the outer boundary a shade lighter, inner boundaries back in the ground colour.
        // Triangulated once per boundary (ear clipping), kept while the fence does not change.
        private static readonly Dictionary<int, FillCache> fills = new Dictionary<int, FillCache>();
        private class FillCache { public int Count; public double FirstE, FirstN; public List<vec2> Tris = new List<vec2>(); }

        public static void DrawFieldFill(List<CBoundaryList> bnd, ColorRgba ground)
        {
            for (int b = 0; b < bnd.Count; b++)
            {
                var fence = bnd[b].fenceLine;
                if (fence == null || fence.Count < 3) continue;
                if (!fills.TryGetValue(b, out FillCache c) || c.Count != fence.Count
                    || Math.Abs(c.FirstE - fence[0].easting) > 0.001 || Math.Abs(c.FirstN - fence[0].northing) > 0.001)
                {
                    c = new FillCache { Count = fence.Count, FirstE = fence[0].easting, FirstN = fence[0].northing };
                    Triangulate(fence, c.Tris);
                    fills[b] = c;
                }
                if (c.Tris.Count == 0) continue;
                if (b == 0) GLW.SetColor(FieldFill); else GLW.SetColor(ground);
                GL.Begin(PrimitiveType.Triangles);
                double z = b == 0 ? -0.09 : -0.085;
                for (int i = 0; i < c.Tris.Count; i++) GL.Vertex3(c.Tris[i].easting, c.Tris[i].northing, z);
                GL.End();
            }
            if (fills.Count > bnd.Count) fills.Clear();
        }

        public static void ResetFieldFill() { fills.Clear(); }

        // Plain ear clipping; boundaries have a few hundred points, so O(n^2) once is fine.
        private static void Triangulate(List<vec3> fence, List<vec2> tris)
        {
            var idx = new List<int>(fence.Count);
            int n = fence.Count;
            if (Math.Abs(fence[0].easting - fence[n - 1].easting) < 0.01 && Math.Abs(fence[0].northing - fence[n - 1].northing) < 0.01) n--;
            double area = 0;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                area += fence[i].easting * fence[j].northing - fence[j].easting * fence[i].northing;
            }
            if (area > 0) for (int i = 0; i < n; i++) idx.Add(i);
            else for (int i = n - 1; i >= 0; i--) idx.Add(i);   // make it counter-clockwise

            int guard = 0;
            while (idx.Count > 3 && guard++ < 100000)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int i0 = idx[(i + idx.Count - 1) % idx.Count], i1 = idx[i], i2 = idx[(i + 1) % idx.Count];
                    vec3 a = fence[i0], bb = fence[i1], cc = fence[i2];
                    double cross = (bb.easting - a.easting) * (cc.northing - a.northing) - (bb.northing - a.northing) * (cc.easting - a.easting);
                    if (cross <= 1e-9) continue;   // reflex corner
                    bool inside = false;
                    for (int k = 0; k < idx.Count; k++)
                    {
                        int ik = idx[k];
                        if (ik == i0 || ik == i1 || ik == i2) continue;
                        if (InTriangle(fence[ik], a, bb, cc)) { inside = true; break; }
                    }
                    if (inside) continue;
                    tris.Add(new vec2(a.easting, a.northing)); tris.Add(new vec2(bb.easting, bb.northing)); tris.Add(new vec2(cc.easting, cc.northing));
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;   // degenerate remainder: stop, what is drawn is drawn
            }
            if (idx.Count == 3)
            {
                tris.Add(new vec2(fence[idx[0]].easting, fence[idx[0]].northing));
                tris.Add(new vec2(fence[idx[1]].easting, fence[idx[1]].northing));
                tris.Add(new vec2(fence[idx[2]].easting, fence[idx[2]].northing));
            }
        }

        private static bool InTriangle(vec3 p, vec3 a, vec3 b, vec3 c)
        {
            double d1 = (p.easting - b.easting) * (a.northing - b.northing) - (a.easting - b.easting) * (p.northing - b.northing);
            double d2 = (p.easting - c.easting) * (b.northing - c.northing) - (b.easting - c.easting) * (p.northing - c.northing);
            double d3 = (p.easting - a.easting) * (c.northing - a.northing) - (c.easting - a.easting) * (p.northing - a.northing);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        // ---------------------------------------------------------------- vehicle symbols

        // Navigation arrow around the pivot: tip ahead of the front axle, notch at the back.
        // Vehicle space: x to the right, y forward, pivot at the origin, wheelbase along y.
        public static void DrawVehicleArrow(double wheelbase, double opacity)
        {
            if (ThreeD) { Draw3D(wheelbase, opacity); return; }
            if (Symbol == 1) { DrawDot(wheelbase, opacity); return; }
            if (Symbol == 2) { DrawTractor(wheelbase, opacity); return; }
            double len = Math.Max(5.0, wheelbase * 1.8);
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
            double r = Math.Max(2.0, wheelbase * 0.7);
            GL.Color4(Vehicle.Red / 255f, Vehicle.Green / 255f, Vehicle.Blue / 255f, (float)opacity);
            GL.Begin(PrimitiveType.TriangleFan);
            GL.Vertex3(0, 0, 0);
            for (int i = 0; i <= 32; i++) { double a = i / 32.0 * 2 * Math.PI; GL.Vertex3(r * Math.Sin(a), r * Math.Cos(a), 0); }
            GL.End();
            GLW.SetLineWidth(2.5f);
            GLW.SetColor(VehicleEdge);
            GL.Begin(PrimitiveType.LineLoop);
            for (int i = 0; i < 32; i++) { double a = i / 32.0 * 2 * Math.PI; GL.Vertex3(r * Math.Sin(a), r * Math.Cos(a), 0); }
            GL.End();
            GL.Begin(PrimitiveType.Triangles);
            GL.Vertex3(0, r * 1.5, 0); GL.Vertex3(-r * 0.45, r * 0.7, 0); GL.Vertex3(r * 0.45, r * 0.7, 0);
            GL.End();
            GLW.SetLineWidth(1.0f);
        }

        // Simple tractor from above: body, four wheels, cab
        private static void DrawTractor(double wheelbase, double opacity)
        {
            double wb = Math.Max(2.5, wheelbase), hw = wb * 0.28;
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

        // ---------------------------------------------------------------- 3D symbols (tilted camera)

        // Same three symbols with height: arrow and dot as a raised slab, tractor as boxes.
        // Depth test only here, so the faces sort among themselves; the rest of the map is flat.
        private static void Draw3D(double wheelbase, double opacity)
        {
            GL.Enable(EnableCap.DepthTest);
            GL.Clear(ClearBufferMask.DepthBufferBit);
            if (Symbol == 1) Dot3D(wheelbase, opacity);
            else if (Symbol == 2) Tractor3D(wheelbase, opacity);
            else Arrow3D(wheelbase, opacity);
            GL.Disable(EnableCap.DepthTest);
            GLW.SetLineWidth(1.0f);
        }

        private static void Shade(ColorRgba c, float k, double opacity)
        {
            GL.Color4(Math.Min(1f, c.Red / 255f * k), Math.Min(1f, c.Green / 255f * k), Math.Min(1f, c.Blue / 255f * k), (float)opacity);
        }

        // A closed outline raised to height h: top face (fan, convex or star-shaped around the origin) and walls
        private static void Prism(double[] xs, double[] ys, double h, ColorRgba c, double opacity, bool fanFromCentre)
        {
            int n = xs.Length;
            // walls, a touch darker, the ones facing forward a touch lighter
            GL.Begin(PrimitiveType.Quads);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                double nx = ys[j] - ys[i], ny = xs[i] - xs[j];
                double len = Math.Sqrt(nx * nx + ny * ny); if (len < 1e-9) continue;
                float k = (float)(0.62 + 0.18 * (ny / len));
                Shade(c, k, opacity);
                GL.Vertex3(xs[i], ys[i], 0); GL.Vertex3(xs[j], ys[j], 0); GL.Vertex3(xs[j], ys[j], h); GL.Vertex3(xs[i], ys[i], h);
            }
            GL.End();
            // top
            Shade(c, 1.0f, opacity);
            GL.Begin(PrimitiveType.TriangleFan);
            if (fanFromCentre) GL.Vertex3(0, 0, h);
            for (int i = 0; i < n; i++) GL.Vertex3(xs[i], ys[i], h);
            if (fanFromCentre) GL.Vertex3(xs[0], ys[0], h);
            GL.End();
            // light edge on top
            GLW.SetLineWidth(2.0f);
            GLW.SetColor(VehicleEdge);
            GL.Begin(PrimitiveType.LineLoop);
            for (int i = 0; i < n; i++) GL.Vertex3(xs[i], ys[i], h + 0.01);
            GL.End();
        }

        private static void Arrow3D(double wheelbase, double opacity)
        {
            double len = Math.Max(5.0, wheelbase * 1.8);
            double tipY = len * 0.75, backY = -len * 0.35, notchY = -len * 0.12, half = len * 0.42;
            // the notch makes it concave: fan from the notch point itself keeps every triangle inside
            double[] xs = { 0, -half, 0, half }, ys = { notchY, backY, tipY, backY };
            Prism(xs, ys, len * 0.18, Vehicle, opacity, false);
        }

        private static void Dot3D(double wheelbase, double opacity)
        {
            double r = Math.Max(2.0, wheelbase * 0.7);
            int n = 32;
            double[] xs = new double[n], ys = new double[n];
            for (int i = 0; i < n; i++) { double a = i / (double)n * 2 * Math.PI; xs[i] = r * Math.Sin(a); ys[i] = r * Math.Cos(a); }
            Prism(xs, ys, r * 0.5, Vehicle, opacity, true);
            // heading wedge on top
            GLW.SetColor(VehicleEdge);
            GL.Begin(PrimitiveType.Triangles);
            GL.Vertex3(0, r * 1.5, r * 0.5 + 0.02); GL.Vertex3(-r * 0.45, r * 0.7, r * 0.5 + 0.02); GL.Vertex3(r * 0.45, r * 0.7, r * 0.5 + 0.02);
            GL.End();
        }

        private static void Box(double x0, double y0, double z0, double x1, double y1, double z1, ColorRgba c, double opacity)
        {
            double[] xs = { x0, x1, x1, x0 }, ys = { y0, y0, y1, y1 };
            GL.PushMatrix();
            GL.Translate(0, 0, z0);
            int n = 4;
            GL.Begin(PrimitiveType.Quads);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                double nx = ys[j] - ys[i], ny = xs[i] - xs[j];
                double len = Math.Sqrt(nx * nx + ny * ny);
                float k = (float)(0.62 + 0.18 * (ny / len));
                Shade(c, k, opacity);
                GL.Vertex3(xs[i], ys[i], 0); GL.Vertex3(xs[j], ys[j], 0); GL.Vertex3(xs[j], ys[j], z1 - z0); GL.Vertex3(xs[i], ys[i], z1 - z0);
            }
            Shade(c, 1.0f, opacity);
            GL.Vertex3(x0, y0, z1 - z0); GL.Vertex3(x1, y0, z1 - z0); GL.Vertex3(x1, y1, z1 - z0); GL.Vertex3(x0, y1, z1 - z0);
            GL.End();
            GL.PopMatrix();
        }

        // Boxy tractor: big rear wheels, small front wheels, bonnet, cab with a light roof
        private static void Tractor3D(double wheelbase, double opacity)
        {
            double wb = Math.Max(2.5, wheelbase), hw = wb * 0.28;
            var dark = new ColorRgba(0.06f, 0.09f, 0.16f);
            var glass = new ColorRgba(0.53f, 0.81f, 0.92f);
            double rearR = wb * 0.30, frontR = wb * 0.18;
            // wheels
            Box(-hw - wb * 0.18, -wb * 0.30, 0, -hw - wb * 0.02, wb * 0.25, rearR * 2, dark, opacity);
            Box(hw + wb * 0.02, -wb * 0.30, 0, hw + wb * 0.18, wb * 0.25, rearR * 2, dark, opacity);
            Box(-hw - wb * 0.14, wb * 0.75, 0, -hw - wb * 0.02, wb * 1.05, frontR * 2, dark, opacity);
            Box(hw + wb * 0.02, wb * 0.75, 0, hw + wb * 0.14, wb * 1.05, frontR * 2, dark, opacity);
            // body and bonnet
            Box(-hw, -wb * 0.35, rearR * 0.6, hw, wb * 0.55, rearR * 1.4, Vehicle, opacity);
            Box(-hw * 0.8, wb * 0.55, rearR * 0.6, hw * 0.8, wb * 1.15, rearR * 1.25, Vehicle, opacity);
            // cab: glass walls, light roof
            Box(-hw * 0.85, -wb * 0.05, rearR * 1.4, hw * 0.85, wb * 0.55, rearR * 2.6, glass, opacity);
            Box(-hw * 0.95, -wb * 0.1, rearR * 2.6, hw * 0.95, wb * 0.6, rearR * 2.75, VehicleEdge, opacity);
            // light edge around the roof
            GLW.SetLineWidth(2.0f);
            GLW.SetColor(VehicleEdge);
            GL.Begin(PrimitiveType.LineLoop);
            GL.Vertex3(-hw * 0.95, -wb * 0.1, rearR * 2.76); GL.Vertex3(hw * 0.95, -wb * 0.1, rearR * 2.76);
            GL.Vertex3(hw * 0.95, wb * 0.6, rearR * 2.76); GL.Vertex3(-hw * 0.95, wb * 0.6, rearR * 2.76);
            GL.End();
        }
    }
}
