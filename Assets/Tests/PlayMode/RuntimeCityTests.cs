using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace EarthRecovery.Tests
{
    public sealed class RuntimeCityTests
    {
        NetworkSession session;
        Scene scene;
        [UnityTest, Timeout(600000)]
        public IEnumerator ExpeditionUsesCityAndAllFifteenFacilities()
        {
            scene = SceneManager.CreateScene("Runtime city integration"); SceneManager.SetActiveScene(scene);
            var root = new GameObject("City test session");
            session = root.AddComponent<NetworkSession>(); session.PersistArchive = false;
            var world = root.AddComponent<WorldView>(); var hud = root.AddComponent<GameHud>();
            session.world = world; world.session = session; world.hud = hud; hud.session = session; hud.world = world;
            world.Automated = true; session.rules.developerSolo = true;
            Assert.That(session.Connect(true, "127.0.0.1", 17996));
            yield return null; yield return null;
            foreach (int seed in new[] { 100, 200, 300, 400, 500 })
            {
                session.HostGame.Player(session.LocalId).ready = true;
                Assert.That(session.StartMission(seed), Is.True, session.Status);
                yield return null; yield return null;
                Assert.That(session.Online, Is.True, session.Status);
                Assert.That(session.View.cityWorld, Is.True);
                Assert.That(world.City.specialPOIs.Count, Is.EqualTo(15));
                Assert.That(world.City.root.GetComponentsInChildren<LocationController>().Length, Is.EqualTo(15));
                Assert.That(SnapshotValidation.Valid(session.HostGame.ForClient(session.LocalId), session.LocalId));
                var clientPlan = RuntimeCitySettings.Load().Generate(seed);
                foreach (var site in session.HostGame.State.sites)
                {
                    string id = session.HostGame.Content.locations[site.id].id;
                    var poi = world.City.specialPOIs.Single(p => p.id == id);
                    Assert.That(Vector3.Distance(poi.instance.transform.position, site.position), Is.LessThan(.001f));
                    Assert.That(clientPlan.specialPOIs.Single(p => p.definition.id == id).position, Is.EqualTo(site.position));
                    Assert.That(poi.instance.GetComponentInChildren<LocationController>(), Is.Not.Null);
                    var player = session.HostGame.Player(session.LocalId);
                    var body = GameObject.Find("Agent " + session.LocalId);
                    var cc = body.GetComponent<CharacterController>(); cc.enabled = false;
                    body.transform.position = player.position = site.position + Vector3.up * .1f; cc.enabled = true;
                    yield return new WaitForSeconds(.2f);
                    Assert.That(world.TryOpenFacilityConsole(), Is.True, id); hud.ClosePanels();
                }
                foreach (var item in session.HostGame.State.loot)
                    Assert.That(NavMesh.SamplePosition(item.position, out _, .5f, NavMesh.AllAreas), Is.True);
                var agentBody = GameObject.Find("Agent " + session.LocalId);
                var controller = agentBody.GetComponent<CharacterController>(); controller.enabled = false;
                agentBody.transform.position = session.HostGame.Player(session.LocalId).position = new Vector3(0, .1f, -12);
                session.HostGame.Player(session.LocalId).yaw = 180; controller.enabled = true;
                world.MenuOpen = false;
                yield return new WaitForSeconds(1);
                Directory.CreateDirectory("Docs/RuntimeCity");
                var rt = new RenderTexture(1600, 900, 24); var old = RenderTexture.active;
                world.eye.targetTexture = rt; world.eye.Render(); RenderTexture.active = rt;
                var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
                File.WriteAllBytes("Docs/RuntimeCity/Seed" + seed + "-InGame.png", image.EncodeToPNG());
                world.eye.targetTexture = null; RenderTexture.active = old;
                Object.Destroy(image); rt.Release(); Object.Destroy(rt);
                session.HostGame.ResetLobby(); yield return null; yield return null;
                Assert.That(world.City, Is.Null);
            }
            LogAssert.NoUnexpectedReceived();
            Debug.Log("RUNTIME_CITY_PLAY_PASS seeds=100,200,300,400,500 facilities=75 clientLayouts=5 returnToLobby=5");
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (session != null) { session.Leave(); Object.Destroy(session.gameObject); }
            yield return null;
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
