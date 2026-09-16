using UnityEditor;
using UnityEngine;

namespace EarthRecovery.Editor
{
    public static class ToxicBunnyCharacterBuilder
    {
        public const string Source = "Assets/Character/Meshy_AI_Toxic_Bunny_R_31_All_Animations.glb";
        public const string Output = "Assets/Character/ToxicBunny/Generated";
        public const string OriginalPrefab = "Assets/Character/ToxicBunny/PlayerCharacter.prefab";
        public const string VisualPrefab = HumanThingsCharacterVisualBuilder.Folder + "/Prefabs/HumanThings_ToxicBunny_Visual.prefab";
        public const string VisualProfile = HumanThingsCharacterVisualBuilder.Folder + "/Profiles/ToxicBunny.asset";
        public const string CatalogPath = "Assets/Resources/PlayerCharacterCatalog.asset";

        [MenuItem("Tools/HumanThings/Character/Import Toxic Bunny")]
        public static void Import()
        {
            PlayerCharacterBuilder.BuildCharacter(Source, Output, OriginalPrefab, "Toxic Bunny R-31");
            PlayerCharacterBuilder.PreviewCharacter(OriginalPrefab, Output, "QA/CharacterRoster/ToxicBunnyOriginal");
        }

        [MenuItem("Tools/HumanThings/Character/Build Toxic Bunny Visual and Roster")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(OriginalPrefab) == null) Import();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(VisualProfile));
            AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<HumanThingsCharacterVisualProfile>(VisualProfile) == null)
            {
                var profile = Object.Instantiate(AssetDatabase.LoadAssetAtPath<HumanThingsCharacterVisualProfile>(HumanThingsCharacterVisualBuilder.ProfilePath));
                profile.visualPrefab = null;
                AssetDatabase.CreateAsset(profile, VisualProfile);
            }
            HumanThingsCharacterVisualBuilder.BuildVariant(OriginalPrefab, "ToxicBunny", VisualPrefab, VisualProfile, Classify);
            var catalog = AssetDatabase.LoadAssetAtPath<PlayerCharacterCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<PlayerCharacterCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var entries = new System.Collections.Generic.List<PlayerCharacterCatalog.Entry>(catalog.characters);
            Register(PlayerCharacterCatalog.DefaultId, "Neon Vanguard", HumanThingsCharacterVisualBuilder.ProfilePath);
            Register(PlayerCharacterCatalog.ToxicBunnyId, "Toxic Bunny R-31", VisualProfile);
            void Register(string id, string name, string path)
            {
                var entry = entries.Find(c => c.id == id);
                if (entry == null) { entry = new PlayerCharacterCatalog.Entry { id = id }; entries.Add(entry); }
                entry.displayName = name;
                entry.visualProfile = AssetDatabase.LoadAssetAtPath<HumanThingsCharacterVisualProfile>(path);
            }
            catalog.characters = entries.ToArray(); catalog.Validate();
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            PlayerCharacterBuilder.PreviewCharacter(VisualPrefab, Output, "QA/CharacterRoster/ToxicBunnyVisual");
            HumanThingsCharacterVisualBuilder.CaptureCity(VisualPrefab, VisualProfile, "QA/CharacterRoster/City");
            Debug.Log("CHARACTER_ROSTER_BUILD_PASS count=" + catalog.characters.Length);
        }

        public static Color Classify(Vector3 position, Color albedo, float metallic)
        {
            float x = Mathf.Abs(position.x), y = position.y;
            // Warm exposed skin is distinct from the saturated yellow hood and straps.
            bool skinColor = albedo.r > .3f && albedo.r > albedo.g * 1.07f && albedo.r < albedo.g * 1.9f
                && albedo.b > albedo.g * .65f && albedo.b < albedo.r;
            bool face = y > 1.48f && y < 1.73f && x < .14f && position.z > -.03f;
            bool waist = y > 1.02f && y < 1.28f && x < .18f;
            bool wrist = y > .85f && y < 1.14f && x > .25f;
            if (skinColor && (face || waist || wrist)) return new Color(1, 0, 0, 0);
            bool paleHair = y > 1.48f && y < 1.76f && x < .16f && position.z > -.02f
                && Mathf.Min(albedo.r, albedo.g, albedo.b) > .4f && Mathf.Max(albedo.r, albedo.g, albedo.b) - Mathf.Min(albedo.r, albedo.g, albedo.b) < .12f;
            if (paleHair) return new Color(0, 1, 0, 0);
            float cloth = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.22f, .4f, y))
                * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.35f, .8f, metallic)));
            return new Color(0, 0, cloth, 1 - cloth);
        }
    }
}
