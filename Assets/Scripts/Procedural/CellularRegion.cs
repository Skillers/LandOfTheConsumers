using System.Collections.Generic;
using UnityEngine;

namespace LandOfTheConsumers.Procedural
{
    /// <summary>
    /// Represents a region in cellular noise, containing all points that belong to this region
    /// </summary>
    [System.Serializable]
    public class CellularRegion
    {
        public int id;
        public List<Vector2> points;
        public List<Vector2Int> pixels; // Pixel coordinates that belong to this region

        public CellularRegion(int id)
        {
            this.id = id;
            this.points = new List<Vector2>();
            this.pixels = new List<Vector2Int>();
        }

        public void AddPoint(Vector2 point)
        {
            points.Add(point);
        }

        public void AddPixel(Vector2Int pixel)
        {
            pixels.Add(pixel);
        }

        public int PointCount => points.Count;
        public int PixelCount => pixels.Count;
    }
}
