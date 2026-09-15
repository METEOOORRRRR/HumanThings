using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EarthRecovery
{
    [CreateAssetMenu(menuName = "HumanThings/Runtime City Settings")]
    public sealed class RuntimeCitySettings : ScriptableObject
    {
        public CityWorldTemplate template;
        public HumanThingsAssetDatabase database;
        public CityWorldPlan Generate(int seed)
        {
            if (template == null || database == null) throw new InvalidOperationException("Runtime city references are missing.");
            return CityWorldGenerator.Generate(template, seed, database);
        }
        public static RuntimeCitySettings Load() => Resources.Load<RuntimeCitySettings>("RuntimeCitySettings")
            ?? throw new InvalidOperationException("RuntimeCitySettings resource is missing.");
    }

    public static class RuntimeCityLayout
    {
        public static void Apply(Expedition game, CityWorldPlan plan)
        {
            var state = game.State;
            if (plan.seed != state.seed || plan.specialPOIs.Count != game.Content.locations.Length)
                throw new InvalidOperationException("Runtime city does not match the expedition.");
            foreach (var site in state.sites)
            {
                var poi = plan.specialPOIs.Single(p => p.definition.id == game.Content.locations[site.id].id);
                site.position = poi.position;
                site.quarterTurn = Mathf.RoundToInt(poi.rotation.eulerAngles.y / 90) % 4;
            }
            state.cityWorld = true; state.mapSize = new Vector2(plan.bounds.size.x, plan.bounds.size.z);
            // Seeded road sockets avoid the occupied building lots in the solved city plan.
            var random = new System.Random(state.seed ^ 51971);
            var sockets = new List<Vector3>();
            foreach (var node in plan.nodes)
                foreach (var road in plan.chunkPlans[node.gridPosition].roads)
                {
                    int steps = Mathf.Max(1, Mathf.FloorToInt(Vector3.Distance(road.start, road.end) / 5));
                    for (int i = 1; i < steps; i++)
                    {
                        var p = node.worldBounds.center + Vector3.Lerp(road.start, road.end, (float)i / steps); p.y = 0;
                        var local = p - node.worldBounds.center;
                        bool occupied = plan.chunkPlans[node.gridPosition].placement.placements.Where(o => !o.surface).Any(o =>
                            local.x >= o.footprint.min.x - 2 && local.x <= o.footprint.max.x + 2
                            && local.z >= o.footprint.min.z - 2 && local.z <= o.footprint.max.z + 2);
                        if (p.magnitude > 18 && !WorldGeometry.Safe(state, p) && !occupied) sockets.Add(p);
                    }
                }
            sockets = sockets.Distinct().OrderBy(_ => random.Next()).ToList();
            var used = new List<Vector3>();
            Vector3 Take(float spacing)
            {
                int index = sockets.FindIndex(p => used.All(q => Vector3.Distance(p, q) >= spacing));
                if (index < 0) throw new InvalidOperationException("Not enough city road spawn sockets.");
                var p = sockets[index]; sockets.RemoveAt(index); used.Add(p); return p;
            }
            foreach (var station in state.stations)
            {
                station.position = Take(game.Rules.stationMinDistance);
                station.zone = game.Content.locations[state.sites.OrderBy(s => Vector3.Distance(s.position, station.position)).First().id].zone;
            }
            foreach (var item in state.loot) item.position = Take(2.5f);
            foreach (var monster in state.monsters) monster.position = monster.home = monster.destination = Take(3);
        }
    }
}
