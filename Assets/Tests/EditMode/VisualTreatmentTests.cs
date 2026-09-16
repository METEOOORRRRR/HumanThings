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
        public void DuskHasFixedExposureAndRestoresSkyWithoutDuplicatingLightsOrVolumes()
        {
            var profile=Resources.Load<HumanThingsVisualProfile>("HumanThingsVisualProfile");
            Assert.That(profile.duskSky,Is.True); Assert.That(profile.skyShader,Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(profile.skyShader),Is.False);
            Assert.That(profile.lightRotation.x,Is.InRange(-6f,-2f)); Assert.That(profile.lightIntensity,Is.Zero);
            var root=new GameObject("Dusk test"); var camGO=new GameObject("Camera");var sunGO=new GameObject("Sun");
            var sky=RenderSettings.skybox; var ambient=RenderSettings.ambientSkyColor;
            var camera=camGO.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;
            var sun=sunGO.AddComponent<Light>();sun.type=LightType.Directional;
            var treatment=root.AddComponent<HumanThingsVisualTreatment>();
            try
            {
                treatment.Apply(profile,camera,sun); treatment.Apply(profile,camera,sun);
                Assert.That(root.GetComponentsInChildren<Volume>().Length,Is.EqualTo(1));
                Assert.That(root.GetComponentsInChildren<Light>().Length,Is.Zero);
                Assert.That(RenderSettings.skybox.shader,Is.SameAs(profile.skyShader));
                Assert.That(camera.clearFlags,Is.EqualTo(CameraClearFlags.Skybox));
                Assert.That(HumanThingsHorizonSaturation.Registered(camera),Is.True);
                Assert.That((-sun.transform.forward).y,Is.LessThan(0));
                var volume=root.GetComponentInChildren<Volume>();
                Assert.That(volume.sharedProfile.TryGet<ColorAdjustments>(out var adjustment),Is.True);
                Assert.That(adjustment.postExposure.overrideState,Is.True);
                Assert.That(adjustment.postExposure.value,Is.EqualTo(profile.exposure));
                treatment.Restore();
                Assert.That(RenderSettings.skybox,Is.SameAs(sky));Assert.That(RenderSettings.ambientSkyColor,Is.EqualTo(ambient));
                Assert.That(camera.clearFlags,Is.EqualTo(CameraClearFlags.SolidColor));
                Assert.That(HumanThingsHorizonSaturation.Registered(camera),Is.False);
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(camGO);Object.DestroyImmediate(sunGO);}
        }
        [Test]
        public void HorizonSaturationIsOneDepthMaskedPostToneMappingPass()
        {
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            var features=renderer.rendererFeatures.FindAll(f=>f is HumanThingsHorizonSaturation);
            Assert.That(features.Count,Is.EqualTo(1));
            var feature=(HumanThingsHorizonSaturation)features[0];
            Assert.That(feature.injectionPoint,Is.EqualTo(FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing));
            Assert.That(feature.requirements,Is.EqualTo(UnityEngine.Rendering.Universal.ScriptableRenderPassInput.Depth));
            Assert.That(feature.passMaterial,Is.Not.Null);Assert.That(ShaderUtil.ShaderHasError(feature.passMaterial.shader),Is.False);
            Assert.That(Resources.Load<HumanThingsVisualProfile>("HumanThingsVisualProfile").horizonSaturation,Is.EqualTo(1));
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
