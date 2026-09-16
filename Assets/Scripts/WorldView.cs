using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace EarthRecovery
{
    public sealed partial class WorldView : MonoBehaviour
    {
        public NetworkSession session;
        public GameHud hud;
        public Camera eye;
        public Font font;
        public bool MenuOpen = true;
        public int SpectatorIndex;
        public bool Automated;
        public string Nearby = "";
        readonly Dictionary<ulong, GameObject> bodies = new();
        readonly Dictionary<int, GameObject> loot = new(), objects = new(), monsters = new();
        readonly Dictionary<int, NavMeshAgent> agents = new();
        readonly Dictionary<int, GameObject> moduleVisuals = new();
        readonly Dictionary<int, LocationController> locations = new();
        readonly List<WorldBehaviorAnchor> behaviorAnchors = new();
        readonly Dictionary<ulong, CharacterController> controllers = new();
        readonly Dictionary<ulong, PlayerAvatar> avatars = new();
        readonly Dictionary<ulong, Light> torches = new();
        readonly List<Material> materials = new();
        Material textMaterial;
        Transform root;
        NavMeshSurface surface;
        int seed = int.MinValue;
        bool lobbyWorld, hostWorld;
        GameObject sun;
        GameObject playerPrefab;
        PlayerCharacterCatalog characters;
        float yaw, pitch = 18, inputTimer;
        int nearbyLoot = -1, nearbyObject = -1, nearbySite = -1;

        void Awake()
        {
            characters = PlayerCharacterCatalog.Load();
            var characterVisual = Resources.Load<HumanThingsCharacterVisualProfile>("HumanThingsCharacterVisualProfile");
            playerPrefab = characterVisual != null && characterVisual.visualPrefab != null
                ? characterVisual.visualPrefab : Resources.Load<GameObject>("PlayerCharacter");
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 18);
            textMaterial = new Material(Shader.Find("EarthRecovery/WorldText"));
            Font.textureRebuilt += RefreshFont;
            var cameraGo = new GameObject("Agent camera");
            cameraGo.transform.SetParent(transform);
            eye = cameraGo.AddComponent<Camera>(); eye.nearClipPlane = .08f; eye.farClipPlane = 180; eye.fieldOfView = 76;
            eye.backgroundColor = new Color(.3f, .34f, .34f); eye.clearFlags = CameraClearFlags.SolidColor;
            cameraGo.AddComponent<AudioListener>();
            var data = cameraGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            var light = new GameObject("Overcast sun").AddComponent<Light>();
            sun = light.gameObject; sun.transform.SetParent(transform);
            light.type = LightType.Directional; light.intensity = 1.05f; light.color = new Color(.8f, .88f, .93f);
            light.transform.rotation = Quaternion.Euler(48, -30, 0); light.shadows = LightShadows.Soft;
            RenderSettings.ambientLight = new Color(.38f, .42f, .42f);
            RenderSettings.fog = true; RenderSettings.fogColor = eye.backgroundColor; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = .013f;
        }
        Material Material(Color color)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")); m.color = color; materials.Add(m); return m;
        }
        GameObject Shape(string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color, Transform parent, bool collides = true)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent);
            go.transform.localPosition = position; go.transform.localScale = scale; go.layer = 8;
            go.GetComponent<Renderer>().sharedMaterial = Material(color);
            if (!collides) { var c = go.GetComponent<Collider>(); c.enabled = false; Destroy(c); }
            return go;
        }
        void Label(string text, Vector3 at, Transform parent, int size = 36)
        {
            var go = new GameObject(text); go.transform.SetParent(parent); go.transform.localPosition = at;
            var tm = go.AddComponent<TextMesh>(); tm.text = text; tm.font = font; tm.fontSize = size; tm.characterSize = .08f;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = Color.white;
            RefreshFont(font);
            go.GetComponent<MeshRenderer>().sharedMaterial = textMaterial;
        }
        void RefreshFont(Font rebuilt) { if (rebuilt == font && textMaterial != null) textMaterial.mainTexture = font.material.mainTexture; }
        void SafeVolume(Transform parent, Vector3 size)
        {
            parent.gameObject.layer = 8;
            var volume = parent.gameObject.AddComponent<NavMeshModifierVolume>(); volume.center = Vector3.up; volume.size = size; volume.area = 1;
        }
        void Building(string name, Vector3 position, int quarter, bool camp, int siteId = -1)
        {
            if (!camp)
            {
                var instance = Instantiate(HumanContent.Load().locations[siteId].prefab, position, Quaternion.Euler(0, quarter * 90, 0), root);
                locations[siteId] = instance.GetComponent<LocationController>();
                if (City != null)
                {
                    var exterior = City.specialPOIs.Single(p => p.id == HumanContent.Load().locations[siteId].id);
                    instance.transform.SetParent(exterior.instance.transform, true);
                    foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>())
                        if (renderer.name == "Floor" || renderer.name == "Front" || renderer.name == "Rear" || renderer.name == "Side")
                            renderer.gameObject.SetActive(false);
                }
                var console = instance.transform.Find("Console");
                if (console != null) console.GetComponent<Renderer>().sharedMaterial = Material(new Color(.12f, .65f, .72f));
                Label("시설 단말기", new Vector3(0, 2.2f, 2), instance.transform, 22);
                SafeVolume(instance.transform, new Vector3(10.4f,6,10.4f));
                if (City == null) Label(name,new Vector3(0,3.1f,-5.25f),instance.transform);
                behaviorAnchors.AddRange(instance.GetComponentsInChildren<WorldBehaviorAnchor>());
                return;
            }
            var building = new GameObject(name).transform; building.SetParent(root); building.position = position; building.rotation = Quaternion.Euler(0, quarter * 90, 0);
            float r = camp ? 7 : 5;
            Color wall = camp ? new Color(.28f, .43f, .42f) : new Color(.4f, .42f, .4f);
            Shape("Floor", PrimitiveType.Cube, new Vector3(0, -.02f, 0), new Vector3(r * 2, .08f, r * 2), camp ? new Color(.33f, .49f, .45f) : new Color(.44f, .44f, .41f), building);
            Shape("Rear wall", PrimitiveType.Cube, new Vector3(0, 2, r), new Vector3(r * 2, 4, .4f), wall, building);
            foreach (int side in new[] { -1, 1 })
            {
                Shape("Side wall", PrimitiveType.Cube, new Vector3(side * r, 2, 0), new Vector3(.4f, 4, r * 2), wall, building);
                Shape("Front wall", PrimitiveType.Cube, new Vector3(side * (r + 2) / 2, 2, -r), new Vector3(r - 2, 4, .4f), wall, building);
            }
            Shape("Door lintel", PrimitiveType.Cube, new Vector3(0, 3.7f, -r), new Vector3(4, .6f, .4f), wall, building);
            Shape("Terminal", PrimitiveType.Cube, new Vector3(0, .65f, 2), new Vector3(2, 1.3f, 1), new Color(.2f, .24f, .24f), building);
            Shape("Terminal screen", PrimitiveType.Cube, new Vector3(0, 1.4f, 2), new Vector3(1.5f, .9f, .1f), new Color(.38f, .77f, .66f), building, false);
            Label(name, new Vector3(0, 3.1f, -r - .25f), building);
            SafeVolume(building, new Vector3(r * 2 + .4f, 6, r * 2 + .4f));
            if (camp)
            {
                Shape("Hibernation pod", PrimitiveType.Capsule, new Vector3(-4, 1, 3), new Vector3(1.5f, 1.5f, 1.5f), new Color(.5f, .62f, .67f), building);
                Label("동면 장치", new Vector3(-4, 2.8f, 3), building, 24);
            }
        }
        public void EnsureWorld(Snapshot state)
        {
            int nextSeed = state.phase == Phase.Lobby ? 0 : state.seed;
            bool lobby = state.phase == Phase.Lobby, host = session.IsHost;
            if (root != null && seed == nextSeed && lobbyWorld == lobby && hostWorld == host) return;
            lobbyWorld = lobby; hostWorld = host;
            seed = nextSeed;
            if (surface != null) surface.RemoveData();
            if (root != null) { root.gameObject.SetActive(false); Destroy(root.gameObject); }
            foreach (var m in materials) Destroy(m); materials.Clear();
            bodies.Clear(); controllers.Clear(); avatars.Clear(); torches.Clear(); loot.Clear(); objects.Clear(); monsters.Clear(); agents.Clear();
            moduleVisuals.Clear(); locations.Clear(); behaviorAnchors.Clear();
            City = null;
            root = new GameObject(state.cityWorld ? "Expedition city world" : "Lobby world").transform;
            var size = state.mapSize;
            Shape("Ground", PrimitiveType.Cube, new Vector3(0, -.55f, 0), new Vector3(size.x, 1, size.y), new Color(.29f, .32f, .32f), root);
            foreach (int side in new[] { -1, 1 })
            {
                Shape("Map boundary", PrimitiveType.Cube, new Vector3(side * (size.x / 2 - .5f), 2, 0), new Vector3(1, 4, size.y), Color.gray, root);
                Shape("Map boundary", PrimitiveType.Cube, new Vector3(0, 2, side * (size.y / 2 - .5f)), new Vector3(size.x, 4, 1), Color.gray, root);
            }
            if (state.cityWorld) BuildCity(state);
            Building("베이스캠프", Vector3.zero, 0, true);
            Instantiate(HumanContent.Load().recoveryPrefab,Vector3.zero,Quaternion.identity,root);
            if (state.phase != Phase.Lobby)
            {
                foreach (var site in state.sites) Building(Catalog.Sites[site.id], site.position, site.quarterTurn, false, site.id);
                foreach (var station in state.stations)
                {
                    var go = Instantiate(HumanContent.Load().stationPrefab,station.position,Quaternion.identity,root);
                    var stationMaterial = Material(new Color(1f, .42f, .06f));
                    foreach (var renderer in go.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = stationMaterial;
                    go.GetComponent<StationController>().runtimeId = station.id;
                    Label("복원기 " + (station.id+1),Vector3.up*3,go.transform,24);
                }
                var rng = new System.Random(state.seed ^ 89123);
                for (int i = 0; !state.cityWorld && i < 45; i++)
                {
                    var pos = new Vector3(rng.Next(-67, 68), 0, rng.Next(-67, 68));
                    if (WorldGeometry.Safe(state, pos) || state.sites.Any(s => Vector3.Distance(s.position, pos) < 10 || Vector3.Distance(WorldGeometry.Entrance(s), pos) < 6)
                        || state.loot.Any(l => Vector3.Distance(l.position, pos) < 3) || pos.magnitude < 12) continue;
                    Shape("Rubble", PrimitiveType.Cube, pos + Vector3.up * .8f, new Vector3(1.5f, 1.6f, 2), new Color(.34f, .35f, .33f), root);
                }
                for (int d = 0; !state.cityWorld && d < HumanContent.Load().zones.Length; d++)
                {
                    Vector3 center = Catalog.DistrictCenter(d);
                    var sign = new GameObject("District sign").transform; sign.SetParent(root); sign.position = center + new Vector3(0, 0, -19);
                    Shape("Sign pole", PrimitiveType.Cylinder, new Vector3(0, 1.5f, 0), new Vector3(.1f, 1.5f, .1f), Color.gray, sign);
                    Label(Catalog.Districts[d], new Vector3(0, 3, 0), sign, 30);
                }
            }
            Physics.SyncTransforms();
            if(session.IsHost) session.HostGame.Anchors=behaviorAnchors;
            if (session.IsHost || Automated)
            {
                surface = root.gameObject.AddComponent<NavMeshSurface>(); surface.collectObjects = CollectObjects.Children;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders; surface.layerMask = 1 << 8;
                surface.BuildNavMesh();
                if (state.cityWorld && session.IsHost) ValidateCitySpawns(state);
            }
            if (state.cityWorld)
                root.gameObject.AddComponent<HumanThingsVisualTreatment>().Apply(Resources.Load<HumanThingsVisualProfile>("HumanThingsVisualProfile"), eye, sun.GetComponent<Light>());
        }
        GameObject Body(PlayerState p)
        {
            var character = characters != null ? characters.Resolve(p.characterId) : null;
            string characterId = character != null ? character.id : PlayerCharacterCatalog.DefaultId;
            if (bodies.TryGetValue(p.id, out var body))
            {
                if (!avatars.TryGetValue(p.id, out var avatar) || avatar.CharacterId == characterId) return body;
                RemoveBody(p.id);
            }
            body = new GameObject("Agent " + p.id); body.transform.SetParent(root); body.transform.SetPositionAndRotation(p.position, Quaternion.Euler(0, p.yaw, 0));
            var prefab = character != null ? character.Prefab : playerPrefab;
            if (prefab != null)
            {
                var avatar = Instantiate(prefab, body.transform, false).GetComponent<PlayerAvatar>();
                avatar.CharacterId = characterId; avatars[p.id] = avatar;
            }
            else
            {
                Shape("Suit", PrimitiveType.Capsule, new Vector3(0, .85f, 0), new Vector3(.65f, .85f, .65f), Color.HSVToRGB((p.id * .21f) % 1, .2f, .7f), body.transform, false);
                Shape("Mask", PrimitiveType.Sphere, new Vector3(0, 1.5f, .28f), new Vector3(.4f, .28f, .2f), new Color(.08f, .12f, .12f), body.transform, false);
            }
            var torch = new GameObject("Flashlight").AddComponent<Light>(); torch.transform.SetParent(body.transform); torch.transform.localPosition = new Vector3(0, 1.5f, .4f);
            torch.type = LightType.Spot; torch.range = 22; torch.spotAngle = 50; torch.intensity = 3; torches[p.id] = torch;
            if (session.IsHost)
            {
                var cc = body.AddComponent<CharacterController>(); cc.height = 1.8f; cc.radius = .3f; cc.center = Vector3.up * .9f; cc.stepOffset = .45f;
                controllers[p.id] = cc;
            }
            body.AddComponent<PlayerCameraVisibility>();
            bodies[p.id] = body; return body;
        }
        void RemoveBody(ulong id)
        {
            var body = bodies[id]; body.SetActive(false);
            foreach (var renderer in body.GetComponentsInChildren<Renderer>(true))
            { var material = renderer.sharedMaterial; if (materials.Remove(material)) Destroy(material); }
            Destroy(body); bodies.Remove(id); controllers.Remove(id); avatars.Remove(id); torches.Remove(id);
        }
        public void HostStep(Expedition game, float dt)
        {
            var state = game.State;
            foreach (var p in state.players)
            {
                var body = Body(p); var cc = controllers[p.id];
                if (!p.alive || !p.connected) { cc.enabled = false; continue; }
                cc.enabled = true;
                Vector3 velocity = Vector3.zero;
                if (game.Inputs.TryGetValue(p.id, out var input) && state.elapsed - p.lastInputAt < .4f)
                {
                    float speed = p.carrying >= 0 ? game.Rules.carrySpeed : input.flag ? game.Rules.runSpeed : game.Rules.walkSpeed;
                    if (p.carrying >= 0) speed *= game.Content.Artifact(state.sites[p.carrying]).carryProfile.speedMultiplier;
                    velocity = Quaternion.Euler(0, p.yaw, 0) * new Vector3(input.move.x, 0, input.move.y) * speed;
                    if (input.flag && velocity.sqrMagnitude > 1 && !WorldGeometry.Safe(state, p.position)) game.Noise(p.position, 14);
                }
                var before = body.transform.position;
                cc.Move((velocity + Vector3.down * 3) * dt);
                p.position = body.transform.position;
                p.stationary = Vector3.Distance(before, p.position) > .015f ? 0 : p.stationary + dt;
            }
            foreach (var monster in state.monsters)
            {
                var visual = Monster(monster, true);
                var agent = agents[monster.id];
                monster.position = visual.transform.position;
                var brain = visual.GetComponent<MonsterBrain>();
                brain.Tick(game,monster,behaviorAnchors,dt);
                brain.Navigate(game, monster, agent);
                var def = game.Content.monsters[monster.definition];
                agent.speed = monster.alert == AlertState.Chase ? def.chaseSpeed : def.patrolSpeed;
                if (monster.alert == AlertState.Chase)
                    foreach (var p in state.players.Where(p=>p.alive&&p.connected&&!WorldGeometry.Safe(state,p.position)).ToArray())
                        if (Vector3.Distance(p.position,monster.position)<1.2f) game.Kill(p,"변이 개체 접촉");
            }
        }
        GameObject Monster(MonsterState state, bool host)
        {
            if (monsters.TryGetValue(state.id, out var result)) return result;
            result = Instantiate(HumanContent.Load().monsters[state.definition].prefab,state.position,Quaternion.identity,root);
            foreach (var collider in result.GetComponentsInChildren<Collider>()) collider.enabled = false;
            Color signal = state.kind switch { MonsterKind.Sound => new Color(.8f, .66f, .3f), MonsterKind.Light => new Color(.8f, .85f, .72f), MonsterKind.Stillness => new Color(.6f, .28f, .29f), _ => new Color(.4f, .5f, .48f) };
            Shape("Face", PrimitiveType.Cube, new Vector3(0, 2.3f, .35f), new Vector3(.45f, .12f, .1f), signal, result.transform, false);
            if (host)
            {
                var agent = result.AddComponent<NavMeshAgent>(); agent.radius = .45f; agent.height = 2.6f; agent.stoppingDistance = .65f;
                if (NavMesh.SamplePosition(state.position, out var hit, 8, NavMesh.AllAreas)) agent.Warp(hit.position);
                agents[state.id] = agent;
            }
            monsters[state.id] = result; return result;
        }
        void Update()
        {
            var state = session.IsHost ? session.HostGame.State : session.View;
            try { EnsureWorld(state); }
            catch (System.Exception e) when (e is System.InvalidOperationException || e is System.ArgumentException)
            { session.Leave(); session.Status = "맵 생성 실패: " + e.Message; return; }
            foreach (var p in state.players)
            {
                var body = Body(p);
                if (!session.IsHost) body.transform.position = Vector3.Lerp(body.transform.position, p.position, 15 * Time.deltaTime);
                foreach (var renderer in body.GetComponentsInChildren<Renderer>()) renderer.enabled = p.connected && p.alive;
                torches[p.id].enabled = p.alive && p.flashlight;
            }
            foreach (var stale in bodies.Keys.Where(id => state.players.All(p => p.id != id)).ToArray())
                RemoveBody(stale);
            foreach (var l in state.loot)
            {
                if (!loot.TryGetValue(l.id, out var item))
                {
                    item = Instantiate(HumanContent.Load().scrapPrefab,l.position,Quaternion.identity,root);
                    foreach (var collider in item.GetComponentsInChildren<Collider>()) collider.enabled=false;
                    item.GetComponent<ScrapPickup>().runtimeId=l.id; loot[l.id]=item;
                }
                item.SetActive(!l.collected);
                item.transform.position = l.position + Vector3.up * .35f;
            }
            foreach (var site in state.sites)
            {
                if (locations.TryGetValue(site.id,out var location)) location.Present(site);
                if (!objects.TryGetValue(site.id, out var item))
                {
                    item = Instantiate(HumanContent.Load().Artifact(site).worldPrefab,site.position+Vector3.up*.5f,Quaternion.identity,root);
                    item.GetComponent<ArtifactRuntime>().missionLocation=site.id;
                    objects[site.id] = item;
                }
                item.SetActive(site.phase == CraftPhase.Ready || site.phase == CraftPhase.Carried);
                if(item.activeSelf)item.GetComponent<ArtifactRuntime>().Present(site);
                if (site.phase == CraftPhase.Ready) item.transform.position = (session.IsHost ? session.HostGame.ObjectPosition(site) : site.objectPosition) + Vector3.up * .5f;
                if (site.phase == CraftPhase.Carried)
                {
                    var carrier = state.players.Find(p => p.carrying == site.id);
                    if (carrier != null) item.transform.position = carrier.position + Quaternion.Euler(0, carrier.yaw, 0) * new Vector3(0, .9f, 1);
                }
            }
            foreach (var m in state.monsters)
            {
                var go = Monster(m, session.IsHost);
                if (!session.IsHost) go.transform.position = Vector3.Lerp(go.transform.position, m.position, Time.deltaTime * 12);
                if(go.transform.childCount>0)
                {
                    var shell=go.transform.GetChild(0);bool memory=m.alert==AlertState.MemoryBehavior;
                    shell.localRotation=memory&&m.kind==MonsterKind.Sound?Quaternion.Euler(55+Mathf.Sin(Time.time*2)*15,0,0):Quaternion.identity;
                    shell.localPosition=Vector3.up*(memory && m.kind==MonsterKind.Sound ? .8f+Mathf.Sin(Time.time*2)*.1f : 1.2f);
                }
            }
            for(int i=0;i<state.moduleDrops.Count;i++)
            {
                var d=state.moduleDrops[i];
                if(!moduleVisuals.TryGetValue(i,out var visual)) { visual=Shape("Restoration module",PrimitiveType.Cube,Vector3.zero,Vector3.one*.6f,Color.cyan,root,false);moduleVisuals[i]=visual; }
                visual.SetActive(!d.collected);visual.transform.position=d.position+Vector3.up*.4f;
            }
            InputStep();
        }
        void InputStep()
        {
            if (Automated || DisplayPreferences.PopupOpen) return;
            var kb = Keyboard.current; var mouse = Mouse.current; var p = session.LocalPlayer;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { MenuOpen = !MenuOpen; hud.ClosePanels(); }
            bool playing = p != null && p.alive && session.View.phase == Phase.Expedition;
            Cursor.lockState = playing && !MenuOpen && Application.isFocused ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = Cursor.lockState != CursorLockMode.Locked;
            if (playing)
            {
                if (!MenuOpen && mouse != null && Application.isFocused)
                {
                    var delta = mouse.delta.ReadValue(); yaw = Mathf.Repeat(yaw + delta.x * .12f, 360); pitch = Mathf.Clamp(pitch - delta.y * .12f, -30, 65);
                }
                inputTimer -= Time.deltaTime;
                if (inputTimer <= 0)
                {
                    var move = Vector2.zero;
                    if (!MenuOpen && kb != null && Application.isFocused) move = new Vector2((kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0), (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0));
                    session.Send(new Command { action = "move", move = move, yaw = yaw, flag = kb != null && kb.leftShiftKey.isPressed }); inputTimer = .05f;
                }
                FindNearby(p);
                if (kb != null)
                {
                    if (kb.fKey.wasPressedThisFrame && !MenuOpen) session.Send(new Command { action = "light", flag = !p.flashlight });
                    if (kb.gKey.wasPressedThisFrame && !MenuOpen) session.Send(new Command { action = "drop" });
                    if (kb.rKey.wasPressedThisFrame && !MenuOpen && p.carrying>=0) session.Send(new Command {action="activateArtifact",target=p.carrying});
                    if (kb.tabKey.wasPressedThisFrame) { MenuOpen = !MenuOpen; hud.ClosePanels(); hud.InventoryOpen = MenuOpen; }
                    if (kb.eKey.wasPressedThisFrame && !MenuOpen) Interact();
                    if (kb.spaceKey.wasPressedThisFrame && hud.SiteOpen >= 0) hud.SkillPress();
                }
            }
            else if (p != null && !p.alive)
            {
                MenuOpen = true;
                if (kb != null && kb.rightArrowKey.wasPressedThisFrame) SpectatorIndex++;
            }
        }
        void LateUpdate()
        {
            // Present after movement and input, without quantizing local rotation to network ticks.
            var state = session.IsHost ? session.HostGame.State : session.View;
            foreach (var p in state.players)
            {
                if (!bodies.TryGetValue(p.id, out var agent)) continue;
                bool localInput = p.id == session.LocalId && !Automated && state.phase == Phase.Expedition;
                var target = Quaternion.Euler(0, localInput ? yaw : p.yaw, 0);
                agent.transform.rotation = localInput ? target : Quaternion.Slerp(agent.transform.rotation, target, 1 - Mathf.Exp(-18 * Time.deltaTime));
                if (avatars.TryGetValue(p.id, out var avatar)) avatar.Present(p, session.rules, state.phase == Phase.Expedition, Time.deltaTime);
            }
            var local = session.LocalPlayer;
            GameObject followed = null;
            if (local != null && local.alive && session.View.phase == Phase.Expedition)
            {
                Vector3 at = bodies.TryGetValue(local.id, out var body) ? body.transform.position : local.position;
                followed = body;
                var rotation = Quaternion.Euler(pitch, Automated ? local.yaw : yaw, 0);
                var focus = at + Vector3.up * 1.45f;
                FollowCamera(focus, rotation, body);
            }
            else if (local != null && !local.alive)
            {
                var alive = session.View.players.Where(p => p.alive && p.connected && !p.developerDummy).ToArray();
                if (alive.Length > 0)
                {
                    SpectatorIndex = (SpectatorIndex % alive.Length + alive.Length) % alive.Length;
                    var target = alive[SpectatorIndex];
                    Vector3 at = bodies.TryGetValue(target.id, out var body) ? body.transform.position : target.position;
                    followed = body;
                    Vector3 focus = at + Vector3.up * 1.45f;
                    var rotation = Quaternion.Euler(18, body != null ? body.transform.eulerAngles.y : target.yaw, 0);
                    FollowCamera(focus, rotation, body);
                }
            }
            else { cameraTarget = null; eye.transform.position = new Vector3(0, 12, -19); eye.transform.LookAt(new Vector3(0, 0, 2)); }
            foreach (var body in bodies.Values)
                if (body.GetComponent<PlayerCameraVisibility>().Present(eye, body == followed)) ResetCameraHistory();
        }
        public static Vector3 ResolveCameraPosition(Vector3 focus, Quaternion rotation, float radius = .2f)
        {
            const float padding = .05f, distance = 3.6f;
            Vector3 direction = rotation * Vector3.back;
            // Never enforce a minimum boom length: that would push the camera through close walls.
            float length = distance;
            if (Physics.CheckSphere(focus, radius, 1 << 8, QueryTriggerInteraction.Ignore)) return focus;
            if (Physics.SphereCast(focus, radius, direction, out var hit, distance, 1 << 8, QueryTriggerInteraction.Ignore))
                length = Mathf.Max(0, hit.distance - padding);
            return focus + direction * length;
        }
        void FindNearby(PlayerState p)
        {
            nearbyLoot = -1; nearbyObject = -1; nearbySite = -1; Nearby = "";
            var l = session.View.loot.Where(l => !l.collected && Vector3.Distance(l.position, p.position) < 2.5f).OrderBy(l => Vector3.Distance(l.position, p.position)).FirstOrDefault();
            if (l != null) { nearbyLoot = l.id; Nearby = Catalog.Material(l.material); }
            var obj = session.View.sites.Find(s => s.phase == CraftPhase.Ready && Vector3.Distance(s.objectPosition, p.position) < 2.5f);
            if (obj != null) { nearbyObject = obj.id; Nearby = Catalog.Product(obj); }
            var site = session.View.sites.Find(s => WorldGeometry.NearConsole(s, p.position));
            if (site != null) { nearbySite = site.id; Nearby = Catalog.Sites[site.id] + " · 시설 단말기"; }
            if (WorldGeometry.InCamp(p.position) && l == null && obj == null) Nearby = "베이스캠프 단말기";
            var station=session.View.stations.Find(s=>Vector3.Distance(s.position,p.position)<4);
            if(station!=null && l==null && obj==null) Nearby="복원 제작기 "+(station.id+1);
            if(session.View.moduleDrops.Any(d=>!d.collected&&Vector3.Distance(d.position,p.position)<3)) Nearby="시설 복원 모듈";
        }
        void Interact()
        {
            var player=session.LocalPlayer;
            if (TryOpenFacilityConsole()) return;
            int module=session.View.moduleDrops.FindIndex(d=>!d.collected&&Vector3.Distance(d.position,player.position)<3);
            if(module>=0) {session.Send(new Command {action="modulePickup",target=module});return;}
            var station=session.View.stations.Find(s=>Vector3.Distance(s.position,player.position)<4);
            if(station!=null) {hud.ClosePanels();hud.StationOpen=station.id;MenuOpen=true;return;}
            if (nearbyObject >= 0) session.Send(new Command { action = "pickup", target = nearbyObject });
            else if (nearbyLoot >= 0) session.Send(new Command { action = "loot", target = nearbyLoot });
            else if (nearbySite >= 0) { hud.OpenSite(nearbySite); MenuOpen = true; }
            else if (session.LocalPlayer != null && WorldGeometry.InCamp(session.LocalPlayer.position)) { hud.CampOpen = true; MenuOpen = true; }
        }
        public bool TryOpenFacilityConsole()
        {
            var player = session.LocalPlayer;
            if (player == null || !player.alive) return false;
            var site = session.View.sites.Find(s => WorldGeometry.NearConsole(s, player.position));
            if (site == null) return false;
            hud.OpenSite(site.id); MenuOpen = true; return true;
        }
        void OnDestroy()
        {
            Font.textureRebuilt -= RefreshFont;
            if (textMaterial != null) Destroy(textMaterial);
            if (font != null) Destroy(font);
            if (surface != null) surface.RemoveData();
            if (root != null) { root.gameObject.SetActive(false); Destroy(root.gameObject); }
            if (eye != null) Destroy(eye.gameObject);
            if (sun != null) Destroy(sun);
            foreach (var m in materials) Destroy(m);
            if (!Automated) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }
    }
}
