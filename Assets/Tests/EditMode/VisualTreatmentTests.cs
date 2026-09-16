using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EarthRecovery.Tests
{
    public sealed class VisualTreatmentTests
    {
        [Test]
        public void ProfileAndShaderAreAvailableWithoutEditorAssetLookups()
        {
            var profile=Resources.Load<HumanThingsVisualProfile>("HumanThingsVisualProfile");
            Assert.That(profile,Is.Not.Null); Assert.That(profile.detailTexture,Is.Not.Null);
            Assert.That(profile.environmentShader,Is.Not.Null); Assert.That(ShaderUtil.ShaderHasError(profile.environmentShader),Is.False);
        }
        [Test]
        public void TreatmentRestoresMaterialsLightingAndCameraWithoutChangingGeometry()
        {
            var root=GameObject.CreatePrimitive(PrimitiveType.Cube); var cameraGO=new GameObject("Visual test camera"); var sunGO=new GameObject("Visual test sun");
            var original=new Material(Shader.Find("Universal Render Pipeline/Lit")); var renderer=root.GetComponent<Renderer>(); renderer.sharedMaterial=original;
            var camera=cameraGO.AddComponent<Camera>(); var data=camera.GetUniversalAdditionalCameraData(); data.renderPostProcessing=false;
            data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing; data.taaSettings.baseBlendFactor=.6f;
            var light=sunGO.AddComponent<Light>(); light.type=LightType.Directional;light.intensity=1.7f;
            var mesh=root.GetComponent<MeshFilter>().sharedMesh; var collider=root.GetComponent<Collider>();
            string json=EditorJsonUtility.ToJson(original); var fog=RenderSettings.fogColor;
            var treatment=root.AddComponent<HumanThingsVisualTreatment>();
            try
            {
                treatment.Apply(Resources.Load<HumanThingsVisualProfile>("HumanThingsVisualProfile"),camera,light);
                Assert.That(renderer.sharedMaterial,Is.Not.SameAs(original)); Assert.That(data.renderPostProcessing,Is.True);
                Assert.That(data.antialiasing,Is.EqualTo(AntialiasingMode.TemporalAntiAliasing));
                Assert.That(data.taaSettings.mipBias,Is.Zero);
                Assert.That(root.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(mesh)); Assert.That(root.GetComponent<Collider>(),Is.SameAs(collider));
                Assert.That(EditorJsonUtility.ToJson(original),Is.EqualTo(json));
                treatment.Restore();
                Assert.That(renderer.sharedMaterial,Is.SameAs(original)); Assert.That(data.renderPostProcessing,Is.False);
                Assert.That(data.antialiasing,Is.EqualTo(AntialiasingMode.SubpixelMorphologicalAntiAliasing));
                Assert.That(data.taaSettings.baseBlendFactor,Is.EqualTo(.6f).Within(.001f));
                Assert.That(light.intensity,Is.EqualTo(1.7f));Assert.That(RenderSettings.fogColor,Is.EqualTo(fog));
                Assert.That(root.GetComponentsInChildren<Volume>().Length,Is.Zero);
                treatment.Restore();
            }
            finally {Object.DestroyImmediate(root);Object.DestroyImmediate(cameraGO);Object.DestroyImmediate(sunGO);Object.DestroyImmediate(original);}
        }
    }
}
