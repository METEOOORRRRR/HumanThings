using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EarthRecovery
{
    public static class SpecialPOIPlacementSolver
    {
        public static void ValidateInputs(CityWorldTemplate t)
        {
            if (t == null || t.specialPOIDatabase == null || t.baseCamp == null || t.baseCamp.prefab == null)
                throw new ArgumentException("World requires a Special POI Database and Base Camp prefab definition.");
            t.specialPOIDatabase.Validate();
            if (!SpecialPOIDatabase.Positive(t.baseCamp.boundsSize) || !SpecialPOIDatabase.Finite(t.baseCamp.boundsCenter)
                || !float.IsFinite(t.baseCamp.footprintClearMultiplier) || t.baseCamp.footprintClearMultiplier < 1
                || !float.IsFinite(t.baseCamp.chunkClearRatio) || t.baseCamp.chunkClearRatio < 0 || t.baseCamp.chunkClearRatio > .2f)
                throw new ArgumentException("Invalid Base Camp bounds or clearance settings.");
            var s = t.specialPOISettings;
            if (s == null || !float.IsFinite(s.footprintDistanceFactor) || s.footprintDistanceFactor < 0
                || !float.IsFinite(s.chunkDistanceRatio) || s.chunkDistanceRatio < 0 || s.chunkDistanceRatio > 1
                || s.maxRetries < 1 || s.maxRetries > 10 || s.searchBudgetPerRetry < 100 || s.searchBudgetPerRetry > 100000)
                throw new ArgumentException("Invalid POI distance/retry settings.");
            if (s.showDebugLocationLabels && s.labelFont == null) throw new ArgumentException("Assign a Korean debug label font or disable Show POI Labels.");
        }

        public static Bounds CampReservation(CityWorldPlan world) => new(world.bounds.center, new Vector3(world.baseCampClearRadius * 2, 1, world.baseCampClearRadius * 2));
        public static void Initialize(CityWorldPlan world)
        {
            ValidateInputs(world.template);
            float size = world.nodes.Min(n => Mathf.Min(n.worldBounds.size.x, n.worldBounds.size.z));
            var camp = world.template.baseCamp;
            world.baseCampClearRadius = camp.ClearRadius(size);
            world.baseCampBounds = new Bounds(world.bounds.center + Vector3.up * camp.boundsSize.y / 2, camp.boundsSize);
            float mean = world.template.specialPOIDatabase.definitions.Average(d => Mathf.Max(d.footprint.x, d.footprint.y));
            world.minPOIDistance = Mathf.Max(mean * world.template.specialPOISettings.footprintDistanceFactor, size * world.template.specialPOISettings.chunkDistanceRatio);
            world.maxPOIsPerChunk = Mathf.CeilToInt(15f / world.nodes.Count) + (world.nodes.Count < 9 ? 1 : 0);
        }

        public static void Place(CityWorldPlan world, Func<bool> cancel = null)
        {
            var defs = world.template.specialPOIDatabase.definitions.OrderByDescending(d => d.footprint.x * d.footprint.y).ThenBy(d => d.id, StringComparer.Ordinal).ToArray();
            var candidates = defs.ToDictionary(d => d.id, d => Candidates(world, d));
            foreach (var d in defs) if (candidates[d.id].Count == 0) throw new InvalidOperationException("World Generation Failed: no valid lot for required POI " + d.id);
            var placed = new List<SpecialPOIPlacement>();
            var used = new HashSet<string>(); var counts = new Dictionary<Vector2Int, int>();
            int attempts = world.template.specialPOISettings.maxRetries;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                if (cancel?.Invoke() == true) throw new OperationCanceledException();
                world.poiAttempts = attempt + 1;
                var rng = new System.Random(CityWorldLayout.DeriveChunkSeed(world.seed, attempt, 0x504f49));
                var orders = defs.ToDictionary(d => d.id, d => candidates[d.id].Select(p => (p, rank: rng.Next())).OrderBy(p => p.rank).Select(p => p.p).ToList());
                placed.Clear(); used.Clear(); counts.Clear(); int budget = world.template.specialPOISettings.searchBudgetPerRetry;
                if (!Search(0)) continue;
                world.specialPOIs = placed.ToList();
                foreach (var p in placed)
                {
                    var plan = world.chunkPlans[p.chunk.gridPosition];
                    var lot = plan.lots.Single(l => l.id == p.lotId);
                    lot.occupied = lot.occupiedBySpecialPOI = true; lot.occupantId = p.definition.id;
                    plan.specialPOIs.Add(p);
                }
                var errors = SpecialPOIValidation.ValidatePlan(world);
                if (errors.Count > 0) throw new InvalidOperationException("Special POI validation failed: " + string.Join("; ", errors));
                return;

                bool Search(int index)
                {
                    if (--budget < 0) return false;
                    if (budget % 128 == 0 && cancel?.Invoke() == true) throw new OperationCanceledException();
                    if (index == defs.Length) return true;
                    var d = defs[index];
                    // Seed breaks ties; least-populated chunks are tried first, with bounded backtracking.
                    foreach (var p in orders[d.id].OrderBy(p => counts.GetValueOrDefault(p.chunk.gridPosition)))
                    {
                        string key = p.chunk.Id + "/" + p.lotId;
                        int n = counts.GetValueOrDefault(p.chunk.gridPosition);
                        if (used.Contains(key) || n >= world.maxPOIsPerChunk || placed.Any(other => !Compatible(world, p, other))) continue;
                        placed.Add(p); used.Add(key); counts[p.chunk.gridPosition] = n + 1;
                        if (Search(index + 1)) return true;
                        placed.RemoveAt(placed.Count - 1); used.Remove(key); counts[p.chunk.gridPosition] = n;
                        if (budget < 0) return false;
                    }
                    return false;
                }
            }
            throw new InvalidOperationException("World Generation Failed: all 15 required Special POIs could not be placed after " + attempts + " bounded retries. Reduce distance multipliers, use larger lots/world or smaller prefabs.");
        }

        public static float DistanceXZ(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        public static float DistanceFromBoundsXZ(Bounds b, Vector3 point) => DistanceXZ(b.ClosestPoint(new Vector3(point.x, b.center.y, point.z)), point);
        public static bool Compatible(CityWorldPlan w, SpecialPOIPlacement a, SpecialPOIPlacement b) =>
            !HumanThingsPlacementSolver.Overlaps(a.bounds, b.bounds, .5f)
            && DistanceXZ(a.bounds.center, b.bounds.center) + .001f >= Mathf.Max(w.minPOIDistance, Mathf.Max(a.definition.minDistanceFromOtherPOI, b.definition.minDistanceFromOtherPOI));

        static List<SpecialPOIPlacement> Candidates(CityWorldPlan w, SpecialPOIDefinition d)
        {
            var result = new List<SpecialPOIPlacement>();
            foreach (var node in w.nodes)
            {
                var plan = w.chunkPlans[node.gridPosition];
                foreach (var lot in plan.lots.Where(l => !l.occupied))
                {
                    var direction = lot.zone.roadDirection;
                    if (direction.sqrMagnitude < .01f) continue;
                    float yaw = d.frontFacesRoad ? Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg - Mathf.Atan2(d.localForward.x, d.localForward.z) * Mathf.Rad2Deg : 0;
                    var rotation = Quaternion.Euler(0, yaw, 0);
                    var x = rotation * new Vector3(d.footprint.x, 0, 0); var z = rotation * new Vector3(0, 0, d.footprint.y);
                    var size = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(z.x), d.boundsSize.y, Mathf.Abs(x.z) + Mathf.Abs(z.z));
                    var center = lot.zone.position;
                    if (direction.x != 0) center.x += direction.x * (lot.zone.size.x / 2 - size.x / 2 - .5f);
                    else center.z += direction.z * (lot.zone.size.y / 2 - size.z / 2 - .5f);
                    // Also try frontage offsets in long lots without changing the fixed appearance.
                    foreach (float fraction in new[] { 0f, -.3f, .3f })
                    {
                        var c = center;
                        if (direction.x != 0) c.z += fraction * Mathf.Max(0, lot.zone.size.y - size.z - 1);
                        else c.x += fraction * Mathf.Max(0, lot.zone.size.x - size.x - 1);
                        var local = new Bounds(c, size);
                        if (!lot.zone.Contains(local, .5f)) continue;
                        var localMap = new Bounds(Vector3.zero, node.worldBounds.size);
                        if (local.min.x < localMap.min.x + 2 || local.max.x > localMap.max.x - 2 || local.min.z < localMap.min.z + 2 || local.max.z > localMap.max.z - 2) continue;
                        if (plan.roads.Any(r => HumanThingsPlacementSolver.Overlaps(local, CityRect.Bounds(CityRect.Expand(CityRect.FromBounds(r.bounds), plan.config.sidewalkWidth))))) continue;
                        local.center += node.worldBounds.center + Vector3.up * size.y / 2;
                        if (DistanceFromBoundsXZ(local, w.bounds.center) < Mathf.Max(w.baseCampClearRadius, d.minDistanceFromBaseCamp)) continue;
                        var offset = rotation * d.boundsCenter;
                        var position = local.center - offset;
                        position.y = node.worldBounds.center.y - d.boundsCenter.y + d.boundsSize.y / 2;
                        result.Add(new SpecialPOIPlacement { definition = d, chunk = node, lotId = lot.id, bounds = local, position = position, rotation = rotation });
                    }
                }
            }
            return result;
        }
    }
}
