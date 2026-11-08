using System;
using System.Collections.Generic;
using OpenTK.Graphics.OpenGL;

namespace AgOpenGPS
{
    // Johan fork: finished section patches live in GPU buffers.
    // Upstream sends every vertex of every patch through GL.Begin/GL.Vertex3 on every frame, which is the
    // main cost on a tablet with a big field. A patch that is no longer growing never changes, so it is
    // uploaded once (two floats per vertex) and drawn with one GL.DrawArrays afterwards. The growing patch
    // of a section that is switched on keeps the immediate-mode path, as do the optional section lines and
    // direction markers. The section control (oglBack) is not touched.
    public partial class FormGPS
    {
        private sealed class PatchVbo
        {
            public int BufferId;
            public int VertexCount;
            public byte R, G, B;
            public double MinE, MaxE, MinN, MaxN;
        }

        private readonly Dictionary<List<vec3>, PatchVbo> patchVbos = new Dictionary<List<vec3>, PatchVbo>();
        private int patchVboSweepCounter;
        private bool patchVboResetRequested;

        // Called when a field closes; the buffers are released on the next paint, with the GL context current
        public void RequestPatchVboReset() => patchVboResetRequested = true;

        private PatchVbo BuildPatchVbo(List<vec3> triList)
        {
            int n = triList.Count - 1;                       // element 0 holds the colour
            var data = new float[n * 2];
            double minE = double.MaxValue, maxE = double.MinValue, minN = double.MaxValue, maxN = double.MinValue;
            for (int i = 1; i <= n; i++)
            {
                double e = triList[i].easting, no = triList[i].northing;
                data[(i - 1) * 2] = (float)e;
                data[(i - 1) * 2 + 1] = (float)no;
                if (e < minE) minE = e; if (e > maxE) maxE = e;
                if (no < minN) minN = no; if (no > maxN) maxN = no;
            }
            int id = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, id);
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(data.Length * sizeof(float)), data, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            return new PatchVbo
            {
                BufferId = id,
                VertexCount = n,
                R = (byte)triList[0].easting,
                G = (byte)triList[0].northing,
                B = (byte)triList[0].heading,
                MinE = minE,
                MaxE = maxE,
                MinN = minN,
                MaxN = maxN
            };
        }

        // Draws one finished patch from its buffer (uploading it first when it is new).
        // Returns false when the patch cannot go through this path, so the caller draws it the old way.
        private bool DrawPatchVbo(List<vec3> triList, bool isDay)
        {
            if (triList.Count < 4) return false;
            if (!patchVbos.TryGetValue(triList, out PatchVbo vbo))
            {
                try { vbo = BuildPatchVbo(triList); }
                catch { return false; }
                patchVbos[triList] = vbo;
            }

            GL.Color4(vbo.R, vbo.G, vbo.B, isDay ? (byte)152 : (byte)76);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo.BufferId);
            GL.EnableClientState(ArrayCap.VertexArray);
            GL.VertexPointer(2, VertexPointerType.Float, 0, IntPtr.Zero);
            GL.DrawArrays(PrimitiveType.TriangleStrip, 0, vbo.VertexCount);
            GL.DisableClientState(ArrayCap.VertexArray);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            return true;
        }

        // Every few hundred frames: drop buffers of patches that no longer exist (field closed, patch removed)
        private void SweepPatchVbos()
        {
            if (patchVboResetRequested)
            {
                patchVboResetRequested = false;
                foreach (var v in patchVbos.Values) GL.DeleteBuffer(v.BufferId);
                patchVbos.Clear();
                return;
            }
            if (++patchVboSweepCounter < 300 || patchVbos.Count == 0) return;
            patchVboSweepCounter = 0;

            var live = new HashSet<List<vec3>>();
            foreach (var sec in triStrip)
                foreach (var pl in sec.patchList) live.Add(pl);

            List<List<vec3>> dead = null;
            foreach (var kv in patchVbos)
                if (!live.Contains(kv.Key)) (dead ?? (dead = new List<List<vec3>>())).Add(kv.Key);
            if (dead == null) return;
            foreach (var k in dead)
            {
                GL.DeleteBuffer(patchVbos[k].BufferId);
                patchVbos.Remove(k);
            }
        }
    }
}
