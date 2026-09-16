using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EarthRecovery.Editor
{
    /// <summary>Non-destructive gameplay variants for the five supplied concept characters.</summary>
    public static class AdditionalCharacterBuilder
    {
        public sealed class Definition
        {
            public readonly string key, prefix, id, displayName;
            public Definition(string key, string prefix, string id, string displayName)
            { this.key = key; this.prefix = prefix; this.id = id; this.displayName = displayName; }
            public string Source => "Assets/Character/Meshy_AI_" + key + "_All_Animations.glb";
            public string Output => "Assets/Character/" + prefix + "/Generated";
            public string OriginalPrefab => "Assets/Character/" + prefix + "/PlayerCharacter.prefab";
            public string VisualPrefab => HumanThingsCharacterVisualBuilder.Folder + "/Prefabs/HumanThings_" + prefix + "_Visual.prefab";
            public string Profile => HumanThingsCharacterVisualBuilder.Folder + "/Profiles/" + prefix + ".asset";
            public string Material => HumanThingsCharacterVisualBuilder.Folder + "/Materials/HT_" + prefix + ".mat";
            public string Albedo => "Assets/Character/" + prefix + "Clean/" + prefix + "_BaseColor_4K.png";
        }

        // Names verified against the actual meshes: Ashen is KAI; Wasteland is ASH.
        public static readonly Definition[] Characters =
        {
            new Definition("Tomorrow_s_Sentinel", "TomorrowSentinel", "tomorrows-sentinel", "HAZE"),
            new Definition("Ashen_Sentinel", "AshenSentinel", "ashen-sentinel", "KAI"),
            new Definition("Nova_Ghost_Scout", "NovaGhostScout", "nova-ghost-scout", "NOVA"),
            new Definition("Crimson_Reclaimer", "CrimsonReclaimer", "crimson-reclaimer", "REI"),
            new Definition("Wasteland_Sentinel", "WastelandSentinel", "wasteland-sentinel", "ASH"),
        };

        [MenuItem("Tools/HumanThings/Character/Build Five Clean Characters")]
        public static void Build()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PlayerCharacterCatalog>(ToxicBunnyCharacterBuilder.CatalogPath);
            if (catalog == null) throw new InvalidOperationException("The existing character catalog is required.");
            var entries = new List<PlayerCharacterCatalog.Entry>(catalog.characters);
            foreach (var character in Characters)
            {
                if (!File.Exists(character.Albedo)) throw new FileNotFoundException("Bake the clean texture first.", character.Albedo);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(character.OriginalPrefab) == null)
                    PlayerCharacterBuilder.BuildCharacter(character.Source, character.Output, character.OriginalPrefab, character.displayName);
                Directory.CreateDirectory(Path.GetDirectoryName(character.Profile));
                AssetDatabase.Refresh();
                if (AssetDatabase.LoadAssetAtPath<HumanThingsCharacterVisualProfile>(character.Profile) == null)
                {
                    var profile = Object.Instantiate(AssetDatabase.LoadAssetAtPath<HumanThingsCharacterVisualProfile>(HumanThingsCharacterVisualBuilder.ProfilePath));
                    profile.visualPrefab = null;
                    AssetDatabase.CreateAsset(profile, character.Profile);
                }
                // Region effects are neutralized for these concept-painted albedos.
                HumanThingsCharacterVisualBuilder.BuildVariant(character.OriginalPrefab, character.prefix, character.VisualPrefab,
                    character.Profile, (position, albedo, metallic) => new Color(0, 0, 1, 0));
                ApplyCleanTexture(character);
                var entry = entries.Find(c => c.id == character.id);
                if (entry == null) { entry = new PlayerCharacterCatalog.Entry { id = character.id }; entries.Add(entry); }
                entry.displayName = character.displayName;
                entry.visualProfile = AssetDatabase.LoadAssetAtPath<HumanThingsCharacterVisualProfile>(character.Profile);
                PlayerCharacterBuilder.PreviewCharacter(character.VisualPrefab, character.Output, "QA/CharacterBatch/" + character.key + "/Unity");
                Debug.Log("ADDITIONAL_CHARACTER_BUILD_PASS id=" + character.id + " alias=" + character.displayName);
            }
            catalog.characters = entries.ToArray();
            catalog.Validate(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            Debug.Log("ADDITIONAL_CHARACTER_ROSTER_PASS count=" + catalog.characters.Length);
        }

        [MenuItem("Tools/HumanThings/Character/Refresh Five Clean Textures")]
        public static void ApplyCleanTextures()
        {
            foreach (var character in Characters)
            {
                ApplyCleanTexture(character);
                PlayerCharacterBuilder.PreviewCharacter(character.VisualPrefab, character.Output, "QA/CharacterBatch/" + character.key + "/Unity");
            }
            Debug.Log("ADDITIONAL_CLEAN_TEXTURE_REFRESH_PASS count=" + Characters.Length);
        }

        static void ApplyCleanTexture(Definition character)
        {
            AssetDatabase.ImportAsset(character.Albedo, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(character.Albedo) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing albedo importer: " + character.Albedo);
            importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true;
            importer.mipmapEnabled = true; importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 4;
            importer.maxTextureSize = 4096; importer.textureCompression = TextureImporterCompression.CompressedHQ;
            var platform = importer.GetPlatformTextureSettings("Standalone");
            platform.overridden = true; platform.maxTextureSize = 4096;
            platform.format = TextureImporterFormat.BC7; platform.compressionQuality = 100;
            importer.SetPlatformTextureSettings(platform); importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(character.Albedo);
            var material = AssetDatabase.LoadAssetAtPath<Material>(character.Material);
            var profile = AssetDatabase.LoadAssetAtPath<HumanThingsCharacterVisualProfile>(character.Profile);
            material.SetTexture("_BaseMap", texture); material.SetColor("_BaseColor", Color.white);
            material.SetTextureScale("_BaseMap", Vector2.one); material.SetTextureOffset("_BaseMap", Vector2.zero);
            profile.saturation = profile.brightness = profile.skinSaturation = profile.skinBrightness = 1;
            profile.tint = Color.white; profile.whiteCompression = profile.charcoalFloor = 0;
            profile.skinSmoothness = profile.hairSmoothness = profile.clothSmoothness = profile.equipmentSmoothness = .18f;
            profile.metallicMultiplier = profile.normalStrength = profile.detailStrength = 0;
            profile.dirtStrength = profile.dustStrength = profile.wearStrength = profile.cavityStrength = profile.wetness = 0;
            profile.Apply(material); EditorUtility.SetDirty(material); EditorUtility.SetDirty(profile);
            var visual = profile.visualPrefab.GetComponent<HumanThingsCharacterVisual>();
            if (texture.width != 4096 || texture.height != 4096 || visual.humanThingsMaterial != material
                || AssetDatabase.GetAssetPath(visual.originalMaterial) != character.Source)
                throw new InvalidOperationException("Clean variant must retain the original comparison material and separate 4K albedo.");
            AssetDatabase.SaveAssets();
        }
    }
}
