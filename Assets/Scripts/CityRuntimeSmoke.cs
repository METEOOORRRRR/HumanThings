#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace EarthRecovery
{
    public sealed class CityRuntimeSmoke : MonoBehaviour
    {
        public NetworkSession session;
        string role, output;
        float started, readyAt = -1;
        bool ready, missionStarted, captured;
        static string Arg(string key, string fallback)
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }
        void Start()
        {
            role = Arg("--city-smoke", "host"); output = Arg("--city-output", Application.persistentDataPath);
            Directory.CreateDirectory(output); started = Time.realtimeSinceStartup;
            session.PersistArchive = false; session.rules.minPlayers = 2; session.world.Automated = true;
            session.UserName = role; session.radio.Muted = true;
            session.Connect(role == "host", "127.0.0.1", 17998);
        }
        void Update()
        {
            if (Time.realtimeSinceStartup - started > 90) { Finish("TIMEOUT " + session.Status, false); return; }
            if (!session.Online) return;
            if (!ready) { session.Send(new Command { action = "ready", flag = true }); ready = true; }
            if (session.IsHost && !missionStarted && session.View.players.Count == 2 && session.View.players.All(p => p.ready))
            {
                missionStarted = true;
                if (!session.StartMission(100)) { Finish(session.Status, false); return; }
            }
            if (session.View.phase != Phase.Expedition || session.world.City == null) return;
            if (session.View.sites.Count != 15 || session.world.City.specialPOIs.Count != 15 || session.View.players.Count != 2)
            { Finish("World count mismatch", false); return; }
            foreach (var s in session.View.sites)
            {
                var poi = session.world.City.specialPOIs.Single(p => p.id == HumanContent.Load().locations[s.id].id);
                if (Vector3.Distance(poi.instance.transform.position, s.position) > .01f) { Finish("Facility mismatch", false); return; }
            }
            if (readyAt < 0)
            {
                readyAt = Time.realtimeSinceStartup; session.world.MenuOpen = false;
                File.WriteAllText(Path.Combine(output, role + "-snapshot.json"), JsonUtility.ToJson(session.View, true));
            }
            if (!captured && Time.realtimeSinceStartup - readyAt > .5f) { Capture(); captured = true; }
            if (Time.realtimeSinceStartup - readyAt > (role == "host" ? 12 : 8))
                Finish("CITY_PLAYER_PASS role=" + role + " seed=100 players=2 facilities=15", true);
        }
        void Capture()
        {
            // Hidden test windows do not have a back buffer that ScreenCapture can read.
            var camera = session.world.eye; var previous = camera.targetTexture; var active = RenderTexture.active;
            var rt = new RenderTexture(1280, 720, 24); var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(output, role + "-ingame.png"), image.EncodeToPNG());
            }
            finally { camera.targetTexture = previous; RenderTexture.active = active; rt.Release(); Destroy(rt); Destroy(image); }
        }
        void Finish(string message, bool success)
        {
            File.WriteAllText(Path.Combine(output, role + "-result.txt"), message);
            Debug.Log(message); enabled = false; Application.Quit(success ? 0 : 1);
        }
    }
}
#endif
