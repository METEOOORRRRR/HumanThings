using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EarthRecovery
{
    public static class HumanThingsPlacementSolver
    {
        public static bool Overlaps(Bounds a, Bounds b, float gap = 0) => a.min.x < b.max.x + gap && a.max.x > b.min.x - gap && a.min.z < b.max.z + gap && a.max.z > b.min.z - gap;
        public static IEnumerable<HumanThingsAssetEntry> Shuffled(List<HumanThingsAssetEntry> entries, System.Random rng)
        {
            var list = entries.ToArray();
            for (int i = list.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); (list[i], list[j]) = (list[j], list[i]); }
            return list;
        }
        public static Quaternion? Rotation(HumanThingsAssetEntry e, Vector3 direction, System.Random rng, bool vehicle)
        {
            float yaw = vehicle || e.frontFacesRoad ? Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg - Mathf.Atan2(e.localForward.x, e.localForward.z) * Mathf.Rad2Deg
                : e.allowRandomRotation ? (e.canRotate90 ? rng.Next(4) * 90 : rng.Next(2) * 180) : 0;
            if (!e.canRotate90 && Mathf.Abs(Mathf.DeltaAngle(yaw, Mathf.Round(yaw / 180) * 180)) > .01f) return null;
            return Quaternion.Euler(0, yaw, 0);
        }
        public static Vector3 RotatedSize(HumanThingsAssetEntry e, Quaternion rotation)
        {
            float width = Mathf.Max(e.boundsSize.x, e.footprintWidth), depth = Mathf.Max(e.boundsSize.z, e.footprintDepth);
            var x = rotation * new Vector3(width, 0, 0); var z = rotation * new Vector3(0, 0, depth);
            return new Vector3(Mathf.Abs(x.x) + Mathf.Abs(z.x), e.boundsSize.y, Mathf.Abs(x.z) + Mathf.Abs(z.z));
        }
        public static bool TryPlace(HumanThingsBlockPlan plan, HumanThingsAssetEntry e, HumanThingsPlacementZone zone, Vector3 center, Quaternion rotation, string group, float gap)
        {
            var bounds = new Bounds(center, RotatedSize(e, rotation));
            if (!zone.Contains(bounds, .4f) || plan.placements.Any(p => !p.surface && Overlaps(bounds, p.footprint, gap))) return false;
            if (plan.reserved.Any(b => Overlaps(bounds, b, gap))) return false;
            if (group == "StreetProps" && plan.entrances.Any(b => Overlaps(bounds, b, .25f))) return false;
            var offset = rotation * e.boundsCenter;
            var position = center - new Vector3(offset.x, 0, offset.z);
            position.y = zone.position.y - (e.boundsCenter.y - e.boundsSize.y / 2) + e.groundOffset;
            bounds.center = new Vector3(center.x, zone.position.y + e.boundsSize.y / 2 + e.groundOffset, center.z);
            plan.placements.Add(new HumanThingsPlacement { asset = e, zoneId = zone.id, group = group, position = position, rotation = rotation, footprint = bounds });
            return true;
        }
        public static void Surface(HumanThingsBlockPlan plan, HumanThingsAssetEntry e, string zone, Vector3 top, Vector2 size, Quaternion rotation)
        {
            if (!e.canRotate90) rotation = Quaternion.identity;
            bool turn = Mathf.Abs(Mathf.DeltaAngle(rotation.eulerAngles.y, 90)) < 1;
            var scale = new Vector3((turn ? size.y : size.x) / e.boundsSize.x, 1, (turn ? size.x : size.y) / e.boundsSize.z);
            var offset = rotation * Vector3.Scale(e.boundsCenter, scale);
            var position = top - new Vector3(offset.x, e.boundsCenter.y + e.boundsSize.y / 2, offset.z);
            plan.placements.Add(new HumanThingsPlacement { asset = e, group = "Roads", zoneId = zone, position = position, rotation = rotation, scale = scale,
                footprint = new Bounds(top, new Vector3(size.x, e.boundsSize.y, size.y)), surface = true });
        }

        public static HumanThingsBlockPlan Solve(CompiledCityLayout compiled, HumanThingsAssetDatabase db, IEnumerable<Bounds> reserved = null)
        {
            if (compiled == null || db == null) throw new ArgumentNullException();
            var result = new HumanThingsBlockPlan(); result.zones.AddRange(compiled.zones); result.warnings.AddRange(compiled.warnings);
            if (reserved != null) result.reserved.AddRange(reserved);
            var rng = new System.Random(compiled.seed);
            foreach (var request in compiled.requests.OrderBy(r => r.priority))
            {
                var candidates = CityLayoutAssets.Resolve(db, request.query);
                if (candidates.Count == 0) { result.warnings.Add((request.required ? "REQUIRED " : "") + request.id + ": missing assets; skipped."); continue; }
                int placed = 0;
                if (request.surface)
                {
                    foreach (var zone in request.zones)
                    {
                        bool alongZ = zone.size.y >= zone.size.x; float length = Mathf.Max(zone.size.x, zone.size.y);
                        int tiles = request.junction ? 1 : Mathf.Max(1, Mathf.CeilToInt(length / 12));
                        for (int i = 0; i < tiles; i++)
                        {
                            var size = alongZ ? new Vector2(zone.size.x, length / tiles) : new Vector2(length / tiles, zone.size.y);
                            var position = zone.position + (alongZ ? Vector3.forward : Vector3.right) * (-length / 2 + (i + .5f) * length / tiles);
                            Surface(result, candidates[0], zone.id, position, size, alongZ ? Quaternion.identity : Quaternion.Euler(0, 90, 0));
                            result.placements[result.placements.Count - 1].requestId = request.id;
                        }
                    }
                    continue;
                }
                var zones = request.zones;
                if (zones.Count == 0) { result.warnings.Add(request.id + ": no target zones; skipped."); continue; }
                for (int attempt = 0; attempt < Math.Min(1200, Math.Max(100, request.count * 40)) && placed < request.count; attempt++)
                {
                    var zone = zones[attempt % zones.Count];
                    var ordered = Shuffled(candidates, rng).OrderBy(e => request.query.preferredFloors > 0 ? Mathf.Abs(e.boundsSize.y / 3 - request.query.preferredFloors) : 0)
                        .ThenBy(e => request.query.preferCorner && !e.cornerCompatible ? 1 : 0);
                    foreach (var e in ordered)
                    {
                        bool vehicle = request.group == "Vehicles", building = request.query.category == AssetCategory.Building;
                        var direction = zone.roadDirection;
                        if (vehicle && request.vehicleMode == VehiclePlacementMode.Random && rng.Next(2) == 0) direction = -direction;
                        var rotation = Rotation(e, direction, rng, vehicle); if (!rotation.HasValue) continue;
                        var pos = zone.position; var size = RotatedSize(e, rotation.Value);
                        bool alongZ = zone.size.y >= zone.size.x; var along = alongZ ? Vector3.forward : Vector3.right;
                        if (building)
                        {
                            if (direction.x != 0) pos.x += direction.x * (zone.size.x / 2 - size.x / 2 - .5f);
                            else pos.z += direction.z * (zone.size.y / 2 - size.z / 2 - .5f);
                        }
                        else
                        {
                            float length = Mathf.Max(zone.size.x, zone.size.y);
                            if (request.spacing > 0)
                            {
                                int slots = Mathf.Max(1, Mathf.FloorToInt((length - 2) / request.spacing));
                                int slot = (attempt / zones.Count) % slots;
                                pos += along * ((slot + .5f) * (length / slots) - length / 2);
                            }
                            else pos += along * (float)(rng.NextDouble() - .5) * Mathf.Max(0, length - 4);
                            if (vehicle)
                            {
                                float width = Mathf.Min(zone.size.x, zone.size.y);
                                float lateral = request.vehicleMode == VehiclePlacementMode.Parked ? width / 2 - (alongZ ? size.x : size.z) / 2 - .7f : width * .25f;
                                pos += Vector3.Cross(Vector3.up, direction) * lateral;
                            }
                        }
                        if (!TryPlace(result, e, zone, pos, rotation.Value, request.group, vehicle ? 2 : .5f)) continue;
                        result.placements[result.placements.Count - 1].requestId = request.id;
                        placed++;
                        if (building)
                        {
                            var frontage = pos + direction * (direction.x != 0 ? size.x / 2 : size.z / 2);
                            frontage += direction * (compiled.sidewalkWidth / 2 + .5f);
                            result.entrances.Add(new Bounds(frontage, direction.x != 0 ? new Vector3(compiled.sidewalkWidth, 1, 3) : new Vector3(3, 1, compiled.sidewalkWidth)));
                        }
                        break;
                    }
                }
                if (placed < request.count) result.warnings.Add((request.required ? "REQUIRED " : "") + request.id + ": placed " + placed + "/" + request.count + " (fit/clearance/candidate limit).");
            }
            return result;
        }
    }
}
