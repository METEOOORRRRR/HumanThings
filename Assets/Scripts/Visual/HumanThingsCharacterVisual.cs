using UnityEngine;

namespace EarthRecovery
{
    [ExecuteAlways]
    public sealed class HumanThingsCharacterVisual : MonoBehaviour
    {
        public SkinnedMeshRenderer skin;
        [SerializeField, HideInInspector] string originalMaterialGuid;
        [SerializeField, HideInInspector] long originalMaterialLocalId;
#if UNITY_EDITOR
        Material cachedOriginal;
#endif
        // Keep editor comparison available without a serialized runtime dependency.
        public Material originalMaterial
        {
            get
            {
#if UNITY_EDITOR
                if (cachedOriginal == null && !string.IsNullOrEmpty(originalMaterialGuid))
                    foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(UnityEditor.AssetDatabase.GUIDToAssetPath(originalMaterialGuid)))
                        if (asset is Material material && UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(material, out string guid, out long id)
                            && id == originalMaterialLocalId) { cachedOriginal = material; break; }
                return cachedOriginal;
#else
                return null;
#endif
            }
#if UNITY_EDITOR
            set
            {
                cachedOriginal = value;
                originalMaterialGuid = ""; originalMaterialLocalId = 0;
                if (value != null) UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out originalMaterialGuid, out originalMaterialLocalId);
            }
#endif
        }
        public Material humanThingsMaterial;
        public HumanThingsCharacterVisualProfile profileOverride;
        Material instanceMaterial;
        public bool ShowingOriginal { get; private set; }

        void OnEnable() => Refresh();

        public void Refresh()
        {
            ReleaseOverride();
            if (profileOverride != null && humanThingsMaterial != null)
            {
                instanceMaterial = new Material(humanThingsMaterial) { name = "HT Character Override", hideFlags = HideFlags.HideAndDontSave };
                profileOverride.Apply(instanceMaterial);
            }
            CompareOriginal(ShowingOriginal);
        }

        public void CompareOriginal(bool original)
        {
            var comparison = original ? originalMaterial : null;
            ShowingOriginal = comparison != null;
            if (skin != null && humanThingsMaterial != null)
                skin.sharedMaterial = ShowingOriginal ? comparison : instanceMaterial != null ? instanceMaterial : humanThingsMaterial;
        }

        void OnDisable()
        {
            if (skin != null && humanThingsMaterial != null) skin.sharedMaterial = humanThingsMaterial;
            ShowingOriginal = false;
            ReleaseOverride();
        }

        void ReleaseOverride()
        {
            if (instanceMaterial == null) return;
            if (Application.isPlaying) Destroy(instanceMaterial); else DestroyImmediate(instanceMaterial);
            instanceMaterial = null;
        }
    }
}
