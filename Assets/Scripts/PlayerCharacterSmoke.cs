#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace EarthRecovery
{
    public sealed class PlayerCharacterSmoke : MonoBehaviour
    {
        NetworkSession session;
        WorldView world;
        string output;
        bool finished;

        IEnumerator Start()
        {
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "QA", "CharacterRoster", "Game"));
            Directory.CreateDirectory(output);
            session = GetComponent<NetworkSession>(); world = GetComponent<WorldView>();
            session.PersistArchive = false; session.rules.developerSolo = true; world.Automated = true;
            Screen.SetResolution(1280, 720, false);
            if (!session.Connect(true, "127.0.0.1", 17998)) { Fail("host connection"); yield break; }
            float deadline = Time.realtimeSinceStartup + 20;
            while (!session.Online && Time.realtimeSinceStartup < deadline) yield return null;
            if (!session.Online) { Fail("connection timeout"); yield break; }
            session.Send(new Command { action = "ready", flag = true });
            if (!session.StartMission(20260916)) { Fail("mission start"); yield break; }
            session.HostGame.State.monsters.Clear();
            yield return new WaitForSecondsRealtime(.6f);
            world.MenuOpen = false;
            var catalog = PlayerCharacterCatalog.Load();
            if (catalog == null || catalog.characters.Length < 2) { Fail("character roster missing"); yield break; }
            Debug.Log("CHARACTER_RANDOM_ASSIGNMENT " + session.HostGame.Player(session.LocalId).characterId);
            foreach (var character in catalog.characters)
            {
                session.HostGame.Player(session.LocalId).characterId = character.id;
                yield return new WaitForSecondsRealtime(.3f);
                var body = GameObject.Find("Agent " + session.LocalId);
                var avatar = body == null ? null : body.GetComponentInChildren<PlayerAvatar>();
                if (avatar == null || body.transform.Find("Suit") != null) { Fail("player prefab missing"); yield break; }
                if (avatar.CharacterId != character.id) { Fail("wrong character " + character.id); yield break; }
                var controller = body.GetComponent<CharacterController>(); controller.enabled = false;
                body.transform.position = session.HostGame.Player(session.LocalId).position = new Vector3(-2, 0, -3);
                controller.enabled = true;
                yield return new WaitForSecondsRealtime(.3f);
                var skin = avatar.GetComponentInChildren<SkinnedMeshRenderer>();
                if (!skin.enabled || skin.sharedMaterial.shader.name != "HumanThings/Weathered Character"
                    || skin.sharedMesh != character.Prefab.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh)
                { Fail("skin/material missing"); yield break; }
                yield return Capture(character.id + "-idle"); if (finished) yield break;
                yield return Move(false, .65f);
                if (avatar.Speed < 2) { Fail("walking does not drive animation"); yield break; }
                yield return Capture(character.id + "-walk"); if (finished) yield break;
                yield return Move(true, .55f);
                if (avatar.Speed < 4.5f) { Fail("sprinting does not drive animation"); yield break; }
                yield return Capture(character.id + "-sprint"); if (finished) yield break;
                session.Send(new Command { action = "move", move = Vector2.zero });
                yield return new WaitForSecondsRealtime(.7f);
                if (avatar.Speed > .15f) { Fail("idle transition"); yield break; }
                Debug.Log("CHARACTER_ROSTER_MODEL_PASS " + character.id + " mesh=" + skin.sharedMesh.vertexCount);
            }
            finished = true;
            File.WriteAllText(Path.Combine(output, "player-result.txt"), "PASS: both registered HumanThings characters visible; idle, walking, sprinting and return to idle; shared host controller; development solo fixture.");
            Debug.Log("CHARACTER_PLAYER_PASS");
            session.Leave(); Application.Quit();
        }

        IEnumerator Move(bool sprint, float duration)
        {
            float end = Time.realtimeSinceStartup + duration;
            while (Time.realtimeSinceStartup < end)
            {
                session.Send(new Command { action = "move", move = Vector2.right, yaw = 0, flag = sprint });
                yield return null;
            }
        }

        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                if (image.GetPixels32().Count(c => c.r > 50 || c.g > 50 || c.b > 50) < 1000) { Fail("blank capture"); yield break; }
                File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
            }
            finally { Destroy(image); }
        }

        void OnEnable() { Application.logMessageReceived += Log; Invoke(nameof(Timeout), 90); }
        void OnDisable() { Application.logMessageReceived -= Log; }
        void Timeout() { if (!finished) Fail("timeout"); }
        void Log(string message, string trace, LogType type) { if (!finished && type == LogType.Exception) Fail(message); }
        void Fail(string message)
        {
            if (finished) return;
            finished = true;
            if (output != null) File.WriteAllText(Path.Combine(output, "player-result.txt"), "FAIL: " + message);
            Debug.LogError("CHARACTER_PLAYER_FAIL " + message); Application.Quit(2);
        }
    }
}
#endif
