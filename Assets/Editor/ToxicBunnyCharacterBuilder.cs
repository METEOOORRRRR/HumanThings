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
        public const string CleanAlbedo = "Assets/Character/ToxicBunnyClean/ToxicBunny_R31_BaseColor_4K.png";
        public const string VisualMaterial = HumanThingsCharacterVisualBuilder.Folder + "/Materials/HT_ToxicBunny.mat";

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
            ApplyCleanTextures();
            PlayerCharacterBuilder.PreviewCharacter(VisualPrefab, Output, "QA/CharacterRoster/ToxicBunnyVisual");
            HumanThingsCharacterVisualBuilder.CaptureCity(VisualPrefab, VisualProfile, "QA/CharacterRoster/City");
            Debug.Log("CHARACTER_ROSTER_BUILD_PASS count=" + catalog.characters.Length);
        }

        [MenuItem("Tools/HumanThings/Character/Apply Clean Toxic Bunny Textures")]
        public static void ApplyCleanTextures()
        {
            AssetDatabase.ImportAsset(CleanAlbedo, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(CleanAlbedo) as TextureImporter;
            if (importer == null) throw new System.InvalidOperationException("Missing cleaned Toxic Bunny albedo: " + CleanAlbedo);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            var platform = importer.GetPlatformTextureSettings("Standalone");
            platform.overridden = true; platform.maxTextureSize = 4096;
            platform.format = TextureImporterFormat.BC7; platform.compressionQuality = 100;
            importer.SetPlatformTextureSettings(platform);
            importer.SaveAndReimport();

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(CleanAlbedo);
            var material = AssetDatabase.LoadAssetAtPath<Material>(VisualMaterial);
            var profile = AssetDatabase.LoadAssetAtPath<HumanThingsCharacterVisualProfile>(VisualProfile);
            if (texture == null || material == null || profile == null)
                throw new System.InvalidOperationException("Build the Toxic Bunny visual prefab and profile before applying clean textures.");
            // Preserve the original model/material for comparison. Only the in-game
            // variant uses this painting; all regions receive its matte finish.
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetTextureOffset("_BaseMap", Vector2.zero);
            profile.saturation = profile.brightness = profile.skinSaturation = profile.skinBrightness = 1;
            profile.tint = Color.white;
            profile.whiteCompression = profile.charcoalFloor = 0;
            profile.skinSmoothness = profile.hairSmoothness = profile.clothSmoothness = profile.equipmentSmoothness = .18f;
            profile.metallicMultiplier = profile.normalStrength = profile.detailStrength = 0;
            profile.dirtStrength = profile.dustStrength = profile.wearStrength = profile.cavityStrength = profile.wetness = 0;
            profile.Apply(material);
            EditorUtility.SetDirty(material); EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var visual = PlayerCharacterCatalog.Load().Resolve(PlayerCharacterCatalog.ToxicBunnyId).Prefab.GetComponent<HumanThingsCharacterVisual>();
            if (visual.humanThingsMaterial != material || texture.width != 4096 || texture.height != 4096)
                throw new System.InvalidOperationException("Clean texture is not connected to the registered in-game Toxic Bunny.");
            if (AssetDatabase.GetAssetPath(visual.originalMaterial) != Source)
                throw new System.InvalidOperationException("Original Toxic Bunny comparison material must remain unchanged.");
            Debug.Log("TOXIC_BUNNY_CLEAN_TEXTURE_PASS texture=" + AssetDatabase.GetAssetPath(texture)
                + " size=" + texture.width + "x" + texture.height + " material=" + VisualMaterial + " originalPreserved=true");
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
