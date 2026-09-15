using UnityEngine;

namespace EarthRecovery
{
    [CreateAssetMenu(menuName = "HumanThings/Base Camp Definition")]
    public sealed class BaseCampDefinition : ScriptableObject
    {
        public GameObject prefab;
        public Vector3 boundsCenter, boundsSize;
        [Min(1)] public float footprintClearMultiplier = 1.3f;
        [Range(0, .2f)] public float chunkClearRatio = .08f;
        public float ClearRadius(float chunkSize) => Mathf.Max(new Vector2(boundsSize.x, boundsSize.z).magnitude * .5f * footprintClearMultiplier, chunkSize * chunkClearRatio);
    }
}
