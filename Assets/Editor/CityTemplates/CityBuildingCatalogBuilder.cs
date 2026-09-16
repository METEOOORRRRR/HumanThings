using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace EarthRecovery.Editor
{
    public static class CityBuildingCatalogBuilder
    {
        public const string Folder="Assets/CityGeneration/Buildings";
        public const string Tag="reviewed-building-assembly";
        static GameObject root; static readonly List<float> joints=new();
        static readonly List<string> paths=new();
        static readonly StringBuilder audit=new();

        public static void ValidateWorlds()
        {
            var settings=RuntimeCitySettings.Load();var report=new StringBuilder();
            foreach(string name in new[]{"CatalogMixed","SparseBlocks"})
            {
                var t=AssetDatabase.LoadAssetAtPath<CityLayoutTemplate>(CityTemplateAutoBuilder.DefaultFolder+"/"+name+".asset");
                if(t.buildings.residentialRatio.max>0)continue;
                if(!CityTemplateAutoBuilder.Unmodified(t))throw new InvalidOperationException("Edited template protected: "+name);
                t.buildings.residentialRatio=new FloatRange(.18f,.22f);
                t.buildings.commercialRatio=new FloatRange(t.buildings.commercialRatio.min*.8f,t.buildings.commercialRatio.max*.8f);
                t.buildings.officeRatio=new FloatRange(t.buildings.officeRatio.min*.8f,t.buildings.officeRatio.max*.8f);
                t.buildings.publicRatio=new FloatRange(t.buildings.publicRatio.min*.8f,t.buildings.publicRatio.max*.8f);
                t.assetBasis+="\nReviewed building catalogue: 20 supplementary assemblies; residential weight 0.18-0.22 enabled. Existing roads, lot sizes and density preserved.";
                t.generatedContentHash=CityTemplateAutoBuilder.ContentHash(t);EditorUtility.SetDirty(t);
            }
            AssetDatabase.SaveAssets();
            foreach(int seed in new[]{100,200,300,400,500})
            {
                BuildingCatalogAudit.Setup();settings=RuntimeCitySettings.Load();
                var plan=settings.Generate(seed);var repeat=settings.Generate(seed);
                var validation=CityWorldValidation.Validate(plan);
                if(!validation.passed)throw new InvalidOperationException(string.Join(";",validation.errors));
                var placed=plan.chunkPlans.Values.SelectMany(p=>p.placement.placements).Where(p=>p.group=="Buildings").ToArray();
                var second=repeat.chunkPlans.Values.SelectMany(p=>p.placement.placements).Where(p=>p.group=="Buildings").ToArray();
                if(!placed.Select(p=>p.asset.assetGuid+":"+p.position.ToString("R")).SequenceEqual(second.Select(p=>p.asset.assetGuid+":"+p.position.ToString("R"))))throw new InvalidOperationException("Non-deterministic buildings.");
                report.AppendLine("SEED "+seed+" buildings="+placed.Length+" unique="+placed.Select(p=>p.asset.assetGuid).Distinct().Count()+" POIs="+plan.specialPOIs.Count);
                foreach(var group in placed.GroupBy(p=>p.asset.prefab.GetComponent<CityBuildingAssembly>()?.family ?? p.asset.subCategory))report.AppendLine("  "+group.Key+"="+group.Count());
                foreach(var group in placed.GroupBy(p=>p.asset.displayName))report.AppendLine("    "+group.Key+"="+group.Count());
                BuildingCatalogAudit.CaptureWorld(plan,seed);
            }
            File.WriteAllText(BuildingCatalogAudit.Output+"/WorldValidation.txt",report.ToString());
            Debug.Log("BUILDING_WORLD_PASS seeds=5 deterministic=true");
        }

        public static void RegisterReviewed()
        {
            BuildingCatalogAudit.Setup();var db=AssetDatabase.LoadAssetAtPath<HumanThingsAssetDatabase>("Assets/CityGeneration/CityAssetDatabase.asset");
            var assets=AssetDatabase.FindAssets("t:Prefab",new[]{Folder}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
            if(assets.Length!=20)throw new InvalidOperationException("Expected the 20 visually reviewed assemblies.");
            var report=new StringBuilder();
            foreach(var path in assets)
            {
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                try
                {
                    string check=CheckEnvelope(instance);report.AppendLine(instance.name+" "+check);
                    if(!check.StartsWith("wallMisses=0/") || !check.EndsWith("roofMisses=0/9"))throw new InvalidOperationException(path+" "+check);
                    if(instance.GetComponentsInChildren<Renderer>().Any(r=>r.sharedMaterials.Any(m=>m==null || m.shader==null)))throw new InvalidOperationException("Missing material "+path);
                }
                finally{UnityEngine.Object.DestroyImmediate(instance);}
            }
            foreach(var path in assets)
            {
                var content=PrefabUtility.LoadPrefabContents(path);
                try{content.GetComponent<CityBuildingAssembly>().reviewed=true;PrefabUtility.SaveAsPrefabAsset(content,path);}
                finally{PrefabUtility.UnloadPrefabContents(content);}
                var p=AssetDatabase.LoadAssetAtPath<GameObject>(path);string guid=AssetDatabase.AssetPathToGUID(path);
                var e=db.entries.FirstOrDefault(e=>e.assetGuid==guid);
                if(e==null){e=new HumanThingsAssetEntry();db.entries.Add(e);}
                e.assetGuid=guid;e.assetPath=path;e.prefab=p;e.displayName=p.name;
                HumanThingsPlacementMetadata.Initialize(e);
                e.tags=new(){"building",Tag,p.GetComponent<CityBuildingAssembly>().family.ToLowerInvariant(),e.subCategory=="Apartment"?"아파트":"빌딩"};
            }
            EditorUtility.SetDirty(db);AssetDatabase.SaveAssets();
            File.WriteAllText(BuildingCatalogAudit.Output+"/Registration.txt",report.ToString());
            Debug.Log("BUILDING_REGISTRATION_PASS assemblies=20 usableBuildings="+db.FindAssets(AssetCategory.Building).Count(HumanThingsAssetEligibility.Usable));
        }

        [MenuItem("Tools/HumanThings/Buildings/Build Review Drafts")]
        public static void BuildDrafts()
        {
            BuildingCatalogAudit.Setup();Directory.CreateDirectory(Folder);AssetDatabase.Refresh();paths.Clear();audit.Clear();
            for(int style=1;style<=3;style++) foreach(int width in new[]{10,15}) Apartment(style,width,style+2);
            foreach(int style in new[]{1,2,3,4}) Office("Square",style,1,3.5f);
            foreach(int style in new[]{1,2,4}) Office("Round",style,1,6);
            foreach(int floors in new[]{2,4,6}) Octagon(floors);
            foreach(string size in new[]{"Small","Large"}) foreach(int floors in new[]{2,4}) Old(size,floors);
            File.WriteAllText(BuildingCatalogAudit.Output+"/AssemblyAudit.txt",audit.ToString());
            AssetDatabase.SaveAssets(); Debug.Log("BUILDING_DRAFTS_PASS count="+paths.Count);
        }
        static void Begin(string name,string family)
        {
            root=new GameObject("HT_Building_"+name);root.AddComponent<CityBuildingAssembly>().family=family;joints.Clear();
        }
        static GameObject Part(string name,Vector3 position,float yaw=0)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(BuildingCatalogAudit.Source+"SM_Bld_"+name+".prefab");
            if(prefab==null)throw new InvalidOperationException("Missing source "+name);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);go.transform.SetParent(root.transform,false);
            go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(0,yaw,0);
            return go;
        }
        static void Apartment(int style,int width,int floors)
        {
            Begin($"Apartment_{style}_{width}m_{floors}F","Apartment");
            for(int level=0;level<=floors;level++)
            {
                float y=level*3;joints.Add(y);
                for(int side=0;side<4;side++)
                {
                    float x=side<2?width/2f:-width/2f,z=side==0||side==3?5:-5;
                    string module=level==floors?"Roof_Corner_"+(style==1?1:style==2?3:2).ToString("00")
                        :level==0 && side==0?"Door_Corner_"+(style==1?2:1).ToString("00"):"Corner_"+style.ToString("00");
                    Part("Apartment_"+module,new Vector3(x,y,z),side*90);
                }
                if(width==15) for(int side=0;side<2;side++)
                {
                    string module=level==floors?"Roof_"+style.ToString("00"):style.ToString("00");
                    Part("Apartment_"+module,new Vector3(side==0?2.5f:-2.5f,y,side==0?5:-5),side*180);
                }
            }
            Finish();
        }
        static void Office(string shape,int style,int repeat,float baseHeight)
        {
            Begin($"Office{shape}_{style:00}","Office"+shape);Part("Office"+shape+"_Base_01",Vector3.zero);
            float y=baseHeight;joints.Add(y);
            for(int i=0;i<repeat;i++)
            {
                var body=Part("Office"+shape+"_"+style.ToString("00"),Vector3.up*y);
                y+=HumanThingsPlacementMetadata.Measure(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(body))).size.y;
                joints.Add(y);
            }
            Part("Office"+shape+"_Roof_01",Vector3.up*y);Finish();
        }
        static void Octagon(int floors)
        {
            Begin("OfficeOctagon_"+floors+"F","OfficeOctagon");Part("OfficeOctagon_Base_01",Vector3.zero);
            for(int i=0;i<floors;i++){float y=3.75f+3*i;joints.Add(y);Part("OfficeOctagon_Floor_01",Vector3.up*y);}
            joints.Add(3.75f+3*floors);Part("OfficeOctagon_Roof_01",Vector3.up*joints.Last());Finish();
        }
        static void Old(string size,int floors)
        {
            Begin("OfficeOld_"+size+"_"+floors+"F","OfficeOld");Part("OfficeOld_"+size+"_Base_01",Vector3.zero);
            for(int i=0;i<floors;i++){float y=6+3*i;joints.Add(y);Part("OfficeOld_"+size+"_Floor_01",Vector3.up*y);}
            joints.Add(6+3*floors);Part("OfficeOld_"+size+"_Roof_01",Vector3.up*joints.Last());Finish();
        }
        static void Finish()
        {
            try
            {
                var marker=root.GetComponent<CityBuildingAssembly>();marker.joints=joints.ToArray();
                foreach(var collider in root.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(collider);
                var bounds=HumanThingsPlacementMetadata.Measure(root);
                var box=root.AddComponent<BoxCollider>();box.center=bounds.center;box.size=bounds.size;
                string path=Folder+"/"+root.name+".prefab";
                if(AssetDatabase.LoadAssetAtPath<GameObject>(path)?.GetComponent<CityBuildingAssembly>()?.reviewed==true)
                    throw new InvalidOperationException("Reviewed prefab protected; create a new draft name: "+path);
                // Drafts cannot enter the runtime catalogue until explicit review registration.
                PrefabUtility.SaveAsPrefabAsset(root,path);paths.Add(path);
                audit.AppendLine(root.name+" bounds="+bounds.size.ToString("F3")+" joints="+string.Join(",",joints)+" "+CheckEnvelope(root));
                var sheet=new Texture2D(1600,400,TextureFormat.RGB24,false);
                for(int view=0;view<4;view++){var tile=BuildingCatalogAudit.Render(root,view,400,400);sheet.SetPixels(view*400,0,400,400,tile.GetPixels());UnityEngine.Object.DestroyImmediate(tile);}
                sheet.Apply();File.WriteAllBytes(BuildingCatalogAudit.Output+"/"+root.name+".png",sheet.EncodeToPNG());UnityEngine.Object.DestroyImmediate(sheet);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        public static string CheckEnvelope(GameObject go)
        {
            var boxes=go.GetComponentsInChildren<Collider>();foreach(var box in boxes)box.enabled=false;
            var colliders=new List<MeshCollider>();
            try
            {
                foreach(var f in go.GetComponentsInChildren<MeshFilter>()){var mc=f.gameObject.AddComponent<MeshCollider>();mc.sharedMesh=f.sharedMesh;colliders.Add(mc);}
                Physics.SyncTransforms();var b=HumanThingsPlacementMetadata.Measure(go);int misses=0,total=0;
                var heights=new List<float>{.5f,b.size.y*.25f,b.size.y*.5f,b.size.y*.75f};
                heights.AddRange(go.GetComponent<CityBuildingAssembly>().joints.Where(y=>y>0).SelectMany(y=>new[]{y-.025f,y+.025f}));
                bool Hit(Vector3 origin,Vector3 direction,float distance)=>colliders.Any(c=>c.Raycast(new Ray(origin,direction),out _,distance));
                foreach(float y in heights)for(int side=0;side<16;side++)
                {
                    var dir=Quaternion.Euler(0,side*22.5f,0)*Vector3.forward;var target=new Vector3(b.center.x,y,b.center.z);
                    total++;if(!Hit(target+dir*b.size.magnitude,-dir,b.size.magnitude))misses++;
                }
                int roofMisses=0;for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
                {
                    var at=new Vector3(b.center.x+b.size.x*.2f*x,b.max.y+2,b.center.z+b.size.z*.2f*z);
                    float roofBottom=go.GetComponent<CityBuildingAssembly>().joints.Last()-.5f;
                    if(!Hit(at,Vector3.down,at.y-roofBottom))roofMisses++;
                }
                return $"wallMisses={misses}/{total} roofMisses={roofMisses}/9";
            }
            finally{foreach(var c in colliders)UnityEngine.Object.DestroyImmediate(c);foreach(var box in boxes)box.enabled=true;}
        }
    }
}
