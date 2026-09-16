using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EarthRecovery
{
    public sealed class HumanThingsVisualTreatment : MonoBehaviour
    {
        readonly Dictionary<Renderer,Material[]> originals=new();
        readonly Dictionary<(Material,string),Material> overrides=new();
        readonly List<GameObject> additions=new();
        HumanThingsVisualProfile profile;
        Camera eye; Light sun;
        VolumeProfile volumeProfile;
        bool applied, fog, post, hdr;
        Color fogColor, ambientSky, ambientEquator, ambientGround, ambientLight, lightColor, background;
        float fogDensity, ambientIntensity, reflectionIntensity, intensity, shadowStrength, shadowBias, normalBias;
        AmbientMode ambientMode; FogMode fogMode; Material skybox;
        Quaternion rotation; LightShadows shadows; CameraClearFlags clearFlags;
        AntialiasingMode aa;
        TemporalAA.Settings taa;
        SphericalHarmonicsL2 ambientProbe;

        public void Apply(HumanThingsVisualProfile settings, Camera camera, Light directional)
        {
            Restore(); profile=settings; eye=camera; sun=directional;
            if (profile==null || profile.environmentShader==null || eye==null || sun==null) return;
            fog=RenderSettings.fog; fogColor=RenderSettings.fogColor; fogMode=RenderSettings.fogMode; fogDensity=RenderSettings.fogDensity;
            ambientMode=RenderSettings.ambientMode; ambientSky=RenderSettings.ambientSkyColor; ambientEquator=RenderSettings.ambientEquatorColor;
            ambientGround=RenderSettings.ambientGroundColor; ambientLight=RenderSettings.ambientLight; ambientIntensity=RenderSettings.ambientIntensity;
            reflectionIntensity=RenderSettings.reflectionIntensity; skybox=RenderSettings.skybox;
            ambientProbe=RenderSettings.ambientProbe;
            rotation=sun.transform.rotation; lightColor=sun.color; intensity=sun.intensity; shadows=sun.shadows;
            shadowStrength=sun.shadowStrength; shadowBias=sun.shadowBias; normalBias=sun.shadowNormalBias;
            var data=eye.GetUniversalAdditionalCameraData(); post=data.renderPostProcessing; aa=data.antialiasing;
            taa=data.taaSettings;
            hdr=eye.allowHDR; background=eye.backgroundColor; clearFlags=eye.clearFlags;
            applied=true;
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.GetComponent<TMPro.TMP_Text>()!=null || renderer.GetComponent<TextMesh>()!=null) continue;
                var mats=renderer.sharedMaterials; originals[renderer]=mats;
                string category=Category(renderer);
                renderer.sharedMaterials=mats.Select(m=>Override(m,category)).ToArray();
            }
            sun.transform.rotation=Quaternion.Euler(profile.lightRotation); sun.color=profile.lightColor; sun.intensity=profile.lightIntensity;
            sun.shadows=LightShadows.Soft; sun.shadowStrength=profile.shadowStrength; sun.shadowBias=.035f; sun.shadowNormalBias=.25f;
            RenderSettings.ambientMode=AmbientMode.Trilight; RenderSettings.ambientSkyColor=profile.ambientSky;
            RenderSettings.ambientEquatorColor=profile.ambientEquator; RenderSettings.ambientGroundColor=profile.ambientGround;
            RenderSettings.ambientIntensity=1; RenderSettings.reflectionIntensity=.25f; RenderSettings.skybox=null;
            var probe=new SphericalHarmonicsL2(); probe.AddAmbientLight(profile.ambientEquator);
            probe.AddDirectionalLight(Vector3.up,profile.ambientSky,.45f);
            probe.AddDirectionalLight(Vector3.down,profile.ambientGround,.2f); RenderSettings.ambientProbe=probe;
            RenderSettings.fog=true; RenderSettings.fogMode=FogMode.ExponentialSquared;
            RenderSettings.fogColor=profile.fogColor; RenderSettings.fogDensity=profile.fogDensity;
            eye.clearFlags=CameraClearFlags.SolidColor; eye.backgroundColor=profile.fogColor; eye.allowHDR=true;
            data.renderPostProcessing=true; data.antialiasing=AntialiasingMode.TemporalAntiAliasing;
            data.taaSettings=TemporalAA.Settings.Create();
            data.taaSettings.baseBlendFactor=.85f;
            data.taaSettings.contrastAdaptiveSharpening=.1f;
            data.resetHistory=true;
            CreateVolume(); AddFocalLights();
        }
        static string Category(Renderer r)
        {
            string name=r.name.ToLowerInvariant();
            if (name.Contains("road") || name.Contains("sidewalk") || name.Contains("ground") || name.Contains("floor") || name.Contains("platform")) return "Road";
            if (name.Contains("tree") || name.Contains("bush") || name.Contains("grass") || name.Contains("ivy")) return "Vegetation";
            if (name.Contains("veh") || name.Contains("car") || name.Contains("truck") || name.Contains("bus")) return "Vehicle";
            if (name.Contains("metal") || name.Contains("fence") || name.Contains("lamp") || name.Contains("barrier") || name.Contains("sign")) return "Metal";
            if (name.Contains("brick") || name.Contains("shop")) return "Brick";
            return "Concrete";
        }
        Material Override(Material original,string category)
        {
            if (original==null) return null;
            if (overrides.TryGetValue((original,category),out var existing)) return existing;
            bool transparent=original.renderQueue>=3000 || original.GetTag("RenderType",false)=="Transparent";
            var mat=transparent ? new Material(original) : new Material(profile.environmentShader);
            mat.name="HT_"+category+"_"+original.name; mat.hideFlags=HideFlags.DontSave;
            var baseColor=original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor") : original.HasProperty("_Color") ? original.GetColor("_Color") : Color.white;
            if (transparent)
            {
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor",baseColor*new Color(.72f,.8f,.83f,1));
            }
            else
            {
                string key=new[]{"_Albedo_Map","_BaseMap","_MainTex"}.FirstOrDefault(k=>original.HasProperty(k) && original.GetTexture(k)!=null);
                if (key!=null)
                { mat.SetTexture("_BaseMap",original.GetTexture(key)); mat.SetTextureScale("_BaseMap",original.GetTextureScale(key)); mat.SetTextureOffset("_BaseMap",original.GetTextureOffset(key)); }
                mat.SetColor("_BaseColor",baseColor); mat.SetTexture("_DetailMap",profile.detailTexture);
                mat.SetColor("_AmbientSky",profile.ambientSky); mat.SetColor("_AmbientEquator",profile.ambientEquator); mat.SetColor("_AmbientGround",profile.ambientGround);
                mat.SetFloat("_Saturation",profile.materialSaturation); mat.SetFloat("_Brightness",profile.materialBrightness);
                mat.SetFloat("_ValueFloor",category=="Road" ? profile.valueFloor : profile.valueFloor*.45f);
                mat.SetFloat("_Dirt",profile.dirt); mat.SetFloat("_DetailStrength",profile.detailStrength); mat.SetFloat("_DetailNormal",profile.detailNormal);
                mat.SetFloat("_Wetness",category=="Road" ? profile.wetness : category=="Concrete" ? profile.wetness*.2f : 0);
                mat.SetFloat("_Smoothness",category=="Road" ? profile.roadSmoothness : category=="Metal" || category=="Vehicle" ? profile.metalSmoothness : profile.concreteSmoothness);
                mat.SetFloat("_Metallic",category=="Metal" ? .18f : .02f); mat.SetFloat("_Rust",category=="Metal" ? .4f : 0);
                mat.SetColor("_PaletteTint",category switch {"Road"=>profile.roadTint,"Vegetation"=>profile.vegetationTint,"Brick"=>profile.brickTint,"Metal"=>profile.metalTint,_=>profile.concreteTint});
                mat.SetFloat("_Cutoff",original.IsKeywordEnabled("_ALPHATEST_ON") ? .5f : .01f);
                mat.SetFloat("_Cull",original.HasProperty("_Cull") ? original.GetFloat("_Cull") : 2);
                mat.enableInstancing=true;
            }
            overrides.Add((original,category),mat); return mat;
        }
        void CreateVolume()
        {
            var go=new GameObject("HumanThings atmosphere"); go.transform.SetParent(transform,false); additions.Add(go);
            var volume=go.AddComponent<Volume>(); volume.isGlobal=true; volume.priority=50;
            volumeProfile=ScriptableObject.CreateInstance<VolumeProfile>(); volumeProfile.hideFlags=HideFlags.DontSave;
            volume.sharedProfile=volumeProfile;
            var color=volumeProfile.Add<ColorAdjustments>(true); color.postExposure.value=profile.exposure; color.saturation.value=profile.saturation; color.contrast.value=profile.contrast;
            var balance=volumeProfile.Add<WhiteBalance>(true); balance.temperature.value=profile.temperature;
            volumeProfile.Add<Tonemapping>(true).mode.value=TonemappingMode.ACES;
            var bloom=volumeProfile.Add<Bloom>(true); bloom.intensity.value=profile.bloom; bloom.threshold.value=profile.bloomThreshold; bloom.scatter.value=.35f;
            var vignette=volumeProfile.Add<Vignette>(true); vignette.intensity.value=profile.vignette; vignette.smoothness.value=.65f;
        }
        void AddFocalLights()
        {
            if (!profile.localLights) return;
            var marker=GetComponentInChildren<GeneratedWorldRoot>(); if (marker?.result==null) return;
            var spots=new List<Vector3>{new(0,2.8f,1)};
            spots.AddRange(marker.result.specialPOIs.OrderBy(p=>p.id,System.StringComparer.Ordinal).Take(Mathf.Max(0,profile.localLightBudget-1))
                .Select(p=>p.instance.transform.position+p.instance.transform.rotation*new Vector3(0,2.6f,-3.8f)));
            foreach(var at in spots.Take(Mathf.Max(0,profile.localLightBudget)))
            {
                var go=new GameObject("Dim entrance light"); go.transform.SetParent(transform,false); go.transform.position=at; additions.Add(go);
                var lamp=go.AddComponent<Light>(); lamp.type=LightType.Point; lamp.intensity=profile.localLightIntensity;
                lamp.range=profile.localLightRange; lamp.color=profile.localLightColor; lamp.shadows=LightShadows.None;
            }
        }
        public void Restore()
        {
            foreach(var pair in originals) if(pair.Key!=null) pair.Key.sharedMaterials=pair.Value;
            originals.Clear(); foreach(var mat in overrides.Values) Release(mat); overrides.Clear();
            foreach(var go in additions) if(go!=null) {go.SetActive(false); Release(go);} additions.Clear();
            if(volumeProfile!=null) {foreach(var c in volumeProfile.components) Release(c); Release(volumeProfile); volumeProfile=null;}
            if(!applied) return; applied=false;
            RenderSettings.fog=fog; RenderSettings.fogColor=fogColor; RenderSettings.fogMode=fogMode; RenderSettings.fogDensity=fogDensity;
            RenderSettings.ambientMode=ambientMode; RenderSettings.ambientLight=ambientLight; RenderSettings.ambientSkyColor=ambientSky;
            RenderSettings.ambientEquatorColor=ambientEquator; RenderSettings.ambientGroundColor=ambientGround; RenderSettings.ambientIntensity=ambientIntensity;
            RenderSettings.reflectionIntensity=reflectionIntensity; RenderSettings.skybox=skybox;
            RenderSettings.ambientProbe=ambientProbe;
            if(sun!=null) {sun.transform.rotation=rotation; sun.color=lightColor; sun.intensity=intensity; sun.shadows=shadows; sun.shadowStrength=shadowStrength; sun.shadowBias=shadowBias; sun.shadowNormalBias=normalBias;}
            if(eye!=null) {var data=eye.GetUniversalAdditionalCameraData(); data.renderPostProcessing=post; data.antialiasing=aa; data.taaSettings=taa; data.resetHistory=true; eye.allowHDR=hdr; eye.backgroundColor=background; eye.clearFlags=clearFlags;}
        }
        static void Release(Object obj) {if(obj==null)return; if(Application.isPlaying) Destroy(obj); else DestroyImmediate(obj);}
        void OnDisable()=>Restore();
        void OnDestroy()=>Restore();
    }
}
