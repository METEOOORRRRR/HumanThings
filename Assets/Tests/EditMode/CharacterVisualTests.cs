using System.Linq;
using EarthRecovery.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace EarthRecovery.Tests
{
    public sealed class CharacterVisualTests
    {
        [Test]
        public void VisualIsAVariantAndPreservesAllOriginalGeometryAndAnimation()
        {
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerCharacterBuilder.PrefabPath);
            var variant = AssetDatabase.LoadAssetAtPath<GameObject>(HumanThingsCharacterVisualBuilder.PrefabPath);
            Assert.That(PrefabUtility.GetPrefabAssetType(variant), Is.EqualTo(PrefabAssetType.Variant));
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(variant), Is.EqualTo(original));
            var a = original.GetComponentInChildren<SkinnedMeshRenderer>(); var b = variant.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.That(b.sharedMesh, Is.SameAs(a.sharedMesh));
            Assert.That(b.sharedMaterials.Length, Is.EqualTo(a.sharedMaterials.Length));
            Assert.That(b.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On)); Assert.That(b.receiveShadows, Is.True);
            Assert.That(b.sharedMaterial, Is.Not.SameAs(a.sharedMaterial));
            Assert.That(AssetDatabase.GetAssetPath(a.sharedMaterial), Is.EqualTo(PlayerCharacterBuilder.Source));
            foreach (var t in original.GetComponentsInChildren<Transform>())
            {
                string path = AnimationUtility.CalculateTransformPath(t, original.transform);
                var other = path.Length == 0 ? variant.transform : variant.transform.Find(path);
                Assert.That(other, Is.Not.Null, path);
                Assert.That(other.localPosition, Is.EqualTo(t.localPosition), path);
                Assert.That(other.localRotation, Is.EqualTo(t.localRotation), path);
                Assert.That(other.localScale, Is.EqualTo(t.localScale), path);
            }
            Assert.That(variant.GetComponent<PlayerAvatar>().animator.runtimeAnimatorController,
                Is.SameAs(original.GetComponent<PlayerAvatar>().animator.runtimeAnimatorController));
            Assert.That(variant.GetComponentsInChildren<Collider>(), Is.Empty);
        }

        [Test]
        public void MaterialUsesMipmappedCopiesAndPreservesTheOriginalMaps()
        {
            var visual = AssetDatabase.LoadAssetAtPath<GameObject>(HumanThingsCharacterVisualBuilder.PrefabPath).GetComponent<HumanThingsCharacterVisual>();
            var material = visual.humanThingsMaterial;
            Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
            foreach (var pair in new[] { ("_BaseMap", "baseColorTexture"), ("_BumpMap", "normalTexture"), ("_MetallicRoughnessMap", "metallicRoughnessTexture") })
            {
                var original = visual.originalMaterial.GetTexture(pair.Item2);
                var copy = (Texture2D)material.GetTexture(pair.Item1);
                Assert.That(AssetDatabase.GetAssetPath(original), Is.EqualTo(PlayerCharacterBuilder.Source));
                Assert.That(copy, Is.Not.SameAs(original)); Assert.That(copy.width, Is.EqualTo(original.width)); Assert.That(copy.height, Is.EqualTo(original.height));
                Assert.That(copy.mipmapCount, Is.GreaterThan(1)); Assert.That(copy.filterMode, Is.EqualTo(FilterMode.Trilinear));
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(copy));
                Assert.That(importer.sRGBTexture, Is.EqualTo(pair.Item1 == "_BaseMap"));
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Default));
            }
            Assert.That(material.FindPass("MotionVectors"), Is.GreaterThanOrEqualTo(0));
            Assert.That(visual.skin.skinnedMotionVectors, Is.True);
            foreach (string name in new[] { "_Regions", "_WeatherMask" })
            {
                var path = AssetDatabase.GetAssetPath(material.GetTexture(name));
                Assert.That(path, Does.StartWith(HumanThingsCharacterVisualBuilder.Folder));
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.sRGBTexture, Is.False); Assert.That(importer.mipmapEnabled, Is.True);
                Assert.That(importer.alphaIsTransparency, Is.False);
            }
        }

        [Test]
        public void GameLoadsVisualProfileAndTheMaterialMatchesItsValues()
        {
            var profile = Resources.Load<HumanThingsCharacterVisualProfile>("HumanThingsCharacterVisualProfile");
            Assert.That(profile.visualPrefab, Is.Not.Null);
            var material = profile.visualPrefab.GetComponent<HumanThingsCharacterVisual>().humanThingsMaterial;
            Assert.That(material.GetFloat("_Saturation"), Is.EqualTo(profile.saturation));
            Assert.That(material.GetFloat("_ValueFloor"), Is.EqualTo(profile.charcoalFloor));
            Assert.That(profile.environmentLighting, Is.Not.Null);
            Assert.That(material.GetColor("_AmbientEquator"), Is.EqualTo(profile.environmentLighting.ambientEquator));
            Assert.That(material.GetVector("_Weather").w, Is.Zero);
            Assert.That(material.GetVector("_Surfaces").x, Is.LessThan(material.GetVector("_Surfaces").y));
            Assert.That(material.renderQueue, Is.LessThan(3000));
        }

        [TestCase(0, 1.65f, .65f, .5f, .48f, 0)]
        [TestCase(0, 1.78f, .2f, .15f, .17f, 1)]
        [TestCase(.3f, 1.35f, .8f, .8f, .8f, 2)]
        [TestCase(.15f, .15f, .1f, .1f, .1f, 3)]
        public void AtlasClassificationSeparatesSkinHairClothAndBoots(float x, float y, float r, float g, float b, int channel)
        {
            Color region = HumanThingsCharacterVisualBuilder.Classify(new Vector3(x, y, 0), new Color(r, g, b), .1f);
            Assert.That(region[channel], Is.GreaterThan(.9f));
            Assert.That(region.r + region.g + region.b + region.a, Is.EqualTo(1).Within(.001f));
        }
    }
}
