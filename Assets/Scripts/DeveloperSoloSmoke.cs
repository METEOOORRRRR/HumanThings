#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace EarthRecovery
{
    public sealed class DeveloperSoloSmoke : MonoBehaviour
    {
        NetworkSession session;
        string output;
        readonly StringBuilder report = new();
        bool finished;
        IEnumerator Start()
        {
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "QA", "DeveloperSolo", "Runtime"));
            Directory.CreateDirectory(output);
            session = GetComponent<NetworkSession>(); session.PersistArchive = false;
            var world = GetComponent<WorldView>(); world.Automated = true;
            var lobby = GetComponent<ExpeditionLobby>(); var waiting = GetComponent<WaitingRoom>();
            Check(Environment.GetCommandLineArgs().Contains("--dev-solo") && session.FillDeveloperSlots, "Real developer launch enables automatic party");
            Screen.SetResolution(1280, 720, false); yield return new WaitForSecondsRealtime(1);
            GetComponent<MainMenu>().Activate(0);
            Check(lobby.Rooms.Create("개발자 혼자 테스트", 4, false, ""), "Create through normal LAN room flow");
            yield return new WaitForSecondsRealtime(1);
            CheckParty(waiting);
            Check(!waiting.StartButton.interactable, "Real operator still controls readiness");
            yield return Capture("01-waiting-six");
            waiting.ReadyButton.onClick.Invoke(); yield return new WaitForSecondsRealtime(.3f);
            Check(waiting.StartButton.interactable, "Only local ready click enables start");
            waiting.StartButton.onClick.Invoke(); yield return new WaitForSecondsRealtime(2);
            Check(session.View.phase == Phase.Expedition, "Normal start button enters expedition");
            foreach (var player in session.View.players)
            {
                var body = GameObject.Find("Agent " + player.id);
                var avatar = body == null ? null : body.GetComponentInChildren<PlayerAvatar>();
                Check(avatar != null && avatar.CharacterId == player.characterId, "In-world character matches " + player.name);
                Check(body.GetComponent<CharacterController>().enabled, "Active world body for " + player.name);
            }
            session.SendVoice(new byte[800]);
            Check(session.HostGame.Alive == 1, "Idle stand-ins excluded from operator survival count");
            yield return Capture("02-expedition-six");
            session.HostGame.Kill(session.HostGame.Player(session.LocalId), "QA");
            yield return new WaitForSecondsRealtime(.4f);
            Check(session.View.phase == Phase.AllDead, "Solo death ends mission without spectating idle stand-ins");
            session.ReturnToLobby(); yield return new WaitForSecondsRealtime(.6f);
            CheckParty(waiting);
            Check(!session.LocalPlayer.ready && !waiting.StartButton.interactable, "Return resets real readiness only");
            session.Send(new Command { action = "ready", flag = true });
            Check(session.StartMission(42), "Second mission starts with automatic readiness");
            session.Send(new Command { action = "hibernate" });
            Check(session.HostGame.State.phase == Phase.Abandoned, "Solo hibernation remains available");
            session.Leave(); yield return new WaitForSecondsRealtime(.7f);
            Check(session.View.players.Count == 0 && session.CanConnect, "Leaving clears party");
            Check(lobby.Rooms.Create("다시 테스트", 4, false, ""), "Rehost succeeds");
            yield return new WaitForSecondsRealtime(.7f); CheckParty(waiting);
            finished = true;
            File.WriteAllText(Path.Combine(output, "result.txt"), report + "DEVELOPER_SOLO_PASS\n");
            Debug.Log("DEVELOPER_SOLO_PASS"); session.Leave(); Application.Quit();
        }
        void CheckParty(WaitingRoom waiting)
        {
            Check(session.Online && session.View.players.Count == 6, "One operator plus five stand-ins");
            Check(session.View.players.Select(p => p.characterId).Distinct().Count() == 6, "All six characters are unique");
            Check(session.View.players.Count(p => p.developerDummy && p.ready && !p.voiceEnabled) == 5, "Five automatically ready silent stand-ins");
            Check(session.Manager.ConnectedClientsIds.Count == 1, "Only one actual transport connection");
            Check(SnapshotValidation.Valid(session.View, session.LocalId), "Valid six-person snapshot");
            Check(waiting.Visible && waiting.Cards.Count(c => c.gameObject.activeSelf) == 6, "Six visible waiting cards");
            foreach (var p in session.View.players)
            {
                var card = waiting.Cards.Single(c => c.gameObject.activeSelf && c.PlayerId == p.id);
                Check(card.GetComponentInChildren<UnityEngine.UI.RawImage>().texture == waiting.PortraitFor(p.characterId), "Matching portrait for " + p.name);
            }
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG()); }
            finally { Destroy(image); }
        }
        void Check(bool condition, string label)
        {
            if (!condition) { Fail(label); throw new InvalidOperationException(label); }
            report.AppendLine("PASS " + label);
        }
        void OnEnable() { Application.logMessageReceived += Log; Invoke(nameof(Timeout), 100); }
        void OnDisable() { Application.logMessageReceived -= Log; }
        void Timeout() { if (!finished) Fail("timeout"); }
        void Log(string message, string trace, LogType type)
        { if (!finished && (type == LogType.Exception || type == LogType.Error)) Fail(message); }
        void Fail(string message)
        {
            if (finished) return; finished = true;
            if (output != null) File.WriteAllText(Path.Combine(output, "result.txt"), report + "FAIL " + message);
            Debug.LogError("DEVELOPER_SOLO_FAIL " + message); Application.Quit(2);
        }
    }
}
#endif
