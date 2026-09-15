using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EarthRecovery.Editor
{
    public static class HumanContentBuilder
    {
        const string Folder = "Assets/Resources/HumanThings";
        [MenuItem("Earth Recovery/Update Artifact Text Data")]
        public static void UpdateTextData()
        {
            var source = HumanContentSource.Parse(File.ReadAllText("Assets/Resources/HumanThingsSeed.json"));
            var db = AssetDatabase.LoadAssetAtPath<HumanContent>("Assets/Resources/HumanContent.asset");
            if (db == null || db.locations.Length != 15 || db.artifacts.Length != 45
                || db.locations.Any(l => l == null) || db.artifacts.Any(a => a == null)
                || db.locations.Select(l => l.id).Distinct().Count() != 15 || db.artifacts.Select(a => a.id).Distinct().Count() != 45)
                throw new InvalidOperationException("Existing content database is incomplete");
            // Validate identities before updating text; do not regenerate prefabs, recipes or layout.
            foreach (var l in source.locations)
            {
                var target = db.locations.SingleOrDefault(x => x.id == l.id);
                if (target == null || target.zone != l.zone || !target.artifacts.Select(a => a.id).SequenceEqual(l.artifactIds))
                    throw new InvalidOperationException("Location mapping changed: " + l.id);
            }
            foreach (var a in source.artifacts)
                if (!db.artifacts.Any(x => x.id == a.id && x.locationId == a.locationId))
                    throw new InvalidOperationException("Artifact identity changed: " + a.id);
            foreach (var l in source.locations)
            {
                var target = db.locations.Single(x => x.id == l.id);
                target.displayNameKnown = l.displayNameKnown; target.missionLocationHint = l.missionLocationHint; target.moduleName = l.moduleName;
                EditorUtility.SetDirty(target);
            }
            foreach (var a in source.artifacts)
            {
                var target = db.artifacts.Single(x => x.id == a.id);
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(a), target);
                EditorUtility.SetDirty(target);
            }
            db.Validate(); AssetDatabase.SaveAssets();
            Debug.Log("ARTIFACT_TEXT_IMPORT_PASS locations=15 artifacts=45 identities=unchanged");
        }
        static T Asset<T>(string path) where T : ScriptableObject
        { var a = AssetDatabase.LoadAssetAtPath<T>(path); if (a == null) { a = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(a, path); } return a; }
        static GameObject Shape(Transform root, string name, Vector3 p, Vector3 scale, PrimitiveType primitive = PrimitiveType.Cube)
        {
            var go = GameObject.CreatePrimitive(primitive); go.name = name; go.transform.SetParent(root); go.transform.localPosition = p; go.transform.localScale = scale; go.layer = 8;
            go.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Greybox.mat"); return go;
        }
        static Transform Anchor(Transform parent, string name, Vector3 p)
        { var t = new GameObject(name).transform; t.SetParent(parent); t.localPosition = p; return t; }
        static GameObject SavePrefab(GameObject go, string name)
        { var prefab = PrefabUtility.SaveAsPrefabAsset(go, Folder + "/Prefabs/" + name + ".prefab"); UnityEngine.Object.DestroyImmediate(go); return prefab; }
        [MenuItem("Earth Recovery/Import Human Things v03")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder + "/Prefabs"); AssetDatabase.Refresh();
            var seed = HumanContentSource.Parse(File.ReadAllText("Assets/Resources/HumanThingsSeed.json"));
            var db = Asset<HumanContent>("Assets/Resources/HumanContent.asset");
            var puzzles = new PuzzleProfile[3];
            for (int i = 0; i < 3; i++) { puzzles[i] = Asset<PuzzleProfile>(Folder + "/Puzzle" + i + ".asset"); puzzles[i].puzzleType = (PuzzleKind)i; EditorUtility.SetDirty(puzzles[i]); }
            db.locations = new LocationDefinition[seed.locations.Length]; db.artifacts = new ArtifactDefinition[seed.artifacts.Length];
            for (int i = 0; i < seed.artifacts.Length; i++)
            {
                var data = seed.artifacts[i]; var a = Asset<ArtifactDefinition>(Folder + "/" + data.id + ".asset");
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(data), a);
                a.carryProfile = new CarryProfile { twoHanded = data.trueNameEn is "Piano" or "Drum Set" or "Tire", speedMultiplier = data.trueNameEn == "Piano" ? .7f : 1 };
                var go = new GameObject("Unidentified specimen"); var visual = Shape(go.transform, "Specimen shell", Vector3.zero, Vector3.one * .7f, (PrimitiveType)(i % 3));
                foreach (var c in go.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
                go.AddComponent<Rigidbody>().isKinematic = true; go.AddComponent<ArtifactRuntime>().definition = a;
                a.worldPrefab = SavePrefab(go, data.id); db.artifacts[i] = a; EditorUtility.SetDirty(a);
            }
            string[][] tags = {
                new[]{"Chair","DiningTable"}, new[]{"Chair","DiningTable","QueuePoint"}, new[]{"StreetLight"},
                new[]{"HomeAnchor","Chair"}, new[]{"EntranceLight","HomeAnchor"}, new[]{"Convenience","QueuePoint"},
                new[]{"Office","StaffRoom"}, new[]{"Station","StationGate"}, new[]{"Office","Chair"},
                new[]{"PatientRoom","HospitalGuideLight","QueuePoint"}, new[]{"StaffRoom","EntranceLight"}, new[]{"Station","StreetLight"},
                new[]{"Chair","HomeAnchor"}, new[]{"Apartment","QueuePoint"}, new[]{"Apartment","HomeAnchor","Chair"}
            };
            for (int i = 0; i < db.locations.Length; i++)
            {
                var data = seed.locations[i]; var l = Asset<LocationDefinition>(Folder + "/" + data.id + ".asset");
                l.id = data.id; l.zone = data.zone; l.displayNameKnown = data.displayNameKnown; l.missionLocationHint = data.missionLocationHint; l.moduleName = data.moduleName;
                l.artifacts = data.artifactIds.Select(id => db.artifacts.First(a => a.id == id)).ToArray(); l.puzzle = puzzles[i % 3];
                l.recipe = Asset<RestorationRecipe>(Folder + "/MOD_" + data.id + ".asset"); l.recipe.id = "MOD_" + data.id; l.recipe.locationId = data.id; EditorUtility.SetDirty(l.recipe);
                var go = new GameObject(data.id); var c = go.AddComponent<LocationController>(); c.definition = l;
                Shape(go.transform, "Floor", new Vector3(0, -.02f, 0), new Vector3(10, .08f, 10));
                Shape(go.transform, "Rear", new Vector3(0, 2, 5), new Vector3(10, 4, .4f));
                foreach (int sign in new[]{-1,1})
                { Shape(go.transform, "Side", new Vector3(sign*5,2,0), new Vector3(.4f,4,10)); Shape(go.transform, "Front", new Vector3(sign*3.5f,2,-5), new Vector3(3,4,.4f)); }
                Shape(go.transform, "Console", new Vector3(0,.65f,2), new Vector3(2,1.3f,1));
                c.console = Anchor(go.transform, "FacilityConsole", new Vector3(0, 1.5f, 2)); c.artifactSpawn = Anchor(go.transform, "ArtifactSpawn", new Vector3(2, .5f, -1));
                c.entrance = Anchor(go.transform, "Entrance", new Vector3(0, 1, -5)); var entrance = c.entrance.gameObject.AddComponent<BoxCollider>(); entrance.isTrigger = true; entrance.size = new Vector3(4,2,1);
                c.powerLight = Anchor(go.transform, "Power light", new Vector3(0,3,0)).gameObject.AddComponent<Light>(); c.powerLight.range = 10; c.powerLight.color = Color.green; c.powerLight.enabled = false;
                for (int t = 0; t < tags[i].Length; t++)
                {
                    var anchor = Anchor(go.transform, tags[i][t], new Vector3(-3 + t * 2, 0, -8)).gameObject.AddComponent<WorldBehaviorAnchor>(); anchor.tagId = tags[i][t];
                    Shape(anchor.transform, "Memory prop", new Vector3(0,.35f,0), new Vector3(.5f,.7f,.5f));
                    if (tags[i][t].Contains("Light")) { var light = anchor.gameObject.AddComponent<Light>(); light.range = 6; light.intensity = .7f; }
                }
                l.prefab = SavePrefab(go, data.id); db.locations[i] = l; EditorUtility.SetDirty(l);
            }
            db.zones = new ZoneDefinition[5];
            string[] ids = {"Commercial","Residential","Industrial","Research","Culture"}; string[] names = {"상업구역","주거구역","산업구역","연구구역","문화구역"};
            for (int i = 0; i < 5; i++)
            {
                var z = Asset<ZoneDefinition>(Folder + "/Zone" + i + ".asset"); z.id = ids[i]; z.displayName = names[i]; z.locations = db.locations.Where(l => l.zone == i).ToArray();
                float angle = i * Mathf.PI * 2 / 5; z.center = new Vector3(Mathf.Sin(angle)*46,0,Mathf.Cos(angle)*46);
                z.spawnSockets = new[]{new Vector3(-12,0,-10),new Vector3(12,0,-10),new Vector3(0,0,12)};
                z.fallbackStation = z.center * .48f; z.stationSockets = new[]{z.fallbackStation, z.fallbackStation + new Vector3(4,0,0),z.fallbackStation + new Vector3(-4,0,0)};
                db.zones[i] = z; EditorUtility.SetDirty(z);
            }
            var station = new GameObject("Restoration Station"); Shape(station.transform, "Machine", Vector3.up, new Vector3(2,2,1));
            var controller = station.AddComponent<StationController>(); controller.moduleOutput = Anchor(station.transform,"ModuleOutput",new Vector3(0,1,-1)); controller.uiAnchor = Anchor(station.transform,"UIAnchor",new Vector3(0,2.5f,0)); station.AddComponent<AudioSource>();
            db.stationPrefab = SavePrefab(station,"RestorationStation");
            var scrap = new GameObject("Scrap"); Shape(scrap.transform,"Scrap",Vector3.up*.3f,Vector3.one*.5f); scrap.AddComponent<ScrapPickup>(); db.scrapPrefab = SavePrefab(scrap,"Scrap");
            var recovery = new GameObject("BaseRecoveryZone"); var box = recovery.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(14,4,14); recovery.AddComponent<RecoveryZone>(); db.recoveryPrefab = SavePrefab(recovery,"BaseRecoveryZone");
            db.monsters = new MonsterDefinition[6];
            string[] codes = {"청취자","잔광","통근자","에코","귀환자","대기자"};
            MonsterKind[] kinds = {MonsterKind.Sound,MonsterKind.Light,MonsterKind.Patrol,MonsterKind.Echo,MonsterKind.Pollution,MonsterKind.Stillness};
            string[] memoryTags = {"Chair","StreetLight","Station","HomeAnchor","HomeAnchor","QueuePoint"};
            for (int i = 0; i < 6; i++)
            {
                var m = Asset<MonsterDefinition>(Folder + "/M0" + (i+1) + ".asset"); m.id = "M0" + (i+1); m.displayCode = m.id + " " + codes[i]; m.kind = kinds[i];
                m.memories = new[]{new MemoryRule {id=m.id+"_memory",requiredWorldTag=memoryTags[i],animationId="Memory"+i, optionalAudioCue=i==3?"Legacy":"", duration=i==4?18:6 }};
                m.routeTags = new[]{"Apartment","Station","Office","Convenience","Apartment"}; m.spawnTags = Array.Empty<string>();
                m.legacyLines = i == 3 ? new[]{"밥 먹었어?","다녀왔어.","문 잠가."} : Array.Empty<string>();
                m.legacyClips = i == 3 ? Enumerable.Range(0,3).Select(n => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/EchoLegacy"+n+".wav")).Where(c => c != null).ToArray() : Array.Empty<AudioClip>();
                m.reactions = Array.Empty<MonsterReactionRule>();
                var go = new GameObject(m.id); Shape(go.transform,"Silhouette",new Vector3(0,1.2f,0),new Vector3(.6f+i*.04f,1.2f,.6f),PrimitiveType.Capsule);
                go.AddComponent<Animator>(); go.AddComponent<AudioSource>(); go.AddComponent<MonsterBrain>(); m.prefab = SavePrefab(go,m.id); db.monsters[i] = m; EditorUtility.SetDirty(m);
            }
            void Reaction(int monster, string artifact, ReactionTrigger trigger, string behavior, bool rain = false, float probability = 1)
            {
                var m = db.monsters[monster]; var a = db.artifacts.First(a => a.id == artifact);
                var rule = new MonsterReactionRule {monsterId=m.id,artifactId=artifact,trigger=trigger,behavior=behavior,observationId=m.id+"_"+artifact,contextTags=rain?new[]{"Rain"}:Array.Empty<string>(),probability=probability};
                m.reactions = m.reactions.Append(rule).ToArray(); a.reactions = a.reactions.Where(r=>r.monsterId!=m.id).Append(rule).ToArray(); EditorUtility.SetDirty(a); EditorUtility.SetDirty(m);
            }
            Reaction(0,"HT_A03_03",ReactionTrigger.Activated,"Listen"); Reaction(1,"HT_A03_02",ReactionTrigger.Flash,"CoverFace",probability:.2f);
            Reaction(2,"HT_B02_03",ReactionTrigger.Equipped,"WaitUnderUmbrella",true); Reaction(3,"HT_D03_01",ReactionTrigger.Activated,"LegacyVoice");
            Reaction(3,"HT_A03_03",ReactionTrigger.Activated,"LegacyVoice"); Reaction(4,"HT_E03_03",ReactionTrigger.PlacedNearby,"LookAtPhotograph");
            db.Validate(); EditorUtility.SetDirty(db); AssetDatabase.SaveAssets(); Debug.Log("HUMAN_CONTENT_PASS zones=5 locations=15 artifacts=45 monsters=6");
        }
    }
}
