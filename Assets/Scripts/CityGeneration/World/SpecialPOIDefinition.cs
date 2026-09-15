using UnityEngine;

namespace EarthRecovery
{
    [CreateAssetMenu(menuName = "HumanThings/Special POI Definition")]
    public sealed class SpecialPOIDefinition : ScriptableObject
    {
        public string id, displayName;
        public GameObject prefab;
        public Vector3 boundsCenter, boundsSize;
        public Vector2 footprint;
        public bool frontFacesRoad = true;
        public Vector3 localForward = Vector3.back;
        [Min(0)] public float minDistanceFromOtherPOI, minDistanceFromBaseCamp;
        public bool placementEnabled = true;
        public string[] tags;
    }
}
