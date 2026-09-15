using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace EarthRecovery.Editor
{
    public static class SpecialPOIAssetBuilder
    {
        public const string Folder = "Assets/CityGeneration/SpecialPOIs";
        [MenuItem("Tools/HumanThings/Create Special POI Test Assets")]
        public static void CreateDefaults() => Configure(CityWorldGeneratorWindow.CreateStandard());

        public static void Configure(CityWorldTemplate template)
        {
            EnsureFolder(Folder); EnsureFolder(Folder + "/Exteriors"); EnsureFolder(Folder + "/Definitions"); EnsureFolder(Folder + "/Materials");
            var db = AssetDatabase.LoadAssetAtPath<SpecialPOIDatabase>(Folder + "/SpecialPOIDatabase.asset");
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<SpecialPOIDatabase>();
                var source = HumanContentSource.Parse(File.ReadAllText("Assets/Resources/HumanThingsSeed.json"));
                for (int i = 0; i < SpecialPOIDatabase.RequiredIds.Length; i++)
                {
                    string id = SpecialPOIDatabase.RequiredIds[i];
                    var location = source.locations.Single(l => l.id == id);
                    var prefab = Exterior(id, i);
                    string path = Folder + "/Definitions/" + id + ".asset";
                    var def = AssetDatabase.LoadAssetAtPath<SpecialPOIDefinition>(path);
                    if (def == null)
                    {
                        def = ScriptableObject.CreateInstance<SpecialPOIDefinition>(); def.id = id; def.displayName = location.displayNameKnown;
                        def.prefab = prefab; def.localForward = Vector3.back;
                        var b = Measure(prefab); def.boundsCenter = b.center; def.boundsSize = b.size; def.footprint = new Vector2(b.size.x, b.size.z);
                        def.tags = new[] { id.Split('_')[0].ToLowerInvariant(), id.ToLowerInvariant() };
                        AssetDatabase.CreateAsset(def, path);
                    }
                    db.definitions.Add(def);
                }
                db.Validate(); AssetDatabase.CreateAsset(db, Folder + "/SpecialPOIDatabase.asset");
            }
            var camp = AssetDatabase.LoadAssetAtPath<BaseCampDefinition>(Folder + "/BaseCampDefinition.asset");
            if (camp == null)
            {
                string path = Folder + "/Exteriors/BaseCamp.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    var root = new GameObject("BaseCamp_TestExterior");
                    try
                    {
                        var material = Material("BaseCamp", new Color(.18f, .4f, .48f));
                        void Box(string name, Vector3 p, Vector3 size)
                        {
                            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name; g.transform.SetParent(root.transform, false);
                            g.transform.localPosition = p; g.transform.localScale = size; g.GetComponent<Renderer>().sharedMaterial = material;
                        }
                        Box("Platform", new Vector3(0, .1f, 0), new Vector3(12, .2f, 12));
                        Box("Shelter", new Vector3(0, 1.7f, 0), new Vector3(8, 3, 8));
                        Box("Roof", new Vector3(0, 3.35f, 0), new Vector3(9, .3f, 9));
                        prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(root); }
                }
                camp = ScriptableObject.CreateInstance<BaseCampDefinition>(); camp.prefab = prefab;
                var b = Measure(prefab); camp.boundsCenter = b.center; camp.boundsSize = b.size;
                AssetDatabase.CreateAsset(camp, Folder + "/BaseCampDefinition.asset");
            }
            if (template.specialPOIDatabase == null) template.specialPOIDatabase = db;
            if (template.baseCamp == null) template.baseCamp = camp;
            template.specialPOISettings ??= new SpecialPOISettings();
            if (template.specialPOISettings.labelFont == null) template.specialPOISettings.labelFont = LabelFont(db);
            EditorUtility.SetDirty(template); AssetDatabase.SaveAssets();
        }

        static GameObject Exterior(string id, int index)
        {
            string path = Folder + "/Exteriors/" + id + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing != null) return existing;
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/HumanThings/Prefabs/" + id + ".prefab");
            if (source == null) throw new InvalidOperationException("Missing original location prefab: " + id);
            var root = new GameObject(id); var visual = new GameObject("Exterior"); visual.transform.SetParent(root.transform, false);
            try
            {
                var color = Color.HSVToRGB(index / 15f, .55f, .72f);
                var material = Material(id, color);
                foreach (var renderer in source.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (renderer.name != "Floor" && renderer.name != "Front" && renderer.name != "Rear" && renderer.name != "Side") continue;
                    var mesh = renderer.GetComponent<MeshFilter>(); if (mesh == null || mesh.sharedMesh == null) continue;
                    var part = new GameObject(renderer.name); part.transform.SetParent(visual.transform, false);
                    part.transform.localPosition = source.transform.InverseTransformPoint(renderer.transform.position);
                    part.transform.localRotation = Quaternion.Inverse(source.transform.rotation) * renderer.transform.rotation;
                    part.transform.localScale = renderer.transform.lossyScale;
                    part.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                    part.AddComponent<MeshRenderer>().sharedMaterial = material;
                    var collider = renderer.GetComponent<BoxCollider>();
                    if (collider != null) { var copy = part.AddComponent<BoxCollider>(); copy.center = collider.center; copy.size = collider.size; }
                }
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static Material Material(string name, Color color)
        {
            string path = Folder + "/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path); if (mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
            AssetDatabase.CreateAsset(mat, path); return mat;
        }
        public static Bounds Measure(GameObject prefab)
        {
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new ArgumentException("Exterior prefab has no Renderer.");
            Bounds bounds = default; bool first = true;
            foreach (var r in renderers)
                for (int i = 0; i < 8; i++)
                {
                    var b = r.localBounds;
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = Quaternion.Inverse(prefab.transform.rotation) * (r.transform.TransformPoint(corner) - prefab.transform.position);
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; } else bounds.Encapsulate(p);
                }
            return bounds;
        }
        static TMP_FontAsset LabelFont(SpecialPOIDatabase db)
        {
            string path = Folder + "/POILabelFont.asset";
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path); if (font != null) return font;
            font = TMP_FontAsset.CreateFontAsset("Batang", "Regular", 64) ?? TMP_FontAsset.CreateFontAsset("Malgun Gothic", "Regular", 64);
            if (font == null) throw new InvalidOperationException("Cannot create Korean debug label font.");
            string chars = "[] BASE CAMP" + string.Concat(db.definitions.Select(d => d.displayName));
            if (!font.TryAddCharacters(chars, out string missing)) throw new InvalidOperationException("Missing label glyphs: " + missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static; font.name = "POILabelFont"; font.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(font, path);
            font.material.hideFlags = HideFlags.None; AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var atlas in font.atlasTextures) { atlas.hideFlags = HideFlags.None; AssetDatabase.AddObjectToAsset(atlas, font); }
            EditorUtility.SetDirty(font); return font;
        }
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path[..path.LastIndexOf('/')]; EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path[(path.LastIndexOf('/') + 1)..]);
        }
    }

    [CustomEditor(typeof(SpecialPOIDefinition))]
    public sealed class SpecialPOIDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var d = (SpecialPOIDefinition)target; var previousPrefab = d.prefab; DrawDefaultInspector();
            using (new EditorGUI.DisabledScope(d.prefab == null)) if (GUILayout.Button("Measure Prefab Bounds / Footprint") || d.prefab != null && d.prefab != previousPrefab)
            {
                Undo.RecordObject(d, "Measure Special POI"); var b = SpecialPOIAssetBuilder.Measure(d.prefab);
                d.boundsCenter = b.center; d.boundsSize = b.size; d.footprint = new Vector2(b.size.x, b.size.z); EditorUtility.SetDirty(d);
            }
        }
    }

    [CustomEditor(typeof(BaseCampDefinition))]
    public sealed class BaseCampDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var d = (BaseCampDefinition)target; var previousPrefab = d.prefab; DrawDefaultInspector();
            using (new EditorGUI.DisabledScope(d.prefab == null)) if (GUILayout.Button("Measure Prefab Bounds") || d.prefab != null && d.prefab != previousPrefab)
            {
                Undo.RecordObject(d, "Measure Base Camp"); var b = SpecialPOIAssetBuilder.Measure(d.prefab);
                d.boundsCenter = b.center; d.boundsSize = b.size; EditorUtility.SetDirty(d);
            }
        }
    }
}
