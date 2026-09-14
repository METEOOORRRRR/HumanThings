using UnityEngine;
namespace EarthRecovery
{
    [CreateAssetMenu(menuName = "Human Things/Location")]
    public sealed class LocationDefinition : ScriptableObject
    {
        public string id, displayNameKnown, displayNameUnknown, moduleName;
        public int zone;
        public ArtifactDefinition[] artifacts;
        public RestorationRecipe recipe;
        public PuzzleProfile puzzle;
        public GameObject prefab;
        public Vector2 footprint = new(10, 10);
        public string compatibilityTag = "SmallBuilding";
        public string[] craftAnchorIds = { "FacilityConsole", "ArtifactSpawn" };
        public string[] monsterAffinity;
    }
}
