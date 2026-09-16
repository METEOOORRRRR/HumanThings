#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;

namespace EarthRecovery
{
    // Opt-in diagnostics only; normal launches do not create this component.
    public sealed class GraphicsUploadProbe : MonoBehaviour
    {
        readonly List<float> frames = new();
        string output;
        bool sampling;
        public static bool Enabled => Environment.GetCommandLineArgs().Contains("--graphics-trace");
        public static string Arg(string key, string fallback)
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            if (!Enabled) return;
            if (int.TryParse(Arg("--upload-mb", "0"), out int size) && size >= 2 && size <= 2047)
                QualitySettings.asyncUploadBufferSize = size;
            var root = new GameObject("Graphics upload diagnostics"); DontDestroyOnLoad(root);
            root.AddComponent<GraphicsUploadProbe>();
            Mark("BeforeSceneLoad api=" + SystemInfo.graphicsDeviceType + " GPU=" + SystemInfo.graphicsDeviceName
                + " asyncMB=" + QualitySettings.asyncUploadBufferSize);
        }
        public static void Mark(string message)
        {
            if (Enabled) Debug.Log("GRAPHICS_TRACE t=" + Time.realtimeSinceStartupAsDouble.ToString("F4", CultureInfo.InvariantCulture) + " " + message);
        }
        IEnumerator Start()
        {
            output = Arg("--graphics-output", Application.persistentDataPath);
            Directory.CreateDirectory(output);
            if (Environment.GetCommandLineArgs().Contains("--graphics-isolate"))
            {
                foreach (string resource in new[] { "GameRules", "PlayerCharacterCatalog", "HumanThingsCharacterVisualProfile", "HumanContent" })
                {
                    Mark("LOAD_BEGIN " + resource);
                    var value = Resources.Load(resource);
                    Mark("LOAD_END " + resource + " found=" + (value != null));
                    yield return new WaitForSecondsRealtime(1);
                    Inventory(resource);
                }
                Application.Quit(); yield break;
            }
            yield return new WaitForSecondsRealtime(2);
            Inventory("startup");
            var session = FindFirstObjectByType<NetworkSession>();
            session.PersistArchive = false; session.rules.developerSolo = true; session.world.Automated = true;
            session.FillDeveloperSlots = false;
            if (!session.Connect(true, "127.0.0.1", 17997)) { Debug.LogError("GRAPHICS_PROBE connection failed"); Application.Quit(1); yield break; }
            float deadline = Time.realtimeSinceStartup + 15;
            while (!session.Online && Time.realtimeSinceStartup < deadline) yield return null;
            if (!session.Online) { Debug.LogError("GRAPHICS_PROBE timeout"); Application.Quit(1); yield break; }
            session.Send(new Command { action = "ready", flag = true });
            Mark("MISSION_BEGIN");
            if (!session.StartMission(100)) { Debug.LogError("GRAPHICS_PROBE start failed"); Application.Quit(1); yield break; }
            Mark("MISSION_RETURN");
            yield return new WaitForSecondsRealtime(2);
            session.world.MenuOpen = false;
            Inventory("mission");
            frames.Clear(); sampling = true;
            yield return new WaitForSecondsRealtime(20);
            sampling = false;
            var sorted = frames.OrderBy(x => x).ToArray();
            string result = "api=" + SystemInfo.graphicsDeviceType + " frames=" + sorted.Length
                + " medianMs=" + sorted[sorted.Length / 2].ToString("F3", CultureInfo.InvariantCulture)
                + " p95Ms=" + sorted[(int)(sorted.Length * .95)].ToString("F3", CultureInfo.InvariantCulture)
                + " maxMs=" + sorted.Last().ToString("F3", CultureInfo.InvariantCulture)
                + " over50ms=" + sorted.Count(x => x > 50);
            File.WriteAllText(Path.Combine(output, "result.txt"), result);
            File.WriteAllLines(Path.Combine(output, "frames-ms.csv"), frames.Select(x => x.ToString("F4", CultureInfo.InvariantCulture)));
            Mark("RESULT " + result);
            session.Leave(); Application.Quit();
        }
        void Update() { if (sampling) frames.Add(Time.unscaledDeltaTime * 1000); }
        void Inventory(string stage)
        {
            var textures = Resources.FindObjectsOfTypeAll<Texture2D>().OrderByDescending(Profiler.GetRuntimeMemorySizeLong);
            File.WriteAllLines(Path.Combine(output, stage + "-textures.tsv"), textures.Select(t =>
                t.name + "\t" + t.width + "x" + t.height + "\t" + t.format + "\tmips=" + t.mipmapCount
                + "\treadable=" + t.isReadable + "\tbytes=" + Profiler.GetRuntimeMemorySizeLong(t)));
            Mark("INVENTORY " + stage);
        }
    }
}
#endif
