using UnityEngine;
namespace EarthRecovery
{
    [CreateAssetMenu(menuName = "Human Things/Puzzle")]
    public sealed class PuzzleProfile : ScriptableObject
    {
        public PuzzleKind puzzleType;
        public int difficulty = 1, maxAttempts;
        public float failNoiseRadius = 24;
        public GameObject localViewPrefab, baseControlPrefab;
    }
}
