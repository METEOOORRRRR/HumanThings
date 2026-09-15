using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EarthRecovery
{
    public static class CityWorldGenerator
    {
        public static CityWorldPlan Generate(CityWorldTemplate template, int seed, HumanThingsAssetDatabase database,
            Func<float, string, bool> cancel = null)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            SpecialPOIPlacementSolver.ValidateInputs(template);
            var plan = CityWorldLayout.Generate(template, seed);
            SpecialPOIPlacementSolver.Initialize(plan);
            foreach (var node in plan.nodes)
            {
                if (cancel?.Invoke((float)plan.chunkPlans.Count / plan.nodes.Count, node.Id) == true) throw new OperationCanceledException();
                var request = Request(node, template.sidewalkWidth);
                var camp = SpecialPOIPlacementSolver.CampReservation(plan);
                if (HumanThingsPlacementSolver.Overlaps(camp, node.worldBounds)) { camp.center -= node.worldBounds.center; request.baseCampReservation = camp; }
                plan.chunkPlans.Add(node.gridPosition, CityChunkGenerator.Prepare(request));
            }
            SpecialPOIPlacementSolver.Place(plan, () => cancel?.Invoke(.8f, "Reserving all 15 Special POIs") == true);
            foreach (var node in plan.nodes)
            {
                if (cancel?.Invoke(.9f, "Populating " + node.Id) == true) throw new OperationCanceledException();
                var reserved = plan.specialPOIs.Select(p => p.bounds).Append(SpecialPOIPlacementSolver.CampReservation(plan))
                    .Select(b => { b.center -= node.worldBounds.center; return b; }).ToArray();
                ProceduralCityGenerator.Complete(plan.chunkPlans[node.gridPosition], database, reserved);
            }
            var errors = SpecialPOIValidation.ValidatePlan(plan);
            if (errors.Count != 0) throw new InvalidOperationException("World Generation Failed: " + string.Join("; ", errors));
            return plan;
        }

        public static CityChunkGenerationRequest Request(CityChunkNode node, float sidewalkWidth) => new()
        {
            template = node.chunkTemplate, seed = node.chunkSeed, chunkBounds = node.worldBounds,
            requiredConnectors = node.connectors, sidewalkWidth = sidewalkWidth
        };

        public static GeneratedWorldResult Build(CityWorldPlan plan, Scene scene, Func<GameObject, Transform, GameObject> instantiate = null)
        {
            if (plan == null || !scene.IsValid() || !scene.isLoaded || plan.nodes.Count != plan.chunkPlans.Count)
                throw new ArgumentException("Complete world plan and loaded scene required.");
            var validation = CityWorldValidation.Validate(plan);
            if (!validation.passed) throw new InvalidOperationException("World validation failed: " + string.Join("; ", validation.errors.Take(8)));
            var root = new GameObject("HumanThings_GeneratedWorld");
            root.SetActive(false); SceneManager.MoveGameObjectToScene(root, scene);
            var result = new GeneratedWorldResult { root = root, worldSeed = plan.seed, worldTemplate = plan.template,
                worldBounds = plan.bounds, globalRoads = plan.connections.ToList(), baseCampBounds = plan.baseCampBounds,
                baseCampClearRadius = plan.baseCampClearRadius, minPOIDistance = plan.minPOIDistance };
            root.AddComponent<GeneratedWorldRoot>().result = result;
            try
            {
                instantiate ??= (prefab, parent) => UnityEngine.Object.Instantiate(prefab, parent, false);
                var camp = plan.template.baseCamp;
                result.baseCampInstance = instantiate(camp.prefab, root.transform);
                result.baseCampInstance.name = "BaseCamp";
                result.baseCampInstance.transform.localPosition = plan.baseCampBounds.center - camp.boundsCenter;
                result.baseCampInstance.transform.localRotation = Quaternion.identity;
                POIDebugLabel.Create(result.baseCampInstance.transform, "BASE CAMP", plan.baseCampBounds, Vector3.back,
                    plan.template.specialPOISettings.labelFont, plan.template.specialPOISettings.showDebugLocationLabels);
                var specialRoot = new GameObject("SpecialPOIs"); specialRoot.transform.SetParent(root.transform, false);
                foreach (var node in plan.nodes) LoadChunk(result, node, plan.chunkPlans[node.gridPosition], scene, instantiate);
                var global = new GameObject("Global"); global.transform.SetParent(root.transform, false);
                var debug = new GameObject("Debug"); debug.transform.SetParent(global.transform, false);
                root.SetActive(true); result.RefreshLocations();
                POIDebugLabel.Refresh(root);
                var poiErrors = SpecialPOIValidation.ValidateInstances(result);
                if (poiErrors.Count > 0) throw new InvalidOperationException(string.Join("; ", poiErrors));
                return result;
            }
            catch { CitySceneBuilder.DestroyRoot(root); foreach (var node in plan.nodes) node.generatedResult = null; throw; }
        }

        public static GeneratedCityChunk LoadChunk(GeneratedWorldResult world, CityChunkNode node, ProceduralCityPlan plan, Scene scene,
            Func<GameObject, Transform, GameObject> instantiate = null)
        {
            if (world?.root == null || node == null || plan == null || world.root.scene != scene)
                throw new ArgumentException("A world root, matching scene and solved chunk are required.");
            if (world.chunks.Any(c => c.node.gridPosition == node.gridPosition)) throw new InvalidOperationException("Chunk already loaded: " + node.Id);
            if (plan.template != node.chunkTemplate || plan.config.seed != node.chunkSeed
                || Mathf.Abs(plan.config.mapWidth - node.worldBounds.size.x) > .002f || Mathf.Abs(plan.config.mapDepth - node.worldBounds.size.z) > .002f)
                throw new ArgumentException("Solved chunk plan does not match this node's template, seed or dimensions.");
            instantiate ??= (prefab, parent) => UnityEngine.Object.Instantiate(prefab, parent, false);
            var added = new System.Collections.Generic.List<GeneratedSpecialPOI>();
            GeneratedCityResult city;
            try
            {
                foreach (var p in plan.specialPOIs)
                {
                    var instance = instantiate(p.definition.prefab, world.root.transform.Find("SpecialPOIs"));
                    instance.name = p.definition.id; instance.transform.position = p.position; instance.transform.rotation = p.rotation;
                    var location = new GeneratedLocation { id = p.definition.id, type = LocationType.SpecialPOI, root = instance, bounds = p.bounds,
                        tags = new[] { "special_poi", p.definition.id, p.definition.displayName, node.Id }.Concat(p.definition.tags ?? Array.Empty<string>())
                            .Concat(p.definition.id.ToLowerInvariant().Split('_').Skip(1)).Distinct().ToList() };
                    var poi = new GeneratedSpecialPOI { id = p.definition.id, displayName = p.definition.displayName, definition = p.definition,
                        instance = instance, bounds = p.bounds, chunk = node, location = location };
                    added.Add(poi); world.specialPOIs.Add(poi);
                    POIDebugLabel.Create(instance.transform, poi.displayName, p.bounds, p.rotation * p.definition.localForward,
                        world.worldTemplate.specialPOISettings.labelFont, world.worldTemplate.specialPOISettings.showDebugLocationLabels);
                }
                city = CitySceneBuilder.Build(plan, scene, instantiate);
            }
            catch
            {
                foreach (var poi in added) { CitySceneBuilder.DestroyRoot(poi.instance); world.specialPOIs.Remove(poi); }
                world.RefreshLocations(); throw;
            }
            city.root.name = node.Id; city.root.transform.SetParent(world.root.transform, false);
            city.root.transform.localPosition = node.worldBounds.center;
            var chunk = new GeneratedCityChunk { node = node, city = city };
            node.generatedResult = chunk; world.chunks.Add(chunk); world.RefreshLocations(); return chunk;
        }

        public static void UnloadChunk(GeneratedWorldResult world, Vector2Int gridPosition)
        {
            if (world == null) return;
            var chunk = world.chunks.FirstOrDefault(c => c.node.gridPosition == gridPosition);
            if (chunk == null) return;
            CitySceneBuilder.DestroyRoot(chunk.city.root); chunk.node.generatedResult = null;
            foreach (var poi in world.specialPOIs.Where(p => p.chunk.gridPosition == gridPosition).ToArray())
            { CitySceneBuilder.DestroyRoot(poi.instance); world.specialPOIs.Remove(poi); }
            world.chunks.Remove(chunk); world.RefreshLocations();
        }

        public static GeneratedWorldRoot[] FindWorlds(Scene scene) => scene.IsValid() && scene.isLoaded
            ? scene.GetRootGameObjects().Select(g => g.GetComponent<GeneratedWorldRoot>()).Where(m => m != null).ToArray()
            : Array.Empty<GeneratedWorldRoot>();
        public static void Clear(Scene scene)
        {
            foreach (var world in FindWorlds(scene)) CitySceneBuilder.DestroyRoot(world.gameObject);
        }
    }
}
