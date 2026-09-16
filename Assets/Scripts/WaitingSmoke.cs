#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace EarthRecovery
{
    public sealed class WaitingSmoke : MonoBehaviour
    {
        public WaitingRoom room;
        readonly List<NetworkSession> clients = new();
        readonly StringBuilder report = new();
        string output;
        Mouse testMouse, physicalMouse;
        Keyboard testKeyboard, physicalKeyboard;
        float deadline;
        IEnumerator Start()
        {
            var project = Application.isEditor ? Path.Combine(Application.dataPath, "..") : Path.Combine(Application.dataPath, "..", "..", "..");
            output = Path.GetFullPath(Path.Combine(project, "QA", "WaitingPortrait", "Runtime")); Directory.CreateDirectory(output);
            deadline = Time.realtimeSinceStartup + 100;
            Screen.SetResolution(1672, 941, false); yield return new WaitForSecondsRealtime(2);
            physicalMouse = Mouse.current; if (physicalMouse != null) InputSystem.DisableDevice(physicalMouse);
            testMouse = InputSystem.AddDevice<Mouse>("Waiting QA Mouse");
            physicalKeyboard = Keyboard.current; if (physicalKeyboard != null) InputSystem.DisableDevice(physicalKeyboard);
            testKeyboard = InputSystem.AddDevice<Keyboard>("Waiting QA Keyboard");
            var session = GetComponent<NetworkSession>(); var lobby = GetComponent<ExpeditionLobby>();
            Check(!room.Visible, "No waiting room before connection");
            GetComponent<MainMenu>().Activate(0);
            Check(lobby.Rooms.Create("대기실 검증", 6, false, ""), "Real host created");
            yield return new WaitForSecondsRealtime(.7f);
            session.HostGame.Player(session.LocalId).characterId = PlayerCharacterCatalog.ToxicBunnyId;
            yield return new WaitForSecondsRealtime(.3f);
            CheckButtonLayout();
            Check(room.Layout.Find("Options") == null, "Old settings text button removed");
            yield return Click((RectTransform)room.SettingsButton.transform);
            Check(room.OptionsOpen, "Gear opens settings"); yield return Capture("00-settings");
            yield return Escape(); Check(!room.OptionsOpen, "Escape closes settings");
            yield return Escape(); Check(room.OptionsOpen, "Escape opens settings");
            yield return Escape();
            Check(room.Layout.Find("SettingsTooltip") == null, "Settings hover text removed");
            var frame = (RectTransform)room.Layout.Find("Region/Frame");
            var underline = (RectTransform)room.Layout.Find("TitleUnderline");
            var regionRoot = (RectTransform)room.Layout.Find("Region");
            Check(frame.anchoredPosition.y == 0 && frame.sizeDelta.y == 410 && -regionRoot.anchoredPosition.y == -underline.anchoredPosition.y + underline.rect.height / 2, "Region top aligned with title underline");
            var heading = room.Layout.Find("Region/RegionLabel").GetComponent<TMPro.TMP_Text>();
            var footer = room.Layout.Find("Region/RoomCode").GetComponent<TMPro.TMP_Text>();
            heading.ForceMeshUpdate(); footer.ForceMeshUpdate();
            float topGap = -regionRoot.InverseTransformPoint(heading.transform.TransformPoint(heading.textBounds.max)).y;
            float bottomGap = regionRoot.rect.height + regionRoot.InverseTransformPoint(footer.transform.TransformPoint(footer.textBounds.min)).y;
            Check(Mathf.Abs(topGap - bottomGap) < .5f && Mathf.Abs(topGap - 16) < .5f, "Visible header and footer glyph padding match");
            Check(((RectTransform)room.Layout.Find("Region/RegionName")).anchoredPosition == new Vector2(18, -42), "Region name position unchanged");
            room.ChatInput.caretBlinkRate = 0;
            yield return Click((RectTransform)room.ChatInput.transform);
            yield return CheckCaret("empty"); yield return Capture("00-chat-empty");
            room.ChatInput.text = "한글 입력 확인"; room.ChatInput.MoveTextEnd(false);
            yield return new WaitForSecondsRealtime(.2f);
            yield return CheckCaret("Korean");
            Check(!room.ChatInput.placeholder.enabled, "Placeholder hides after typing");
            yield return Capture("00-chat-korean");
            room.ChatInput.text = ""; room.ChatInput.DeactivateInputField(); room.ChatInput.caretBlinkRate = .85f;
            var regionRect = (RectTransform)room.Layout.Find("Region"); var chatRect = (RectTransform)room.Layout.Find("ChatHistory");
            Check(-regionRect.anchoredPosition.y + regionRect.rect.height < -chatRect.anchoredPosition.y, "Region and chat do not overlap");
            var regionImage = room.Layout.Find("Region/Image").GetComponent<UnityEngine.UI.Image>();
            Check(regionImage.sprite == lobby.RegionSprite(session.View.regionId), "Waiting uses common region image");
            Check(Mathf.Abs(regionImage.rectTransform.rect.width / regionImage.rectTransform.rect.height - regionImage.sprite.rect.width / regionImage.sprite.rect.height) < .001f, "Region image preserves complete source aspect");
            for (int count = 1; count <= 6; count++)
            {
                if (count > 1)
                {
                    var client = new GameObject("Waiting QA client " + count).AddComponent<NetworkSession>(); client.PersistArchive = false; clients.Add(client);
                    var lan = client.gameObject.AddComponent<LanRooms>(); lan.Initialize(client);
                    var target = JsonUtility.FromJson<LanRoom>(JsonUtility.ToJson(lobby.Rooms.Hosted)); target.address = "127.0.0.1";
                    Check(lan.Join(target, ""), "Join dispatched " + count); yield return new WaitForSecondsRealtime(.8f);
                    Check(client.Online, "Actual client connected " + count);
                    client.UserName = count == 6 ? "긴이름가나다라마바사아자차카타파하" : "요원 " + count;
                    client.Send(new Command { action = "name", text = client.UserName });
                }
                var assigned = session.HostGame.State.players.Where(p => p.connected).OrderBy(p => p.id).ToArray();
                for (int i = 0; i < assigned.Length; i++)
                    assigned[i].characterId = i % 2 == 0 ? PlayerCharacterCatalog.ToxicBunnyId : PlayerCharacterCatalog.DefaultId;
                yield return new WaitForSecondsRealtime(.3f);
                Check(room.Visible && !lobby.Visible && !GetComponent<MainMenu>().Visible, "Independent screen " + count);
                Check(room.Cards.Count(c => c.gameObject.activeSelf) == count, "Actual card count " + count);
                Check(!room.StartButton.interactable, "Unready players prevent start " + count);
                for (int i = 0; i < count; i++)
                {
                    var rect = WaitingRoom.CardRect(count, i);
                    Check(rect.xMin >= 200 && rect.xMax <= 1160 && rect.yMin >= 292 && rect.yMax <= 724, "Card bounds " + count + "/" + i);
                    Check(room.Cards[i].transform.Find("HostBadge").gameObject.activeSelf == (i == 0), "Host badge " + count + "/" + i);
                    Check(!room.Cards[i].transform.Find("ReadyHighlight").gameObject.activeSelf, "Unready highlight off " + count + "/" + i);
                    var portrait = room.Cards[i].GetComponentInChildren<UnityEngine.UI.RawImage>();
                    Check(portrait.texture == room.PortraitFor(assigned[i].characterId), "Portrait matches assigned character " + count + "/" + i);
                }
                yield return Capture("01-players-" + count);
            }
            yield return Click((RectTransform)room.ReadyButton.transform);
            Check(room.ReadyButton.transform.Find("Highlight").GetComponent<WaitingFocusGradient>() != null, "Full button gradient replaces stripe overlay");
            yield return Capture("02-hover-gradient");
            foreach (var client in clients) client.Send(new Command { action = "ready", flag = true });
            yield return new WaitForSecondsRealtime(.5f);
            Check(room.StartButton.interactable, "Six ready players enable host start");
            Check(room.Cards.All(c => c.transform.Find("ReadyHighlight").gameObject.activeSelf), "Ready highlights replicated");
            clients[0].Send(new Command { action = "voiceState", flag = true });
            clients[0].Send(new Command { action = "chat", text = "통신 확인: 함께 출발합시다." });
            yield return new WaitForSecondsRealtime(.4f);
            Check(session.View.lobbyChat.Count == 1 && clients.All(c => c.View.lobbyChat.Count == 1), "Chat replicated to all six players");
            Check(clients.All(c => c.View.regionId == session.View.regionId), "Host region replicated to every client");
            Check(room.Cards[1].transform.Find("Voice").GetComponent<UnityEngine.UI.Image>().sprite == room.Asset("03_PlayerCard/PlayerCard_Voice_On") && session.View.players[1].voiceEnabled, "Voice enabled state replicated");
            room.ChatInput.text = "네, 준비 완료했습니다."; yield return Click((RectTransform)room.SendButton.transform);
            yield return new WaitForSecondsRealtime(.4f);
            Check(session.View.lobbyChat.Count == 2 && room.ChatInput.text == "", "Chat send button works");
            yield return Capture("02-ready-chat");
            Check(room.PortraitFor(PlayerCharacterCatalog.ToxicBunnyId) != room.PortraitFor(PlayerCharacterCatalog.DefaultId), "Characters have distinct portraits");
            var portraits = room.GetComponentInChildren<WaitingCharacterPortraits>();
            var bunny = portraits.GetComponentsInChildren<HumanThingsCharacterVisual>().Single(v => v.name == PlayerCharacterCatalog.ToxicBunnyId);
            Check(!bunny.ShowingOriginal && bunny.skin.sharedMaterial.GetTexture("_BaseMap").name == "ToxicBunny_R31_BaseColor_4K", "Bunny portrait uses corrected gameplay texture");
            Check(portraits.GetComponentsInChildren<Camera>().All(c => !c.enabled), "Cached portraits stop rendering");
            foreach (var character in PlayerCharacterCatalog.Load().characters) CapturePortrait(character.id);
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(1024, 768) })
            {
                Screen.SetResolution(size.x, size.y, false); yield return new WaitForSecondsRealtime(.6f);
                var corners = new Vector3[4]; room.Layout.GetWorldCorners(corners);
                Check(corners.All(p => p.x >= -1 && p.y >= -1 && p.x <= Screen.width + 1 && p.y <= Screen.height + 1), "Canvas fits " + size);
                CheckButtonLayout();
                CheckPortraitResolution();
                yield return Capture("03-size-" + size.x + "x" + size.y);
            }
            clients[0].Send(new Command { action = "ready", flag = false }); yield return new WaitForSecondsRealtime(.4f);
            Check(!room.StartButton.interactable, "Ready cancellation disables start");
            clients[0].Send(new Command { action = "ready", flag = true }); yield return new WaitForSecondsRealtime(.4f);
            yield return Click((RectTransform)room.StartButton.transform); yield return new WaitForSecondsRealtime(.8f);
            Check(session.View.phase != Phase.Lobby && !room.Visible, "Host start enters expedition and hides room");
            Check(portraits.GetComponentsInChildren<Camera>().All(c => !c.enabled) && portraits.GetComponentsInChildren<Light>().All(l => !l.enabled), "Portrait cameras and lights disabled in expedition");
            foreach (var client in clients) client.Leave(); session.Leave(); yield return new WaitForSecondsRealtime(.8f);
            Check(!room.Visible && lobby.Visible, "Disconnect returns to browser");
            foreach (var client in clients) Destroy(client.gameObject); clients.Clear();
            var host = new GameObject("Waiting QA second host").AddComponent<NetworkSession>(); clients.Add(host); host.PersistArchive = false;
            var hosts = host.gameObject.AddComponent<LanRooms>(); hosts.Initialize(host); Check(hosts.Create("클라이언트 검증", 4, false, ""), "Second host created"); yield return new WaitForSecondsRealtime(.4f);
            var destination = hosts.Hosted; destination.address = "127.0.0.1"; Check(lobby.Rooms.Join(destination, ""), "Primary joins as client"); yield return new WaitForSecondsRealtime(.8f);
            Check(session.Online && room.Visible && !room.StartButton.gameObject.activeSelf, "Client has no start button"); yield return Capture("04-client");
            host.Leave(); yield return new WaitForSecondsRealtime(1);
            Check(!session.Online && !room.Visible && lobby.Visible, "Host loss clears waiting room");
            Finish();
        }
        void Update() { if (deadline > 0 && Time.realtimeSinceStartup > deadline) { Check(false, "QA timeout"); Finish(); } }
        void Finish() { deadline = 0; File.WriteAllText(Path.Combine(output, "verification.txt"), report.ToString()); Debug.Log(report.ToString()); Application.Quit(report.ToString().Contains("FAIL") ? 1 : 0); }
        void Check(bool value, string message) { report.AppendLine((value ? "PASS " : "FAIL ") + message); }
        void CapturePortrait(string id)
        {
            var texture = room.PortraitFor(id);
            var previous = RenderTexture.active; RenderTexture.active = texture;
            var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); pixels.Apply(); RenderTexture.active = previous;
            var colors = pixels.GetPixels();
            Check(colors.Max(c => c.grayscale) - colors.Min(c => c.grayscale) > .08f, "Character portrait is nonblank " + id);
            File.WriteAllBytes(Path.Combine(output, "portrait-" + id + ".png"), pixels.EncodeToPNG()); Destroy(pixels);
        }
        void CheckPortraitResolution()
        {
            var expected = WaitingRoom.PortraitResolution(room.Layout.GetComponentInParent<Canvas>().scaleFactor);
            foreach (var card in room.Cards.Where(c => c.gameObject.activeSelf))
            {
                var texture = card.GetComponentInChildren<UnityEngine.UI.RawImage>().texture;
                Check(texture.width == expected.x && texture.height == expected.y, "Portrait resolution " + Screen.width + "/" + card.PlayerId);
            }
        }
        IEnumerator Capture(string name) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png")); yield return new WaitForSecondsRealtime(.2f); }
        IEnumerator Escape()
        { InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Escape)); yield return new WaitForSecondsRealtime(.15f); InputSystem.QueueStateEvent(testKeyboard, new KeyboardState()); yield return new WaitForSecondsRealtime(.15f); }
        void CheckButtonLayout()
        {
            Canvas.ForceUpdateCanvases();
            var ready = (RectTransform)room.ReadyButton.transform; var start = (RectTransform)room.StartButton.transform;
            var leave = new Vector3[4]; var r = new Vector3[4]; var s = new Vector3[4]; var chat = new Vector3[4];
            ((RectTransform)room.LeaveButton.transform).GetWorldCorners(leave); ready.GetWorldCorners(r); start.GetWorldCorners(s);
            ((RectTransform)room.ChatInput.transform).GetWorldCorners(chat);
            Check(ready.rect.size == start.rect.size, "Ready and start sizes match " + Screen.width);
            Check(Mathf.Abs(leave[0].y - r[0].y) < .5f && Mathf.Abs(r[0].y - s[0].y) < .5f, "All action bottoms align " + Screen.width);
            Check(Mathf.Abs(leave[0].x - (Screen.width - chat[2].x)) < .5f, "Leave left and chat right screen margins match " + Screen.width);
            var a = room.ReadyButton.GetComponentInChildren<TMPro.TMP_Text>(); var b = room.StartButton.GetComponentInChildren<TMPro.TMP_Text>();
            Check(a.fontSizeMax == b.fontSizeMax && a.alignment == b.alignment && a.rectTransform.anchoredPosition == b.rectTransform.anchoredPosition && a.rectTransform.rect.size == b.rectTransform.rect.size, "Ready and start text styling matches " + Screen.width);
        }
        IEnumerator CheckCaret(string state)
        {
            Canvas.ForceUpdateCanvases(); yield return new WaitForEndOfFrame();
            var caret = room.ChatInput.GetComponentInChildren<TMPro.TMP_SelectionCaret>(true);
            var mesh = caret != null ? caret.canvasRenderer.GetMesh() : null;
            Check(room.ChatInput.isFocused && mesh != null && mesh.vertexCount == 4, "Caret visible " + state);
            if (mesh != null && mesh.vertexCount == 4)
            {
                var vertices = mesh.vertices.Select(v => caret.transform.TransformPoint(v)).ToArray();
                float min = vertices.Min(v => v.y), max = vertices.Max(v => v.y);
                var viewport = room.ChatInput.textViewport;
                float center = viewport.TransformPoint(viewport.rect.center).y;
                Check(Mathf.Abs((min + max) / 2 - center) < 5, "Caret vertically centered " + state);
                Check(max - min >= 8 && max - min < 35, "Caret height fits input " + state);
            }
        }
        IEnumerator Click(RectTransform target)
        {
            var point = RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(target.rect.center));
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = point }); yield return new WaitForSecondsRealtime(.1f);
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = point }.WithButton(MouseButton.Left)); yield return new WaitForSecondsRealtime(.1f);
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = point }); yield return new WaitForSecondsRealtime(.2f);
        }
        void OnDestroy() { foreach (var client in clients) if (client != null) Destroy(client.gameObject); if (testMouse != null && testMouse.added) InputSystem.RemoveDevice(testMouse); if (physicalMouse != null && physicalMouse.added) InputSystem.EnableDevice(physicalMouse); if (testKeyboard != null && testKeyboard.added) InputSystem.RemoveDevice(testKeyboard); if (physicalKeyboard != null && physicalKeyboard.added) InputSystem.EnableDevice(physicalKeyboard); }
    }
}
#endif
