using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EarthRecovery
{
    [Serializable] public sealed class GeneratedRoad
    {
        public string id;
        public CityRoadKind kind;
        public Vector3 start, end;
        public float width;
        public List<string> connections = new();
        public Bounds bounds;
        public GameObject root;
        public Vector3 Direction => (end - start).normalized;
    }
    public static class CityRect
    {
        public static Rect FromBounds(Bounds b) => Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z);
        public static Bounds Bounds(Rect r, float y = 0) => new(new Vector3(r.center.x, y, r.center.y), new Vector3(r.width, .1f, r.height));
        public static Rect Expand(Rect r, float padding) => Rect.MinMaxRect(r.xMin - padding, r.yMin - padding, r.xMax + padding, r.yMax + padding);
        public static Rect Clip(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Max(a.xMin, b.xMin), Mathf.Max(a.yMin, b.yMin), Mathf.Min(a.xMax, b.xMax), Mathf.Min(a.yMax, b.yMax));
        public static List<Rect> Subtract(IEnumerable<Rect> source, Rect cut)
        {
            var result = new List<Rect>();
            foreach (var r in source)
            {
                if (!r.Overlaps(cut)) { result.Add(r); continue; }
                var i = Clip(r, cut);
                Add(Rect.MinMaxRect(r.xMin, r.yMin, i.xMin, r.yMax));
                Add(Rect.MinMaxRect(i.xMax, r.yMin, r.xMax, r.yMax));
                Add(Rect.MinMaxRect(i.xMin, r.yMin, i.xMax, i.yMin));
                Add(Rect.MinMaxRect(i.xMin, i.yMax, i.xMax, r.yMax));
            }
            return result;
            void Add(Rect r) { if (r.width > .01f && r.height > .01f) result.Add(r); }
        }
    }
    public static class RoadNetworkGenerator
    {
        public static List<GeneratedRoad> Generate(RuntimeCityGenerationConfig c)
        {
            var rng = new System.Random(unchecked(c.seed ^ 0x23419));
            bool alongX = rng.Next(2) == 0;
            float length = (alongX ? c.mapWidth : c.mapDepth) - 4;
            float across = (alongX ? c.mapDepth : c.mapWidth) - 4;
            var roads = new List<GeneratedRoad>();
            Vector3 World(float x, float y) => alongX ? new Vector3(x, 0, y) : new Vector3(y, 0, x);
            float Range(float a, float b) => Mathf.Lerp(a, b, (float)rng.NextDouble());
            void Add(CityRoadKind kind, float x1, float y1, float x2, float y2, float width)
            {
                var a = World(x1, y1); var b = World(x2, y2);
                if ((a - b).sqrMagnitude < 16) return;
                var size = new Vector3(Mathf.Abs(a.x - b.x), .1f, Mathf.Abs(a.z - b.z));
                if (size.x < .01f) size.x = width; else size.z = width;
                roads.Add(new GeneratedRoad { id = "Road_" + roads.Count.ToString("D3"), kind = kind, start = a, end = b, width = width, bounds = new Bounds((a + b) / 2, size) });
            }
            var mainOffsets = new List<float>();
            float separation = c.roadWidth + c.sidewalkWidth * 2 + 14;
            int mains = Mathf.Min(c.mainRoadCount, Mathf.Max(1, Mathf.FloorToInt(across * .7f / separation)));
            if (mains != c.mainRoadCount) c.warnings.Add("Main road count limited by map width and clearances.");
            for (int i = 0; i < mains; i++)
            {
                float offset = (i - (mains - 1) / 2f) * separation + Range(-3, 3);
                mainOffsets.Add(offset);
                Add(CityRoadKind.MainRoad, -length / 2 + Range(0, 8), offset, length / 2 - Range(0, 8), offset, c.roadWidth);
            }
            float sideWidth = Mathf.Max(6, c.roadWidth * .65f);
            int sides = Mathf.Min(c.sideRoadCount, Mathf.Max(1, Mathf.FloorToInt(length / (sideWidth + c.sidewalkWidth * 2 + 12))));
            if (sides != c.sideRoadCount) c.warnings.Add("Side road count limited by frontage spacing.");
            var branches = new List<(float x, float low, float high)>();
            for (int i = 0; i < sides; i++)
            {
                float x = -length * .4f + (i + .5f) * length * .8f / sides + Range(-3, 3);
                bool through = i * mains < c.intersectionCount || i == 0 && mains > 1;
                int sign = rng.Next(2) == 0 ? -1 : 1;
                float lo = mainOffsets[0], hi = mainOffsets[mainOffsets.Count - 1];
                if (through || sign < 0) lo = Mathf.Max(-across / 2, lo - Range(across * .13f, across * .3f));
                if (through || sign > 0) hi = Mathf.Min(across / 2, hi + Range(across * .13f, across * .3f));
                Add(CityRoadKind.SideRoad, x, lo, x, hi, sideWidth);
                branches.Add((x, lo, hi));
            }
            for (int i = 0; i < c.alleyCount * 20 && roads.Count(r => r.kind == CityRoadKind.Alley) < c.alleyCount; i++)
            {
                float width = Mathf.Max(4, sideWidth * .65f);
                if (branches.Count > 0)
                {
                    var b = branches[rng.Next(branches.Count)]; float y = Range(b.low + 2, b.high - 2);
                    // Avoid running a parallel alley inside an existing main road corridor.
                    if (mainOffsets.Any(v => Mathf.Abs(y - v) < (c.roadWidth + width) / 2 + c.sidewalkWidth + 3)) continue;
                    if (roads.Any(r => r.kind == CityRoadKind.Alley && Mathf.Abs((alongX ? r.start.z : r.start.x) - y) < width + c.sidewalkWidth * 2 + 4)) continue;
                    int sign = rng.Next(2) == 0 ? -1 : 1;
                    float end = Mathf.Clamp(b.x + sign * Range(12, c.blockSize * 1.5f), -length / 2, length / 2);
                    Add(CityRoadKind.Alley, b.x, y, end, y, width);
                }
                else
                {
                    float x = Range(-length * .4f, length * .4f), y = mainOffsets[0];
                    Add(CityRoadKind.Alley, x, y, x, Mathf.Clamp(y + (rng.Next(2) == 0 ? -1 : 1) * Range(15, across * .3f), -across / 2, across / 2), width);
                }
            }
            foreach (var a in roads) foreach (var b in roads)
                if (a != b && HumanThingsPlacementSolver.Overlaps(a.bounds, b.bounds, .01f)) a.connections.Add(b.id);
            return roads;
        }
    }
}
