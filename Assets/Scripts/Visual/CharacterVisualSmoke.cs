#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;

namespace EarthRecovery
{
    [DefaultExecutionOrder(10000)]
    public sealed class CharacterVisualSmoke : MonoBehaviour
    {
        NetworkSession session;
        WorldView world;
        HumanThingsCharacterVisual visual;
        GameObject body;
        string output;
        bool done, fixedCamera;
        Vector3 cameraAt, cameraLook;
        readonly System.Text.StringBuilder report = new();

        IEnumerator Start()
        {
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "QA", "CharacterVisual", "Game"));
            Directory.CreateDirectory(output);
            Screen.SetResolution(1440, 900, false);
            session = GetComponent<NetworkSession>(); world = GetComponent<WorldView>();
            session.PersistArchive = false; session.rules.developerSolo = true; world.Automated = true;
            if (!session.Connect(true, "127.0.0.1", 17997)) { Fail("connect"); yield break; }
            float deadline = Time.realtimeSinceStartup + 20;
            while (!session.Online && Time.realtimeSinceStartup < deadline) yield return null;
            if (!session.Online) { Fail("connection timeout"); yield break; }
            session.Send(new Command { action = "ready", flag = true });
            if (!session.StartMission(100)) { Fail("mission start"); yield break; }
            session.HostGame.State.monsters.Clear();
            yield return new WaitForSecondsRealtime(1);
            world.MenuOpen = false; GetComponent<GameHud>().enabled = false;
            body = GameObject.Find("Agent " + session.LocalId);
            visual = body.GetComponentInChildren<HumanThingsCharacterVisual>();
            if (visual == null || world.City == null) { Fail("visual/city missing"); yield break; }
            if (!RenderSettings.fog || !world.eye.GetUniversalAdditionalCameraData().renderPostProcessing) { Fail("environment treatment missing"); yield break; }
            report.AppendLine("Seed 100; actual WorldView city, existing fog, sun and post-processing; no environment edits.");
            var buildings = world.City.chunks.SelectMany(c => c.city.buildings).OrderBy(b => b.root.transform.position.sqrMagnitude).ToArray();
            var shop = buildings.First(b => BoundsOf(b.root).size.y > 12);
            var center = BoundsOf(shop.root).center; center.y = 0;
            var roads = world.City.chunks.SelectMany(c => c.city.roads.Select(r => (a: r.start + c.node.worldBounds.center, b: r.end + c.node.worldBounds.center)));
            var point = roads.Select(r => r.a + (r.b - r.a) * Mathf.Clamp01(Vector3.Dot(center - r.a, r.b - r.a) / (r.b - r.a).sqrMagnitude))
                .OrderBy(p => Vector3.Distance(p, center)).First(); point.y = 0;
            if (!NavMesh.SamplePosition(point, out var street, 8, NavMesh.AllAreas)) { Fail("street placement"); yield break; }
            var front = (street.position - center); front.y = 0; front.Normalize();
            foreach (string name in new[] { "StreetFront", "StreetBack", "Close" })
            {
                Place(street.position, name == "StreetBack" ? -front : front);
                Frame(front, name == "Close");
                yield return Pair(name); if (done) yield break;
            }
            bool shaded = false;
            var sun = GetComponentsInChildren<Light>().First(l => l.type == LightType.Directional);
            foreach (var building in buildings.Take(60))
            {
                var bounds = BoundsOf(building.root);
                foreach (var direction in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                {
                    float reach = Mathf.Abs(direction.x) * bounds.extents.x + Mathf.Abs(direction.z) * bounds.extents.z + 1.4f;
                    var candidate = bounds.center + direction * reach; candidate.y = 0;
                    if (!NavMesh.SamplePosition(candidate, out var hit, 1.5f, NavMesh.AllAreas) || hit.position.y > .8f) continue;
                    if (!Physics.Raycast(hit.position + Vector3.up, -sun.transform.forward, 80, 1 << 8)) continue;
                    if (Physics.Linecast(hit.position + Vector3.up * 1.2f, hit.position + direction * 4 + Vector3.up * 1.2f, 1 << 8)) continue;
                    Place(hit.position, direction); Frame(direction, false);
                    report.AppendLine("Building shade verified by sun-direction occlusion at " + hit.position);
                    yield return Pair("Shade"); if (done) yield break;
                    shaded = true; break;
                }
                if (shaded) break;
            }
            if (!shaded) { Fail("no verified shadow location"); yield break; }
            fixedCamera = false; world.eye.fieldOfView = 76; visual.CompareOriginal(false);
            GetComponent<GameHud>().enabled = true;
            yield return new WaitForSecondsRealtime(.3f);
            yield return Capture("Gameplay"); if (done) yield break;
            done = true;
            report.AppendLine("PASS: original/variant front, back, close, verified shade, normal gameplay camera; existing rig and controller.");
            File.WriteAllText(Path.Combine(output, "Result.txt"), report.ToString());
            Debug.Log("CHARACTER_VISUAL_GAME_PASS"); session.Leave(); Application.Quit(0);
        }
        void Place(Vector3 point, Vector3 facing)
        {
            var controller = body.GetComponent<CharacterController>(); controller.enabled = false;
            if (Physics.Raycast(point + Vector3.up, Vector3.down, out var ground, 3, 1 << 8)) point.y = ground.point.y;
            body.transform.position = session.HostGame.Player(session.LocalId).position = point;
            float yaw = Quaternion.LookRotation(facing).eulerAngles.y;
            session.HostGame.Player(session.LocalId).yaw = yaw;
            body.transform.rotation = Quaternion.Euler(0, yaw, 0);
            body.GetComponentInChildren<PlayerAvatar>().facing.localRotation = Quaternion.identity;
            controller.enabled = true; report.AppendLine("Fixture position: " + point);
        }
        void Frame(Vector3 front, bool close)
        {
            fixedCamera = true; world.eye.fieldOfView = 42;
            cameraAt = body.transform.position + front * (close ? 2.05f : 3.95f) + Vector3.up * (close ? 1.5f : 1.2f);
            cameraLook = body.transform.position + Vector3.up * (close ? 1.35f : .96f);
        }
        void LateUpdate() { if (fixedCamera && world != null) { world.eye.transform.position = cameraAt; world.eye.transform.LookAt(cameraLook); } }
        IEnumerator Pair(string name)
        {
            foreach (bool original in visual.originalMaterial != null ? new[] { true, false } : new[] { false })
            {
                visual.CompareOriginal(original); yield return new WaitForSecondsRealtime(.25f);
                yield return Capture((original ? "Original-" : "HumanThings-") + name); if (done) yield break;
            }
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                if (image.GetPixels32().Count(p => p.r > 25 || p.g > 25 || p.b > 25) < 10000) { Fail("blank capture " + name); yield break; }
                File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
            }
            finally { Destroy(image); }
        }
        static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(); var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
        void OnEnable() { Application.logMessageReceived += Log; Invoke(nameof(Timeout), 150); }
        void OnDisable() { Application.logMessageReceived -= Log; }
        void Timeout() { if (!done) Fail("timeout"); }
        void Log(string message, string trace, LogType type) { if (!done && type == LogType.Exception) Fail(message); }
        void Fail(string message)
        {
            if (done) return; done = true; StopAllCoroutines();
            if (output != null) File.WriteAllText(Path.Combine(output, "Result.txt"), "FAIL " + message + "\n" + report);
            Debug.LogError("CHARACTER_VISUAL_GAME_FAIL " + message); Application.Quit(2);
        }
    }
}
#endif
