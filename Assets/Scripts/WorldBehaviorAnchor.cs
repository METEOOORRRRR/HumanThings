using UnityEngine;
namespace EarthRecovery
{
    public sealed class WorldBehaviorAnchor : MonoBehaviour
    {
        public string tagId;
        public string[] allowedMonsterIds;
        public int occupant = -1;
        public float cooldownUntil;
        public bool Allows(string id) => allowedMonsterIds == null || allowedMonsterIds.Length == 0 || System.Array.IndexOf(allowedMonsterIds, id) >= 0;
    }
}
