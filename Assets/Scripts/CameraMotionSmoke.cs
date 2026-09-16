#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;

namespace EarthRecovery
{
    [DefaultExecutionOrder(10000)]
    public sealed class CameraMotionSmoke : MonoBehaviour
    {
        NetworkSession session;
        WorldView world;
        GameObject body;
        PlayerCameraVisibility visibility;
        string output;
        bool done, orbit;
        float angle, maxYawError;
        int hiddenFrames, samples;
        RenderTexture offscreen;
        readonly FieldInfo yaw = typeof(WorldView).GetField("yaw", BindingFlags.Instance | BindingFlags.NonPublic);
        readonly System.Text.StringBuilder report = new();

        IEnumerator Start()
        {
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../QA/CameraDisplay/Game")); Directory.CreateDirectory(output);
            Screen.SetResolution(1600, 900, false); DisplayPreferences.Select(0, false);
            session = GetComponent<NetworkSession>(); world = GetComponent<WorldView>();
            if (Application.isBatchMode)
            {
                offscreen = new RenderTexture(1600, 900, 24);
                world.eye.targetTexture = offscreen;
            }
            world.Automated = true; session.PersistArchive = false; session.rules.developerSolo = true;
            if (!session.Connect(true, "127.0.0.1", 17989)) { Fail("connect"); yield break; }
            float deadline = Time.realtimeSinceStartup + 20;
            while (!session.Online && Time.realtimeSinceStartup < deadline) yield return null;
            if (!session.Online) { Fail("connection timeout"); yield break; }
            session.Send(new Command { action = "ready", flag = true });
            if (!session.StartMission(100)) { Fail("mission"); yield break; }
            session.HostGame.State.monsters.Clear();
            yield return new WaitForSecondsRealtime(1);
            body = GameObject.Find("Agent " + session.LocalId); visibility = body.GetComponent<PlayerCameraVisibility>();
            world.MenuOpen = false;
            var road = world.City.chunks.SelectMany(c => c.city.roads.Select(r => (r.start + r.end)*.5f + c.node.worldBounds.center)).First(p => p.sqrMagnitude > 400);
            if (!NavMesh.SamplePosition(road, out var hit, 8, NavMesh.AllAreas)) { Fail("street"); yield break; }
            Place(hit.position);
            report.AppendLine("Actual seed-100 city, render-rate local yaw with unchanged 20 Hz command/10 Hz snapshot protocol.");
            var groups = FindObjectsByType<LODGroup>(FindObjectsSortMode.None);
            report.AppendLine("LOD groups=" + groups.Length + "; crossfade=" + groups.Count(g => g.fadeMode == LODFadeMode.CrossFade));
            var data = world.eye.GetUniversalAdditionalCameraData();
            foreach (bool temporal in new[] { false, true })
            {
                data.antialiasing = temporal ? AntialiasingMode.TemporalAntiAliasing : AntialiasingMode.FastApproximateAntialiasing;
                data.resetHistory = true; orbit = true;
                for (int i = 0; i < 72; i++)
                {
                    angle = i * 5;
                    yield return new WaitForEndOfFrame();
                    if (i % 6 == 0) Capture((temporal ? "TAA" : "FXAA") + "-Orbit-" + i.ToString("D2"));
                    yield return null;
                }
            }
            Place(new Vector3(6.4f, .04f, 0));
            for (int i = 0; i < 120; i++)
            {
                angle = 180 + i * 1.5f;
                yield return new WaitForEndOfFrame();
                if (visibility.Hidden) { hiddenFrames++; if (hiddenFrames == 5) Capture("Wall-Close"); }
                if (Physics.CheckSphere(world.eye.transform.position, .19f, 1 << 8, QueryTriggerInteraction.Ignore)) { Fail("camera intersects camp wall"); yield break; }
                yield return null;
            }
            if (hiddenFrames == 0) { Fail("close wall was not exercised"); yield break; }
            angle = 90;
            yield return new WaitForSecondsRealtime(1);
            if (visibility.Hidden) { Fail("character not restored after wall"); yield break; }
            Capture("Wall-Restored");
            if (maxYawError > .05f) { Fail("local yaw jitter " + maxYawError); yield break; }
            report.AppendLine("PASS: yaw max error=" + maxYawError + "; samples=" + samples + "; hidden wall frames=" + hiddenFrames + "; body restored; camera collision clear.");
            File.WriteAllText(Path.Combine(output, "Result.txt"), report.ToString());
            done = true; session.Leave(); Debug.Log("CAMERA_MOTION_QA_PASS"); Application.Quit(0);
        }
        void Update()
        {
            if (!orbit || world == null) return;
            // Let normal Update skip physical input, then exercise the real local LateUpdate presentation.
            world.Automated = false; yaw.SetValue(world, angle);
        }
        void LateUpdate()
        {
            if (!orbit || world == null || body == null) return;
            maxYawError = Mathf.Max(maxYawError, Mathf.Abs(Mathf.DeltaAngle(body.transform.eulerAngles.y, world.eye.transform.eulerAngles.y)));
            samples++; world.Automated = true;
        }
        void Place(Vector3 point)
        {
            var controller = body.GetComponent<CharacterController>(); controller.enabled = false;
            body.transform.position = session.HostGame.Player(session.LocalId).position = point;
            controller.enabled = true; Physics.SyncTransforms();
        }
        void Capture(string name)
        {
            Texture2D image;
            if (offscreen != null)
            {
                world.eye.Render();
                var active = RenderTexture.active; RenderTexture.active = offscreen;
                image = new Texture2D(offscreen.width, offscreen.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, offscreen.width, offscreen.height), 0, 0); image.Apply(); RenderTexture.active = active;
            }
            else image = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                if (image.GetPixels32().Count(p => p.r > 30 || p.g > 30 || p.b > 30) < 10000) { Fail("blank " + name); return; }
                File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
            }
            finally { Destroy(image); }
        }
        void OnEnable() { Application.logMessageReceived += Log; Invoke(nameof(Timeout), 160); }
        void OnDisable() { Application.logMessageReceived -= Log; if (offscreen != null) { world.eye.targetTexture = null; offscreen.Release(); Destroy(offscreen); } }
        void Log(string message, string trace, LogType type) { if (!done && type == LogType.Exception) Fail(message); }
        void Timeout() { if (!done) Fail("timeout"); }
        void Fail(string message)
        {
            if (done) return; done = true; StopAllCoroutines();
            if (output != null) File.WriteAllText(Path.Combine(output, "Result.txt"), "FAIL " + message + "\n" + report);
            Debug.LogError("CAMERA_MOTION_QA_FAIL " + message); Application.Quit(2);
        }
    }
}
#endif
