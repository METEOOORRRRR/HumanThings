using UnityEngine;

namespace EarthRecovery
{
    [ExecuteAlways]
    public sealed class HumanThingsCharacterVisual : MonoBehaviour
    {
        public SkinnedMeshRenderer skin;
        public Material originalMaterial;
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
            ShowingOriginal = original;
            if (skin != null && originalMaterial != null && humanThingsMaterial != null)
                skin.sharedMaterial = original ? originalMaterial : instanceMaterial != null ? instanceMaterial : humanThingsMaterial;
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
