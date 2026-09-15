using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EarthRecovery
{
    public static class CitySceneBuilder
    {
        public const string RootName = "HumanThings_RuntimeGeneratedCity";
        public static GeneratedCityRoot[] FindGenerated(Scene scene) => scene.IsValid() && scene.isLoaded
            ? scene.GetRootGameObjects().Select(g => g.GetComponent<GeneratedCityRoot>()).Where(m => m != null).ToArray() : Array.Empty<GeneratedCityRoot>();

        public static GeneratedCityResult Build(ProceduralCityPlan plan, Scene scene, Func<GameObject, Transform, GameObject> instantiate = null)
        {
            if (plan?.placement == null || !scene.IsValid() || !scene.isLoaded) throw new ArgumentException("A solved plan and a loaded scene are required.");
            if (plan.placement.placements.Any(p => !HumanThingsAssetEligibility.Usable(p.asset))) throw new ArgumentException("Missing or invalid prefab reference.");
            instantiate ??= (prefab, parent) => UnityEngine.Object.Instantiate(prefab, parent, false);
            var root = new GameObject(RootName); root.SetActive(false); SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var result = new GeneratedCityResult { seed = plan.config.seed, template = plan.template, config = plan.config, root = root,
                    roads = plan.roads.Select(r => new GeneratedRoad { id = r.id, kind = r.kind, start = r.start, end = r.end, width = r.width, bounds = r.bounds, connections = new(r.connections) }).ToList(), lots = plan.lots,
                    warnings = plan.placement.warnings.Distinct().ToList() };
                root.AddComponent<GeneratedCityRoot>().result = result;
                var groups = new[] { "Roads", "Buildings", "Vehicles", "StreetProps", "POIs", "Debug" }.ToDictionary(n => n, n =>
                { var child = new GameObject(n); child.transform.SetParent(root.transform, false); return child.transform; });
                foreach (var road in result.roads)
                {
                    road.root = new GameObject(road.id); road.root.transform.SetParent(groups["Roads"], false);
                    result.locations.Add(new GeneratedLocation { id = road.id, type = road.kind == CityRoadKind.Alley ? LocationType.Alley : LocationType.Street,
                        bounds = road.bounds, root = road.root, tags = new() { "Road", road.kind.ToString() } });
                }
                int index = 0;
                foreach (var p in plan.placement.placements)
                {
                    var road = result.roads.FirstOrDefault(r => p.zoneId.StartsWith(r.id + "_", StringComparison.Ordinal));
                    Transform parent = p.surface && road != null ? road.root.transform : groups[p.group];
                    var instance = instantiate(p.asset.prefab, parent);
                    if (instance == null) throw new InvalidOperationException("Prefab instantiation failed.");
                    instance.transform.localPosition = p.position; instance.transform.localRotation = p.rotation;
                    instance.transform.localScale = Vector3.Scale(p.asset.prefab.transform.localScale, p.scale);
                    foreach (var part in p.asset.companionPrefabs ?? new())
                    {
                        if (part == null) continue;
                        var child = instantiate(part, instance.transform); child.transform.localPosition = Vector3.zero; child.transform.localRotation = Quaternion.identity;
                        var scale = p.asset.prefab.transform.localScale;
                        child.transform.localScale = new Vector3(part.transform.localScale.x / scale.x, part.transform.localScale.y / scale.y, part.transform.localScale.z / scale.z);
                    }
                    if (p.surface) continue;
                    var id = "Object_" + (index++).ToString("D4");
                    var item = new GeneratedCityObject { id = id, assetGuid = p.asset.assetGuid, lotId = p.zoneId, bounds = p.footprint, root = instance };
                    if (p.asset.category == AssetCategory.Building) result.buildings.Add(item);
                    else if (p.group == "Vehicles") result.vehicles.Add(item);
                    else result.props.Add(item);
                    if (p.group == "Buildings" || p.group == "POIs")
                    {
                        var request = plan.compiled.requests.First(r => r.id == p.requestId);
                        var tags = (p.asset.tags ?? new()).Concat(new[] { p.asset.subCategory, request.locationType.ToString() }).Distinct().ToList();
                        var lot = result.lots.FirstOrDefault(l => l.id == p.zoneId);
                        if (lot != null) { tags.Add(lot.roadKind.ToString()); if (lot.corner) tags.Add("Corner"); }
                        result.locations.Add(new GeneratedLocation { id = id, type = request.locationType, bounds = p.footprint, root = instance, tags = tags });
                    }
                }
                root.SetActive(true);
                return result;
            }
            catch { DestroyRoot(root); throw; }
        }
        public static void Clear(Scene scene)
        {
            foreach (var marker in FindGenerated(scene)) DestroyRoot(marker.gameObject);
        }
        public static void DestroyRoot(GameObject root)
        {
            if (root == null) return;
            root.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(root); else UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
