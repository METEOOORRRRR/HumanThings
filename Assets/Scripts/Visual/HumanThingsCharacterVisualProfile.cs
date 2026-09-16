using UnityEngine;

namespace EarthRecovery
{
    [CreateAssetMenu(menuName = "HumanThings/Character Visual Profile")]
    public sealed class HumanThingsCharacterVisualProfile : ScriptableObject
    {
        public GameObject visualPrefab;
        public HumanThingsVisualProfile environmentLighting;
        [Header("Palette")]
        [Range(0, 1)] public float saturation = .65f;
        [Range(.3f, 1.3f)] public float brightness = .82f;
        public Color tint = new(.93f, .95f, .96f);
        [Range(0, 1)] public float whiteCompression = .45f;
        [Range(0, .06f)] public float charcoalFloor = .032f;
        [Header("Skin and hair")]
        [Range(0, 1)] public float skinSaturation = .88f;
        [Range(.5f, 1.2f)] public float skinBrightness = 1.06f;
        [Range(0, .5f)] public float skinSmoothness = .16f;
        [Range(0, .5f)] public float hairSmoothness = .12f;
        [Header("Cloth and equipment")]
        [Range(0, .5f)] public float clothSmoothness = .1f;
        [Range(0, .5f)] public float equipmentSmoothness = .3f;
        [Range(0, 1)] public float metallicMultiplier = .28f;
        [Range(0, 1)] public float normalStrength = .8f;
        [Range(0, .2f)] public float detailStrength = .08f;
        [Header("Local weathering")]
        public Color dustColor = new(.27f, .285f, .28f);
        [Range(0, 1)] public float dirtStrength = .3f;
        [Range(0, 1)] public float dustStrength = .22f;
        [Range(0, 1)] public float wearStrength = .1f;
        [Range(0, .5f)] public float cavityStrength = .12f;
        [Range(0, 1)] public float wetness;

        public void Apply(Material material)
        {
            if (environmentLighting != null)
            {
                material.SetColor("_AmbientSky", environmentLighting.ambientSky);
                material.SetColor("_AmbientEquator", environmentLighting.ambientEquator);
                material.SetColor("_AmbientGround", environmentLighting.ambientGround);
            }
            material.SetFloat("_Saturation", saturation);
            material.SetFloat("_Brightness", brightness);
            material.SetColor("_PaletteTint", tint);
            material.SetFloat("_WhiteCompression", whiteCompression);
            material.SetFloat("_ValueFloor", charcoalFloor);
            material.SetVector("_SkinHair", new Vector4(skinSaturation, skinBrightness, skinSmoothness, hairSmoothness));
            material.SetVector("_Surfaces", new Vector4(clothSmoothness, equipmentSmoothness, metallicMultiplier, normalStrength));
            material.SetFloat("_DetailStrength", detailStrength);
            material.SetColor("_DustColor", dustColor);
            material.SetVector("_Weather", new Vector4(dirtStrength, dustStrength, wearStrength, wetness));
            material.SetFloat("_CavityStrength", cavityStrength);
        }
    }
}
