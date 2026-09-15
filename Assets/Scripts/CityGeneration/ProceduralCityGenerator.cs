using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EarthRecovery
{
    public sealed class ProceduralCityPlan
    {
        public CityLayoutTemplate template;
        public RuntimeCityGenerationConfig config;
        public List<GeneratedRoad> roads;
        public List<GeneratedLot> lots;
        public CompiledCityLayout compiled;
        public HumanThingsBlockPlan placement;
        public List<SpecialPOIPlacement> specialPOIs = new();
    }
    public static class ProceduralCityGenerator
    {
        public static ProceduralCityPlan Generate(CityLayoutTemplate template, int seed, HumanThingsAssetDatabase database)
        {
            var config = CityGenerationConfigBuilder.Build(template, seed);
            return Generate(template, config, database);
        }

        // Constraints run before lot subdivision and the existing placement solver.
        public static ProceduralCityPlan Generate(CityLayoutTemplate template, RuntimeCityGenerationConfig config,
            HumanThingsAssetDatabase database, Action<List<GeneratedRoad>> constrainRoads = null, float edgeClearance = 0)
        {
            var plan = Prepare(template, config, constrainRoads, edgeClearance);
            Complete(plan, database);
            return plan;
        }

        public static ProceduralCityPlan Prepare(CityLayoutTemplate template, RuntimeCityGenerationConfig config,
            Action<List<GeneratedRoad>> constrainRoads = null, float edgeClearance = 0)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            int seed = config.seed;
            var roads = RoadNetworkGenerator.Generate(config);
            constrainRoads?.Invoke(roads);
            var compiled = new CompiledCityLayout { seed = seed, sidewalkWidth = config.sidewalkWidth };
            compiled.warnings.AddRange(config.warnings);
            var lots = BuildingLotGenerator.Generate(config, roads, compiled.warnings);
            var plan = new ProceduralCityPlan { template = template, config = config, roads = roads, lots = lots, compiled = compiled };
            BuildRequests(plan);
            if (edgeClearance > 0)
            {
                var interior = new Rect(-config.mapWidth / 2 + edgeClearance, -config.mapDepth / 2 + edgeClearance,
                    config.mapWidth - edgeClearance * 2, config.mapDepth - edgeClearance * 2);
                foreach (var request in compiled.requests.Where(r => !r.surface))
                    request.zones = request.zones.Select(z =>
                    {
                        var rect = CityRect.Clip(new Rect(new Vector2(z.position.x, z.position.z) - z.size / 2, z.size), interior);
                        return new HumanThingsPlacementZone { id = z.id, kind = z.kind, position = CityRect.Bounds(rect, z.position.y).center,
                            size = rect.size, roadDirection = z.roadDirection };
                    }).Where(z => z.size.x > .1f && z.size.y > .1f).ToList();
            }
            return plan;
        }

        public static void Complete(ProceduralCityPlan plan, HumanThingsAssetDatabase database, IEnumerable<Bounds> reserved = null)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (CityLayoutAssets.Resolve(database, plan.compiled.requests.First(r => r.surface).query).Count == 0)
                throw new InvalidOperationException("Database needs an enabled road surface with valid placement metadata.");
            var occupied = plan.lots.Where(l => l.occupied).Select(l => l.id).ToHashSet();
            foreach (var request in plan.compiled.requests.Where(r => !r.surface))
                request.zones.RemoveAll(z => occupied.Contains(z.id));
            plan.placement = HumanThingsPlacementSolver.Solve(plan.compiled, database, reserved);
            Validate(plan);
        }

        static void BuildRequests(ProceduralCityPlan p)
        {
            var c = p.config; var compiled = p.compiled;
            var map = new Rect(-c.mapWidth / 2, -c.mapDepth / 2, c.mapWidth, c.mapDepth);
            var coveredRoads = new List<Rect>(); var walks = new List<HumanThingsPlacementZone>();
            HumanThingsPlacementZone Zone(string id, Rect rect, PlacementZoneKind kind, Vector3 direction)
            {
                var zone = new HumanThingsPlacementZone { id = id, kind = kind, position = CityRect.Bounds(rect, kind == PlacementZoneKind.SidewalkZone ? .15f : 0).center,
                    size = rect.size, roadDirection = direction };
                compiled.zones.Add(zone); return zone;
            }
            var roadQuery = new CityAssetQuery { category = AssetCategory.Road, subCategories = new[] { "Straight", "Other", "Intersection" }, surface = PlacementSurface.Ground };
            foreach (var road in p.roads)
            {
                var original = CityRect.Clip(CityRect.FromBounds(road.bounds), map);
                var pieces = new List<Rect> { original };
                foreach (var previous in coveredRoads) pieces = CityRect.Subtract(pieces, previous);
                coveredRoads.Add(original);
                int index = 0;
                compiled.requests.Add(new CityPlacementRequest { id = road.id, group = "Roads", priority = -20, surface = true, query = roadQuery,
                    zones = pieces.Select(r => Zone(road.id + "_Surface_" + index++, r, PlacementZoneKind.RoadZone, road.Direction)).ToList() });
            }
            var coveredWalks = new List<Rect>();
            foreach (var road in p.roads)
            {
                var pieces = new List<Rect> { CityRect.Clip(CityRect.Expand(CityRect.FromBounds(road.bounds), c.sidewalkWidth), map) };
                foreach (var r in coveredRoads) pieces = CityRect.Subtract(pieces, r);
                foreach (var r in coveredWalks) pieces = CityRect.Subtract(pieces, r);
                int i = 0;
                foreach (var r in pieces)
                {
                    coveredWalks.Add(r); var center = CityRect.Bounds(r).center;
                    var direction = road.bounds.ClosestPoint(center) - center;
                    direction = Mathf.Abs(direction.x) > Mathf.Abs(direction.z) ? Vector3.right * Mathf.Sign(direction.x) : Vector3.forward * Mathf.Sign(direction.z);
                    walks.Add(Zone(road.id + "_Walk_" + i++, r, PlacementZoneKind.SidewalkZone, direction));
                }
            }
            compiled.requests.Add(new CityPlacementRequest { id = "Sidewalks", group = "Roads", priority = -10, surface = true, zones = walks,
                query = new CityAssetQuery { category = AssetCategory.Road, subCategories = new[] { "Sidewalk" }, surface = PlacementSurface.Ground } });
            var rng = new System.Random(unchecked(c.seed ^ 0x74ae));
            foreach (var lot in p.lots)
            {
                compiled.zones.Add(lot.zone); lot.usage = BuildingUsageResolver.Resolve(c, rng);
                if (rng.NextDouble() >= c.buildingDensity) continue;
                compiled.requests.Add(new CityPlacementRequest { id = lot.id, group = "Buildings", count = 1, priority = 10, query = CityLayoutAssets.Building(lot.usage, lot.corner),
                    zones = new() { lot.zone }, locationType = lot.usage switch { BuildingUsage.Residential => LocationType.Residential, BuildingUsage.Office => LocationType.Office, BuildingUsage.Public => LocationType.Public, _ => LocationType.Shop } });
            }
            void Poi(string name, string sub, AssetCategory category, LocationType type, int count)
            {
                if (count == 0) return;
                bool transit = category == AssetCategory.Transit;
                compiled.requests.Add(new CityPlacementRequest { id = name, group = "POIs", count = count, required = true, priority = 0, locationType = type,
                    query = new CityAssetQuery { category = category, subCategories = new[] { sub }, surface = transit ? PlacementSurface.Sidewalk : PlacementSurface.Ground },
                    zones = transit ? walks : p.lots.Select(l => l.zone).ToList() });
            }
            Poi("RequiredShops", "Shop", AssetCategory.Building, LocationType.Shop, c.shopCount);
            Poi("RequiredSubways", "SubwayEntrance", AssetCategory.Transit, LocationType.Subway, c.subwayEntranceCount);
            Poi("RequiredBusStops", "BusStop", AssetCategory.Transit, LocationType.Public, c.busStopCount);
            Poi("RequiredPublic", "Public", AssetCategory.Building, LocationType.Public, c.publicBuildingCount);
            compiled.requests.Add(new CityPlacementRequest { id = "ParkedVehicles", group = "Vehicles", priority = 20, count = c.parkedVehicleCount, vehicleMode = VehiclePlacementMode.Parked,
                query = new CityAssetQuery { category = AssetCategory.Vehicle, subCategories = new[] { "Car", "Van", "Truck", "Bus", "Emergency" }, surface = PlacementSurface.Road },
                zones = p.roads.Where(r => r.kind != CityRoadKind.Alley).Select(r => new HumanThingsPlacementZone { id = r.id, kind = PlacementZoneKind.RoadZone,
                    position = r.bounds.center, size = new Vector2(r.bounds.size.x, r.bounds.size.z), roadDirection = r.Direction }).ToList() });
            foreach (string sub in new[] { "Lamp", "Trash", "Sign", "Bench", "TrafficLight" })
                compiled.requests.Add(new CityPlacementRequest { id = sub, group = "StreetProps", priority = 30, count = Mathf.Min(60, Mathf.RoundToInt(walks.Count * c.streetPropDensity * (sub == "Lamp" ? 1 : .35f))),
                    query = new CityAssetQuery { category = AssetCategory.StreetProp, subCategories = new[] { sub }, surface = PlacementSurface.Sidewalk }, zones = walks, spacing = sub == "Lamp" ? 18 : 0 });
        }

        public static void Validate(ProceduralCityPlan p)
        {
            var warnings = p.placement.warnings;
            var reached = new HashSet<string>(); var queue = new Queue<GeneratedRoad>(); queue.Enqueue(p.roads[0]);
            while (queue.Count > 0)
            {
                var road = queue.Dequeue(); if (!reached.Add(road.id)) continue;
                foreach (var next in p.roads.Where(r => road.connections.Contains(r.id))) queue.Enqueue(next);
            }
            if (reached.Count != p.roads.Count) warnings.Add("INVALID: disconnected road network.");
            int intersections = 0;
            for (int i = 0; i < p.roads.Count; i++) for (int j = i + 1; j < p.roads.Count; j++)
                if (Mathf.Abs(Vector3.Dot(p.roads[i].Direction, p.roads[j].Direction)) < .1f && p.roads[i].connections.Contains(p.roads[j].id)) intersections++;
            if (intersections < p.config.intersectionCount) warnings.Add("Intersections: " + intersections + "/" + p.config.intersectionCount + " (space/branch count limited).");
            int alleys = p.roads.Count(r => r.kind == CityRoadKind.Alley);
            if (alleys < p.config.alleyCount) warnings.Add("Alleys: " + alleys + "/" + p.config.alleyCount + " (corridor clearance limited).");
            var solid = p.placement.placements.Where(v => !v.surface).ToList();
            for (int i = 0; i < solid.Count; i++) for (int j = i + 1; j < solid.Count; j++)
                if (HumanThingsPlacementSolver.Overlaps(solid[i].footprint, solid[j].footprint)) warnings.Add("INVALID: object overlap " + solid[i].zoneId + " / " + solid[j].zoneId);
            foreach (var lot in p.lots)
                if (!p.roads.Any(r => r.id == lot.roadId) || lot.zone.roadDirection == Vector3.zero) warnings.Add("INVALID: lot without road access " + lot.id);
            if (p.lots.Count == 0) warnings.Add("No accessible building lots fit this seed.");
            warnings.Add("Road markings use fitted surface tiles; lane markings at junctions are approximate.");
        }
    }
}
