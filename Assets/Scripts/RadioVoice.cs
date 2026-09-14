using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EarthRecovery
{
    public interface IVoiceMimicSource { byte[] RecentToken(); AudioClip Legacy(int index); }
    // Opt-in push-to-talk, 8 kHz mono radio. No recording is saved or sent to an external service.
    public sealed class RadioVoice : MonoBehaviour, IVoiceMimicSource
    {
        public NetworkSession session;
        public bool Enabled, Muted;
        public string Status = "마이크 꺼짐";
        AudioClip microphone;
        string device;
        int lastRead;
        float microphoneStarted;
        readonly Queue<byte> recent = new();
        public IVoiceMimicSource MimicSource;
        public byte[] RecentToken() => recent.ToArray();
        public AudioClip Legacy(int index)
        { var bank=HumanContent.Load().monsters; var echo=System.Array.Find(bank,m=>m.kind==MonsterKind.Echo);return echo!=null&&echo.legacyClips.Length>0?echo.legacyClips[index%echo.legacyClips.Length]:null; }
        public void Remember(byte[] samples)
        { foreach(byte sample in samples) recent.Enqueue(sample); while(recent.Count>8000) recent.Dequeue(); }
        void Start() { MimicSource ??= this; session.Presented+=Present; }
        void Present(GameEvent e)
        {
            if(Muted || (e.kind!="EchoLegacy"&&e.kind!="EchoMimic")) return;
            if(e.kind=="EchoMimic" && !string.IsNullOrEmpty(e.text))
            {
                try
                {
                    var bytes=System.Convert.FromBase64String(e.text);if(bytes.Length>8000||bytes.Length<800)return;
                    var go=new GameObject("Echo radio token");go.transform.SetParent(transform);var stream=go.AddComponent<VoiceStream>();
                    for(int start=0;start+800<=bytes.Length;start+=800) {var packet=new byte[800];System.Array.Copy(bytes,start,packet,0,800);stream.Enqueue(packet);}
                    Destroy(go,2);return;
                }
                catch(System.FormatException) {return;}
            }
            var clip=MimicSource.Legacy(Mathf.Abs(e.sequence));
            if(clip!=null) AudioSource.PlayClipAtPoint(clip,e.position,.65f);
        }
        readonly Dictionary<ulong, VoiceStream> streams = new();
        public readonly Dictionary<ulong, int> ReceivedPackets = new();
        public bool Talking => Enabled && Keyboard.current != null && Keyboard.current.vKey.isPressed && Application.isFocused;
        public void StartMicrophone()
        {
            if (Enabled) return;
            if (Microphone.devices.Length == 0) { Status = "마이크 없음"; return; }
            device = Microphone.devices[0];
            try { microphone = Microphone.Start(device, true, 2, 8000); }
            catch (System.Exception e) when (e is UnityException || e is System.ArgumentException)
            { StopMicrophone(); Status = "마이크 시작 실패 · 권한과 장치 확인"; return; }
            microphoneStarted = Time.realtimeSinceStartup;
            Enabled = microphone != null; lastRead = 0;
            Status = Enabled ? "마이크 켜짐" : "마이크 시작 실패";
        }
        public void StopMicrophone()
        {
            if (device != null && System.Array.IndexOf(Microphone.devices, device) >= 0) Microphone.End(device);
            if (microphone != null) Destroy(microphone);
            device = null;
            microphone = null; Enabled = false; Status = "마이크 꺼짐";
        }
        public void ResetSession()
        {
            StopMicrophone();
            foreach (var stream in streams.Values)
                if (stream != null) { stream.gameObject.SetActive(false); Destroy(stream.gameObject); }
            streams.Clear(); ReceivedPackets.Clear();
            recent.Clear();
        }
        void Update()
        {
            foreach (var id in new List<ulong>(streams.Keys))
                if (session == null || !session.Online || !session.View.players.Exists(p => p.id == id && p.connected)) Forget(id);
            if (!Enabled || microphone == null) return;
            if (System.Array.IndexOf(Microphone.devices, device) < 0 || microphone.samples <= 0 || microphone.frequency < 10)
            { StopMicrophone(); Status = "마이크 연결 끊김"; return; }
            int current = Microphone.GetPosition(device);
            if (current < 0 || (current == 0 && Time.realtimeSinceStartup - microphoneStarted > 5 && !Microphone.IsRecording(device)))
            { StopMicrophone(); Status = "마이크 응답 없음"; return; }
            int available = (current - lastRead + microphone.samples) % microphone.samples;
            if (!Talking || session == null || !session.Online) { lastRead = current; return; }
            int sourceCount = microphone.frequency / 10;
            if (available > sourceCount * 3) { lastRead = current; return; }
            while (available >= sourceCount)
            {
                var source = new float[sourceCount * microphone.channels];
                if (!microphone.GetData(source, lastRead)) { StopMicrophone(); Status = "마이크 읽기 실패"; return; }
                var packet = new byte[800];
                for (int i = 0; i < packet.Length; i++)
                {
                    int index = Mathf.Min(sourceCount - 1, i * sourceCount / packet.Length) * microphone.channels;
                    packet[i] = (byte)Mathf.Clamp(Mathf.RoundToInt((source[index] + 1) * 127.5f), 0, 255);
                }
                session.SendVoice(packet);
                lastRead = (lastRead + sourceCount) % microphone.samples; available -= sourceCount;
            }
        }
        public void Receive(ulong speaker, byte[] data)
        {
            if (data == null || data.Length != 800 || session == null || !session.Online || !session.View.players.Exists(p => p.id == speaker && p.connected)) return;
            ReceivedPackets[speaker] = ReceivedPackets.TryGetValue(speaker, out int count) ? count + 1 : 1;
            if (Muted) return;
            if (!streams.TryGetValue(speaker, out var stream) || stream == null)
            {
                var go = new GameObject("Radio " + speaker); go.transform.SetParent(transform);
                stream = go.AddComponent<VoiceStream>(); streams[speaker] = stream;
            }
            stream.Enqueue(data);
        }
        public void Forget(ulong speaker)
        {
            if (streams.TryGetValue(speaker, out var stream) && stream != null)
            { stream.gameObject.SetActive(false); Destroy(stream.gameObject); }
            streams.Remove(speaker); ReceivedPackets.Remove(speaker);
        }
        void OnDestroy() {if(session!=null) session.Presented-=Present;StopMicrophone();}
    }
    public sealed class VoiceStream : MonoBehaviour
    {
        readonly Queue<float> queue = new();
        readonly object gate = new();
        AudioClip clip;
        void Awake()
        {
            var source = gameObject.AddComponent<AudioSource>();
            clip = AudioClip.Create("Radio stream", 800, 1, 8000, true, Fill);
            source.clip = clip; source.loop = true; source.spatialBlend = 0; source.volume = .7f; source.Play();
        }
        public void Enqueue(byte[] data)
        {
            if (data == null || data.Length != 800) return;
            lock (gate)
            {
                if (queue.Count > 4000) queue.Clear();
                foreach (byte value in data) queue.Enqueue(value / 127.5f - 1);
            }
        }
        void Fill(float[] data)
        {
            lock (gate) for (int i = 0; i < data.Length; i++) data[i] = queue.Count > 0 ? queue.Dequeue() : 0;
        }
        void OnDestroy() { if (clip != null) Destroy(clip); }
    }
}
