using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EarthRecovery
{
    public static class CityChunkGenerator
    {
        public static ProceduralCityPlan Generate(CityChunkGenerationRequest request, HumanThingsAssetDatabase database)
        {
            var plan = Prepare(request);
            ProceduralCityGenerator.Complete(plan, database);
            return plan;
        }

        public static ProceduralCityPlan Prepare(CityChunkGenerationRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var size = request.chunkBounds.size;
            var center = request.chunkBounds.center;
            if (!float.IsFinite(center.x) || !float.IsFinite(center.y) || !float.IsFinite(center.z)) throw new ArgumentException("Chunk center must be finite.");
            if (!float.IsFinite(size.x) || !float.IsFinite(size.z) || size.x < 100 || size.z < 100 || size.x > 500 || size.z > 500)
                throw new ArgumentException("Chunk width/depth must be within the existing generator's 100-500m limits.");
            if (!float.IsFinite(request.sidewalkWidth) || request.sidewalkWidth < 2 || request.sidewalkWidth > 12)
                throw new ArgumentException("Invalid shared sidewalk width.");
            if (request.requiredConnectors == null) throw new ArgumentException("Connector list is null.");
            foreach (var port in request.requiredConnectors)
            {
                if (port == null || !Enum.IsDefined(typeof(ConnectorSide), port.side) || !Enum.IsDefined(typeof(ConnectorType), port.type)
                    || !float.IsFinite(port.normalizedPosition) || port.normalizedPosition < .2f || port.normalizedPosition > .8f
                    || !float.IsFinite(port.roadWidth) || port.roadWidth < 4 || port.roadWidth > 30
                    || Mathf.Abs(port.sidewalkWidth - request.sidewalkWidth) > .001f)
                    throw new ArgumentException("Invalid connector or mismatched sidewalk contract.");
            }
            var config = CityGenerationConfigBuilder.Build(request.template, request.seed);
            config.mapWidth = size.x; config.mapDepth = size.z; config.sidewalkWidth = request.sidewalkWidth;
            return ProceduralCityGenerator.Prepare(request.template, config, roads =>
            {
                Constrain(roads, config, request);
                if (request.baseCampReservation.HasValue) ReserveCamp(roads, config, request.baseCampReservation.Value);
            }, 2);
        }

        static void ReserveCamp(List<GeneratedRoad> roads, RuntimeCityGenerationConfig c, Bounds reserved)
        {
            float width = roads.Max(r => r.width);
            var cut = CityRect.Expand(CityRect.FromBounds(reserved), width / 2 + c.sidewalkWidth + 1);
            var input = roads.ToArray(); var output = new List<GeneratedRoad>(); bool changed = false;
            foreach (var r in input)
            {
                bool xAxis = Mathf.Abs(r.start.x - r.end.x) > .001f;
                float cross = xAxis ? r.start.z : r.start.x;
                float lo = xAxis ? Mathf.Min(r.start.x, r.end.x) : Mathf.Min(r.start.z, r.end.z);
                float hi = xAxis ? Mathf.Max(r.start.x, r.end.x) : Mathf.Max(r.start.z, r.end.z);
                float min = xAxis ? cut.xMin : cut.yMin, max = xAxis ? cut.xMax : cut.yMax;
                float acrossMin = xAxis ? cut.yMin : cut.xMin, acrossMax = xAxis ? cut.yMax : cut.xMax;
                if (cross <= acrossMin || cross >= acrossMax || hi <= min || lo >= max) { output.Add(r); continue; }
                changed = true;
                Vector3 Point(float axis) => xAxis ? new Vector3(axis, 0, cross) : new Vector3(cross, 0, axis);
                if (lo < min) Add(Point(lo), Point(min), r.width, r.kind);
                if (hi > max) Add(Point(max), Point(hi), r.width, r.kind);
            }
            if (changed)
            {
                Add(new Vector3(cut.xMin, 0, cut.yMin), new Vector3(cut.xMax, 0, cut.yMin), width, CityRoadKind.MainRoad);
                Add(new Vector3(cut.xMin, 0, cut.yMax), new Vector3(cut.xMax, 0, cut.yMax), width, CityRoadKind.MainRoad);
                Add(new Vector3(cut.xMin, 0, cut.yMin), new Vector3(cut.xMin, 0, cut.yMax), width, CityRoadKind.MainRoad);
                Add(new Vector3(cut.xMax, 0, cut.yMin), new Vector3(cut.xMax, 0, cut.yMax), width, CityRoadKind.MainRoad);
            }
            roads.Clear(); roads.AddRange(output);
            for (int i = 0; i < roads.Count; i++) roads[i].id = "Road_" + i.ToString("D3");
            Reconnect(roads);
            void Add(Vector3 a, Vector3 b, float w, CityRoadKind kind)
            {
                if ((a - b).sqrMagnitude < .0001f) return;
                var r = new GeneratedRoad { start = a, end = b, width = w, kind = kind }; SetBounds(r, true); output.Add(r);
            }
        }

        static void Constrain(List<GeneratedRoad> roads, RuntimeCityGenerationConfig config, CityChunkGenerationRequest request)
        {
            // Leave an edge band for paired ports. Original topology and seed variation are retained inside it.
            float inset = Mathf.Max(config.roadWidth, request.requiredConnectors.Select(c => c.roadWidth).DefaultIfEmpty(0).Max()) / 2 + config.sidewalkWidth + 3;
            float sx = (config.mapWidth - inset * 2) / config.mapWidth;
            float sz = (config.mapDepth - inset * 2) / config.mapDepth;
            foreach (var road in roads)
            {
                road.start = Vector3.Scale(road.start, new Vector3(sx, 1, sz));
                road.end = Vector3.Scale(road.end, new Vector3(sx, 1, sz));
                SetBounds(road, false);
            }
            Vector3 hub = (roads[0].start + roads[0].end) / 2;
            foreach (var port in request.requiredConnectors)
            {
                var edge = port.LocalPoint(request.chunkBounds);
                bool horizontal = port.side == ConnectorSide.East || port.side == ConnectorSide.West;
                var bend = horizontal ? new Vector3(hub.x, 0, edge.z) : new Vector3(edge.x, 0, hub.z);
                Add(edge, bend, port); Add(bend, hub, port);
            }
            var map = new Rect(-config.mapWidth / 2, -config.mapDepth / 2, config.mapWidth, config.mapDepth);
            foreach (var road in roads) road.bounds = CityRect.Bounds(CityRect.Clip(CityRect.FromBounds(road.bounds), map));
            Reconnect(roads);

            void Add(Vector3 a, Vector3 b, ChunkConnector port)
            {
                if ((a - b).sqrMagnitude < .0001f) return;
                var road = new GeneratedRoad { id = "Port_" + roads.Count.ToString("D3"), kind = (CityRoadKind)port.type,
                    start = a, end = b, width = port.roadWidth };
                SetBounds(road, true); roads.Add(road);
            }
        }

        static void SetBounds(GeneratedRoad road, bool cap)
        {
            var size = new Vector3(Mathf.Abs(road.start.x - road.end.x), .1f, Mathf.Abs(road.start.z - road.end.z));
            if (size.x < .001f) { size.x = road.width; if (cap) size.z += road.width; }
            else { size.z = road.width; if (cap) size.x += road.width; }
            road.bounds = new Bounds((road.start + road.end) / 2, size);
        }

        public static void Reconnect(List<GeneratedRoad> roads)
        {
            foreach (var road in roads) road.connections.Clear();
            for (int i = 0; i < roads.Count; i++) for (int j = i + 1; j < roads.Count; j++)
                if (HumanThingsPlacementSolver.Overlaps(roads[i].bounds, roads[j].bounds, .01f))
                { roads[i].connections.Add(roads[j].id); roads[j].connections.Add(roads[i].id); }
        }
    }
}
