using System;
using UnityEngine;
namespace EarthRecovery
{
    [Serializable] public sealed class CarryProfile { public float speedMultiplier = 1; public bool twoHanded; public float noiseRadius; }
    [CreateAssetMenu(menuName = "Human Things/Artifact")]
    public sealed class ArtifactDefinition : ScriptableObject
    {
        public string id, locationId, trueNameKo, trueNameEn, categoryTag, materialHint, functionHint, facilityHint;
        public string[] level1Clues, level2Clues;
        public string[] dataTags;
        [TextArea] public string archiveDescription, marsComment;
        public GameObject worldPrefab;
        public CarryProfile carryProfile = new();
        public PuzzleProfile puzzle;
        public MonsterReactionRule[] reactions = Array.Empty<MonsterReactionRule>();
    }
}
