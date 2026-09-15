using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EarthRecovery
{
    public static class SpecialPOIValidation
    {
        public static List<string> ValidatePlan(CityWorldPlan world)
        {
            var errors = new List<string>();
            try { SpecialPOIPlacementSolver.ValidateInputs(world.template); }
            catch (Exception e) { errors.Add(e.Message); return errors; }
            var items = world.specialPOIs;
            if (items == null || items.Count != 15 || items.Any(p => p?.definition == null)) { errors.Add("Special POIs: exactly 15 valid placements required."); return errors; }
            if (items.Select(p => p.definition.id).Distinct().Count() != 15 || !SpecialPOIDatabase.RequiredIds.All(id => items.Any(p => p.definition.id == id))) errors.Add("Missing or duplicate Special POI IDs.");
            if (items.Select(p => p.chunk.Id + "/" + p.lotId).Distinct().Count() != 15) errors.Add("Duplicate Special POI lot occupancy.");
            foreach (var p in items)
            {
                if (!world.chunkPlans.TryGetValue(p.chunk.gridPosition, out var chunk)) { errors.Add("Missing owner chunk: " + p.definition.id); continue; }
                var lot = chunk.lots.SingleOrDefault(l => l.id == p.lotId);
                var local = p.bounds; local.center -= p.chunk.worldBounds.center;
                if (lot == null || !lot.occupiedBySpecialPOI || !lot.occupied || lot.occupantId != p.definition.id || !lot.zone.Contains(local, .4f)) errors.Add("Invalid occupied lot: " + p.definition.id);
                if (chunk.roads.Any(r => HumanThingsPlacementSolver.Overlaps(local, CityRect.Bounds(CityRect.Expand(CityRect.FromBounds(r.bounds), chunk.config.sidewalkWidth))))) errors.Add("POI overlaps road/sidewalk: " + p.definition.id);
                if (SpecialPOIPlacementSolver.DistanceFromBoundsXZ(p.bounds, world.bounds.center) + .001f < Mathf.Max(world.baseCampClearRadius, p.definition.minDistanceFromBaseCamp)) errors.Add("POI inside Base Camp clearance: " + p.definition.id);
                if (p.definition.frontFacesRoad && Vector3.Dot((p.rotation * p.definition.localForward).normalized, lot.zone.roadDirection.normalized) < .999f) errors.Add("POI not facing its road: " + p.definition.id);
                if (chunk.placement != null && chunk.placement.placements.Any(v => !v.surface && HumanThingsPlacementSolver.Overlaps(local, v.footprint))) errors.Add("Ordinary object overlaps POI: " + p.definition.id);
            }
            for (int i = 0; i < items.Count; i++) for (int j = i + 1; j < items.Count; j++)
                if (!SpecialPOIPlacementSolver.Compatible(world, items[i], items[j])) errors.Add("POI distance/overlap failure: " + items[i].definition.id + " / " + items[j].definition.id);
            if (items.GroupBy(p => p.chunk.gridPosition).Any(g => g.Count() > world.maxPOIsPerChunk)) errors.Add("POI per-chunk capacity exceeded.");
            foreach (var node in world.nodes)
            {
                if (!world.chunkPlans.TryGetValue(node.gridPosition, out var p)) continue;
                var camp = SpecialPOIPlacementSolver.CampReservation(world); camp.center -= node.worldBounds.center;
                if (p.roads.Any(r => HumanThingsPlacementSolver.Overlaps(CityRect.Bounds(CityRect.Expand(CityRect.FromBounds(r.bounds), p.config.sidewalkWidth)), camp))) errors.Add("Road/sidewalk enters Base Camp reservation: " + node.Id);
                if (p.placement != null && p.placement.placements.Any(v => !v.surface && HumanThingsPlacementSolver.Overlaps(v.footprint, camp))) errors.Add("Ordinary object enters Base Camp reservation: " + node.Id);
            }
            return errors;
        }

        public static List<string> ValidateInstances(GeneratedWorldResult world)
        {
            var errors = new List<string>();
            if (world.specialPOIs.Count != 15 || world.specialPOIs.Any(p => p == null || p.instance == null || p.definition == null || p.definition.prefab == null)) { errors.Add("15 live Special POI instances required."); return errors; }
            if (world.specialPOIs.Select(p => p.id).Distinct().Count() != 15 || world.specialPOIs.Select(p => p.instance).Distinct().Count() != 15) errors.Add("Duplicate POI IDs/instances.");
            if (!SpecialPOIDatabase.RequiredIds.All(id => world.specialPOIs.Any(p => p.id == id))) errors.Add("Missing required POI instance ID.");
            if (world.baseCampInstance == null) errors.Add("Missing Base Camp instance.");
            foreach (var p in world.specialPOIs)
            {
                if (p.id != p.definition.id || p.location?.type != LocationType.SpecialPOI || p.location.root != p.instance || p.location.id != p.id) errors.Add("Incorrect generated POI identity: " + p.id);
                if (SpecialPOIPlacementSolver.DistanceFromBoundsXZ(p.bounds, world.worldBounds.center) < world.baseCampClearRadius - .001f) errors.Add("POI inside camp radius: " + p.id);
                var renderers = p.instance.GetComponentsInChildren<Renderer>().Where(r => r.GetComponentInParent<POIDebugLabel>() == null).ToArray();
                if (renderers.Length == 0) { errors.Add("No exterior renderers: " + p.id); continue; }
                Bounds actual = renderers[0].bounds; foreach (var r in renderers.Skip(1)) actual.Encapsulate(r.bounds);
                if (actual.min.x < p.bounds.min.x - .03f || actual.max.x > p.bounds.max.x + .03f || actual.min.z < p.bounds.min.z - .03f || actual.max.z > p.bounds.max.z + .03f) errors.Add("Prefab exceeds measured footprint: " + p.id);
            }
            for (int i = 0; i < world.specialPOIs.Count; i++) for (int j = i + 1; j < world.specialPOIs.Count; j++)
            {
                var a = world.specialPOIs[i]; var b = world.specialPOIs[j];
                float min = Mathf.Max(world.minPOIDistance, Mathf.Max(a.definition.minDistanceFromOtherPOI, b.definition.minDistanceFromOtherPOI));
                if (HumanThingsPlacementSolver.Overlaps(a.bounds, b.bounds) || SpecialPOIPlacementSolver.DistanceXZ(a.bounds.center, b.bounds.center) < min - .001f) errors.Add("Invalid generated POI separation.");
            }
            return errors;
        }
    }
}
