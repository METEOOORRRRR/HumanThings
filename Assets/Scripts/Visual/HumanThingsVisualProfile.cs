using UnityEngine;

namespace EarthRecovery
{
    [CreateAssetMenu(menuName="HumanThings/Visual Profile")]
    public sealed class HumanThingsVisualProfile : ScriptableObject
    {
        public Shader environmentShader;
        public Texture2D detailTexture;
        [Header("Surface palette")]
        [Range(0,1)] public float materialSaturation=.3f;
        [Range(.2f,1.5f)] public float materialBrightness=1;
        [Range(0,.15f)] public float valueFloor=.105f;
        public Color concreteTint=new(.84f,.87f,.88f), roadTint=new(.8f,.82f,.83f), metalTint=new(.73f,.8f,.83f),
            vegetationTint=new(.63f,.7f,.53f), brickTint=new(.86f,.78f,.71f);
        [Header("Weathering")]
        [Range(0,1)] public float dirt=.35f, detailStrength=.12f, wetness=.45f;
        [Range(0,.2f)] public float detailNormal=.012f;
        [Range(0,1)] public float concreteSmoothness=.08f, roadSmoothness=.12f, metalSmoothness=.24f;
        [Header("Overcast light")]
        public Vector3 lightRotation=new(28,-35,0);
        public Color lightColor=new(.86f,.9f,.92f);
        public float lightIntensity=1.05f;
        [Range(0,1)] public float shadowStrength=.8f;
        public Color ambientSky=new(.42f,.46f,.48f), ambientEquator=new(.28f,.3f,.31f), ambientGround=new(.13f,.14f,.15f);
        [Header("Atmosphere")]
        public Color fogColor=new(.395f,.41f,.415f);
        public float fogDensity=.0065f;
        [Header("Post processing")]
        public float exposure=.1f, saturation=-40, contrast=9, temperature=-2;
        public float bloom=.06f, bloomThreshold=1.4f, vignette=.1f;
        [Header("Sparse focal lighting")]
        public bool localLights=true;
        public int localLightBudget=4;
        public float localLightIntensity=1.8f, localLightRange=7;
        public Color localLightColor=new(.82f,.74f,.58f);
    }
}
