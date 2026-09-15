using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EarthRecovery
{
    [Serializable] public sealed class GeneratedLot
    {
        public string id, roadId;
        public CityRoadKind roadKind;
        public bool corner;
        public BuildingUsage usage;
        public HumanThingsPlacementZone zone;
        public bool occupied, occupiedBySpecialPOI;
        public string occupantId;
    }
    public static class BuildingLotGenerator
    {
        public static List<GeneratedLot> Generate(RuntimeCityGenerationConfig c, List<GeneratedRoad> roads, List<string> warnings)
        {
            var free = new List<Rect> { new(-c.mapWidth / 2, -c.mapDepth / 2, c.mapWidth, c.mapDepth) };
            foreach (var road in roads) free = CityRect.Subtract(free, CityRect.Expand(CityRect.FromBounds(road.bounds), c.sidewalkWidth));
            var lots = new List<GeneratedLot>(); var rng = new System.Random(unchecked(c.seed ^ 0x54712));
            int tiny = 0, inaccessible = 0;
            foreach (var residual in free.OrderBy(r => r.xMin).ThenBy(r => r.yMin))
            {
                var block = residual;
                if (Mathf.Min(block.width, block.height) < 8) { tiny++; continue; }
                var neighbors = roads.Select(r => (road: r, edge: Facing(block, r, c.sidewalkWidth))).Where(v => v.edge != Vector3.zero).ToList();
                if (neighbors.Count == 0) { inaccessible++; continue; }
                var nearest = neighbors.OrderBy(v => v.road.bounds.SqrDistance(new Vector3(block.center.x, 0, block.center.y))).ThenBy(v => v.road.id, StringComparer.Ordinal).First();
                bool faceX = nearest.edge.x != 0;
                var roadBounds = nearest.road.bounds;
                // Restrict every lot's frontage to a real road segment, including dead ends.
                block = faceX ? Rect.MinMaxRect(block.xMin, Mathf.Max(block.yMin, roadBounds.min.z), block.xMax, Mathf.Min(block.yMax, roadBounds.max.z))
                    : Rect.MinMaxRect(Mathf.Max(block.xMin, roadBounds.min.x), block.yMin, Mathf.Min(block.xMax, roadBounds.max.x), block.yMax);
                if (Mathf.Min(block.width, block.height) < 8) { tiny++; continue; }
                float frontage = faceX ? block.height : block.width;
                int count = Mathf.Clamp(Mathf.FloorToInt(frontage / (c.blockSize * Mathf.Lerp(.7f, 1.2f, (float)rng.NextDouble()))), 1, 20);
                for (int i = 0; i < count; i++)
                {
                    var position = new Vector3(block.center.x, 0, block.center.y);
                    if (faceX) position.z = block.yMin + (i + .5f) * frontage / count;
                    else position.x = block.xMin + (i + .5f) * frontage / count;
                    var size = faceX ? new Vector2(block.width - 1, frontage / count - 1) : new Vector2(frontage / count - 1, block.height - 1);
                    var id = "Lot_" + lots.Count.ToString("D4");
                    lots.Add(new GeneratedLot { id = id, roadId = nearest.road.id, roadKind = nearest.road.kind, corner = neighbors.Count > 1 && (i == 0 || i == count - 1),
                        zone = new HumanThingsPlacementZone { id = id, kind = PlacementZoneKind.BuildingLot, position = position, size = size, roadDirection = nearest.edge } });
                }
            }
            if (tiny > 0) warnings.Add(tiny + " narrow residual blocks excluded (minimum side 8m).");
            if (inaccessible > 0) warnings.Add(inaccessible + " residual blocks have no direct road frontage; excluded.");
            return lots;
        }
        static Vector3 Facing(Rect block, GeneratedRoad road, float sidewalk)
        {
            var r = CityRect.FromBounds(road.bounds); float epsilon = .03f;
            bool zOverlap = Mathf.Min(block.yMax, r.yMax) - Mathf.Max(block.yMin, r.yMin) > 4;
            bool xOverlap = Mathf.Min(block.xMax, r.xMax) - Mathf.Max(block.xMin, r.xMin) > 4;
            if (zOverlap && Mathf.Abs(block.xMin - r.xMax - sidewalk) < epsilon) return Vector3.left;
            if (zOverlap && Mathf.Abs(r.xMin - block.xMax - sidewalk) < epsilon) return Vector3.right;
            if (xOverlap && Mathf.Abs(block.yMin - r.yMax - sidewalk) < epsilon) return Vector3.back;
            if (xOverlap && Mathf.Abs(r.yMin - block.yMax - sidewalk) < epsilon) return Vector3.forward;
            return Vector3.zero;
        }
    }
    public static class BuildingUsageResolver
    {
        public static BuildingUsage Resolve(RuntimeCityGenerationConfig c, System.Random rng)
        {
            double v = rng.NextDouble();
            if ((v -= c.commercialRatio) < 0) return BuildingUsage.Commercial;
            if ((v -= c.residentialRatio) < 0) return BuildingUsage.Residential;
            if ((v -= c.officeRatio) < 0) return BuildingUsage.Office;
            return BuildingUsage.Public;
        }
    }
}
