using System;
using System.Linq;
using EarthRecovery.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EarthRecovery.Tests
{
    public sealed class AssetClassifierTests
    {
        [TestCase("SM_BusStop_01", AssetCategory.Transit, "BusStop")]
        [TestCase("SM_bus_stop_01", AssetCategory.Transit, "BusStop")]
        [TestCase("SM_SubwayEntrance", AssetCategory.Transit, "SubwayEntrance")]
        [TestCase("SM_Bus_01", AssetCategory.Vehicle, "Bus")]
        [TestCase("SM_TrafficLight", AssetCategory.StreetProp, "TrafficLight")]
        [TestCase("SM_traffic_light_01", AssetCategory.StreetProp, "TrafficLight")]
        [TestCase("SM_StreetLight", AssetCategory.StreetProp, "Lamp")]
        [TestCase("SM_LightPole", AssetCategory.StreetProp, "Lamp")]
        [TestCase("SM_Lamp", AssetCategory.StreetProp, "Lamp")]
        [TestCase("SM_FireEscape", AssetCategory.Structure, "FireEscape")]
        [TestCase("SM_FireTruck", AssetCategory.Vehicle, "Emergency")]
        [TestCase("SM_PoliceStation", AssetCategory.Building, "Public")]
        [TestCase("SM_Apartment", AssetCategory.Building, "Apartment")]
        [TestCase("SM_Office", AssetCategory.Building, "Office")]
        [TestCase("SM_Shop", AssetCategory.Building, "Shop")]
        [TestCase("SM_Store", AssetCategory.Building, "Shop")]
        [TestCase("SM_Shop_Sign", AssetCategory.StreetProp, "Sign")]
        [TestCase("SM_Hotel", AssetCategory.Building, "Commercial")]
        [TestCase("SM_Building", AssetCategory.Building, "Other")]
        [TestCase("SM_Road_Straight", AssetCategory.Road, "Straight")]
        [TestCase("SM_Road_Corner", AssetCategory.Road, "Corner")]
        [TestCase("SM_Road_Intersection", AssetCategory.Road, "Intersection")]
        [TestCase("SM_Road4Way", AssetCategory.Road, "Intersection")]
        [TestCase("SM_Bridge", AssetCategory.Road, "Bridge")]
        [TestCase("SM_Sidewalk", AssetCategory.Road, "Sidewalk")]
        [TestCase("SM_Road", AssetCategory.Road, "Other")]
        [TestCase("SM_Sedan", AssetCategory.Vehicle, "Car")]
        [TestCase("SM_Taxi", AssetCategory.Vehicle, "Car")]
        [TestCase("SM_Van", AssetCategory.Vehicle, "Van")]
        [TestCase("SM_Truck", AssetCategory.Vehicle, "Truck")]
        [TestCase("SM_Bin", AssetCategory.StreetProp, "Trash")]
        [TestCase("SM_Garbage", AssetCategory.StreetProp, "Trash")]
        [TestCase("SM_Bench", AssetCategory.StreetProp, "Bench")]
        [TestCase("SM_Barrier", AssetCategory.StreetProp, "Barrier")]
        [TestCase("SM_Hydrant", AssetCategory.StreetProp, "Utility")]
        [TestCase("SM_Tree", AssetCategory.Nature, "Tree")]
        [TestCase("SM_Bush", AssetCategory.Nature, "Bush")]
        [TestCase("SM_Plant", AssetCategory.Nature, "Plant")]
        [TestCase("SM_Fence", AssetCategory.Structure, "Fence")]
        [TestCase("SM_Stairs", AssetCategory.Structure, "Stair")]
        [TestCase("SM_Rooftop", AssetCategory.Structure, "Rooftop")]
        [TestCase("SM_Carpet", AssetCategory.Unknown, "")]
        [TestCase("SM_Canvas", AssetCategory.Unknown, "")]
        [TestCase("SM_Prop_Trashbin_01", AssetCategory.StreetProp, "Trash")]
        [TestCase("SM_Veh_Car_Ambo_01", AssetCategory.Vehicle, "Emergency")]
        [TestCase("SM_Veh_Car_Police_01", AssetCategory.Vehicle, "Emergency")]
        [TestCase("SM_Bld_CityHall_01", AssetCategory.Building, "Public")]
        [TestCase("SM_Env_Bridge_Wall_01", AssetCategory.Road, "Bridge")]
        [TestCase("SM_Prop_LightPole_CrossLights_01", AssetCategory.StreetProp, "TrafficLight")]
        [TestCase("SM_Prop_Billboard_Roof_01", AssetCategory.StreetProp, "Sign")]
        [TestCase("SM_Design", AssetCategory.Unknown, "")]
        public void ClassifiesNamesWithoutSubstringFalsePositives(string name, AssetCategory category, string sub)
        {
            var result = HumanThingsAssetClassifier.Classify(name, "Assets/Pack/" + name + ".prefab");
            Assert.That(result.category, Is.EqualTo(category)); Assert.That(result.subCategory, Is.EqualTo(sub));
        }
        [Test] public void NameWinsOverFolderAndMultipleTagsAreKept()
        {
            var result = HumanThingsAssetClassifier.Classify("SM_Bench", "Assets/Pack/Roads/SM_Bench.prefab");
            Assert.That(result.category, Is.EqualTo(AssetCategory.StreetProp));
            Assert.That(result.tags, Does.Contain("벤치").And.Contain("도로"));
            Assert.That(HumanThingsAssetClassifier.Classify("SM_01", "Assets/Pack/Office/SM_01.prefab").category, Is.EqualTo(AssetCategory.Building));
        }
        [Test] public void KoreanAndEnglishSearchAndUnknownFilterWork()
        {
            var db = ScriptableObject.CreateInstance<HumanThingsAssetDatabase>();
            try
            {
                db.entries.Add(HumanThingsAssetClassifier.Classify("SM_BusStop", "Assets/Pack/SM_BusStop.prefab"));
                db.entries.Add(HumanThingsAssetClassifier.Classify("SM_Thing", "Assets/Pack/SM_Thing.prefab"));
                Assert.That(db.Search("버스정류장").Count(), Is.EqualTo(1));
                Assert.That(db.Search("BUS STOP").Count(), Is.EqualTo(1));
                Assert.That(db.Search("", AssetCategory.Unknown).Count(), Is.EqualTo(1));
                Assert.That(db.Search("버스정류장", AssetCategory.Road).Count(), Is.EqualTo(0));
            }
            finally { UnityEngine.Object.DestroyImmediate(db); }
        }
        [Test] public void TagsTrimAndDeduplicate()
            => CollectionAssert.AreEqual(new[] { "Road", "도로", "custom" }, HumanThingsAssetClassifier.ParseTags(" Road,road; 도로\ncustom,,"));
        [Test] public void ImportedCityCatalogCoversEveryPrefabAndSupportsRequestedKoreanQueries()
        {
            var db = AssetDatabase.LoadAssetAtPath<HumanThingsAssetDatabase>("Assets/CityGeneration/CityAssetDatabase.asset");
            if (db == null) Assert.Ignore("City Pack is not installed in this project.");
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { db.sourceFolderPath })
                .Where(g => AssetDatabase.GUIDToAssetPath(g).EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)).ToArray();
            CollectionAssert.AreEquivalent(guids, db.entries.Where(e=>e.assetPath.StartsWith(db.sourceFolderPath+"/",StringComparison.Ordinal)).Select(e => e.assetGuid));
            Assert.That(db.entries.Where(e=>!e.assetPath.StartsWith(db.sourceFolderPath+"/",StringComparison.Ordinal)).All(e=>e.prefab.GetComponent<CityBuildingAssembly>()?.reviewed==true),Is.True);
            Assert.That(db.entries.All(e => e.prefab != null));
            foreach (string word in new[] { "도로", "상점", "차량", "버스정류장" }) Assert.That(db.Search(word).Any(), Is.True, word);
            Assert.That(db.Search("", AssetCategory.Unknown).All(e => e.category == AssetCategory.Unknown));
        }
    }

    public sealed class AssetScannerTests
    {
        string root;
        [SetUp] public void Setup()
        {
            root = "Assets/Editor/ScannerTest_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets/Editor", root.Substring(root.LastIndexOf('/') + 1));
            AssetDatabase.CreateFolder(root, "Nested");
            CreatePrefab(root + "/SM_Road_Straight.prefab", "SM_Road_Straight");
            CreatePrefab(root + "/Nested/SM_BusStop.prefab", "SM_BusStop");
            CreatePrefab(root + "/Nested/SM_Thing.prefab", "SM_Thing");
        }
        [TearDown] public void Cleanup() { AssetDatabase.DeleteAsset(root); }
        static void CreatePrefab(string path, string name)
        {
            var go = new GameObject(name);
            try { PrefabUtility.SaveAsPrefabAsset(go, path); }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        [Test] public void RecursiveScanHasUniqueGuidsAndReferences()
        {
            var entries = HumanThingsAssetScanner.Scan(root);
            Assert.That(entries.Count, Is.EqualTo(3));
            Assert.That(entries.Select(e => e.assetGuid).Distinct().Count(), Is.EqualTo(3));
            Assert.That(entries.All(e => e.prefab != null && AssetDatabase.GUIDToAssetPath(e.assetGuid) == e.assetPath));
            Assert.That(entries.Count(e => e.category == AssetCategory.Unknown), Is.EqualTo(1));
        }
        [Test] public void RescanPreservesManualChangesAcrossMoveAndRemovesDeletedAssets()
        {
            var entries = HumanThingsAssetScanner.Scan(root); var entry = entries.Single(e => e.subCategory == "BusStop");
            entry.category = AssetCategory.Building; entry.subCategory = "Custom"; entry.tags.Add("manual tag");
            string moved = root + "/Moved.prefab"; Assert.That(AssetDatabase.MoveAsset(entry.assetPath, moved), Is.Empty);
            AssetDatabase.DeleteAsset(entries.Single(e => e.category == AssetCategory.Unknown).assetPath);
            var rescanned = HumanThingsAssetScanner.Scan(root, entries);
            var updated = rescanned.Single(e => e.assetGuid == entry.assetGuid);
            Assert.That(rescanned.Count, Is.EqualTo(2)); Assert.That(updated.assetPath, Is.EqualTo(moved));
            Assert.That(updated.category, Is.EqualTo(AssetCategory.Building)); Assert.That(updated.subCategory, Is.EqualTo("Custom"));
            Assert.That(updated.tags, Does.Contain("manual tag")); Assert.That(entry.assetPath, Is.Not.EqualTo(moved));
        }
        [Test] public void SaveReloadRetainsPrefabReferenceAndSearchableEdits()
        {
            var db = ScriptableObject.CreateInstance<HumanThingsAssetDatabase>(); db.entries = HumanThingsAssetScanner.Scan(root);
            db.entries[0].tags.Add("수동태그"); db.sourceFolderPath = root; db.sourceFolderGuid = AssetDatabase.AssetPathToGUID(root);
            string path = root + "/Database.asset"; AssetDatabase.CreateAsset(db, path); AssetDatabase.SaveAssetIfDirty(db);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var loaded = AssetDatabase.LoadAssetAtPath<HumanThingsAssetDatabase>(path);
            Assert.That(loaded.entries.Count, Is.EqualTo(3)); Assert.That(loaded.Search("수동태그").Single().prefab, Is.Not.Null);
        }
        [Test] public void CancelAndInvalidFolderDoNotMutateExistingEntries()
        {
            var entries = HumanThingsAssetScanner.Scan(root);
            Assert.Throws<OperationCanceledException>(() => HumanThingsAssetScanner.Scan(root, entries, (i, n, p) => true));
            Assert.That(entries.Count, Is.EqualTo(3));
            Assert.Throws<ArgumentException>(() => HumanThingsAssetScanner.Scan("Packages"));
            Assert.Throws<ArgumentException>(() => HumanThingsAssetScanner.Scan(root + "/SM_Road_Straight.prefab"));
        }
    }
}
