#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace EarthRecovery
{
    public sealed class TerminalSmoke : MonoBehaviour
    {
        IEnumerator Start()
        {
            var session = GetComponent<NetworkSession>();
            var hud = GetComponent<GameHud>();
            var world = GetComponent<WorldView>();
            world.Automated = true;
            var output = Path.Combine(Application.dataPath, "..", "TerminalQA");
            Directory.CreateDirectory(output);
            session.Connect(true, "127.0.0.1", 17987);
            float deadline = Time.realtimeSinceStartup + 20;
            while (!session.Online || session.LocalPlayer == null)
            {
                if (Time.realtimeSinceStartup > deadline) { Debug.LogError("TERMINAL_QA_CONNECT_FAILED"); Application.Quit(2); yield break; }
                yield return null;
            }
            // Render fixtures use the real expedition and content, without changing production player-count rules.
            session.HostGame.State.players[0].ready = true;
            for (ulong id = 1; id < 4; id++) session.HostGame.State.players.Add(new PlayerState { id = id, name = "QA", ready = true });
            if (!session.HostGame.Start(20260915)) { Debug.LogError("TERMINAL_QA_START_FAILED"); Application.Quit(3); yield break; }
            yield return new WaitForSecondsRealtime(1);
            session.HostGame.State.monsters.Clear();
            world.MenuOpen = false;
            foreach (var size in new[] { new Vector2Int(1672,941), new Vector2Int(1280,720), new Vector2Int(1920,1080) })
            {
                Screen.SetResolution(size.x, size.y, false);
                yield return new WaitForSecondsRealtime(1);
                hud.CampOpen = true;
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(output, "terminal-" + size.x + ".png"));
                yield return new WaitForSecondsRealtime(.5f);
            }
            Debug.Log("TERMINAL_QA_PASS sites=" + session.View.sites.Count + " missions=" + session.View.sites.Count(s => s.mission));
            Application.Quit();
        }
    }
}
#endif
