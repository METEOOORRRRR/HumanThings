using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace EarthRecovery
{
    [Serializable] public sealed class LanRoom
    {
        public int version = 1, port, players, capacity;
        public string id, name, region = "서울 (도심)";
        public string regionId = ExpeditionRegions.DefaultId;
        public bool locked, playing;
        [NonSerialized] public string address;
        [NonSerialized] public float seen;
        public string Code => LanRooms.EncodeCode(address, port, id);
    }
    [Serializable] public sealed class RoomAdmission { public string id, proof; }

    public sealed class LanRooms : MonoBehaviour
    {
        public const int FirstPort = 7777, PortCount = 8, DiscoveryOffset = 30000;
        const string Query = "HUMANTHINGS_LAN_1";
        public readonly List<LanRoom> Rooms = new();
        public NetworkSession Session { get; private set; }
        public LanRoom Hosted { get; private set; }
        public bool Searching { get; private set; }
        public bool Resolving { get; private set; }
        public string Status { get; private set; } = "";
        public event Action Changed;
        public event Action<LanRoom> Resolved;
        UdpClient client, host;
        bool isPublic;
        float nextQuery, resolveDeadline;
        string resolveAddress, resolveTag;
        int resolvePort;

        public void Initialize(NetworkSession session) { Session = session; }
        public static string EncodeCode(string address, int port, string id)
        {
            if (!IPAddress.TryParse(address, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork || port < FirstPort || port >= FirstPort + PortCount || !Guid.TryParseExact(id, "N", out _)) return "";
            var bytes = ip.GetAddressBytes();
            return string.Concat(bytes.Select(b => b.ToString("X2"))) + port.ToString("X4") + id.Substring(0, 4).ToUpperInvariant();
        }
        public static bool DecodeCode(string value, out string address, out int port, out string tag)
        {
            address = tag = ""; port = 0;
            if (value == null || value.Length > 64) return false;
            value = new string(value.Where(c => c != '-' && !char.IsWhiteSpace(c)).ToArray());
            if (value.Length != 16 || value.Any(c => !Uri.IsHexDigit(c))) return false;
            var bytes = new byte[4];
            for (int i = 0; i < 4; i++) bytes[i] = Convert.ToByte(value.Substring(i * 2, 2), 16);
            if (bytes[0] == 0 || bytes[0] >= 224 || bytes[3] == 255) return false;
            port = Convert.ToInt32(value.Substring(8, 4), 16);
            if (port < FirstPort || port >= FirstPort + PortCount) return false;
            address = new IPAddress(bytes).ToString(); tag = value.Substring(12, 4).ToLowerInvariant(); return true;
        }
        public static string PasswordProof(string id, string password)
        {
            using var sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(id + ":" + (password ?? ""))));
        }
        public static bool ValidRoom(LanRoom room) => room != null && room.version == 1 && Guid.TryParseExact(room.id, "N", out _)
            && !string.IsNullOrWhiteSpace(room.name) && room.name.Length <= 20 && room.name.All(c => !char.IsControl(c))
            && ExpeditionRegions.Find(room.regionId) != null && ExpeditionRegions.Find(room.regionId).name == room.region && room.port >= FirstPort && room.port < FirstPort + PortCount
            && room.capacity >= 4 && room.capacity <= 6 && room.players >= 1 && room.players <= room.capacity;

        public bool Create(string name, int capacity, bool publicRoom, string password, string regionId = ExpeditionRegions.DefaultId)
        {
            if (!Session.CanConnect || ExpeditionRegions.Find(regionId) == null) return false;
            name = (name ?? "").Trim();
            if (name.Length < 1 || name.Length > 20 || name.Any(char.IsControl) || capacity < 4 || capacity > 6 || (password?.Length ?? 0) > 6)
            { SetStatus("방 설정을 확인해 주세요."); return false; }
            StopHosting();
            for (int port = FirstPort; port < FirstPort + PortCount; port++)
            {
                try
                {
                    using (var probe = new UdpClient(new IPEndPoint(IPAddress.Any, port))) { }
                    host = new UdpClient(new IPEndPoint(IPAddress.Any, port + DiscoveryOffset));
                    Hosted = new LanRoom { id = Guid.NewGuid().ToString("N"), name = name, regionId = regionId, region = ExpeditionRegions.Find(regionId).name, port = port, capacity = capacity, players = 1, locked = !string.IsNullOrEmpty(password), address = LocalAddress() };
                    isPublic = publicRoom; Session.rules.maxPlayers = capacity;
                    bool ok = Session.Connect(true, "127.0.0.1", (ushort)port, Hosted.id, PasswordProof(Hosted.id, password));
                    if (ok) { Session.HostGame.State.regionId = regionId; Session.View.regionId = regionId; }
                    if (!ok) StopHosting();
                    SetStatus(Session.Status); return ok;
                }
                catch (SocketException) { host?.Close(); host = null; Hosted = null; }
            }
            SetStatus("방 생성 실패: 사용할 수 있는 LAN 포트가 없습니다."); return false;
        }
        public bool Join(LanRoom room, string password)
        {
            if (!ValidRoom(room) || room.playing || room.players >= room.capacity || !Session.CanConnect) { SetStatus("참가할 수 없는 방입니다. 목록을 새로고침해 주세요."); return false; }
            bool result = Session.Connect(false, room.address, (ushort)room.port, room.id, PasswordProof(room.id, password));
            SetStatus(Session.Status); return result;
        }
        public void BeginSearch() { Searching = true; Refresh(); }
        public void StopSearch() { Searching = false; Resolving = false; }
        public void Refresh()
        {
            Rooms.RemoveAll(r => Time.realtimeSinceStartup - r.seen > 6);
            if (!EnsureClient()) return;
            for (int port = FirstPort; port < FirstPort + PortCount; port++)
            {
                Send(client, Query, IPAddress.Broadcast, port + DiscoveryOffset);
                Send(client, Query, IPAddress.Loopback, port + DiscoveryOffset);
            }
            nextQuery = Time.realtimeSinceStartup + 2; SetStatus(Rooms.Count == 0 ? "공개 방을 찾는 중입니다." : "");
        }
        public void Resolve(string code)
        {
            if (!DecodeCode(code, out resolveAddress, out resolvePort, out resolveTag)) { SetStatus("올바른 참가 코드 16자리를 입력해 주세요."); return; }
            if (!EnsureClient()) return;
            Resolving = true; resolveDeadline = Time.realtimeSinceStartup + 3;
            Send(client, Query + ":" + resolveTag, IPAddress.Parse(resolveAddress), resolvePort + DiscoveryOffset);
            SetStatus("참가 코드를 확인하는 중입니다.");
        }
        bool EnsureClient()
        {
            if (client != null) return true;
            try { client = new UdpClient(new IPEndPoint(IPAddress.Any, 0)); client.EnableBroadcast = true; return true; }
            catch (SocketException) { SetStatus("LAN 검색 소켓을 열지 못했습니다."); return false; }
        }
        static void Send(UdpClient socket, string text, IPAddress address, int port)
        {
            try { var bytes = Encoding.UTF8.GetBytes(text); socket.Send(bytes, bytes.Length, new IPEndPoint(address, port)); }
            catch (SocketException) { /* A blocked interface must not prevent queries on other interfaces. */ }
        }
        void Update()
        {
            if (Session == null) return;
            if (Hosted != null && !Session.IsHost && !Session.IsConnecting) StopHosting();
            if (host != null && Hosted != null && Session.IsHost)
            {
                Hosted.players = Session.View.players.Count(p => p.connected); Hosted.playing = Session.View.phase != Phase.Lobby;
                Poll(host, (bytes, sender) =>
                {
                    if (bytes.Length > 64) return;
                    string query = Encoding.UTF8.GetString(bytes);
                    if ((isPublic && query == Query) || query == Query + ":" + Hosted.id.Substring(0, 4))
                        Send(host, JsonUtility.ToJson(Hosted), sender.Address, sender.Port);
                });
            }
            if (Searching && Time.realtimeSinceStartup >= nextQuery) Refresh();
            if (client != null) Poll(client, Receive);
            if (Resolving && Time.realtimeSinceStartup >= resolveDeadline) { Resolving = false; SetStatus("방을 찾을 수 없습니다. 코드와 같은 네트워크인지 확인해 주세요."); }
        }
        static void Poll(UdpClient socket, Action<byte[], IPEndPoint> receive)
        {
            try
            {
                for (int i = 0; i < 16 && socket.Available > 0; i++)
                {
                    var sender = new IPEndPoint(IPAddress.Any, 0); var bytes = socket.Receive(ref sender);
                    if (bytes.Length <= 2048) receive(bytes, sender);
                }
            }
            catch (SocketException) { }
        }
        void Receive(byte[] bytes, IPEndPoint sender)
        {
            LanRoom room;
            try { room = JsonUtility.FromJson<LanRoom>(Encoding.UTF8.GetString(bytes)); }
            catch (ArgumentException) { return; }
            if (!ValidRoom(room) || sender.Port != room.port + DiscoveryOffset) return;
            room.address = sender.Address.ToString(); room.seen = Time.realtimeSinceStartup;
            if (Resolving && room.address == resolveAddress && room.port == resolvePort && room.id.StartsWith(resolveTag, StringComparison.Ordinal))
            { Resolving = false; SetStatus(""); Resolved?.Invoke(room); return; }
            if (!Searching) return;
            var existing = Rooms.Find(r => r.id == room.id);
            if (existing == null && Rooms.Count >= 64) return;
            if (existing != null) { if (room.address == "127.0.0.1" && existing.address != "127.0.0.1") room.address = existing.address; Rooms.Remove(existing); }
            Rooms.Add(room); Rooms.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal)); SetStatus("");
        }
        void SetStatus(string value) { Status = value; Changed?.Invoke(); }
        void StopHosting() { host?.Close(); host = null; Hosted = null; }
        static string LocalAddress()
        {
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback).OrderByDescending(n => n.GetIPProperties().GatewayAddresses.Count))
                    foreach (var address in nic.GetIPProperties().UnicastAddresses)
                        if (address.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address.Address)) return address.Address.ToString();
            }
            catch (NetworkInformationException) { }
            return "127.0.0.1";
        }
        void OnDestroy() { StopHosting(); client?.Close(); client = null; }
    }
}
