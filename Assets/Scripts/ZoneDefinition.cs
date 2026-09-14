using UnityEngine;
namespace EarthRecovery
{
    [CreateAssetMenu(menuName = "Human Things/Zone")]
    public sealed class ZoneDefinition : ScriptableObject
    {
        public string id, displayName;
        public LocationDefinition[] locations;
        public Vector3 center;
        public Vector3[] spawnSockets;
        public Vector3[] stationSockets;
        public Vector3 fallbackStation;
        public Vector2 socketFootprint = new(12, 12);
        public string compatibilityTag = "SmallBuilding";
    }
}
