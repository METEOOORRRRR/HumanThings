using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EarthRecovery
{
    [Serializable] public sealed class CityWorldSeedReport
    {
        public int seed, requestedChunks, generatedChunks, connectorPairs, validConnectorPairs, reachedChunks;
        public int roadFailures, emptyChunks, overlapPairs, boundaryFailures, requestedBuildings, placedBuildings, locations;
        public bool passed;
        public int specialPOIs, poiAttempts;
        public List<string> errors = new(), warnings = new();
    }
    [Serializable] public sealed class CityWorldValidationReport
    {
        public List<CityWorldSeedReport> seeds = new();
        public bool passed;
    }

    public static class CityWorldValidation
    {
        public static CityWorldValidationReport Run(CityWorldTemplate template, HumanThingsAssetDatabase database, int count = 10,
            Func<float, string, bool> cancel = null)
        {
            if (count < 10 || count > 100) throw new ArgumentOutOfRangeException(nameof(count));
            var result = new CityWorldValidationReport();
            for (int i = 0; i < count; i++)
            {
                int seed = i == 0 ? 1234 : i == 1 ? 5678 : 1009 + i * 7919;
                if (cancel?.Invoke((float)i / count, "World Seed " + seed) == true) throw new OperationCanceledException();
                try { result.seeds.Add(Validate(CityWorldGenerator.Generate(template, seed, database))); }
                catch (Exception e) { result.seeds.Add(new CityWorldSeedReport { seed = seed, errors = new() { e.ToString() } }); }
            }
            result.passed = result.seeds.All(s => s.passed); return result;
        }

        public static CityWorldSeedReport Validate(CityWorldPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var r = new CityWorldSeedReport { seed = plan.seed, requestedChunks = plan.nodes.Count, generatedChunks = plan.chunkPlans.Count,
                connectorPairs = plan.connections.Count };
            r.specialPOIs = plan.specialPOIs.Count; r.poiAttempts = plan.poiAttempts;
            r.errors.AddRange(SpecialPOIValidation.ValidatePlan(plan));
            var nodes = plan.nodes.ToDictionary(n => n.gridPosition);
            if (plan.mainRoute.Count < 2 || plan.mainRoute.Distinct().Count() != plan.mainRoute.Count || plan.mainRoute.Any(p => !nodes.ContainsKey(p)))
                r.errors.Add("Missing or invalid global main route.");
            for (int i = 1; i < plan.mainRoute.Count; i++)
            {
                var a = plan.mainRoute[i - 1]; var b = plan.mainRoute[i];
                if (!plan.connections.Any(e => e.globalMainRoute && e.type == ConnectorType.MainRoad && (e.a == a && e.b == b || e.a == b && e.b == a)))
                    r.errors.Add("Global main route has a missing arterial edge.");
            }
            var external = plan.nodes.SelectMany(n => n.connectors.Where(c => c.external).Select(c => (node: n, port: c))).ToArray();
            if (external.Length != 2 || external.Any(e => e.port.type != ConnectorType.MainRoad)) r.errors.Add("World needs two main-road boundary ports.");
            foreach (var e in external)
            {
                var p = e.port.WorldPoint(e.node.worldBounds);
                float delta = e.port.side switch { ConnectorSide.East => p.x - plan.bounds.max.x, ConnectorSide.West => p.x - plan.bounds.min.x,
                    ConnectorSide.North => p.z - plan.bounds.max.z, _ => p.z - plan.bounds.min.z };
                if (Mathf.Abs(delta) > .002f) r.errors.Add("External port is not on the world boundary.");
            }
            var reached = new HashSet<Vector2Int>(); var queue = new Queue<Vector2Int>(); queue.Enqueue(plan.startChunk);
            while (queue.Count > 0)
            {
                var p = queue.Dequeue(); if (!nodes.ContainsKey(p) || !reached.Add(p)) continue;
                foreach (var edge in plan.connections.Where(e => e.a == p || e.b == p)) queue.Enqueue(edge.a == p ? edge.b : edge.a);
            }
            r.reachedChunks = reached.Count;
            if (reached.Count != nodes.Count) r.errors.Add("Disconnected chunk graph.");
            foreach (var edge in plan.connections)
            {
                if (!nodes.TryGetValue(edge.a, out var a) || !nodes.TryGetValue(edge.b, out var b)) { r.errors.Add("Missing connector node."); continue; }
                var ca = a.connectors.Where(c => c.connectionId == edge.id).ToArray();
                var cb = b.connectors.Where(c => c.connectionId == edge.id).ToArray();
                bool valid = ca.Length == 1 && cb.Length == 1;
                if (valid)
                {
                    var x = ca[0]; var y = cb[0];
                    valid = !x.external && !y.external && x.neighbor == edge.b && y.neighbor == edge.a
                        && Mathf.Abs(edge.a.x - edge.b.x) + Mathf.Abs(edge.a.y - edge.b.y) == 1
                        && x.type == y.type && x.type == edge.type && CityWorldLayout.Opposite(x.side) == y.side
                        && Mathf.Abs(x.roadWidth - y.roadWidth) < .001f && Mathf.Abs(x.sidewalkWidth - y.sidewalkWidth) < .001f
                        && Mathf.Abs(x.normalizedPosition - y.normalizedPosition) < .001f
                        && Vector3.Distance(x.WorldPoint(a.worldBounds), y.WorldPoint(b.worldBounds)) < .002f
                        && Vector3.Distance(edge.worldPoint, x.WorldPoint(a.worldBounds)) < .002f;
                }
                if (valid) r.validConnectorPairs++; else r.errors.Add("Mismatched connector pair " + edge.id);
            }
            var worldSolids = new List<(Vector2Int chunk, Bounds bounds)>();
            foreach (var node in plan.nodes)
            {
                if (!plan.chunkPlans.TryGetValue(node.gridPosition, out var p)) { r.errors.Add("Missing chunk " + node.Id); continue; }
                var solids = p.placement.placements.Where(v => !v.surface).ToArray();
                int buildings = solids.Count(v => v.asset.category == AssetCategory.Building);
                if (buildings + p.specialPOIs.Count == 0 || p.roads.Count == 0) { r.emptyChunks++; r.errors.Add("Empty chunk " + node.Id); }
                r.placedBuildings += buildings;
                r.requestedBuildings += p.compiled.requests.Where(q => !q.surface && q.query.category == AssetCategory.Building).Sum(q => q.count);
                r.locations += p.roads.Count + p.specialPOIs.Count + solids.Count(v => v.group == "Buildings" || v.group == "POIs");
                var connected = ReachRoads(p.roads, false);
                if (connected.Count != p.roads.Count) { r.roadFailures++; r.errors.Add("Disconnected road network " + node.Id); }
                var main = ReachRoads(p.roads, true);
                foreach (var port in node.connectors)
                {
                    if (!port.external && !plan.connections.Any(e => e.id == port.connectionId)) r.errors.Add("Orphan connector " + node.Id);
                    var point = port.LocalPoint(node.worldBounds);
                    var onRoads = p.roads.Where(road => ContainsXZ(road.bounds, point) && road.kind == (CityRoadKind)port.type).ToArray();
                    bool portOK = onRoads.Any(road => connected.Contains(road.id))
                        && (port.type != ConnectorType.MainRoad || onRoads.Any(road => main.Contains(road.id))) && SurfaceCoversPort(p, point, port);
                    if (!portOK) { r.roadFailures++; r.errors.Add("Road/sidewalk aperture or route failure " + node.Id + "/" + port.connectionId); }
                }
                var map = new Rect(-p.config.mapWidth / 2, -p.config.mapDepth / 2, p.config.mapWidth, p.config.mapDepth);
                foreach (var solid in solids)
                {
                    var b = solid.footprint;
                    if (b.min.x < map.xMin - .01f || b.max.x > map.xMax + .01f || b.min.z < map.yMin - .01f || b.max.z > map.yMax + .01f)
                    { r.boundaryFailures++; r.errors.Add("Object exceeds chunk boundary " + node.Id); }
                    b.center += node.worldBounds.center; worldSolids.Add((node.gridPosition, b));
                }
                r.warnings.AddRange(p.placement.warnings.Select(w => node.Id + ": " + w));
                r.errors.AddRange(p.placement.warnings.Where(w => w.StartsWith("INVALID:", StringComparison.Ordinal)).Select(w => node.Id + ": " + w));
            }
            for (int i = 0; i < worldSolids.Count; i++) for (int j = i + 1; j < worldSolids.Count; j++)
                if (HumanThingsPlacementSolver.Overlaps(worldSolids[i].bounds, worldSolids[j].bounds)) r.overlapPairs++;
            if (r.overlapPairs > 0) r.errors.Add("Overlapping solid footprints: " + r.overlapPairs);
            if (r.generatedChunks != r.requestedChunks) r.errors.Add("Not all chunks generated.");
            if (r.validConnectorPairs != r.connectorPairs) r.errors.Add("Not all connectors paired.");
            r.passed = r.errors.Count == 0; return r;
        }

        static HashSet<string> ReachRoads(List<GeneratedRoad> roads, bool mainOnly)
        {
            var byId = roads.Where(r => !mainOnly || r.kind == CityRoadKind.MainRoad).ToDictionary(r => r.id);
            var reached = new HashSet<string>(); if (byId.Count == 0) return reached;
            var queue = new Queue<string>(); queue.Enqueue(byId.Keys.First());
            while (queue.Count > 0)
            {
                string id = queue.Dequeue(); if (!byId.TryGetValue(id, out var road) || !reached.Add(id)) continue;
                foreach (var next in road.connections) if (byId.ContainsKey(next)) queue.Enqueue(next);
            }
            return reached;
        }
        static bool ContainsXZ(Bounds b, Vector3 p) => p.x >= b.min.x - .002f && p.x <= b.max.x + .002f && p.z >= b.min.z - .002f && p.z <= b.max.z + .002f;
        static bool SurfaceCoversPort(ProceduralCityPlan p, Vector3 point, ChunkConnector port)
        {
            var surfaces = p.placement.placements.Where(v => v.surface).ToArray();
            bool horizontal = port.side == ConnectorSide.East || port.side == ConnectorSide.West;
            Vector3 tangent = horizontal ? Vector3.forward : Vector3.right;
            Vector3 inward = port.side switch { ConnectorSide.East => Vector3.left, ConnectorSide.West => Vector3.right,
                ConnectorSide.North => Vector3.back, _ => Vector3.forward };
            float extent = port.roadWidth / 2 + port.sidewalkWidth;
            // Sample actual solved surface footprints, not only the abstract graph, on both sides of every seam.
            for (float offset = -extent + .1f; offset < extent; offset += .4f)
            {
                var sample = point + inward * .05f + tangent * offset;
                bool walk = Mathf.Abs(offset) > port.roadWidth / 2 + .05f;
                if (!surfaces.Any(v => (v.requestId == "Sidewalks") == walk && ContainsXZ(v.footprint, sample))) return false;
            }
            return true;
        }
    }
}
