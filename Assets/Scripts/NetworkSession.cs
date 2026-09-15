using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace EarthRecovery
{
    public sealed class NetworkSession : MonoBehaviour
    {
        public GameRules rules;
        public Snapshot View { get; private set; } = new();
        public Expedition HostGame { get; private set; }
        public WorldView world;
        public RadioVoice radio;
        public string Status = "대기", UserName = "요원";
        bool TransportActive => manager != null && !closing && !manager.ShutdownInProgress && manager.IsListening;
        public bool Online => TransportActive && manager.IsConnectedClient && !connecting && LocalPlayer?.connected == true;
        public bool IsConnecting => connecting && !closing;
        public const float ConnectionTimeoutSeconds = 8;
        public const string BuildLabel = "Revision2";
        public bool IsHost => TransportActive && manager.IsHost && HostGame != null;
        public bool CanConnect => manager != null && !closing && !connecting && !manager.IsListening && !manager.ShutdownInProgress;
        public ulong LocalId => manager == null ? ulong.MaxValue : manager.LocalClientId;
        public PlayerState LocalPlayer => View.players.Find(p => p.id == LocalId);
        public NetworkManager Manager => manager;
        NetworkManager manager;
        float snapshotTimer, connectStarted;
        bool connecting, closing;
        readonly Dictionary<ulong, (float time, int count)> budgets = new();
        ArchiveStore archiveStore;
        public bool PersistArchive = true;
        public event Action<GameEvent> Presented;
        int presentedSequence;
        string presentedRun = "";
        string admissionId = "", admissionProof = "";

        void Awake()
        {
            rules ??= Resources.Load<GameRules>("GameRules");
            rules = rules == null ? ScriptableObject.CreateInstance<GameRules>() : Instantiate(rules);
            rules.Sanitize();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            rules.developerSolo |= Environment.GetCommandLineArgs().Contains("--dev-solo");
#endif
            manager = gameObject.AddComponent<NetworkManager>();
            var transport = gameObject.AddComponent<UnityTransport>();
            transport.MaxPayloadSize = 32768;
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false, ConnectionApproval = true, TickRate = 30 };
            manager.NetworkConfig.ProtocolVersion = 7;
            PersistArchive = !Environment.GetCommandLineArgs().Any(a => a == "--qa-role" || a == "-runTests");
            archiveStore = new ArchiveStore(System.IO.Path.Combine(Application.persistentDataPath,"human-things-archive-v1.json"));
            if(PersistArchive) View.archive=archiveStore.Load();
            manager.ConnectionApprovalCallback += Approve;
            manager.OnClientConnectedCallback += Connected;
            manager.OnClientDisconnectCallback += Disconnected;
            manager.OnTransportFailure += TransportFailed;
            manager.OnClientStopped += Stopped;
            manager.OnServerStopped += Stopped;
        }
        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = HostGame != null && HostGame.State.phase == Phase.Lobby && manager.ConnectedClientsIds.Count < rules.maxPlayers;
            if (response.Approved && !string.IsNullOrEmpty(admissionId))
            {
                RoomAdmission admission = null;
                try { if (request.Payload != null && request.Payload.Length <= 512) admission = JsonUtility.FromJson<RoomAdmission>(System.Text.Encoding.UTF8.GetString(request.Payload)); }
                catch (ArgumentException) { }
                response.Approved = admission != null && admission.id == admissionId && admission.proof == admissionProof;
            }
            response.CreatePlayerObject = false;
            response.Pending = false;
            response.Reason = response.Approved ? "" : "방이 종료되었거나, 비밀번호가 다르거나, 참가할 수 없는 상태입니다.";
        }
        public bool Connect(bool host, string address, ushort port, string roomId = "", string proof = "")
        {
            if (!CanConnect) return false;
            if (port == 0 || !IPAddress.TryParse(address?.Trim(), out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            { Status = "올바른 IPv4 주소와 포트를 입력해 주세요."; return false; }
            address = ip.ToString(); UserName = Expedition.CleanName(UserName); rules.Sanitize();
            admissionId = host ? roomId : ""; admissionProof = host ? proof : "";
            manager.NetworkConfig.ConnectionData = string.IsNullOrEmpty(roomId) ? Array.Empty<byte>() : System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(new RoomAdmission { id = roomId, proof = proof }));
            budgets.Clear(); snapshotTimer = 0;
            Status = host ? "방 생성 중" : "접속 중";
            manager.GetComponent<UnityTransport>().SetConnectionData(address, port, host ? "0.0.0.0" : null);
            if (host)
            {
                HostGame = new Expedition(rules);
                if(PersistArchive) HostGame.State.archive = archiveStore.Load();
                HostGame.Changed += e =>
                {
                    if(e.kind=="EchoMimic" && radio!=null) e.text=Convert.ToBase64String(radio.MimicSource?.RecentToken() ?? Array.Empty<byte>());
                    if(e.kind=="ArchiveUpdated" && PersistArchive && !archiveStore.Write(HostGame.State.archive)) Status="Archive 저장 실패: "+archiveStore.LastError;
                };
            }
            connecting = true; connectStarted = Time.realtimeSinceStartup;
            bool ok;
            try { ok = host ? manager.StartHost() : manager.StartClient(); }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is System.Net.Sockets.SocketException)
            { StopSession("방 연결 실패 · " + e.Message); return false; }
            if (ok)
            {
                Register();
                if (host && HostGame.Player(LocalId) != null) ReceiveSnapshot(HostGame.ForClient(LocalId));
            }
            else StopSession("방 연결 실패 · 주소와 포트 확인");
            return ok;
        }
        void Register()
        {
            manager.CustomMessagingManager.RegisterNamedMessageHandler("command", OnCommand);
            manager.CustomMessagingManager.RegisterNamedMessageHandler("snapshot", OnSnapshot);
            manager.CustomMessagingManager.RegisterNamedMessageHandler("voice", OnVoice);
        }
        void Connected(ulong id)
        {
            if (!TransportActive) return;
            if (manager.IsServer && HostGame != null && !HostGame.Join(id, id == manager.LocalClientId ? UserName : "요원 " + (id + 1)))
            { manager.DisconnectClient(id); return; }
            if (id == manager.LocalClientId) Status = "방 정보 수신 중";
        }
        void Disconnected(ulong id)
        {
            if (closing) return;
            budgets.Remove(id);
            if (radio != null) radio.Forget(id);
            if (manager.IsServer) HostGame?.Disconnect(id);
            else if (id == manager.LocalClientId)
            {
                StopSession((connecting ? "방에 연결하지 못했습니다. 주소와 방 생성 여부를 확인해 주세요. " : "연결 종료 ") + manager.DisconnectReason);
            }
        }
        public void Leave()
        {
            StopSession("연결 종료");
        }
        void StopSession(string status)
        {
            if (closing) return;
            // NGO shuts down asynchronously; stop consumers before releasing their state.
            closing = true;
            Status = status;
            ClearSession();
            if (manager != null && !manager.ShutdownInProgress) manager.Shutdown();
        }
        void ClearSession()
        {
            connecting = false; View = new Snapshot(); HostGame = null;
            if(PersistArchive && archiveStore!=null) View.archive=archiveStore.Load();
            budgets.Clear(); snapshotTimer = 0;
            if (radio != null) radio.ResetSession();
            if (world != null)
            {
                world.MenuOpen = true;
                if (world.hud != null) world.hud.ClosePanels();
            }
        }
        void Stopped(bool wasHost)
        {
            closing = true;
            ClearSession();
        }
        void TransportFailed() => StopSession("네트워크 연결 실패");
        public bool StartMission(int seed = 0)
        {
            if (!IsHost) return false;
            bool result;
            try { result = HostGame.Start(seed == 0 ? UnityEngine.Random.Range(1, int.MaxValue) : seed); }
            catch(InvalidOperationException e) {StopSession("콘텐츠 설정 오류: "+e.Message);return false;}
            if (!result) Status = "최소 "+rules.MinimumStartPlayers+"명의 준비 완료가 필요합니다.";
            return result;
        }
        public void ReturnToLobby() { if (IsHost && HostGame.State.phase > Phase.Expedition) HostGame.ResetLobby(); }
        public void Send(Command command)
        {
            if (!Online || command == null) return;
            if (IsHost) HandleCommand(LocalId, command);
            else SendText("command", NetworkManager.ServerClientId, JsonUtility.ToJson(command));
        }
        void HandleCommand(ulong id, Command c)
        {
            if (c?.action == "start" && id == NetworkManager.ServerClientId) StartMission();
            else HostGame?.ValidateAndExecute(id, c);
        }
        bool Budget(ulong id)
        {
            float t = Time.realtimeSinceStartup;
            if (!budgets.TryGetValue(id, out var b) || t - b.time >= 1) b = (t, 0);
            b.count++; budgets[id] = b;
            return b.count <= 100;
        }
        void OnCommand(ulong sender, FastBufferReader reader)
        {
            if (!IsHost || reader.Length > 1024 || !Budget(sender)) return;
            try
            {
                int start = reader.Position;
                if (reader.Length - start < 4) return;
                // Validate before NGO multiplies the character count and allocates a string.
                reader.ReadValueSafe(out int characters);
                if (characters < 1 || characters > 480 || characters * 2 != reader.Length - reader.Position) return;
                reader.Seek(start);
                reader.ReadValueSafe(out string json); HandleCommand(sender, JsonUtility.FromJson<Command>(json));
            }
            catch (Exception) { Status = "잘못된 명령 무시"; }
        }
        void OnSnapshot(ulong sender, FastBufferReader reader)
        {
            if (!TransportActive || manager.IsServer || sender != NetworkManager.ServerClientId) return;
            try
            {
                int remaining = reader.Length - reader.Position;
                if (remaining < 4 || remaining > 65540) throw new System.IO.InvalidDataException();
                reader.ReadValueSafe(out int size);
                if (size < 1 || size > 65536 || size != reader.Length - reader.Position) throw new System.IO.InvalidDataException();
                var bytes = new byte[size]; reader.ReadBytesSafe(ref bytes, size);
                ReceiveSnapshot(JsonUtility.FromJson<Snapshot>(SnapshotCodec.Decode(bytes)));
            }
            catch (Exception e) when (e is System.IO.InvalidDataException || e is System.IO.IOException || e is ArgumentException || e is OverflowException)
            { StopSession("서버 데이터 오류 · 연결을 종료했습니다."); }
        }
        void ReceiveSnapshot(Snapshot next)
        {
            if (!SnapshotValidation.Valid(next, LocalId) || !next.players.Any(p=>p.id==LocalId && p.connected)) { StopSession("유효하지 않은 서버 상태 · 연결 종료"); return; }
            if (!IsHost && !string.IsNullOrEmpty(next.rulesJson)) { JsonUtility.FromJsonOverwrite(next.rulesJson, rules); rules.Sanitize(); }
            View = next;
            if (connecting) { connecting = false; Status = "연결됨"; }
            if(presentedRun!=next.runId) {presentedRun=next.runId;presentedSequence=0;}
            foreach(var e in next.events.Where(e=>e.sequence>presentedSequence).ToArray()) {presentedSequence=e.sequence;Presented?.Invoke(e);}
            UserName = Expedition.CleanName(UserName);
            if (LocalPlayer != null && LocalPlayer.name != UserName && View.phase == Phase.Lobby) Send(new Command { action = "name", text = UserName });
        }
        void SendText(string channel, ulong recipient, string json)
        {
            if (!TransportActive || manager.CustomMessagingManager == null) return;
            if (channel == "snapshot")
            {
                var bytes = SnapshotCodec.Encode(json);
                using var compressed = new FastBufferWriter(bytes.Length + 4, Allocator.Temp);
                compressed.WriteValueSafe(bytes.Length); compressed.WriteBytesSafe(bytes);
                manager.CustomMessagingManager.SendNamedMessage(channel, recipient, compressed, NetworkDelivery.ReliableFragmentedSequenced);
                return;
            }
            if (json.Length > 480) { Status = "명령이 너무 깁니다."; return; }
            using var writer = new FastBufferWriter(json.Length * 2 + 16, Allocator.Temp);
            writer.WriteValueSafe(json);
            manager.CustomMessagingManager.SendNamedMessage(channel, recipient, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }
        void Update()
        {
            if (closing)
            {
                if (manager != null && !manager.IsListening && !manager.ShutdownInProgress) closing = false;
                return;
            }
            if (connecting && Time.realtimeSinceStartup - connectStarted > ConnectionTimeoutSeconds)
            { StopSession("방에 연결할 수 없습니다. 방 생성 여부와 주소를 확인해 주세요."); return; }
            if (!IsHost || HostGame == null) return;
            float dt = Mathf.Min(Time.deltaTime, .05f);
            if (world != null) world.EnsureWorld(HostGame.State);
            if (HostGame.State.phase == Phase.Expedition)
            {
                if (world != null) world.HostStep(HostGame, dt);
                HostGame.Tick(Time.deltaTime);
            }
            snapshotTimer -= Time.unscaledDeltaTime;
            if (snapshotTimer > 0) return;
            snapshotTimer = .1f;
            foreach (var id in manager.ConnectedClientsIds.ToArray())
            {
                var snapshot = HostGame.ForClient(id);
                if (id == LocalId) ReceiveSnapshot(snapshot);
                else SendText("snapshot", id, JsonUtility.ToJson(snapshot));
                if (!IsHost) break;
            }
        }
        public void SendVoice(byte[] samples)
        {
            if (!Online || LocalPlayer == null || samples == null || samples.Length != 800) return;
            if (IsHost) RouteVoice(LocalId, samples);
            else VoicePacket(NetworkManager.ServerClientId, LocalId, samples);
        }
        void OnVoice(ulong sender, FastBufferReader reader)
        {
            if (!Online || reader.Length > 850) return;
            try
            {
                reader.ReadValueSafe(out ulong speaker);
                reader.ReadValueSafe(out int size);
                if (size != 800) return;
                var data = new byte[size]; reader.ReadBytesSafe(ref data, size);
                if (IsHost) { if (Budget(sender)) RouteVoice(sender, data); }
                else if (sender == NetworkManager.ServerClientId && View.players.Any(p => p.id == speaker && p.connected)) radio?.Receive(speaker, data);
            }
            catch (Exception) { }
        }
        void RouteVoice(ulong speaker, byte[] data)
        {
            if (!IsHost) return;
            var talker = HostGame.Player(speaker);
            if (talker == null) return;
            if(talker.alive && HostGame.State.phase==Phase.Expedition) {HostGame.Emit("Communication",position:talker.position);HostGame.Noise(talker.position,12);}
            if(radio!=null && talker.alive) radio.Remember(samples: data);
            foreach (var p in HostGame.State.players)
            {
                if (!p.connected || p.id == speaker || (!talker.alive && p.alive)) continue;
                if (p.id == LocalId) radio?.Receive(speaker, data);
                else VoicePacket(p.id, speaker, data);
            }
        }
        void VoicePacket(ulong recipient, ulong speaker, byte[] data)
        {
            if (!Online || manager.CustomMessagingManager == null) return;
            using var writer = new FastBufferWriter(820, Allocator.Temp);
            writer.WriteValueSafe(speaker); writer.WriteValueSafe(data.Length); writer.WriteBytesSafe(data);
            manager.CustomMessagingManager.SendNamedMessage("voice", recipient, writer, NetworkDelivery.UnreliableSequenced);
        }
        void OnDestroy()
        {
            closing = true;
            if (rules != null) Destroy(rules);
            if (manager == null) return;
            manager.ConnectionApprovalCallback -= Approve;
            manager.OnClientConnectedCallback -= Connected;
            manager.OnClientDisconnectCallback -= Disconnected;
            manager.OnTransportFailure -= TransportFailed;
            manager.OnClientStopped -= Stopped;
            manager.OnServerStopped -= Stopped;
            if (manager.IsListening && !manager.ShutdownInProgress) manager.Shutdown();
        }
    }
}
