#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace EarthRecovery
{
    public sealed class ArtifactSmoke : MonoBehaviour
    {
        NetworkSession session; GameHud hud; WorldView world;
        string output; bool measured; int savedAspect;
        IEnumerator Start()
        {
            session = GetComponent<NetworkSession>(); hud = GetComponent<GameHud>(); world = GetComponent<WorldView>();
            world.Automated = true; session.PersistArchive = false;
            output = Path.Combine(Application.dataPath, "..", "ArtifactQA"); Directory.CreateDirectory(output);
            savedAspect = DisplayPreferences.Selected; DisplayPreferences.Select(5, false);
            Screen.SetResolution(1596, 896, false); yield return new WaitForSecondsRealtime(1);
            session.rules.developerSolo = true;
            if (!session.Connect(true, "127.0.0.1", 17988)) { Fail("connect"); yield break; }
            float deadline = Time.realtimeSinceStartup + 20;
            while (!session.Online) { if (Time.realtimeSinceStartup > deadline) { Fail("connect timeout"); yield break; } yield return null; }
            session.Send(new Command { action = "ready", flag = true });
            if (!session.StartMission(42)) { Fail("start"); yield break; }
            yield return new WaitForSecondsRealtime(1);
            var state = session.HostGame.State; state.monsters.Clear();
            foreach (var s in state.sites)
                s.product = Enumerable.Range(0, 3).OrderByDescending(i => session.HostGame.Content.locations[s.id].artifacts[i].missionHint.structure.Length
                    + session.HostGame.Content.locations[s.id].artifacts[i].missionHint.function.Length).First();
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080) })
            {
                Screen.SetResolution(size.x, size.y, false); yield return new WaitForSecondsRealtime(.6f);
                hud.ClosePanels(); world.MenuOpen = false; yield return Capture("missions-" + size.x);
                hud.CampOpen = true; yield return Capture("terminal-top-" + size.x);
                SetScroll("terminalMissionScroll", new Vector2(0, 10000)); yield return Capture("terminal-bottom-" + size.x);
                SetScroll("terminalMissionScroll", Vector2.zero);
            }
            hud.ClosePanels();
            foreach (var a in session.HostGame.Content.artifacts)
                state.archive.Add(new ArchiveEntry { artifactId = a.id, discovered = true, recoverCount = 1 });
            state.archive[1].discovered = false; state.archive[1].observations.Add("QA observation");
            var recovered = state.sites.First(s => s.mission); var artifact = session.HostGame.Content.Artifact(recovered);
            session.HostGame.Emit("ArtifactRecovered", recovered.id, artifact.trueNameEn + " / " + artifact.trueNameKo + "\n" + artifact.archiveDescription);
            yield return Capture("recovery");
            state.phase = Phase.Success; yield return new WaitForSecondsRealtime(.4f);
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080) })
            {
                Screen.SetResolution(size.x, size.y, false); yield return new WaitForSecondsRealtime(.6f);
                hud.OpenArchive(); SetScroll("archiveScroll", Vector2.zero); yield return Capture("archive-top-" + size.x);
                SetScroll("archiveScroll", new Vector2(0, 100000)); yield return Capture("archive-bottom-" + size.x);
            }
            if (!measured) { Fail("no text measurements"); yield break; }
            File.WriteAllText(Path.Combine(output, "result.txt"), "PASS: 45 mission text bounds; mission, terminal, archive and recovery captures at 1280/1920; fixture archive, no persistent save.");
            DisplayPreferences.Select(savedAspect, false); Debug.Log("ARTIFACT_QA_PASS"); Application.Quit();
        }
        void OnGUI()
        {
            if (measured || world == null || world.font == null) return;
            var style = new GUIStyle(GUI.skin.label) { font = world.font, fontSize = 14, wordWrap = true };
            foreach (var l in HumanContent.Load().locations)
                foreach (var a in l.artifacts)
                {
                    var hint = Catalog.MissionDetails(new SiteState { missionHint = a.missionHint, missionLocationHint = l.missionLocationHint });
                    var fitted = GameHud.FitText(style, hint, 292, 120);
                    if (fitted.CalcHeight(new GUIContent(hint), 292) > 120.1f) { Fail("overflow " + a.id); return; }
                }
            measured = true;
        }
        void SetScroll(string name, Vector2 value) => typeof(GameHud).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, value);
        IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(.4f); yield return new WaitForEndOfFrame();
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            if (!image.GetPixels32().Any(p => p.r > 50 || p.g > 50 || p.b > 50)) { Destroy(image); Fail("blank " + name); yield break; }
            File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG()); Destroy(image);
        }
        void OnEnable() { Application.logMessageReceived += Log; Invoke(nameof(Timeout), 90); }
        void OnDisable() { Application.logMessageReceived -= Log; }
        void Log(string message, string trace, LogType type) { if (type == LogType.Exception) Fail(message); }
        void Timeout() => Fail("timeout");
        void Fail(string reason)
        {
            if (output != null) File.WriteAllText(Path.Combine(output, "result.txt"), "FAIL: " + reason);
            DisplayPreferences.Select(savedAspect, false); Debug.LogError("ARTIFACT_QA_FAIL " + reason); Application.Quit(2);
        }
    }
}
#endif
