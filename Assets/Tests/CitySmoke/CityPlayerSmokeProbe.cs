using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace EarthRecovery.Tests
{
    public sealed class CityPlayerSmokeProbe : MonoBehaviour
    {
        IEnumerator Start()
        {
            if (!Environment.GetCommandLineArgs().Contains("--city-smoke")) yield break;
            // Allow the preview component's normal Start generation to finish first.
            yield return null;
            int buildings = 0, roads = 0;
            var generator = GetComponent<RuntimeCityGenerator>();
            try
            {
                if (generator.database.entries.Count != 335) throw new Exception("Database count changed.");
                var a = generator.Generate(); var signature = Signature(a);
                var b = generator.Generate();
                if (signature != Signature(b)) throw new Exception("Non-deterministic runtime result.");
                if (b.buildings.Count == 0 || b.roads.Count < 2) throw new Exception("Empty city in Player.");
                if (b.warnings.Any(w => w.StartsWith("INVALID:"))) throw new Exception("Invalid geometry.");
                int previous = generator.seed; var c = generator.RegenerateWithNewSeed();
                if (previous == generator.seed || signature == Signature(c)) throw new Exception("Regeneration did not change city.");
                buildings = c.buildings.Count; roads = c.roads.Count;
                generator.ClearGenerated();
            }
            catch (Exception e) { Debug.LogError("LOCAL_CITY_PLAYER_FAIL " + e); Application.Quit(1); yield break; }
            yield return null;
            if (CitySceneBuilder.FindGenerated(gameObject.scene).Length != 0)
            { Debug.LogError("LOCAL_CITY_PLAYER_FAIL Clear left generated roots."); Application.Quit(1); yield break; }
            Debug.Log("LOCAL_CITY_PLAYER_PASS database=335 deterministic=true regenerate=true clear=true buildings=" + buildings + " roads=" + roads);
            Application.Quit(0);
        }
        static string Signature(GeneratedCityResult result) => string.Join("|", result.buildings.Select(b => b.assetGuid + b.root.transform.position.ToString("F5")))
            + string.Join("|", result.roads.Select(r => r.start.ToString("F5") + r.end.ToString("F5")));
    }
}
