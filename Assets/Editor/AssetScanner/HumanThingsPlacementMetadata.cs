using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EarthRecovery.Editor
{
    public static class HumanThingsPlacementMetadata
    {
        // Renderer local bounds are transformed into an origin/identity-root placement frame.
        // Root scale is retained because prefab instances retain their authored scale.
        public static Bounds Measure(GameObject prefab)
        {
            if (prefab == null) return new Bounds();
            var matrix = Matrix4x4.Scale(prefab.transform.localScale) * prefab.transform.worldToLocalMatrix;
            Bounds result = new Bounds(); bool found = false;
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
                var b = renderer.localBounds;
                var transform = matrix * renderer.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = transform.MultiplyPoint3x4(corner);
                    if (!found) { result = new Bounds(p, Vector3.zero); found = true; } else result.Encapsulate(p);
                }
            }
            return result;
        }

        public static void Initialize(HumanThingsAssetEntry e)
        {
            var assembly=e.prefab!=null?e.prefab.GetComponent<CityBuildingAssembly>():null;
            if(assembly!=null){e.category=AssetCategory.Building;e.subCategory=assembly.family=="Apartment"?"Apartment":"Office";}
            e.companionPrefabs = new();
            // City lamps share an authored assembly origin. Keep their parts as prefab references.
            if (e.displayName == "SM_Prop_LightPole_Base_01")
                foreach (string name in new[] { "SM_Prop_LightPole_Arm_01", "SM_Prop_LightPole_Lights_01" })
                {
                    string path = Path.GetDirectoryName(e.assetPath)?.Replace('\\', '/') + "/" + name + ".prefab";
                    var part = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (part != null) e.companionPrefabs.Add(part);
                }
            var b = MeasureEntry(e);
            e.boundsSize = b.size; e.boundsCenter = b.center;
            e.footprintWidth = b.size.x; e.footprintDepth = b.size.z;
            e.localForward = Vector3.forward; e.groundOffset = 0;
            e.frontFacesRoad = e.category == AssetCategory.Building || e.category == AssetCategory.Transit || e.subCategory == "Lamp";
            e.canRotate90 = true; e.allowRandomRotation = e.category == AssetCategory.Nature;
            e.placementSurface = e.category == AssetCategory.Vehicle ? PlacementSurface.Road
                : e.category == AssetCategory.StreetProp || e.category == AssetCategory.Transit ? PlacementSurface.Sidewalk : PlacementSurface.Ground;
            var words = HumanThingsAssetClassifier.Normalize(e.displayName).Split(' ');
            bool Has(params string[] terms) => terms.Any(words.Contains);
            e.cornerCompatible = Has("corner", "intersection", "junction");
            e.againstWall = Has("attachment", "poster", "wall", "window");
            bool fragment = Has("roof", "floor", "base", "stack", "cover", "door", "interior", "spire", "lid", "attachment", "patch", "arrow", "lines", "median");
            e.placementEnabled = b.size.x > .01f && b.size.z > .01f && !fragment && !e.againstWall;
            if (e.category == AssetCategory.Building && (Has("square", "round", "octagon") || Has("apartment"))) e.placementEnabled = false;
            if(assembly!=null)e.placementEnabled=assembly.reviewed && b.size.x>.01f && b.size.z>.01f;
            if (e.subCategory == "Lamp") e.placementEnabled = e.companionPrefabs.Count == 2;
            if (e.subCategory == "Sign" && !Has("stop", "give", "parking", "street", "warning")) { e.againstWall = true; e.placementEnabled = false; }
            e.placementVersion = 2;
        }

        public static Bounds MeasureEntry(HumanThingsAssetEntry e)
        {
            var b = Measure(e.prefab);
            foreach (var part in e.companionPrefabs ?? new()) if (part != null) b.Encapsulate(Measure(part));
            return b;
        }

        public static bool Usable(HumanThingsAssetEntry e) => HumanThingsAssetEligibility.Usable(e);
    }
}
