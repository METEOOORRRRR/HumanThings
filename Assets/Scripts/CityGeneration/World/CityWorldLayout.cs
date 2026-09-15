using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EarthRecovery
{
    public static class CityWorldLayout
    {
        public static int DeriveChunkSeed(int seed, int x, int z)
        {
            unchecked
            {
                uint h = (uint)seed ^ 0x9e3779b9u;
                h = (h ^ (uint)x) * 0x85ebca6bu; h = (h ^ (uint)z) * 0xc2b2ae35u;
                h ^= h >> 16; h *= 0x7feb352du; h ^= h >> 15; h *= 0x846ca68bu;
                return (int)(h ^ (h >> 16));
            }
        }

        public static CityWorldPlan Generate(CityWorldTemplate template, int seed)
        {
            ValidateTemplate(template);
            var rng = new System.Random(seed);
            var plan = new CityWorldPlan { template = template, seed = seed,
                countX = template.chunkCountXRange.Sample(rng), countZ = template.chunkCountZRange.Sample(rng) };
            float width = template.worldWidthRange.Sample(rng), depth = template.worldDepthRange.Sample(rng);
            float[] widths = Partition(width, plan.countX, template.columnVariation, rng);
            float[] depths = Partition(depth, plan.countZ, template.columnVariation, rng);
            if (template.baseCamp != null)
            {
                float radius = template.baseCamp.ClearRadius(Mathf.Min(widths.Min(), depths.Min()));
                float maxRoadWidth = Mathf.Max(template.mainRoadWidth, template.chunkTemplates.Max(c => Mathf.Clamp(c.template.roads.width.max, 6, 30)));
                float margin = radius + maxRoadWidth + template.sidewalkWidth * 2 + 4;
                KeepCenterInsideCell(widths, width, margin);
                KeepCenterInsideCell(depths, depth, margin);
            }
            plan.bounds = new Bounds(Vector3.zero, new Vector3(width, 0, depth));
            float zStart = -depth / 2;
            for (int z = 0; z < plan.countZ; z++)
            {
                float xStart = -width / 2;
                for (int x = 0; x < plan.countX; x++)
                {
                    plan.nodes.Add(new CityChunkNode { gridPosition = new Vector2Int(x, z), chunkSeed = DeriveChunkSeed(seed, x, z),
                        worldBounds = new Bounds(new Vector3(xStart + widths[x] / 2, 0, zStart + depths[z] / 2), new Vector3(widths[x], 0, depths[z])) });
                    xStart += widths[x];
                }
                zStart += depths[z];
            }
            var byGrid = plan.nodes.ToDictionary(n => n.gridPosition);
            bool alongX = rng.Next(2) == 0;
            int longitudinal = alongX ? plan.countX : plan.countZ, transverse = alongX ? plan.countZ : plan.countX;
            Vector2Int Point(int a, int b) => alongX ? new Vector2Int(a, b) : new Vector2Int(b, a);
            int lane = rng.Next(transverse);
            plan.mainRoute.Add(Point(0, lane));
            for (int step = 0; step < longitudinal; step++)
            {
                if (step > 0) plan.mainRoute.Add(Point(step, lane));
                if (step < longitudinal - 1 && rng.NextDouble() < .5)
                {
                    int next = Mathf.Clamp(lane + (rng.Next(2) == 0 ? -1 : 1), 0, transverse - 1);
                    if (next != lane) { lane = next; plan.mainRoute.Add(Point(step, lane)); }
                }
            }
            plan.startChunk = plan.mainRoute[0];
            for (int i = 1; i < plan.mainRoute.Count; i++) Pair(plan.mainRoute[i - 1], plan.mainRoute[i], ConnectorType.MainRoad, true);
            External(plan.mainRoute[0], alongX ? ConnectorSide.West : ConnectorSide.South, "WorldEntry");
            External(plan.mainRoute.Last(), alongX ? ConnectorSide.East : ConnectorSide.North, "WorldExit");

            // Grow a spanning graph from the arterial, then add optional loops.
            var reached = new HashSet<Vector2Int>(plan.mainRoute);
            while (reached.Count < plan.nodes.Count)
            {
                var frontier = plan.nodes.Where(n => reached.Contains(n.gridPosition))
                    .SelectMany(n => Neighbors(n.gridPosition, plan).Where(p => !reached.Contains(p)).Select(p => (a: n.gridPosition, b: p))).ToList();
                var edge = frontier[rng.Next(frontier.Count)]; Pair(edge.a, edge.b, ConnectorType.SideRoad, false); reached.Add(edge.b);
            }
            foreach (var node in plan.nodes)
                foreach (var neighbor in Neighbors(node.gridPosition, plan).Where(p => p.x > node.gridPosition.x || p.y > node.gridPosition.y))
                    if (!HasPair(node.gridPosition, neighbor) && rng.NextDouble() < template.extraConnectionChance)
                        Pair(node.gridPosition, neighbor, ConnectorType.SideRoad, false);

            AssignDistricts(plan, rng);
            return plan;

            bool HasPair(Vector2Int a, Vector2Int b) => plan.connections.Any(e => e.a == a && e.b == b || e.a == b && e.b == a);
            void Pair(Vector2Int a, Vector2Int b, ConnectorType type, bool main)
            {
                if (HasPair(a, b)) return;
                ConnectorSide side = b.x > a.x ? ConnectorSide.East : b.x < a.x ? ConnectorSide.West : b.y > a.y ? ConnectorSide.North : ConnectorSide.South;
                float position = Mathf.Lerp(.35f, .65f, (float)rng.NextDouble());
                string id = "Connection_" + plan.connections.Count.ToString("D3");
                float roadWidth = type == ConnectorType.MainRoad ? template.mainRoadWidth : template.sideRoadWidth;
                var ca = new ChunkConnector { connectionId = id, side = side, normalizedPosition = position, type = type,
                    roadWidth = roadWidth, sidewalkWidth = template.sidewalkWidth, neighbor = b };
                var cb = new ChunkConnector { connectionId = id, side = Opposite(side), normalizedPosition = position, type = type,
                    roadWidth = roadWidth, sidewalkWidth = template.sidewalkWidth, neighbor = a };
                byGrid[a].connectors.Add(ca); byGrid[b].connectors.Add(cb);
                plan.connections.Add(new GeneratedRoadConnection { id = id, a = a, b = b, type = type,
                    worldPoint = ca.WorldPoint(byGrid[a].worldBounds), roadWidth = roadWidth, sidewalkWidth = template.sidewalkWidth, globalMainRoute = main });
            }
            void External(Vector2Int point, ConnectorSide side, string id)
            {
                byGrid[point].connectors.Add(new ChunkConnector { connectionId = id, external = true, side = side,
                    normalizedPosition = Mathf.Lerp(.35f, .65f, (float)rng.NextDouble()), type = ConnectorType.MainRoad,
                    roadWidth = template.mainRoadWidth, sidewalkWidth = template.sidewalkWidth });
            }
        }

        static float[] Partition(float total, int count, float variation, System.Random rng)
        {
            var weights = Enumerable.Range(0, count).Select(_ => Mathf.Lerp(1 - variation, 1 + variation, (float)rng.NextDouble())).ToArray();
            float sum = weights.Sum(); var sizes = weights.Select(w => total * w / sum).ToArray();
            if (sizes.Any(s => s < 100 || s > 500)) throw new ArgumentException("World dimensions/count produce chunks outside 100-500m. Adjust world size or chunk counts.");
            return sizes;
        }

        static void KeepCenterInsideCell(float[] sizes, float total, float margin)
        {
            float edge = -total / 2;
            for (int i = 0; i < sizes.Length - 1; i++)
            {
                edge += sizes[i];
                if (Mathf.Abs(edge) >= margin) continue;
                float target = edge < 0 ? -margin : margin, move = target - edge;
                sizes[i] += move; sizes[i + 1] -= move; edge = target;
            }
            if (sizes.Any(s => s < 100 || s > 500)) throw new ArgumentException("World cannot reserve a central camp without invalid chunk sizes. Increase world dimensions.");
        }

        static void AssignDistricts(CityWorldPlan plan, System.Random rng)
        {
            var choices = plan.template.chunkTemplates.Where(c => c != null && c.template != null && c.weight > 0).ToList();
            var assigned = new Dictionary<Vector2Int, CityChunkNode>();
            foreach (var node in plan.nodes)
            {
                var neighbors = Neighbors(node.gridPosition, plan).Where(p => assigned.ContainsKey(p)).Select(p => assigned[p]).ToList();
                bool Run(CityDistrictKind kind, Vector2Int delta)
                {
                    var p = node.gridPosition; int run = 1;
                    while (assigned.TryGetValue(p += delta, out var previous) && previous.district == kind) run++;
                    return run > plan.template.maxSameDistrictRun;
                }
                var allowed = choices.Where(c => !Run(c.district, Vector2Int.left) && !Run(c.district, Vector2Int.down)).ToList();
                if (allowed.Count == 0) throw new InvalidOperationException("District run limit cannot be satisfied by available template types.");
                double Score(CityChunkTemplateChoice choice)
                {
                    double score = choice.weight;
                    foreach (var neighbor in neighbors)
                    {
                        if (neighbor.district == choice.district) score *= .4;
                        foreach (var rule in plan.template.adjacency)
                            if (rule.a == choice.district && rule.b == neighbor.district || rule.b == choice.district && rule.a == neighbor.district) score *= rule.multiplier;
                    }
                    if (plan.mainRoute.Contains(node.gridPosition) && (choice.district == CityDistrictKind.Downtown || choice.district == CityDistrictKind.Transit)) score *= 1.8;
                    return score;
                }
                var scores = allowed.Select(Score).ToArray(); double sum = scores.Sum();
                if (sum <= 0 || !double.IsFinite(sum)) throw new InvalidOperationException("Adjacency weights leave no valid district choice.");
                double pick = rng.NextDouble() * sum; int selected = scores.Length - 1;
                for (int i = 0; i < scores.Length; i++) if ((pick -= scores[i]) < 0) { selected = i; break; }
                node.chunkTemplate = allowed[selected].template; node.district = allowed[selected].district; assigned.Add(node.gridPosition, node);
            }
        }

        public static IEnumerable<Vector2Int> Neighbors(Vector2Int p, CityWorldPlan plan)
        {
            foreach (var delta in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.down, Vector2Int.up })
            { var n = p + delta; if (n.x >= 0 && n.y >= 0 && n.x < plan.countX && n.y < plan.countZ) yield return n; }
        }
        public static ConnectorSide Opposite(ConnectorSide side) => side switch
        { ConnectorSide.North => ConnectorSide.South, ConnectorSide.South => ConnectorSide.North, ConnectorSide.East => ConnectorSide.West, _ => ConnectorSide.East };

        static void ValidateTemplate(CityWorldTemplate t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (t.chunkCountXRange.min < 2 || t.chunkCountZRange.min < 2 || t.chunkCountXRange.max > 8 || t.chunkCountZRange.max > 8
                || t.chunkCountXRange.min > t.chunkCountXRange.max || t.chunkCountZRange.min > t.chunkCountZRange.max)
                throw new ArgumentException("World grid ranges must be 2-8 inclusive and ordered.");
            if (t.chunkTemplates == null || t.chunkTemplates.Any(c => c == null || c.template == null || !float.IsFinite(c.weight) || c.weight < 0
                || !Enum.IsDefined(typeof(CityDistrictKind), c.district)) || t.chunkTemplates.Where(c => c.weight > 0).Select(c => c.district).Distinct().Count() < 2)
                throw new ArgumentException("World needs at least two weighted district types and valid chunk template references.");
            if (t.adjacency == null || t.adjacency.Any(a => a == null || !float.IsFinite(a.multiplier) || a.multiplier < 0 || a.multiplier > 10))
                throw new ArgumentException("Invalid adjacency multiplier (0-10).");
            if (!float.IsFinite(t.mainRoadWidth) || t.mainRoadWidth < 6 || t.mainRoadWidth > 30
                || !float.IsFinite(t.sideRoadWidth) || t.sideRoadWidth < 4 || t.sideRoadWidth > t.mainRoadWidth
                || !float.IsFinite(t.sidewalkWidth) || t.sidewalkWidth < 2 || t.sidewalkWidth > 12
                || !float.IsFinite(t.columnVariation) || t.columnVariation < 0 || t.columnVariation > .3f
                || !float.IsFinite(t.extraConnectionChance) || t.extraConnectionChance < 0 || t.extraConnectionChance > 1 || t.maxSameDistrictRun < 1)
                throw new ArgumentException("Invalid world road, variation or district rule.");
        }
    }
}
