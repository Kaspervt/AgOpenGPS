using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using AgLibrary.Logging;
using AgOpenGPS.Core.Models;
using AgOpenGPS.Core.Streamers;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;

namespace AgOpenGPS
{
    // Johan fork: the aerial photo without the map window. One tap fetches the Bing tiles for the
    // open field (fitted to its boundary) or, without a boundary, for a square around the tractor,
    // stitches them into one bitmap and saves it as BackPic in the field folder, exactly what the
    // "background" checkbox of FormMap produces. GMap keeps its own tile cache, so a second fetch
    // of the same place works offline too.
    public partial class FormGPS
    {
        private bool johanFotoBezig;

        public void JohanLuchtfotoOphalen()
        {
            if (!isJobStarted || johanFotoBezig) return;

            // area in WGS84: the outer boundary with a margin, or 400 m around the position
            double minLat, maxLat, minLon, maxLon;
            if (bnd.bndList.Count > 0 && bnd.bndList[0].fenceLine.Count > 2)
            {
                minLat = 90; maxLat = -90; minLon = 180; maxLon = -180;
                foreach (var p in bnd.bndList[0].fenceLine)
                {
                    Wgs84 w = AppModel.LocalPlane.ConvertGeoCoordToWgs84(new GeoCoord(p.northing, p.easting));
                    if (w.Latitude < minLat) minLat = w.Latitude;
                    if (w.Latitude > maxLat) maxLat = w.Latitude;
                    if (w.Longitude < minLon) minLon = w.Longitude;
                    if (w.Longitude > maxLon) maxLon = w.Longitude;
                }
                double padLat = (maxLat - minLat) * 0.10 + 0.0002, padLon = (maxLon - minLon) * 0.10 + 0.0003;
                minLat -= padLat; maxLat += padLat; minLon -= padLon; maxLon += padLon;
            }
            else
            {
                Wgs84 here = AppModel.CurrentLatLon;
                if (!isGPSPositionInitialized) { TimedMessageBox(2000, "Luchtfoto", "Nog geen GPS-positie"); return; }
                double dLat = 400.0 / 111320.0;
                double dLon = 400.0 / (111320.0 * Math.Cos(here.Latitude * Math.PI / 180.0));
                minLat = here.Latitude - dLat; maxLat = here.Latitude + dLat;
                minLon = here.Longitude - dLon; maxLon = here.Longitude + dLon;
            }

            johanFotoBezig = true;
            TimedMessageBox(2500, "Luchtfoto", "Luchtfoto ophalen voor dit perceel");
            string veld = currentFieldDirectory;
            var map = AppCore.ActiveField.FieldDirectory;
            var plane = AppModel.LocalPlane;   // captured here, the field may change while the task runs

            Task.Run(() =>
            {
                BingMap resultaat = null;
                string fout = null;
                try
                {
                    resultaat = JohanHaalTegels(plane, minLat, maxLat, minLon, maxLon, out fout);
                }
                catch (Exception ex) { fout = ex.Message; }

                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke(new Action(() =>
                {
                    johanFotoBezig = false;
                    if (resultaat == null)
                    {
                        Log.EventWriter("Aerial photo: nothing fetched for " + veld + ": " + fout);
                        TimedMessageBox(3000, "Luchtfoto", fout != null && fout.StartsWith("area")
                            ? "Perceel te groot voor één luchtfoto" : "Geen luchtfoto binnengekregen (internet?)");
                        return;
                    }
                    if (currentFieldDirectory != veld || !isJobStarted) return;   // field changed meanwhile
                    worldGrid.BingMap = resultaat;
                    try { new BingMapStreamer().TryWrite(resultaat, map); }
                    catch (Exception ex) { Log.EventWriter("Aerial photo: save failed: " + ex.Message); }
                    Log.EventWriter("Aerial photo fetched for " + veld + ": " + resultaat.Bitmap.Width + "x" + resultaat.Bitmap.Height);
                }));
            });
        }

        // Tiles straight from GMap (server or its cache), stitched; null when no tile came in
        private BingMap JohanHaalTegels(LocalPlane plane, double minLat, double maxLat, double minLon, double maxLon, out string fout)
        {
            fout = null;
            // normally done by the static constructor of GMapControl; without it no tile is ever decoded
            GMapImageProxy.Enable();
            GMapProvider provider = GMapProviders.BingHybridMap;
            GMaps.Instance.Mode = AccessMode.ServerAndCache;
            // the map control normally does this when the provider is assigned (Bing fetches its session key here)
            // (IsInitialized has an internal setter, so only the check is possible here, not the flag)
            try { if (!provider.IsInitialized) provider.OnInitialized(); } catch (Exception ex) { fout = "init: " + ex.Message; }
            var proj = provider.Projection;
            long ts = proj.TileSize.Width;

            // the finest zoom at which the area stays within about 1500 px (plus the rounding to whole tiles)
            int zoom = 15;
            for (int z = 19; z >= 15; z--)
            {
                GPoint a = proj.FromLatLngToPixel(maxLat, minLon, z), b = proj.FromLatLngToPixel(minLat, maxLon, z);
                if (Math.Abs(b.X - a.X) <= 1500 && Math.Abs(b.Y - a.Y) <= 1500) { zoom = z; break; }
            }

            GPoint pxTL = proj.FromLatLngToPixel(maxLat, minLon, zoom);
            GPoint pxBR = proj.FromLatLngToPixel(minLat, maxLon, zoom);
            GPoint tTL = proj.FromPixelToTileXY(pxTL), tBR = proj.FromPixelToTileXY(pxBR);
            int nx = (int)(tBR.X - tTL.X + 1), ny = (int)(tBR.Y - tTL.Y + 1);
            if (nx <= 0 || ny <= 0 || nx * ny > 64) { fout = "area too large: " + nx + "x" + ny + " tiles"; return null; }

            var bmp = new Bitmap((int)(nx * ts), (int)(ny * ts));
            int gelukt = 0, mislukt = 0;
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(63, 74, 58));
                for (long x = tTL.X; x <= tBR.X && !(mislukt >= 3 && gelukt == 0); x++)
                {
                    for (long y = tTL.Y; y <= tBR.Y; y++)
                    {
                        Exception ex;
                        PureImage img = GMaps.Instance.GetImageFrom(provider, new GPoint(x, y), zoom, out ex);
                        var gi = img as GMapImage;
                        if (gi?.Img == null)
                        {
                            if (ex != null) fout = ex.Message;
                            // offline: give up after three misses instead of waiting out dozens of time-outs
                            if (++mislukt >= 3 && gelukt == 0) break;
                            continue;
                        }
                        g.DrawImage(gi.Img, (x - tTL.X) * ts, (y - tTL.Y) * ts, ts, ts);
                        gelukt++;
                        gi.Dispose();
                    }
                }
            }
            if (gelukt == 0) { bmp.Dispose(); if (fout == null) fout = "no tiles"; return null; }

            // the stitched block in local coordinates (its corners, not the requested area)
            PointLatLng nw = proj.FromPixelToLatLng(proj.FromTileXYToPixel(tTL), zoom);
            PointLatLng se = proj.FromPixelToLatLng(proj.FromTileXYToPixel(new GPoint(tBR.X + 1, tBR.Y + 1)), zoom);
            var box = GeoBoundingBox.CreateEmpty();
            box.Include(plane.ConvertWgs84ToGeoCoord(new Wgs84(nw.Lat, nw.Lng)));
            box.Include(plane.ConvertWgs84ToGeoCoord(new Wgs84(se.Lat, se.Lng)));
            Log.EventWriter("Aerial photo: zoom " + zoom + ", " + nx + "x" + ny + " tiles, " + gelukt + " received");
            return new BingMap(box, bmp);
        }
    }
}
