using UnityEngine;

namespace EarthRecovery
{
    [CreateAssetMenu(menuName="HumanThings/Ruin Surface Profile")]
    public sealed class HumanThingsRuinProfile : ScriptableObject
    {
        public Shader shader;
        public bool enabledByDefault=true;
        [Range(0,1)] public float cracks=.7f;
        [Range(0,1)] public float scorch=.78f;
        [Min(.05f)] public float scale=.8f;
    }
}
