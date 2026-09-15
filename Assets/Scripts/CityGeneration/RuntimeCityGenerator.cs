using System;
using UnityEngine;

namespace EarthRecovery
{
    public sealed class RuntimeCityGenerator : MonoBehaviour
    {
        public HumanThingsAssetDatabase database;
        public CityLayoutTemplate template;
        public int seed = 1234;
        public bool generateOnStart;
        public GeneratedCityResult Result { get; private set; }
        public event Action<GeneratedCityResult> Generated;

        void Start()
        {
            if (!generateOnStart) return;
            try { Generate(); }
            catch (Exception e) { Debug.LogError("City generation failed: " + e.Message, this); }
        }
        public GeneratedCityResult Generate()
        {
            // Prepare the complete plan before replacing any previously generated root.
            var plan = ProceduralCityGenerator.Generate(template, seed, database);
            var previous = CitySceneBuilder.FindGenerated(gameObject.scene);
            var next = CitySceneBuilder.Build(plan, gameObject.scene);
            foreach (var marker in previous) CitySceneBuilder.DestroyRoot(marker.gameObject);
            Result = next;
            Generated?.Invoke(next);
            return next;
        }
        public GeneratedCityResult RegenerateWithNewSeed()
        {
            int next = BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0);
            if (next == seed) next = unchecked(next + 1);
            seed = next; return Generate();
        }
        public void ClearGenerated() { CitySceneBuilder.Clear(gameObject.scene); Result = null; }
    }
}
