using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;

namespace AgOpenGPS
{
    public partial class CBoundary
    {
        public List<vec3> bndBeingMadePts = new List<vec3>(128);

        public double createBndOffset;
        public bool isBndBeingMade;

        public bool isDrawRightSide = true, isDrawAtPivot = true, isOkToAddPoints = false;
        public bool isRecBoundaryWhenSectionOn = false;

        public bool IsPointInsideFenceArea(vec3 testPoint)
        {
            //first where are we, must be inside outer and outside of inner geofence non drive thru turn borders
            if (bndList[0].fenceLineEar.IsPointInPolygon(testPoint))
            {
                for (int i = 1; i < bndList.Count; i++)
                {
                    //make sure not inside a non drivethru boundary
                    //if (bndList[i].isDriveThru) continue;
                    if (bndList[i].fenceLineEar.IsPointInPolygon(testPoint))
                    {
                        return false;
                    }
                }
                //we are safely inside outer, outside inner boundaries
                return true;
            }
            return false;
        }

        public bool IsPointInsideFenceArea(vec2 testPoint)
        {
            //first where are we, must be inside outer and outside of inner geofence non drive thru turn borders
            if (bndList[0].fenceLineEar.IsPointInPolygon(testPoint))
            {
                for (int i = 1; i < bndList.Count; i++)
                {
                    //make sure not inside a non drivethru boundary
                    //if (bndList[i].isDriveThru) continue;
                    if (bndList[i].fenceLineEar.IsPointInPolygon(testPoint))
                    {
                        return false;
                    }
                }
                //we are safely inside outer, outside inner boundaries
                return true;
            }
            return false;
        }

        public void DrawFenceLines()
        {
            if (!mf.mc.isOutOfBounds)
            {
                if (JohanLook.On) GL.Color4(JohanLook.BoundaryHalo.Red / 255f, JohanLook.BoundaryHalo.Green / 255f, JohanLook.BoundaryHalo.Blue / 255f, JohanLook.BoundaryHalo.Alpha / 255f);
                else GL.Color4(0, 0, 0, 0.8);
                GL.LineWidth(6);

                for (int i = 0; i < bndList.Count; i++)
                {
                    bndList[i].fenceLineEar.DrawPolygon();
                }

                if (JohanLook.On) { GL.Color4(JohanLook.Boundary.Red / 255f, JohanLook.Boundary.Green / 255f, JohanLook.Boundary.Blue / 255f, 0.95f); GL.LineWidth(3); }
                else { GL.Color4(0.95f, 0.44f, 0.350f, 0.8f); GL.LineWidth(2); }

                for (int i = 0; i < bndList.Count; i++)
                {
                    bndList[i].fenceLineEar.DrawPolygon();
                }
            }
            else
            {
                GL.LineWidth(mf.ABLine.lineWidth * 3);
                if (JohanLook.On) GL.Color3(JohanLook.OutOfBounds.Red / 255f, JohanLook.OutOfBounds.Green / 255f, JohanLook.OutOfBounds.Blue / 255f);
                else GL.Color3(0.95f, 0.25f, 0.250f);

                for (int i = 0; i < bndList.Count; i++)
                {
                    bndList[i].fenceLineEar.DrawPolygon();
                }
            }

            ////closest points  TooDoo
            //GL.Color3(0.70f, 0.95f, 0.95f);
            //GL.PointSize(6.0f);
            //GL.Begin(PrimitiveType.Points);
            //GL.Vertex3(mf.bnd.closestTurnPt.easting, mf.bnd.closestTurnPt.northing, 0);
            //GL.End();

            if (bndBeingMadePts.Count > 0)
            {
                //the boundary so far
                vec3 pivot = mf.pivotAxlePos;
                GL.LineWidth(mf.ABLine.lineWidth);
                GL.Color3(0.825f, 0.22f, 0.90f);
                GL.Begin(PrimitiveType.LineStrip);
                for (int h = 0; h < bndBeingMadePts.Count; h++) GL.Vertex3(bndBeingMadePts[h].easting, bndBeingMadePts[h].northing, 0);
                GL.Color3(0.295f, 0.972f, 0.290f);
                GL.Vertex3(bndBeingMadePts[0].easting, bndBeingMadePts[0].northing, 0);
                GL.End();

                //line from last point to pivot marker
                GL.Color3(0.825f, 0.842f, 0.0f);
                GL.Enable(EnableCap.LineStipple);
                GL.LineStipple(1, 0x0700);
                GL.Begin(PrimitiveType.LineStrip);

                if (isDrawAtPivot)
                {
                    if (isDrawRightSide)
                    {
                        GL.Vertex3(bndBeingMadePts[0].easting, bndBeingMadePts[0].northing, 0);

                        GL.Vertex3(pivot.easting + (Math.Sin(pivot.heading - glm.PIBy2) * -createBndOffset),
                                pivot.northing + (Math.Cos(pivot.heading - glm.PIBy2) * -createBndOffset), 0);
                        GL.Vertex3(bndBeingMadePts[bndBeingMadePts.Count - 1].easting, bndBeingMadePts[bndBeingMadePts.Count - 1].northing, 0);
                    }
                    else
                    {
                        GL.Vertex3(bndBeingMadePts[0].easting, bndBeingMadePts[0].northing, 0);

                        GL.Vertex3(pivot.easting + (Math.Sin(pivot.heading - glm.PIBy2) * createBndOffset),
                                pivot.northing + (Math.Cos(pivot.heading - glm.PIBy2) * createBndOffset), 0);
                        GL.Vertex3(bndBeingMadePts[bndBeingMadePts.Count - 1].easting, bndBeingMadePts[bndBeingMadePts.Count - 1].northing, 0);
                    }
                }
                else //draw from tool
                {
                    if (isDrawRightSide)
                    {
                        GL.Vertex3(bndBeingMadePts[0].easting, bndBeingMadePts[0].northing, 0);
                        GL.Vertex3(mf.section[mf.tool.numOfSections - 1].rightPoint.easting, mf.section[mf.tool.numOfSections - 1].rightPoint.northing, 0);
                        GL.Vertex3(bndBeingMadePts[bndBeingMadePts.Count - 1].easting, bndBeingMadePts[bndBeingMadePts.Count - 1].northing, 0);
                    }
                    else
                    {
                        GL.Vertex3(bndBeingMadePts[0].easting, bndBeingMadePts[0].northing, 0);
                        GL.Vertex3(mf.section[0].leftPoint.easting, mf.section[0].leftPoint.northing, 0);
                        GL.Vertex3(bndBeingMadePts[bndBeingMadePts.Count - 1].easting, bndBeingMadePts[bndBeingMadePts.Count - 1].northing, 0);
                    }
                }
                GL.End();
                GL.Disable(EnableCap.LineStipple);

                //boundary points
                GL.Color3(0.0f, 0.95f, 0.95f);
                GL.PointSize(6.0f);
                GL.Begin(PrimitiveType.Points);
                for (int h = 0; h < bndBeingMadePts.Count; h++) GL.Vertex3(bndBeingMadePts[h].easting, bndBeingMadePts[h].northing, 0);
                GL.End();
            }
        }
    }
}