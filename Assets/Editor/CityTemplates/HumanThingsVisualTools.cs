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
    public static class HumanThingsVisualTools
    {
        public const string Folder="Assets/HumanThings/Visual";
        public const string ProfilePath=Folder+"/Resources/HumanThingsVisualProfile.asset";
        public static void FinishPass()
        {
            Setup(); var p=AssetDatabase.LoadAssetAtPath<HumanThingsVisualProfile>(ProfilePath);
            p.detailStrength=.12f; p.detailNormal=.012f; p.roadTint=new Color(.8f,.82f,.83f); p.valueFloor=.105f;
            p.materialSaturation=.3f; p.saturation=-40; p.temperature=-2; p.fogColor=new Color(.395f,.41f,.415f);
            EditorUtility.SetDirty(p); AssetDatabase.SaveAssets(); Capture();
        }
        public static void Refine()
        {
            Setup(); var p=AssetDatabase.LoadAssetAtPath<HumanThingsVisualProfile>(ProfilePath);
            p.ambientSky=new Color(.42f,.46f,.48f); p.ambientEquator=new Color(.28f,.3f,.31f); p.ambientGround=new Color(.13f,.14f,.15f);
            p.fogColor=new Color(.38f,.42f,.43f); p.lightIntensity=1.05f; p.exposure=.1f; p.contrast=9;
            p.materialBrightness=1; p.materialSaturation=.36f; p.dirt=.35f; p.detailStrength=.2f; p.detailNormal=.035f;
            EditorUtility.SetDirty(p); AssetDatabase.SaveAssets(); Capture();
        }
        [MenuItem("Tools/HumanThings/Visual/Create Profile")]
        public static void Setup()
        {
            Directory.CreateDirectory(Folder+"/Resources"); Directory.CreateDirectory(Folder+"/Textures"); AssetDatabase.Refresh();
            var profile=AssetDatabase.LoadAssetAtPath<HumanThingsVisualProfile>(ProfilePath);
            if(profile==null) {profile=ScriptableObject.CreateInstance<HumanThingsVisualProfile>(); AssetDatabase.CreateAsset(profile,ProfilePath);}
            profile.environmentShader=Shader.Find("HumanThings/Weathered Environment");
            const string path=Folder+"/Textures/SurfaceDetail.asset";
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(texture==null)
            {
                texture=new Texture2D(128,128,TextureFormat.RGBA32,true,true) {name="SurfaceDetail",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear};
                var pixels=new Color[128*128];
                for(int y=0;y<128;y++) for(int x=0;x<128;x++)
                    pixels[y*128+x]=new Color(Mathf.PerlinNoise(x*.16f,y*.16f),Mathf.PerlinNoise(17+x*.035f,7+y*.035f),
                        Mathf.PerlinNoise(21+x*.075f,11+y*.021f),Mathf.PerlinNoise(5+x*.35f,32+y*.35f));
                texture.SetPixels(pixels); texture.Apply(true,false); AssetDatabase.CreateAsset(texture,path);
            }
            profile.detailTexture=texture; EditorUtility.SetDirty(profile); AssetDatabase.SaveAssets();
            Debug.Log("VISUAL_PROFILE_READY");
        }

        public static void Capture()
        {
            Setup();
            if(ShaderUtil.ShaderHasError(Shader.Find("HumanThings/Weathered Environment"))) throw new InvalidOperationException("Environment shader compilation failed.");
            Directory.CreateDirectory("Docs/VisualPass");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var runtime=RuntimeCitySettings.Load(); var plan=runtime.Generate(100);
            var world=CityWorldGenerator.Build(plan,scene);
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube); ground.name="Ground";
            ground.transform.SetParent(world.root.transform); ground.transform.position=new Vector3(0,-.55f,0); ground.transform.localScale=new Vector3(650,1,650);
            var groundMat=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=new Color(.29f,.32f,.32f)}; ground.GetComponent<Renderer>().sharedMaterial=groundMat;
            var light=new GameObject("Visual review sun").AddComponent<Light>(); light.type=LightType.Directional;
            light.intensity=1.05f; light.color=new Color(.8f,.88f,.93f); light.transform.rotation=Quaternion.Euler(48,-30,0); light.shadows=LightShadows.Soft;
            RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.38f,.42f,.42f);
            RenderSettings.fog=true; RenderSettings.fogColor=new Color(.3f,.34f,.34f); RenderSettings.fogDensity=.013f; RenderSettings.fogMode=FogMode.ExponentialSquared;
            var camera=new GameObject("Visual review camera").AddComponent<Camera>(); camera.fieldOfView=62; camera.nearClipPlane=.08f; camera.farClipPlane=1200;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=RenderSettings.fogColor; camera.allowHDR=true;
            var treatment=world.root.AddComponent<HumanThingsVisualTreatment>();
            var profile=AssetDatabase.LoadAssetAtPath<HumanThingsVisualProfile>(ProfilePath);
            var buildings=world.chunks.SelectMany(c=>c.city.buildings).ToArray();
            var shop=buildings.Where(b=>BoundsOf(b.root).size.y>12)
                .OrderByDescending(b=>buildings.Count(other=>Vector3.Distance(b.root.transform.position,other.root.transform.position)<80))
                .ThenBy(b=>b.root.transform.position.sqrMagnitude).First();
            var center=BoundsOf(shop.root).center;
            var roads=world.chunks.SelectMany(c=>c.city.roads.Select(r=>(a:r.start+c.node.worldBounds.center,b:r.end+c.node.worldBounds.center))).ToArray();
            Vector3 Closest(Vector3 a,Vector3 b,Vector3 p)=>a+Vector3.Project(p-a,b-a).normalized*Mathf.Clamp(Vector3.Dot(p-a,(b-a).normalized),0,Vector3.Distance(a,b));
            var street=roads.OrderBy(r=>Vector3.Distance(Closest(r.a,r.b,center),center)).First();
            var streetPoint=Closest(street.a,street.b,center); streetPoint.y=0;
            Vector3 front=(streetPoint-new Vector3(center.x,0,center.z)).normalized;
            var views=new[]{
                (name:"Street",at:streetPoint+Vector3.up*2.1f-front*2,look:new Vector3(center.x,3,center.z)),
                (name:"Boulevard",at:Vector3.Lerp(street.a,street.b,.2f)+Vector3.up*2.1f,look:Vector3.Lerp(street.a,street.b,.85f)+Vector3.up*2.1f),
                (name:"Detail",at:Vector3.Lerp(streetPoint,new Vector3(center.x,0,center.z),.35f)+Vector3.up*1.8f,look:new Vector3(center.x,2.2f,center.z)),
                (name:"Overview",at:streetPoint+new Vector3(45,55,-55),look:streetPoint+Vector3.up*4)};
            foreach(var view in views)
            {
                camera.transform.position=view.at; camera.transform.LookAt(view.look);
                Render(camera,"Original-"+view.name);
                var stage=UnityEngine.Object.Instantiate(profile);
                for(int pass=1;pass<=5;pass++)
                {
                    stage.dirt=pass==5?profile.dirt:0; stage.detailStrength=pass==5?profile.detailStrength:0; stage.detailNormal=pass==5?profile.detailNormal:0; stage.wetness=pass==5?profile.wetness:0;
                    treatment.Apply(stage,camera,light);
                    if(pass==1) {light.intensity=1.05f; light.color=new Color(.8f,.88f,.93f); light.transform.rotation=Quaternion.Euler(48,-30,0); RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.38f,.42f,.42f);}
                    if(pass<3) {RenderSettings.fogDensity=.013f; RenderSettings.fogColor=new Color(.3f,.34f,.34f);camera.backgroundColor=RenderSettings.fogColor;}
                    if(pass<4) camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
                    Render(camera,"Pass"+pass+"-"+view.name); treatment.Restore();
                }
                UnityEngine.Object.DestroyImmediate(stage);
            }
            File.WriteAllText("Docs/VisualPass/Profile.json",JsonUtility.ToJson(profile,true));
            File.WriteAllText("Docs/VisualPass/Audit.txt","Pipeline="+GraphicsSettings.currentRenderPipeline.name+"\nColorSpace="+QualitySettings.activeColorSpace+"\nCamera HDR="+camera.allowHDR+"\nOriginal shared shaders:\n"+
                string.Join("\n",world.root.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).GroupBy(m=>m.shader.name).Select(g=>g.Key+": "+g.Count())));
            Debug.Log("VISUAL_CAPTURE_PASS views=4 stages=5 originals=4 layoutSeed=100");
        }
        static Bounds BoundsOf(GameObject go)
        {
            var renderers=go.GetComponentsInChildren<Renderer>(); var b=renderers[0].bounds;foreach(var r in renderers.Skip(1))b.Encapsulate(r.bounds); return b;
        }
        static void Render(Camera camera,string name)
        {
            var rt=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB); var old=RenderTexture.active; camera.targetTexture=rt;
            var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
            try
            {
                VolumeManager.instance.Update(camera.transform,~0);
                camera.Render(); camera.Render(); RenderTexture.active=rt; image.ReadPixels(new Rect(0,0,1600,900),0,0); image.Apply();
                File.WriteAllBytes("Docs/VisualPass/"+name+".png",image.EncodeToPNG());
            }
            finally {camera.targetTexture=null;RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);}
        }
    }

    public sealed class HumanThingsVisualWindow : EditorWindow
    {
        static HumanThingsVisualTreatment active;
        [MenuItem("Tools/HumanThings/Visual/Compare Original and HumanThings")]
        static void Open()=>GetWindow<HumanThingsVisualWindow>("City Visual");
        void OnGUI()
        {
            if(Application.isPlaying) {EditorGUILayout.HelpBox("Use this comparison window outside Play Mode. The game applies its visual profile automatically.",MessageType.Info);return;}
            var profile=AssetDatabase.LoadAssetAtPath<HumanThingsVisualProfile>(HumanThingsVisualTools.ProfilePath);
            EditorGUILayout.ObjectField("Visual Profile",profile,typeof(HumanThingsVisualProfile),false);
            if(GUILayout.Button("Select Profile")) Selection.activeObject=profile;
            if(GUILayout.Button("HumanThings Visual / Refresh"))
            {
                Clear();
                var root=UnityEngine.Object.FindFirstObjectByType<GeneratedWorldRoot>();
                var sun=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l=>l.type==LightType.Directional);
                var camera=Camera.main??UnityEngine.Object.FindFirstObjectByType<Camera>()??SceneView.lastActiveSceneView?.camera;
                if(root==null || camera==null || sun==null || profile==null) {EditorUtility.DisplayDialog("City Visual","Open a generated city scene with a camera and directional light first.","OK");return;}
                active=root.gameObject.AddComponent<HumanThingsVisualTreatment>(); active.hideFlags=HideFlags.DontSave; active.Apply(profile,camera,sun);
                SceneView.RepaintAll();
            }
            if(GUILayout.Button("Original Visual")) Clear();
        }
        void OnDisable()=>Clear();
        public static void Clear() {if(active==null)return;active.Restore();UnityEngine.Object.DestroyImmediate(active);active=null;SceneView.RepaintAll();}
    }
    public sealed class HumanThingsVisualSaveGuard : AssetModificationProcessor
    {
        static string[] OnWillSaveAssets(string[] paths) {HumanThingsVisualWindow.Clear();return paths;}
    }
}
