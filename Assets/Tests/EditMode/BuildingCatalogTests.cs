using System.Linq;
using EarthRecovery.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EarthRecovery.Tests
{
    public sealed class BuildingCatalogTests
    {
        static HumanThingsAssetDatabase Db()=>AssetDatabase.LoadAssetAtPath<HumanThingsAssetDatabase>("Assets/CityGeneration/CityAssetDatabase.asset");
        [Test] public void ReviewedCatalogHasTwentyCompleteAssembliesAndNoRawModulesEnabled()
        {
            var db=Db();var assemblies=db.entries.Where(e=>e.prefab.GetComponent<CityBuildingAssembly>()!=null).ToArray();
            Assert.That(assemblies.Length,Is.EqualTo(20));Assert.That(assemblies.All(HumanThingsAssetEligibility.Usable),Is.True);
            Assert.That(assemblies.Count(e=>e.subCategory=="Apartment"),Is.EqualTo(6));
            Assert.That(assemblies.Select(e=>e.assetGuid).Distinct().Count(),Is.EqualTo(20));
            foreach(var e in db.entries.Where(e=>e.category==AssetCategory.Building && e.assetPath.StartsWith(BuildingCatalogAudit.Source) && (e.displayName.Contains("Apartment") || e.displayName.Contains("OfficeSquare") || e.displayName.Contains("OfficeRound") || e.displayName.Contains("OfficeOctagon"))))
                Assert.That(e.placementEnabled,Is.False,e.displayName);
            foreach(var e in assemblies)
            {
                var go=Object.Instantiate(e.prefab);
                try
                {
                    Assert.That(go.GetComponentsInChildren<Transform>().All(t=>t.localScale==Vector3.one),Is.True,e.displayName);
                    Assert.That(HumanThingsPlacementMetadata.Measure(go).min.y,Is.EqualTo(0).Within(.001f));
                    Assert.That(go.GetComponentsInChildren<Collider>().Length,Is.EqualTo(1));
                    var check=CityBuildingCatalogBuilder.CheckEnvelope(go);
                    Assert.That(check,Does.StartWith("wallMisses=0/"),e.displayName);
                    Assert.That(check,Does.EndWith("roofMisses=0/9"),e.displayName);
                }
                finally{Object.DestroyImmediate(go);}
            }
        }
        [Test] public void RescanningPurchasedPackPreservesSupplementaryAssembliesAndIds()
        {
            var db=Db();string before=JsonUtility.ToJson(db);
            var scanned=HumanThingsAssetScanner.Scan(db.sourceFolderPath,db.entries);
            Assert.That(scanned.Select(e=>e.assetGuid),Is.EquivalentTo(db.entries.Select(e=>e.assetGuid)));
            Assert.That(scanned.Count(HumanThingsAssetEligibility.Usable),Is.EqualTo(db.entries.Count(HumanThingsAssetEligibility.Usable)));
            Assert.That(JsonUtility.ToJson(db),Is.EqualTo(before));
        }
        [Test] public void UnreviewedDraftCannotBecomePlacementCandidate()
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.AddComponent<CityBuildingAssembly>().family="OfficeSquare";
            try
            {
                var e=new HumanThingsAssetEntry{prefab=go,displayName="Draft"};HumanThingsPlacementMetadata.Initialize(e);
                Assert.That(e.placementEnabled,Is.False);e.placementEnabled=true;
                Assert.That(HumanThingsAssetEligibility.Usable(e),Is.False);
            }
            finally{Object.DestroyImmediate(go);}
        }
    }
}
