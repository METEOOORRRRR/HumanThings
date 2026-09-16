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
    public static class HumanThingsRuinTools
    {
        public static void Setup()
        {
            const string path="Assets/HumanThings/Visual/Resources/HumanThingsRuinProfile.asset";
            var p=AssetDatabase.LoadAssetAtPath<HumanThingsRuinProfile>(path);
            if(p==null){p=ScriptableObject.CreateInstance<HumanThingsRuinProfile>();AssetDatabase.CreateAsset(p,path);}
            p.shader=Shader.Find("HumanThings/Ruined Environment");
            EditorUtility.SetDirty(p); AssetDatabase.SaveAssets();
        }
        [MenuItem("Tools/HumanThings/Visual/Ruins/Existing Surfaces")]
        static void Existing(){foreach(var r in UnityEngine.Object.FindObjectsByType<HumanThingsRuinSurfaces>(FindObjectsSortMode.None))r.Restore();}
        [MenuItem("Tools/HumanThings/Visual/Ruins/Ruined Surfaces")]
        static void Ruined(){foreach(var r in UnityEngine.Object.FindObjectsByType<HumanThingsRuinSurfaces>(FindObjectsSortMode.None))r.ApplyDefault();}
        public static void Capture()
        {
            Setup();
            if(ShaderUtil.ShaderHasError(Shader.Find("HumanThings/Ruined Environment"))) throw new InvalidOperationException("Ruin shader error.");
            if(ShaderUtil.ShaderHasError(Shader.Find("HumanThings/Weathered Environment"))) throw new InvalidOperationException("Environment shader compilation failed.");
            Directory.CreateDirectory("Docs/RuinSurfaces");
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
            var profile=AssetDatabase.LoadAssetAtPath<HumanThingsVisualProfile>(HumanThingsVisualTools.ProfilePath);
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
                treatment.Apply(profile,camera,light);
                camera.GetUniversalAdditionalCameraData().antialiasing=AntialiasingMode.FastApproximateAntialiasing;
                var ruin=world.root.GetComponent<HumanThingsRuinSurfaces>();
                ruin.Restore();
                var poiMaterials=world.specialPOIs.SelectMany(p=>p.instance.GetComponentsInChildren<Renderer>()).SelectMany(r=>r.sharedMaterials).ToArray();
                var meshes=world.root.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh).ToArray();
                Render(camera,"Before-"+view.name);
                ruin.ApplyDefault();
                if(!ruin.IsApplied) throw new InvalidOperationException("No background surfaces treated.");
                if(!poiMaterials.SequenceEqual(world.specialPOIs.SelectMany(p=>p.instance.GetComponentsInChildren<Renderer>()).SelectMany(r=>r.sharedMaterials))) throw new InvalidOperationException("POI changed.");
                if(!meshes.SequenceEqual(world.root.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh))) throw new InvalidOperationException("Mesh changed.");
                Render(camera,"After-"+view.name);
                ruin.Restore();
                if(ruin.IsApplied) throw new InvalidOperationException("Ruin restore failed.");
                treatment.Restore();
            }
            File.WriteAllText("Docs/RuinSurfaces/Profile.json",JsonUtility.ToJson(profile,true));
            File.WriteAllText("Docs/RuinSurfaces/Audit.txt","Pipeline="+GraphicsSettings.currentRenderPipeline.name+"\nColorSpace="+QualitySettings.activeColorSpace+"\nCamera HDR="+camera.allowHDR+"\nOriginal shared shaders:\n"+
                string.Join("\n",world.root.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).GroupBy(m=>m.shader.name).Select(g=>g.Key+": "+g.Count())));
            Debug.Log("RUIN_CAPTURE_PASS views=4 poiMaterialsUnchanged=true meshesUnchanged=true");
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
                File.WriteAllBytes("Docs/RuinSurfaces/"+name+".png",image.EncodeToPNG());
            }
            finally {camera.targetTexture=null;RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);}
        }
    }

}
