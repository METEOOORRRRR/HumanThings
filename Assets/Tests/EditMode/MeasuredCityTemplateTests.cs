using System;
using System.Collections.Generic;
using System.Linq;
using EarthRecovery.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EarthRecovery.Tests
{
    public sealed class MeasuredCityTemplateTests
    {
        static HumanThingsAssetDatabase Db() => AssetDatabase.LoadAssetAtPath<HumanThingsAssetDatabase>("Assets/CityGeneration/CityAssetDatabase.asset");
        [Test] public void AnalysisMatchesActualMetadataAndDoesNotMutateSource()
        {
            var db = Db(); string before = JsonUtility.ToJson(db);
            var a = CityAssetAnalysis.Analyze(db);
            Assert.That(a.total, Is.EqualTo(335));
            var b = a.categories.Single(c => c.category == AssetCategory.Building);
            Assert.That(b.total, Is.EqualTo(62)); Assert.That(b.usable, Is.EqualTo(13));
            Assert.That(b.usableSizes.meanFootprint.x, Is.EqualTo(9.2835f).Within(.001f));
            Assert.That(b.usableSizes.meanFootprint.y, Is.EqualTo(8.9578f).Within(.001f));
            Assert.That(b.usableSizes.frontFacesRoadRatio, Is.EqualTo(1));
            Assert.That(a.unknownNames.Count, Is.EqualTo(59));
            Assert.That(a.warnings.Any(w => w.Contains("SidewalkPoles")), Is.True);
            Assert.That(a.fingerprint, Is.EqualTo(CityAssetAnalysis.Analyze(db).fingerprint));
            Assert.That(JsonUtility.ToJson(db), Is.EqualTo(before));
        }
        [Test] public void MeasurementsDriveRangesAndAbsentAssetsHaveZeroDemand()
        {
            var clone = UnityEngine.Object.Instantiate(Db()); clone.entries = Db().entries.Select(e => e.Copy()).ToList();
            List<CityLayoutTemplate> original = null, changed = null;
            try
            {
                original = CityTemplateDesigner.Design(CityAssetAnalysis.Analyze(clone), new());
                Assert.That(original.Count, Is.InRange(4, 8));
                Assert.That(original.All(t => t.buildings.residentialRatio.max == 0), Is.True);
                foreach (var e in clone.entries.Where(e => e.category == AssetCategory.Building && e.placementEnabled))
                { e.footprintWidth *= 1.5f; e.footprintDepth *= 1.5f; }
                clone.entries.RemoveAll(e => e.category == AssetCategory.Transit);
                changed = CityTemplateDesigner.Design(CityAssetAnalysis.Analyze(clone), new());
                Assert.That(changed.Any(t => t.templateName == "TransitCorridor"), Is.False);
                Assert.That(changed.All(t => t.pois.subwayEntranceCount.max == 0 && t.pois.busStopCount.max == 0), Is.True);
                Assert.That(changed.Single(t => t.templateName == "CatalogMixed").roads.blockSize.min,
                    Is.GreaterThan(original.Single(t => t.templateName == "CatalogMixed").roads.blockSize.min));
            }
            finally
            {
                foreach (var t in (original ?? new()).Concat(changed ?? new())) UnityEngine.Object.DestroyImmediate(t);
                UnityEngine.Object.DestroyImmediate(clone);
            }
        }
        [TestCase("CatalogMixed")] [TestCase("SparseBlocks")] [TestCase("WideAvenue")]
        [TestCase("MarketBranches")] [TestCase("OfficeBlocks")] [TestCase("TransitCorridor")]
        public void SavedTemplatesPassTenIndependentSeedsAndRetainOwnership(string name)
        {
            var t = AssetDatabase.LoadAssetAtPath<CityLayoutTemplate>(CityTemplateAutoBuilder.DefaultFolder + "/" + name + ".asset");
            Assert.That(t, Is.Not.Null); Assert.That(CityTemplateAutoBuilder.Unmodified(t), Is.True);
            var report = CityTemplateValidation.Run(t, Db(), 10, true);
            Assert.That(report.passed, Is.True, "Buildings " + report.buildings + " Props " + report.props + " POIs " + report.requiredPOIs);
            Assert.That(report.seeds.Count, Is.EqualTo(10)); Assert.That(report.seeds.Sum(s => s.overlapPairs), Is.Zero);
            Assert.That(t.description, Is.Not.Empty); Assert.That(t.assetBasis, Is.Not.Empty);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                var plan = ProceduralCityGenerator.Generate(t, 1000003, Db());
                var city = CitySceneBuilder.Build(plan, scene);
                Assert.That(city.buildings.Count, Is.GreaterThan(0));
                Assert.That(city.root.GetComponentsInChildren<Renderer>().Length, Is.GreaterThan(0));
                Assert.That(city.root.GetComponentsInChildren<Transform>().All(x => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(x.gameObject) == 0), Is.True);
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
        }
        [Test] public void ManualAndEditedTemplatesAreProtectedAndCancellationIsReadOnly()
        {
            var t = ScriptableObject.CreateInstance<CityLayoutTemplate>();
            try
            {
                Assert.That(CityTemplateAutoBuilder.Owned(t, ""), Is.False);
                t.autoGenerated = true; t.autoGeneratorId = CityTemplateDesigner.Owner; t.sourceDatabaseGuid = "fixture";
                t.generatedContentHash = CityTemplateAutoBuilder.ContentHash(t);
                Assert.That(CityTemplateAutoBuilder.Unmodified(t), Is.True);
                t.buildings.density.max = .1f;
                Assert.That(CityTemplateAutoBuilder.Unmodified(t), Is.False);
                Assert.That(CityTemplateAutoBuilder.Owned(t, "other"), Is.False);
                string folder = "Assets/MeasuredTemplateCancelFixture";
                Assert.That(AssetDatabase.IsValidFolder(folder), Is.False);
                Assert.Throws<OperationCanceledException>(() => CityTemplateAutoBuilder.Build(Db(), folder, true, (_, _) => true));
                Assert.That(AssetDatabase.IsValidFolder(folder), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(t); }
        }
        [Test] public void DesignerAndWindowRejectMissingInputSafely()
        {
            Assert.Throws<ArgumentNullException>(() => CityAssetAnalysis.Analyze(null));
            var empty = ScriptableObject.CreateInstance<HumanThingsAssetDatabase>();
            var window = ScriptableObject.CreateInstance<CityTemplateAutoBuilderWindow>();
            try { Assert.Throws<InvalidOperationException>(() => CityTemplateDesigner.Design(CityAssetAnalysis.Analyze(empty), new())); }
            finally { UnityEngine.Object.DestroyImmediate(empty); UnityEngine.Object.DestroyImmediate(window); }
        }
        [Test] public void RebuildPreservesManualEditsAndExistingGeneratedGuids()
        {
            const string folder = "Assets/MeasuredTemplateOwnershipFixture";
            Assert.That(AssetDatabase.IsValidFolder(folder), Is.False);
            AssetDatabase.CreateFolder("Assets", "MeasuredTemplateOwnershipFixture");
            try
            {
                var manual = ScriptableObject.CreateInstance<CityLayoutTemplate>(); manual.description = "User text";
                string manualPath = folder + "/CatalogMixed.asset"; AssetDatabase.CreateAsset(manual, manualPath);
                string manualGuid = AssetDatabase.AssetPathToGUID(manualPath);
                CityTemplateAutoBuilder.Build(Db(), folder, true, exportReport: false);
                string avenuePath = folder + "/WideAvenue.asset", avenueGuid = AssetDatabase.AssetPathToGUID(avenuePath);
                var edited = AssetDatabase.LoadAssetAtPath<CityLayoutTemplate>(folder + "/OfficeBlocks.asset");
                edited.description = "User-edited automatic template"; EditorUtility.SetDirty(edited); AssetDatabase.SaveAssets();
                CityTemplateAutoBuilder.Build(Db(), folder, true, exportReport: false);
                Assert.That(AssetDatabase.AssetPathToGUID(manualPath), Is.EqualTo(manualGuid));
                Assert.That(manual.description, Is.EqualTo("User text"));
                Assert.That(edited.description, Is.EqualTo("User-edited automatic template"));
                Assert.That(AssetDatabase.AssetPathToGUID(avenuePath), Is.EqualTo(avenueGuid));
            }
            finally { AssetDatabase.DeleteAsset(folder); }
        }
        [Test] public void AutomaticTuningRecordsFailuresWithoutChangingCoreTransitMinimum()
        {
            var t = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<CityLayoutTemplate>(CityTemplateAutoBuilder.DefaultFolder + "/TransitCorridor.asset"));
            try
            {
                t.pois.subwayEntranceCount = new(8, 8);
                t.roads.sidewalkWidth = 2;
                var r = CityTemplateDesigner.Tune(t, Db());
                Assert.That(r.seeds.Count, Is.EqualTo(20)); Assert.That(r.tuningRounds, Is.LessThanOrEqualTo(3));
                Assert.That(r.adjustments.Count, Is.GreaterThan(0)); Assert.That(r.passed, Is.False);
                Assert.That(t.pois.subwayEntranceCount.min, Is.EqualTo(8));
            }
            finally { UnityEngine.Object.DestroyImmediate(t); }
        }
    }
}
