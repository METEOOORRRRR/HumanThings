using UnityEngine;
namespace EarthRecovery
{
    [CreateAssetMenu(menuName = "Human Things/Restoration Recipe")]
    public sealed class RestorationRecipe : ScriptableObject
    {
        public string id, locationId;
        public int scrapCost = 3;
        public float duration = 3, installDuration = 2;
    }
}
