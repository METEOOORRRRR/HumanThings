using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EarthRecovery.Editor
{
    public static class HumanThingsDuskTools
    {
        const string Output="Docs/DuskEnvironment";
        [Serializable]
        sealed class PreviousLighting
        {
            public Vector3 lightRotation;
            public Color lightColor,ambientSky,ambientEquator,ambientGround,fogColor;
            public float lightIntensity,fogDensity,exposure;
        }
        static void ReadPreviousLighting(HumanThingsVisualProfile before)
        {
            // Read values only: Unity object instance IDs do not survive an Editor restart.
            var old=JsonUtility.FromJson<PreviousLighting>(File.ReadAllText(Output+"/BeforeProfile.json"));
            before.duskSky=false; before.lightRotation=old.lightRotation; before.lightColor=old.lightColor;
            before.lightIntensity=old.lightIntensity; before.ambientSky=old.ambientSky;
            before.ambientEquator=old.ambientEquator; before.ambientGround=old.ambientGround;
            before.fogColor=old.fogColor; before.fogDensity=old.fogDensity; before.exposure=old.exposure;
        }
        [MenuItem("Tools/HumanThings/Visual/Sync Character Ambient Lighting")]
        public static void SyncCharacterAmbientLighting()
        {
            foreach(var guid in AssetDatabase.FindAssets("t:HumanThingsCharacterVisualProfile"))
            {
                var p=AssetDatabase.LoadAssetAtPath<HumanThingsCharacterVisualProfile>(AssetDatabase.GUIDToAssetPath(guid));
                var material=p.visualPrefab!=null?p.visualPrefab.GetComponent<HumanThingsCharacterVisual>()?.humanThingsMaterial:null;
                if(material==null || p.environmentLighting==null) continue;
                Undo.RecordObject(material,"Sync character ambient lighting");
                material.SetColor("_AmbientSky",p.environmentLighting.ambientSky);
                material.SetColor("_AmbientEquator",p.environmentLighting.ambientEquator);
                material.SetColor("_AmbientGround",p.environmentLighting.ambientGround);
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
        }
        static void OpenAuditScene()
        {
            var s=EditorSceneManager.GetActiveScene();
            // Unity disallows additive creation beside its blank, untitled startup scene.
            // Replace only a verified clean and empty scene in our batch process.
            bool emptyStartup=Application.isBatchMode && EditorSceneManager.sceneCount==1 && string.IsNullOrEmpty(s.path) && !s.isDirty && s.rootCount==0;
            var auditScene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,emptyStartup?NewSceneMode.Single:NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(auditScene);
        }
        public static void ConfigureAndCapture()
        {
            Directory.CreateDirectory(Output);
            OpenAuditScene();
            var profile=Resources.Load<HumanThingsVisualProfile>("HumanThingsVisualProfile");
            var before=UnityEngine.Object.Instantiate(profile);
            if(!profile.duskSky) File.WriteAllText(Output+"/BeforeProfile.json",JsonUtility.ToJson(profile,true));
            profile.skyShader=Shader.Find("HumanThings/Dusk Sky"); profile.duskSky=true;
            profile.lightRotation=new Vector3(-4,-35,0);
            profile.lightColor=new Color(.92f,.87f,.82f); profile.lightIntensity=0;
            profile.ambientSky=new Color(.4f,.44f,.53f);
            profile.ambientEquator=new Color(.38f,.4f,.46f);
            profile.ambientGround=new Color(.16f,.18f,.23f);
            profile.skyZenith=new Color(.2f,.24f,.33f);
            profile.skyMiddle=new Color(.26f,.225f,.30f);
            profile.skyHorizon=new Color(.25f,.27f,.33f);
            profile.fogColor=new Color(.14f,.17f,.225f); profile.fogDensity=.003f;
            profile.exposure=-.2f;
            EditorUtility.SetDirty(profile); AssetDatabase.SaveAssets();
            SyncCharacterAmbientLighting();
            ReadPreviousLighting(before);
            Capture(profile,before);
            UnityEngine.Object.DestroyImmediate(before);
        }
        public static void CaptureCurrent()
        {
            Directory.CreateDirectory(Output);
            OpenAuditScene();
            var profile=Resources.Load<HumanThingsVisualProfile>("HumanThingsVisualProfile");
            var before=UnityEngine.Object.Instantiate(profile);
            ReadPreviousLighting(before);
            Capture(profile,before); UnityEngine.Object.DestroyImmediate(before);
        }
        public static void InstallSaturationAndCapture()
        {
            const string materialPath="Assets/HumanThings/Visual/HorizonSaturation.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null)
            {
                material=new Material(Shader.Find("Hidden/HumanThings/Horizon Saturation"));
                AssetDatabase.CreateAsset(material,materialPath);
            }
            if(ShaderUtil.ShaderHasError(material.shader))throw new InvalidOperationException("Horizon saturation shader failed.");
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            var feature=renderer.rendererFeatures.OfType<HumanThingsHorizonSaturation>().FirstOrDefault();
            if(feature==null)
            {
                feature=ScriptableObject.CreateInstance<HumanThingsHorizonSaturation>(); feature.name="Horizon saturation";
                AssetDatabase.AddObjectToAsset(feature,renderer);renderer.rendererFeatures.Add(feature);
            }
            feature.passMaterial=material; feature.injectionPoint=FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
            feature.fetchColorBuffer=true; feature.requirements=ScriptableRenderPassInput.Depth;
            feature.SetActive(true);EditorUtility.SetDirty(feature);EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets(); renderer.SetDirty(); CaptureCurrent();
        }
        static void Capture(HumanThingsVisualProfile profile,HumanThingsVisualProfile before)
        {
            if(profile.skyShader==null || ShaderUtil.ShaderHasError(profile.skyShader)) throw new InvalidOperationException("Dusk sky shader failed.");
            var world=CityWorldGenerator.Build(RuntimeCitySettings.Load().Generate(200),EditorSceneManager.GetActiveScene());
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube); ground.name="Ground"; ground.transform.SetParent(world.root.transform,false);
            ground.transform.position=new Vector3(0,-.55f,0); ground.transform.localScale=new Vector3(650,1,650);
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=new Color(.29f,.32f,.32f)};
            ground.GetComponent<Renderer>().sharedMaterial=material;
            var sun=new GameObject("Existing directional light").AddComponent<Light>(); sun.type=LightType.Directional;
            var camera=new GameObject("Environment review camera").AddComponent<Camera>();
            camera.scene=EditorSceneManager.GetActiveScene();
            camera.fieldOfView=70; camera.nearClipPlane=.08f; camera.farClipPlane=1200;
            var treatment=world.root.AddComponent<HumanThingsVisualTreatment>();
            var buildings=world.chunks.SelectMany(c=>c.city.buildings).ToArray();
            var focus=buildings.Where(b=>b.root.GetComponent<CityBuildingAssembly>()!=null)
                .OrderByDescending(b=>buildings.Count(other=>Vector3.Distance(b.root.transform.position,other.root.transform.position)<65)).First();
            var renderers=focus.root.GetComponentsInChildren<Renderer>(); var bounds=renderers[0].bounds;
            foreach(var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
            var roads=world.chunks.SelectMany(c=>c.city.roads.Select(r=>(a:r.start+c.node.worldBounds.center,b:r.end+c.node.worldBounds.center))).ToArray();
            Vector3 Closest(Vector3 a,Vector3 b)=>a+(b-a)*Mathf.Clamp01(Vector3.Dot(bounds.center-a,b-a)/(b-a).sqrMagnitude);
            var road=roads.OrderBy(r=>(Closest(r.a,r.b)-bounds.center).sqrMagnitude).First();
            var at=Closest(road.a,road.b); at.y=0;
            var away=(at-new Vector3(bounds.center.x,0,bounds.center.z)).normalized;
            var sunset=-(Quaternion.Euler(profile.lightRotation)*Vector3.forward); sunset.y=0; sunset.Normalize();
            var views=new[]{
                ("Street",at+away*12+Vector3.up*2.1f,new Vector3(bounds.center.x,7,bounds.center.z)),
                ("Road",Vector3.Lerp(road.a,road.b,.25f)+Vector3.up*2.1f,Vector3.Lerp(road.a,road.b,.85f)+Vector3.up*4),
                ("Afterglow",at+Vector3.up*2.1f,at+sunset*100+Vector3.up*12),
                ("Opposite",at+Vector3.up*2.1f,at-sunset*100+Vector3.up*12),
                ("Camp",new Vector3(0,2.1f,-13),new Vector3(0,2,0))};
            var geometry=world.root.GetComponentsInChildren<MeshFilter>().Select(f=>(f,f.sharedMesh,f.transform.localToWorldMatrix)).ToArray();
            foreach(var view in views)
            foreach(bool dusk in new[]{false,true})
            {
                camera.transform.position=view.Item2;camera.transform.LookAt(view.Item3);
                treatment.Apply(dusk?profile:before,camera,sun);
                camera.GetUniversalAdditionalCameraData().antialiasing=AntialiasingMode.FastApproximateAntialiasing;
                Render(camera,(dusk?"Dusk-":"Before-")+view.Item1);
                treatment.Restore();
            }
            if(geometry.Any(g=>g.f.sharedMesh!=g.sharedMesh || g.f.transform.localToWorldMatrix!=g.localToWorldMatrix)) throw new InvalidOperationException("Geometry changed.");
            File.WriteAllText(Output+"/Audit.txt","Seed=200; identical geometry/placements/cameras in before-after pairs.\nSun elevation=-4; direct intensity=0; no sun disc; fixed post exposure="+profile.exposure+"\nClouds=procedural sky layer (not volumetric); fog=URP exponential squared.\n");
            UnityEngine.Object.DestroyImmediate(material);
            Debug.Log("DUSK_CAPTURE_PASS views=5 geometryUnchanged=true");
        }
        static void Render(Camera camera,string name)
        {
            var rt=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var active=RenderTexture.active;camera.targetTexture=rt;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
            try
            {
                VolumeManager.instance.Update(camera.transform,~0);camera.Render();camera.Render();RenderTexture.active=rt;
                image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());
            }
            finally{camera.targetTexture=null;RenderTexture.active=active;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}
