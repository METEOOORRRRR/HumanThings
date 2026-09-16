using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EarthRecovery.Editor
{
    public static class BuildingCatalogAudit
    {
        public const string Source="Assets/Synty/PolygonCity/Prefabs/Buildings/";
        public const string Output="Docs/BuildingCatalog";
        static Camera camera; static GameObject holder;
        public static void CaptureWorld(CityWorldPlan plan,int seed)
        {
            var world=CityWorldGenerator.Build(plan,EditorSceneManager.GetActiveScene());
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.transform.SetParent(world.root.transform,false);ground.name="Audit ground";
            ground.transform.position=new Vector3(0,-.55f,0);ground.transform.localScale=new Vector3(650,1,650);
            var groundMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=new Color(.24f,.26f,.25f)};ground.GetComponent<Renderer>().sharedMaterial=groundMaterial;
            var image=Render(world.root,2,1600,1100);File.WriteAllBytes(Output+"/World-"+seed+"-Aerial.png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
            var light=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).First(l=>l.type==LightType.Directional);
            var visual=world.root.AddComponent<HumanThingsVisualTreatment>();visual.Apply(Resources.Load<HumanThingsVisualProfile>("HumanThingsVisualProfile"),camera,light);
            camera.GetUniversalAdditionalCameraData().antialiasing=AntialiasingMode.FastApproximateAntialiasing;
            var all=world.chunks.SelectMany(c=>c.city.buildings).ToArray();
            var focus=all.Where(b=>b.root.GetComponent<CityBuildingAssembly>()!=null).OrderByDescending(b=>all.Count(other=>Vector3.Distance(b.root.transform.position,other.root.transform.position)<65)).First();
            var b=focus.root.GetComponentsInChildren<Renderer>()[0].bounds;
            var roads=world.chunks.SelectMany(c=>c.city.roads.Select(r=>(a:r.start+c.node.worldBounds.center,z:r.end+c.node.worldBounds.center))).ToArray();
            Vector3 Closest(Vector3 a,Vector3 z)=>a+(z-a)*Mathf.Clamp01(Vector3.Dot(b.center-a,z-a)/(z-a).sqrMagnitude);
            var at=roads.Select(r=>Closest(r.a,r.z)).OrderBy(v=>(v-b.center).sqrMagnitude).First();
            camera.orthographic=false;camera.fieldOfView=70;
            var away=at-new Vector3(b.center.x,0,b.center.z);
            camera.transform.position=at+away.normalized*10+Vector3.up*3;camera.transform.LookAt(new Vector3(b.center.x,6,b.center.z));
            var rt=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);camera.targetTexture=rt;var old=RenderTexture.active;
            try{VolumeManager.instance.Update(camera.transform,~0);camera.Render();RenderTexture.active=rt;image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(Output+"/World-"+seed+"-Street.png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);}
            finally{camera.targetTexture=null;RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);visual.Restore();UnityEngine.Object.DestroyImmediate(groundMaterial);}
        }
        public static void InspectJoints()
        {
            var report=new StringBuilder();
            foreach(var name in new[]{"OfficeSquare_Base_01","OfficeRound_Base_01","OfficeOctagon_Base_01","OfficeOctagon_Floor_01","OfficeOctagon_Roof_01"})
            {
                var p=AssetDatabase.LoadAssetAtPath<GameObject>(Source+"SM_Bld_"+name+".prefab");
                var ys=p.GetComponentsInChildren<MeshFilter>().SelectMany(f=>f.sharedMesh.vertices.Select(v=>Mathf.Round(f.transform.TransformPoint(v).y*1000)/1000));
                report.AppendLine(name+": "+string.Join(", ",ys.GroupBy(y=>y).OrderByDescending(g=>g.Count()).Take(15).Select(g=>g.Key+"("+g.Count()+")")));
            }
            var scene=EditorSceneManager.OpenScene("Assets/Synty/PolygonCity/Scenes/Demo.unity",OpenSceneMode.Single);
            foreach(var t in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Where(t=>t.name.Contains("OfficeRound") || t.name.Contains("OfficeSquare") || t.name.Contains("OfficeOctagon")).Take(100))
                report.AppendLine(t.name+" position="+t.position.ToString("F3")+" parent="+t.parent?.name);
            File.WriteAllText(Output+"/Joints.txt",report.ToString());
        }
        public static void Inspect()
        {
            Setup(); var report=new StringBuilder();
            var names=AssetDatabase.FindAssets("t:Prefab",new[]{Source.TrimEnd('/')}).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p=>!p.Contains("/Custom/") && (p.Contains("Office") || p.Contains("Apartment")))
                .Where(p=>!p.Contains("Stairs") && !p.Contains("Planter")).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
            for(int page=0;page*4<names.Length;page++)
            {
                var sheet=new Texture2D(1600,1280,TextureFormat.RGB24,false);
                for(int row=0;row<4 && page*4+row<names.Length;row++)
                {
                    var path=names[page*4+row]; var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var b=HumanThingsPlacementMetadata.Measure(prefab);
                    report.AppendLine($"sheet={page:00} row={row+1} {prefab.name} min={b.min:F3} max={b.max:F3}");
                    var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);go.transform.SetParent(holder.transform,false);
                    for(int view=0;view<4;view++)
                    {
                        var tile=Render(go,view,400,320);sheet.SetPixels(view*400,(3-row)*320,400,320,tile.GetPixels());UnityEngine.Object.DestroyImmediate(tile);
                    }
                    UnityEngine.Object.DestroyImmediate(go);
                }
                sheet.Apply();File.WriteAllBytes(Output+"/Source-"+page.ToString("00")+".png",sheet.EncodeToPNG());UnityEngine.Object.DestroyImmediate(sheet);
            }
            File.WriteAllText(Output+"/Sources.txt",report.ToString());
            Debug.Log("BUILDING_SOURCE_AUDIT_PASS count="+names.Length);
        }
        public static void Setup()
        {
            Directory.CreateDirectory(Output);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            holder=new GameObject("Audit only");
            var light=new GameObject("Sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;light.transform.rotation=Quaternion.Euler(45,-30,0);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.6f,.6f,.6f);RenderSettings.fog=false;
            camera=new GameObject("Camera").AddComponent<Camera>();camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.22f,.25f,.28f);
            camera.GetUniversalAdditionalCameraData().antialiasing=AntialiasingMode.FastApproximateAntialiasing;
        }
        public static Texture2D Render(GameObject go,int view,int width=600,int height=600)
        {
            var rs=go.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);
            var dirs=new[]{new Vector3(1,.6f,1),new Vector3(-1,.6f,-1),new Vector3(1,2,-1),new Vector3(-1,-.6f,1)};
            camera.transform.position=b.center+dirs[view].normalized*b.size.magnitude*2;camera.transform.LookAt(b.center);
            camera.aspect=(float)width/height;camera.orthographicSize=b.size.magnitude*.57f;camera.farClipPlane=Mathf.Max(1000,b.size.magnitude*4);
            var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var old=RenderTexture.active;camera.targetTexture=rt;
            var image=new Texture2D(width,height,TextureFormat.RGB24,false);
            try{camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();return image;}
            finally{camera.targetTexture=null;RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
        }
    }
}
