using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace EarthRecovery.Editor
{
    [Serializable] public sealed class CitySizeStatistics
    {
        public int count, cornerCount;
        public float frontFacesRoadRatio;
        public Vector3 meanBounds, minBounds, maxBounds;
        public Vector2 meanFootprint, minFootprint, maxFootprint;
        public static CitySizeStatistics From(IEnumerable<HumanThingsAssetEntry> source)
        {
            var a = source.Where(e => float.IsFinite(e.footprintWidth) && float.IsFinite(e.footprintDepth) && float.IsFinite(e.boundsSize.x) && float.IsFinite(e.boundsSize.y) && float.IsFinite(e.boundsSize.z)).ToArray();
            var s = new CitySizeStatistics { count = a.Length }; if (a.Length == 0) return s;
            s.cornerCount = a.Count(e => e.cornerCompatible); s.frontFacesRoadRatio = a.Count(e => e.frontFacesRoad) / (float)a.Length;
            s.meanBounds = a.Aggregate(Vector3.zero, (v, e) => v + e.boundsSize) / a.Length;
            s.minBounds = new(a.Min(e => e.boundsSize.x), a.Min(e => e.boundsSize.y), a.Min(e => e.boundsSize.z));
            s.maxBounds = new(a.Max(e => e.boundsSize.x), a.Max(e => e.boundsSize.y), a.Max(e => e.boundsSize.z));
            s.meanFootprint = new(a.Average(e => e.footprintWidth), a.Average(e => e.footprintDepth));
            s.minFootprint = new(a.Min(e => e.footprintWidth), a.Min(e => e.footprintDepth));
            s.maxFootprint = new(a.Max(e => e.footprintWidth), a.Max(e => e.footprintDepth)); return s;
        }
    }
    [Serializable] public sealed class CityCount { public string name; public int total, usable; }
    [Serializable] public sealed class CityCategoryAnalysis
    {
        public AssetCategory category;
        public int total, usable;
        public List<CityCount> subCategories = new();
        public CitySizeStatistics allSizes, usableSizes;
    }
    [Serializable] public sealed class CityAssetAnalysis
    {
        public string databaseGuid, fingerprint;
        public int total;
        public List<CityCategoryAnalysis> categories = new();
        public List<CityCount> generatorCandidates = new();
        public List<CityCount> tags = new();
        public List<HumanThingsAssetEntry> entries = new();
        public List<string> warnings = new(), unknownNames = new();
        [NonSerialized] public HumanThingsAssetDatabase database;
        public static CityAssetAnalysis Analyze(HumanThingsAssetDatabase db)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            var r = new CityAssetAnalysis { database = db, databaseGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(db)) };
            r.entries = (db.entries ?? new()).Where(e => e != null).Select(e => e.Copy()).OrderBy(e => e.assetGuid, StringComparer.Ordinal).ToList();
            r.total = r.entries.Count;
            r.tags = r.entries.SelectMany(e => (e.tags ?? new()).Distinct(StringComparer.OrdinalIgnoreCase)).GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => new CityCount { name = g.Key, total = g.Count() }).ToList();
            r.fingerprint = Hash(string.Join("\n", r.entries.Select(e =>
            {
                var copy = e.Copy(); copy.prefab = null; copy.companionPrefabs.Clear();
                return e.assetGuid + JsonUtility.ToJson(copy) + (e.prefab == null ? "missing" : AssetDatabase.GetAssetDependencyHash(e.assetPath).ToString())
                    + string.Join("|", (e.companionPrefabs ?? new()).Select(p => p == null ? "missing" : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(p))));
            })));
            foreach (AssetCategory category in Enum.GetValues(typeof(AssetCategory)))
            {
                var a = r.entries.Where(e => e.category == category).ToArray(); var u = a.Where(Usable).ToArray();
                var c = new CityCategoryAnalysis { category = category, total = a.Length, usable = u.Length, allSizes = CitySizeStatistics.From(a), usableSizes = CitySizeStatistics.From(u) };
                string[] expected = category switch
                {
                    AssetCategory.Building => new[] { "Apartment", "Office", "Commercial", "Shop", "Public", "Other" },
                    AssetCategory.Road => new[] { "Straight", "Corner", "Intersection", "TIntersection", "Sidewalk", "Other" },
                    AssetCategory.Vehicle => new[] { "Car", "Van", "Truck", "Bus", "Emergency", "Other" },
                    AssetCategory.StreetProp => new[] { "Lamp", "Sign", "Trash", "TrafficLight", "Utility", "Other" },
                    AssetCategory.Transit => new[] { "BusStop", "SubwayEntrance", "Other" },
                    AssetCategory.Structure => new[] { "Fence", "Stair", "FireEscape", "Rooftop", "Other" }, _ => Array.Empty<string>()
                };
                foreach (var sub in expected.Concat(a.Select(e => e.subCategory ?? "")).Distinct())
                    c.subCategories.Add(new CityCount { name = sub, total = a.Count(e => (e.subCategory ?? "") == sub), usable = u.Count(e => (e.subCategory ?? "") == sub) });
                r.categories.Add(c);
            }
            foreach (BuildingUsage u in Enum.GetValues(typeof(BuildingUsage)))
                r.generatorCandidates.Add(new CityCount { name = "Building/" + u, usable = CityLayoutAssets.Resolve(db, CityLayoutAssets.Building(u)).Count });
            r.generatorCandidates.Add(new CityCount { name = "Road surface", usable = r.Query(AssetCategory.Road, PlacementSurface.Ground, "Straight", "Other", "Intersection").Count });
            r.generatorCandidates.Add(new CityCount { name = "Sidewalk surface", usable = r.Query(AssetCategory.Road, PlacementSurface.Ground, "Sidewalk").Count });
            r.generatorCandidates.Add(new CityCount { name = "Vehicle", usable = r.Query(AssetCategory.Vehicle, PlacementSurface.Road, "Car", "Van", "Truck", "Bus", "Emergency").Count });
            foreach (string sub in new[] { "Lamp", "Sign", "Trash", "Bench", "TrafficLight" })
                r.generatorCandidates.Add(new CityCount { name = "StreetProp/" + sub, usable = r.Query(AssetCategory.StreetProp, PlacementSurface.Sidewalk, sub).Count });
            foreach (string sub in new[] { "BusStop", "SubwayEntrance" })
                r.generatorCandidates.Add(new CityCount { name = "Transit/" + sub, usable = r.Query(AssetCategory.Transit, PlacementSurface.Sidewalk, sub).Count });
            foreach (var e in r.entries)
            {
                string n = HumanThingsAssetClassifier.Normalize(e.displayName);
                void Warn(string issue) => r.warnings.Add(e.displayName + ": " + issue + " Original values preserved.");
                if (e.category == AssetCategory.Unknown) { r.unknownNames.Add(e.displayName); if (n.Contains("building") || n.Contains(" bld ")) Warn("building-like name classified Unknown"); }
                if (e.prefab == null) Warn("missing prefab reference");
                if (e.category == AssetCategory.Building && (e.footprintWidth <= 0 || e.footprintDepth <= 0)) Warn("invalid building footprint");
                if (e.category == AssetCategory.Vehicle && e.placementSurface != PlacementSurface.Road && e.placementSurface != PlacementSurface.Any) Warn("vehicle has non-road placement surface");
                if (e.category == AssetCategory.Road && (Mathf.Min(e.boundsSize.x, e.boundsSize.z) <= 0 || !float.IsFinite(e.boundsSize.x + e.boundsSize.y + e.boundsSize.z))) Warn("invalid road bounds");
                if (e.category == AssetCategory.Road && n.StartsWith("sm prop ")) Warn("prop classified as road surface; review before using it as a tile");
                if (e.frontFacesRoad && new Vector2(e.localForward.x, e.localForward.z).sqrMagnitude < .001f) Warn("invalid localForward for road-facing asset");
                if (e.category == AssetCategory.Transit && n.StartsWith("sm bld ")) Warn("station building assigned sidewalk; review ground placement separately");
                if (e.placementEnabled && e.category != AssetCategory.Unknown && !HumanThingsAssetEligibility.Usable(e)) Warn("enabled but rejected by runtime metadata validation");
            }
            return r;
        }
        public static bool Usable(HumanThingsAssetEntry e) => HumanThingsAssetEligibility.Usable(e) && !e.againstWall;
        public List<HumanThingsAssetEntry> Query(AssetCategory category, PlacementSurface surface, params string[] sub) => CityLayoutAssets.Resolve(database,
            new CityAssetQuery { category = category, surface = surface, subCategories = sub.Length == 0 ? null : sub });
        public static string Hash(string value)
        { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", ""); }
        public static float Percentile(IEnumerable<float> values, float fraction)
        {
            var a = values.Where(float.IsFinite).OrderBy(v => v).ToArray();
            if (a.Length == 0) throw new InvalidOperationException("No usable measurements.");
            float index = Mathf.Clamp01(fraction) * (a.Length - 1); return Mathf.Lerp(a[(int)index], a[Mathf.CeilToInt(index)], index - (int)index);
        }
    }
}
