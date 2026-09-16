using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EarthRecovery
{
    public sealed class WaitingRoom : MonoBehaviour
    {
        public bool Visible { get; private set; }
        public RectTransform Layout { get; private set; }
        public readonly List<WaitingPlayerCard> Cards = new();
        public Button LeaveButton, ReadyButton, StartButton, SendButton;
        public TMP_InputField ChatInput { get; private set; }
        NetworkSession session;
        GameHud hud;
        ExpeditionLobby lobby;
        GameObject canvasObject, portraitStage, options, optionsBackdrop;
        TMP_FontAsset font;
        TMP_FontAsset symbolFont;
        public Button SettingsButton { get; private set; }
        public bool OptionsOpen => options != null && options.activeSelf;
        Image regionImage;
        TMP_Text regionName, regionDescription, regionHeading;
        string alignedRegionCode;
        string shownRegion;
        readonly Dictionary<string, Sprite> sprites = new();
        TMP_Text readyText, message, chatHistory, roomCode, oxygenText, micText, muteText;
        ScrollRect chatScroll;
        RectTransform chatContent;
        WaitingCharacterPortraits portraits;
        float nextVoice, nextChat;
        int lastChat = -1;
        bool previousVisible;
        WaitingButtonSkin readySkin;
        readonly Color ink = new(.86f, .84f, .78f), muted = new(.52f, .54f, .5f), green = new(.61f, .81f, .58f);
        public void Initialize(NetworkSession network, GameHud gameHud, ExpeditionLobby browser)
        {
            session = network; hud = gameHud; lobby = browser; hud.WaitingMenu = this;
            enabled = false;
            font = TMP_FontAsset.CreateFontAsset("Batang", "Regular", 64)
                ?? TMP_FontAsset.CreateFontAsset("Malgun Gothic", "Regular", 64);
            if (font == null) throw new InvalidOperationException("Cannot create waiting-room Korean font");
            canvasObject = new GameObject("ExpeditionWaitingRoomCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false); canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1672, 941); scaler.matchWidthOrHeight = .5f;
            var black = Node("Letterbox", canvasObject.transform, new Rect(0, 0, 1672, 941)); black.anchorMin = Vector2.zero; black.anchorMax = Vector2.one; black.offsetMin = black.offsetMax = Vector2.zero; black.gameObject.AddComponent<Image>().color = Color.black;
            Layout = Node("ReferenceCanvas", canvasObject.transform, new Rect(0, 0, 1672, 941)); Layout.anchorMin = Layout.anchorMax = Layout.pivot = new Vector2(.5f, .5f); Layout.anchoredPosition = Vector2.zero;
            Picture("Background", Layout, "01_Background/Background_Artwork", new Rect(0, 0, 1672, 941));
            Picture("Noise", Layout, "01_Background/Global_Noise_Overlay", new Rect(0, 0, 1672, 941)).color = new Color(1, 1, 1, .07f);
            Label("Title", Layout, "탐사대 대기실", new Rect(460, 120, 752, 90), 64, ink, TextAlignmentOptions.Center);
            Picture("TitleUnderline", Layout, "02_Title/Title_Underline", new Rect(626, 207, 420, 14));
            Label("Subtitle", Layout, "함께, 아직 남아 있는 것을 찾아서.", new Rect(510, 226, 652, 34), 23, ink, TextAlignmentOptions.Center);
            Label("ArchivePoem", Layout, "ARCHIVE\nTHE PLACES\nTHE PEOPLE\nTHE THINGS\nTHAT REMAIN", new Rect(69, 32, 210, 104), 14, muted);
            Label("WaitingPoem", Layout, "SOME\nTHINGS\nSTILL\nWAIT", new Rect(48, 300, 100, 100), 12, muted);
            Label("RightTop", Layout, "잊힌 세계를 걷는 일.\n그것은 여전히, 인간의 일이다.", new Rect(1240, 36, 250, 50), 13, muted);
            Label("Date", Layout, "2147. 03", new Rect(1430, 98, 108, 24), 13, muted, TextAlignmentOptions.Right);
            Label("Version", Layout, "v0.1.0", new Rect(45, 894, 160, 24), 14, muted);
            Picture("Photo", Layout, "08_Decorations/Misc_Decoration_Photo", new Rect(1530, 643, 160, 213));
            Picture("Decoration", Layout, "08_Decorations/Decorative_Line", new Rect(1240, 681, 330, 8));
            BuildPortrait();
            for (int i = 0; i < 6; i++) Cards.Add(BuildCard(i));
            BuildRegion(); BuildActions(); BuildChat(); BuildOptions();
            Picture("Edge", Layout, "01_Background/Edge_Scratch_Overlay", new Rect(0, 0, 1672, 941));
            canvasObject.SetActive(false);
            enabled = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--waiting-qa") >= 0) gameObject.AddComponent<WaitingSmoke>().room = this;
#endif
        }
        static RectTransform Node(string name, Transform parent, Rect rect) => MainMenu.Node(name, parent, rect);
        public Sprite Asset(string path)
        {
            if (sprites.TryGetValue(path, out var sprite)) return sprite;
            var texture = Resources.Load<Texture2D>("WaitingRoom/" + path);
            if (texture == null) throw new InvalidOperationException("Missing waiting-room asset: " + path);
            // These button sources include a baked white focus stripe. Use only their base surface; focus is a full-height UI gradient.
            int stripe = path == "06_Buttons/Ready_Ready_BG_9Slice" || path == "06_Buttons/Start_Enabled_BG_9Slice" ? 22 : 0;
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height - stripe), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, path.Contains("9Slice") ? Vector4.one * 12 : Vector4.zero);
            sprites.Add(path, sprite); return sprite;
        }
        Image Picture(string name, Transform parent, string asset, Rect rect)
        {
            var image = Node(name, parent, rect).gameObject.AddComponent<Image>(); image.sprite = Asset(asset); image.raycastTarget = false;
            image.type = asset.Contains("9Slice") ? Image.Type.Sliced : Image.Type.Simple; return image;
        }
        WaitingFocusGradient Focus(Transform parent, Rect rect)
        {
            var effect = Node("Highlight", parent, rect).gameObject.AddComponent<WaitingFocusGradient>(); effect.raycastTarget = false;
            return effect;
        }
        TMP_Text Label(string name, Transform parent, string text, Rect rect, float size, Color? color = null, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var label = Node(name, parent, rect).gameObject.AddComponent<TextMeshProUGUI>(); label.font = font; label.text = text; label.fontSize = size;
            label.color = color ?? ink; label.alignment = align; label.richText = false; label.raycastTarget = false; label.margin = Vector4.zero;
            label.enableAutoSizing = true; label.fontSizeMax = size; label.fontSizeMin = size * .72f; return label;
        }
        Button ActionButton(string name, Transform parent, string text, Rect rect, string prefix, Action click)
        {
            var root = Node(name, parent, rect);
            var image = Picture("Background", root, "06_Buttons/" + prefix + "_Default_BG_9Slice", new Rect(0, 0, rect.width, rect.height)); image.raycastTarget = true;
            Picture("Frame", root, "06_Buttons/Button_Frame_9Slice", new Rect(0, 0, rect.width, rect.height));
            var highlight = Focus(root, new Rect(0, 0, rect.width, rect.height));
            var disabled = Picture("Disabled", root, "06_Buttons/Button_Disabled_Overlay", new Rect(0, 0, rect.width, rect.height));
            var label = Label("Label", root, text, new Rect(10, 0, rect.width - 20, rect.height), 27, ink, TextAlignmentOptions.Center);
            var button = root.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.transition = Selectable.Transition.None; button.onClick.AddListener(() => click());
            var skin = root.gameObject.AddComponent<WaitingButtonSkin>();
            skin.Initialize(button, image, label, highlight.gameObject, disabled.gameObject, Asset("06_Buttons/" + prefix + "_Default_BG_9Slice"));
            return button;
        }
        WaitingPlayerCard BuildCard(int index)
        {
            var root = Node("PlayerCard_" + index, Layout, new Rect(0, 0, 188, 378));
            Picture("Background", root, "03_PlayerCard/PlayerCard_Card_BG_9Slice", new Rect(0, 0, 188, 378));
            var mask = Picture("PortraitMask", root, "03_PlayerCard/PlayerCard_Portrait_Mask", new Rect(9, 15, 170, 220)); mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var photo = Node("Portrait", mask.transform, new Rect(0, 0, 170, 220)).gameObject.AddComponent<RawImage>(); photo.raycastTarget = false;
            var overrideImage = Node("PortraitOverride", mask.transform, new Rect(0, 0, 170, 220)).gameObject.AddComponent<Image>(); overrideImage.raycastTarget = false; overrideImage.preserveAspect = true; overrideImage.gameObject.SetActive(false);
            Picture("Frame", root, "03_PlayerCard/PlayerCard_Card_Frame", new Rect(0, 0, 188, 378));
            var highlight = Picture("ReadyHighlight", root, "03_PlayerCard/PlayerCard_Card_ReadyHighlight", new Rect(0, 0, 188, 378));
            var badge = Picture("HostBadge", root, "03_PlayerCard/PlayerCard_Host_Badge_BG", new Rect(26, -18, 136, 38));
            Label("Label", badge.transform, "호스트", new Rect(0, 0, 136, 38), 23, ink, TextAlignmentOptions.Center);
            var voice = Picture("Voice", root, "03_PlayerCard/PlayerCard_Voice_Muted", new Rect(151, 10, 27, 38));
            var name = Label("Name", root, "", new Rect(8, 249, 172, 50), 24, ink, TextAlignmentOptions.Center);
            var readyBG = Picture("ReadyState", root, "03_PlayerCard/PlayerCard_ReadyState_Waiting_BG", new Rect(0, 330, 188, 48));
            var readyIcon = Picture("Icon", readyBG.transform, "03_PlayerCard/Icon_Waiting_Ring", new Rect(16, 9, 30, 30));
            var state = Label("Label", readyBG.transform, "", new Rect(48, 0, 136, 48), 22, ink, TextAlignmentOptions.MidlineLeft);
            Picture("Noise", root, "03_PlayerCard/PlayerCard_Noise_Overlay", new Rect(0, 0, 188, 378)).color = new Color(1, 1, 1, .1f);
            var card = root.gameObject.AddComponent<WaitingPlayerCard>(); card.Configure(this, photo, overrideImage, name, badge.gameObject, highlight.gameObject, voice, readyBG, readyIcon, state); root.gameObject.SetActive(false); return card;
        }
        public static Rect CardRect(int count, int index)
        {
            if (count < 1 || count > 6 || index < 0 || index >= count) throw new ArgumentOutOfRangeException();
            // The 378px-tall source cards require a shorter two-row presentation to stay above the bottom actions.
            if (count <= 4)
            {
                float w = count == 4 ? 180.48f : 188, h = count == 4 ? 362.88f : 378;
                float total = count * w + (count - 1) * 48;
                return new Rect(200 + (960 - total) / 2 + index * (w + 48), 315, w, h);
            }
            int row = index / 3, column = index % 3, columns = row == 0 ? 3 : count - 3;
            const float width = 182, height = 205, gap = 55;
            return new Rect(200 + (960 - columns * width - (columns - 1) * gap) / 2 + column * (width + gap), 292 + row * 226, width, height);
        }
        void BuildRegion()
        {
            var root = Node("Region", Layout, new Rect(1214, 214, 404, 410));
            var backing = Node("Backing", root, new Rect(0, 0, 404, 410)).gameObject.AddComponent<Image>();
            backing.color = new Color(.02f, .022f, .021f, 1); backing.raycastTarget = false;
            Picture("Background", root, "05_ExpeditionRegion/RegionPanel_BG_9Slice", new Rect(0, 0, 404, 410));
            Picture("Frame", root, "05_ExpeditionRegion/RegionPanel_Frame", new Rect(0, 0, 404, 410));
            regionHeading = Label("RegionLabel", root, "탐사 지역", new Rect(18, 10, 365, 28), 19);
            regionName = Label("RegionName", root, "", new Rect(18, 42, 368, 35), 26);
            regionImage = Node("Image", root, new Rect(18, 85, 368, 236)).gameObject.AddComponent<Image>(); regionImage.raycastTarget = false;
            regionDescription = Label("Description", root, "", new Rect(18, 327, 368, 42), 18, muted);
            roomCode = Label("RoomCode", root, "", new Rect(18, 375, 368, 29), 16, muted);
            var hit = root.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0);
            var codeButton = root.gameObject.AddComponent<Button>(); codeButton.targetGraphic = hit;
            codeButton.onClick.AddListener(() => { if (lobby.Rooms.Hosted != null) GUIUtility.systemCopyBuffer = ExpeditionLobby.FormatCode(lobby.Rooms.Hosted.Code); });
        }
        void BuildActions()
        {
            LeaveButton = ActionButton("Leave", Layout, "방 나가기", new Rect(70, 831, 196, 52), "Leave", () => session.Leave());
            ReadyButton = ActionButton("Ready", Layout, "준비 완료", new Rect(534, 823, 254, 60), "Ready", ToggleReady);
            readyText = ReadyButton.GetComponentInChildren<TMP_Text>(); readySkin = ReadyButton.GetComponent<WaitingButtonSkin>();
            var root = Node("Start", Layout, new Rect(814, 823, 254, 60));
            var image = Picture("Background", root, "06_Buttons/Start_Disabled_BG_9Slice", new Rect(0, 0, 254, 60)); image.raycastTarget = true;
            Picture("Frame", root, "06_Buttons/Button_Frame_9Slice", new Rect(0, 0, 254, 60));
            var arrow = Picture("Arrow", root, "06_Buttons/Start_Arrow_Icon", new Rect(16, 14, 32, 32));
            var label = Label("Label", root, "탐사 시작", new Rect(10, 0, 234, 60), 27, ink, TextAlignmentOptions.Center);
            var hover = Focus(root, new Rect(0, 0, 254, 60)); hover.transform.SetSiblingIndex(2);
            var disabled = Picture("Disabled", root, "06_Buttons/Button_Disabled_Overlay", new Rect(0, 0, 254, 60));
            StartButton = root.gameObject.AddComponent<Button>(); StartButton.targetGraphic = image; StartButton.transition = Selectable.Transition.None; StartButton.onClick.AddListener(() => session.StartMission());
            var skin = root.gameObject.AddComponent<WaitingButtonSkin>(); var enabled = Asset("06_Buttons/Start_Enabled_BG_9Slice");
            skin.Initialize(StartButton, image, label, hover.gameObject, disabled.gameObject, enabled); skin.DisabledSprite = Asset("06_Buttons/Start_Disabled_BG_9Slice"); skin.ActiveTextColor = new Color(.12f, .14f, .12f);
            message = Label("Status", Layout, "", new Rect(440, 892, 720, 31), 17, muted, TextAlignmentOptions.Center);
        }
        void BuildChat()
        {
            var history = Node("ChatHistory", Layout, new Rect(1232, 694, 370, 156)); history.gameObject.AddComponent<RectMask2D>();
            chatContent = Node("Content", history, new Rect(0, 0, 365, 156));
            chatHistory = Label("Messages", chatContent, "", new Rect(0, 0, 365, 156), 17); chatHistory.enableAutoSizing = false;
            var area = history.gameObject.AddComponent<Image>(); area.color = new Color(0, 0, 0, .7f);
            chatScroll = history.gameObject.AddComponent<ScrollRect>(); chatScroll.viewport = history; chatScroll.content = chatContent; chatScroll.horizontal = false; chatScroll.movementType = ScrollRect.MovementType.Clamped;
            var input = Node("ChatInput", Layout, new Rect(1232, 865, 370, 46));
            var backing = Node("Backing", input, new Rect(0, 0, 370, 46)).gameObject.AddComponent<Image>();
            backing.color = new Color(.025f, .028f, .028f, 1); backing.raycastTarget = false;
            var bg = Picture("Background", input, "07_Chat/Chat_BG_9Slice", new Rect(0, 0, 370, 46)); bg.raycastTarget = true;
            Picture("Icon", input, "07_Chat/Chat_Icon", new Rect(8, 9, 28, 28));
            var viewport = Node("Viewport", input, new Rect(42, 0, 280, 46)); viewport.gameObject.AddComponent<RectMask2D>();
            var text = Label("Text", viewport, "", new Rect(0, 0, 280, 46), 18, ink, TextAlignmentOptions.Left); text.enableAutoSizing = false;
            var hint = Label("Hint", viewport, "메시지를 입력하세요.", new Rect(0, 0, 280, 46), 18, new Color(.7f, .72f, .68f, .55f), TextAlignmentOptions.Left); hint.enableAutoSizing = false;
            ChatInput = input.gameObject.AddComponent<TMP_InputField>(); ChatInput.textViewport = viewport; ChatInput.textComponent = (TextMeshProUGUI)text; ChatInput.placeholder = hint; ChatInput.targetGraphic = bg;
            ChatInput.characterLimit = 120; ChatInput.lineType = TMP_InputField.LineType.SingleLine; ChatInput.richText = false; ChatInput.onSubmit.AddListener(_ => SendChat());
            ChatInput.customCaretColor = true; ChatInput.caretColor = ink; ChatInput.caretWidth = 1;
            var send = Picture("Send", input, "07_Chat/Chat_Send_Icon", new Rect(330, 11, 30, 24)); send.raycastTarget = true;
            SendButton = send.gameObject.AddComponent<Button>(); SendButton.targetGraphic = send; SendButton.onClick.AddListener(SendChat);
            send.gameObject.AddComponent<WaitingIconFocus>().Initialize(SendButton, Focus(send.transform, new Rect(0, 0, 30, 24)));
        }
        public void ToggleReady() { var me = session.LocalPlayer; if (Visible && session.Online && me != null) session.Send(new Command { action = "ready", flag = !me.ready }); }
        public void SendChat()
        {
            if (!Visible || !session.Online || Time.unscaledTime < nextChat || string.IsNullOrWhiteSpace(ChatInput.text)) return;
            session.Send(new Command { action = "chat", text = ChatInput.text }); ChatInput.text = ""; nextChat = Time.unscaledTime + .8f;
        }
        void BuildOptions()
        {
            optionsBackdrop = Node("OptionsBackdrop", Layout, new Rect(0, 0, 1672, 941)).gameObject;
            optionsBackdrop.AddComponent<Image>().color = new Color(0, 0, 0, .55f);
            optionsBackdrop.AddComponent<Button>().onClick.AddListener(ToggleOptions); optionsBackdrop.SetActive(false);
            options = Node("OptionsPanel", Layout, new Rect(380, 295, 720, 400)).gameObject;
            options.AddComponent<Image>().color = new Color(.02f, .03f, .03f, .99f);
            Label("Heading", options.transform, "대기실 설정", new Rect(30, 20, 630, 45), 29);
            oxygenText = Label("Oxygen", options.transform, "", new Rect(30, 76, 360, 40), 22);
            ActionButton("Less", options.transform, "−", new Rect(480, 74, 70, 42), "Leave", () => { if (session.IsHost) session.rules.oxygenSeconds = Mathf.Max(300, session.rules.oxygenSeconds - 60); });
            ActionButton("More", options.transform, "+", new Rect(565, 74, 70, 42), "Leave", () => { if (session.IsHost) session.rules.oxygenSeconds = Mathf.Min(1740, session.rules.oxygenSeconds + 60); });
            var mic = ActionButton("Microphone", options.transform, "마이크 켜기", new Rect(30, 137, 295, 46), "Leave", () => { if (session.radio == null) return; if (session.radio.Enabled) session.radio.StopMicrophone(); else session.radio.StartMicrophone(); }); micText = mic.GetComponentInChildren<TMP_Text>(); micText.fontSizeMax = 21;
            var mute = ActionButton("Mute", options.transform, "수신 음소거", new Rect(360, 137, 295, 46), "Leave", () => { if (session.radio != null) session.radio.Muted = !session.radio.Muted; }); muteText = mute.GetComponentInChildren<TMP_Text>(); muteText.fontSizeMax = 21;
            Label("AspectLabel", options.transform, "화면 비율", new Rect(30, 208, 210, 45), 22);
            DisplayPreferences.AddDropdown(options.transform, font.sourceFontFile, new Rect(260, 208, 395, 45));
            ActionButton("Archive", options.transform, "기록 보관소", new Rect(30, 328, 295, 45), "Leave", () => { options.SetActive(false); optionsBackdrop.SetActive(false); hud.OpenArchive(); });
            ActionButton("Close", options.transform, "닫기", new Rect(360, 328, 295, 45), "Leave", () => { options.SetActive(false); optionsBackdrop.SetActive(false); });
            options.SetActive(false);
            var gear = Node("Settings", Layout, new Rect(1580, 28, 58, 58));
            var hit = gear.gameObject.AddComponent<Image>(); hit.color = Color.clear;
            symbolFont = TMP_FontAsset.CreateFontAsset("Segoe UI Symbol", "Regular", 64);
            var icon = Label("Icon", gear, "\u2699", new Rect(0, 0, 58, 58), 43, new Color(.9f, .9f, .86f, .55f), TextAlignmentOptions.Center);
            if (symbolFont != null) icon.font = symbolFont;
            icon.raycastTarget = true;
            SettingsButton = gear.gameObject.AddComponent<Button>(); SettingsButton.targetGraphic = icon; SettingsButton.onClick.AddListener(ToggleOptions);
            var gearFocus = Focus(gear, new Rect(0, 0, 58, 58)); gearFocus.transform.SetAsFirstSibling();
            gear.gameObject.AddComponent<WaitingIconFocus>().Initialize(SettingsButton, gearFocus);
        }
        public void ToggleOptions() { if (Visible && !hud.ArchiveOpen) { options.SetActive(!options.activeSelf); optionsBackdrop.SetActive(options.activeSelf); } }
        void BuildPortrait()
        {
            portraitStage = new GameObject("Waiting portrait stage"); portraitStage.transform.SetParent(transform);
            portraits = portraitStage.AddComponent<WaitingCharacterPortraits>(); portraits.Initialize();
        }
        public RenderTexture PortraitFor(string characterId) => portraits.TextureFor(characterId);
        public static Vector2Int PortraitResolution(float uiScale)
        {
            int height = Mathf.Clamp(Mathf.CeilToInt(220 * uiScale / 40) * 40, 320, 1280);
            return new Vector2Int(height * 4 / 5, height);
        }
        void Update()
        {
            Visible = session != null && session.Online && session.View.phase == Phase.Lobby;
            canvasObject.SetActive(Visible && !hud.ArchiveOpen);
            portraits.Prepare(canvasObject.GetComponent<Canvas>().scaleFactor, Visible && !hud.ArchiveOpen);
            if (!Visible)
            {
                if (previousVisible) { ChatInput.text = ""; lastChat = -1; options.SetActive(false); }
                previousVisible = false; return;
            }
            if (!previousVisible) nextVoice = 0; previousVisible = true;
            optionsBackdrop.SetActive(options.activeSelf);
            var canvas = (RectTransform)canvasObject.transform; DisplayPreferences.FitCanvas(Layout, canvas);
            var players = session.View.players.Where(p => p.connected).OrderBy(p => p.id).ToArray();
            for (int i = 0; i < Cards.Count; i++)
            {
                Cards[i].gameObject.SetActive(i < players.Length);
                if (i < players.Length) Cards[i].Present(players[i], CardRect(players.Length, i), players.Length > 4);
            }
            var me = session.LocalPlayer;
            readyText.text = me?.ready == true ? "준비 취소" : "준비 완료"; readySkin.DefaultSprite = Asset(me?.ready == true ? "06_Buttons/Ready_Ready_BG_9Slice" : "06_Buttons/Ready_Default_BG_9Slice");
            ReadyButton.interactable = me != null;
            StartButton.gameObject.SetActive(session.IsHost); StartButton.interactable = session.IsHost && players.Length >= session.rules.MinimumStartPlayers && players.All(p => p.ready);
            message.text = players.Length < session.rules.MinimumStartPlayers ? "최소 " + session.rules.MinimumStartPlayers + "명의 요원이 필요합니다." : players.Any(p => !p.ready) ? "요원들의 준비를 기다리는 중입니다." : session.IsHost ? "탐사를 시작할 수 있습니다." : "호스트의 출발을 기다리는 중입니다.";
            int standIns = players.Count(p => p.developerDummy);
            if (standIns > 0) message.text = "[개발자 모드 · 가짜 요원 " + standIns + "명] " + message.text;
            else if (session.rules.MinimumStartPlayers == 1) message.text = "[개발자 1인 모드] " + message.text;
            roomCode.text = lobby.Rooms.Hosted == null ? "" : "참가 코드  " + ExpeditionLobby.FormatCode(lobby.Rooms.Hosted.Code);
            if (alignedRegionCode != roomCode.text)
            {
                alignedRegionCode = roomCode.text;
                AlignRegionTextEdge(regionHeading, true);
                AlignRegionTextEdge(roomCode, false);
            }
            if (shownRegion != session.View.regionId)
            {
                shownRegion = session.View.regionId;
                var region = ExpeditionRegions.Find(shownRegion);
                regionName.text = region.name; regionDescription.text = region.description; regionImage.sprite = lobby.RegionSprite(shownRegion);
                // Fit the complete source image without cropping or stretching, and center it in its slot.
                var source = regionImage.sprite.rect; float scale = Mathf.Min(368 / source.width, 236 / source.height);
                var size = new Vector2(source.width * scale, source.height * scale);
                regionImage.rectTransform.sizeDelta = size;
                regionImage.rectTransform.anchoredPosition = new Vector2(18 + (368 - size.x) / 2, -85 - (236 - size.y) / 2);
            }
            oxygenText.text = "산소 예산   " + Mathf.RoundToInt(session.rules.oxygenSeconds / 60) + "분";
            options.transform.Find("Less").GetComponent<Button>().interactable = session.IsHost && session.rules.oxygenSeconds > 300;
            options.transform.Find("More").GetComponent<Button>().interactable = session.IsHost && session.rules.oxygenSeconds < 1740;
            micText.text = session.radio?.Enabled == true ? "마이크 끄기" : "마이크 켜기"; muteText.text = session.radio?.Muted == true ? "수신 음소거 해제" : "수신 음소거";
            if (Time.unscaledTime >= nextVoice && me != null && me.voiceEnabled != (session.radio?.Enabled == true)) { nextVoice = Time.unscaledTime + .5f; session.Send(new Command { action = "voiceState", flag = session.radio?.Enabled == true }); }
            int sequence = session.View.lobbyChat.LastOrDefault()?.sequence ?? 0;
            if (sequence != lastChat)
            {
                lastChat = sequence; chatHistory.text = string.Join("\n", session.View.lobbyChat.Select(c => c.name + ": " + c.text));
                float height = Mathf.Max(156, chatHistory.GetPreferredValues(chatHistory.text, 365, Mathf.Infinity).y + 8); chatContent.sizeDelta = new Vector2(365, height); chatHistory.rectTransform.sizeDelta = chatContent.sizeDelta;
                Canvas.ForceUpdateCanvases(); chatScroll.verticalNormalizedPosition = 0;
            }
            SendButton.interactable = Time.unscaledTime >= nextChat && !string.IsNullOrWhiteSpace(ChatInput.text);
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (!DisplayPreferences.PopupOpen && keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ToggleOptions();
        }
        static void AlignRegionTextEdge(TMP_Text label, bool top)
        {
            if (string.IsNullOrEmpty(label.text)) return;
            label.ForceMeshUpdate();
            var bounds = label.textBounds;
            var point = label.rectTransform.TransformPoint(top ? bounds.max : bounds.min);
            float edge = label.transform.parent.InverseTransformPoint(point).y;
            // Match visible glyph edges, not font-dependent text-box padding.
            float target = top ? -16 : -(410 - 16);
            label.rectTransform.anchoredPosition += Vector2.up * (target - edge);
        }
        void OnDestroy()
        {
            if (canvasObject != null) Destroy(canvasObject); if (portraitStage != null) Destroy(portraitStage);
            foreach (var sprite in sprites.Values) if (sprite != null) Destroy(sprite);
            if (font != null) { foreach (var texture in font.atlasTextures) if (texture != null) Destroy(texture); if (font.material != null) Destroy(font.material); Destroy(font); }
            if (symbolFont != null) { foreach (var texture in symbolFont.atlasTextures) if (texture != null) Destroy(texture); if (symbolFont.material != null) Destroy(symbolFont.material); Destroy(symbolFont); }
        }
    }

    public sealed class WaitingPlayerCard : MonoBehaviour
    {
        public Sprite PortraitOverride;
        public ulong PlayerId { get; private set; }
        WaitingRoom owner;
        RawImage portrait;
        Image replacement, voice, stateBG, stateIcon;
        TMP_Text nameText, stateText;
        GameObject badge, highlight;
        public void Configure(WaitingRoom room, RawImage photo, Image custom, TMP_Text name, GameObject host, GameObject ready, Image mic, Image background, Image icon, TMP_Text state)
        { owner = room; portrait = photo; replacement = custom; nameText = name; badge = host; highlight = ready; voice = mic; stateBG = background; stateIcon = icon; stateText = state; }
        public void Present(PlayerState player, Rect rect, bool compact)
        {
            portrait.texture = owner.PortraitFor(player.characterId);
            PlayerId = player.id; var root = (RectTransform)transform; root.anchoredPosition = new Vector2(rect.x, -rect.y);
            root.sizeDelta = new Vector2(188, compact ? 230 : 378); root.localScale = new Vector3(rect.width / 188, rect.height / root.sizeDelta.y, 1);
            foreach (var layer in new[] { "Background", "Frame", "ReadyHighlight", "Noise" }) ((RectTransform)transform.Find(layer)).sizeDelta = root.sizeDelta;
            var mask = (RectTransform)transform.Find("PortraitMask"); mask.sizeDelta = new Vector2(170, compact ? 136 : 220);
            replacement.rectTransform.sizeDelta = mask.sizeDelta;
            portrait.rectTransform.sizeDelta = new Vector2(mask.sizeDelta.y * .8f, mask.sizeDelta.y);
            portrait.rectTransform.anchoredPosition = new Vector2((170 - portrait.rectTransform.sizeDelta.x) / 2, 0);
            nameText.rectTransform.anchoredPosition = new Vector2(8, compact ? -151 : -249); nameText.rectTransform.sizeDelta = new Vector2(172, compact ? 35 : 50);
            stateBG.rectTransform.anchoredPosition = new Vector2(0, compact ? -187 : -330); stateBG.rectTransform.sizeDelta = new Vector2(188, compact ? 43 : 48);
            stateText.rectTransform.sizeDelta = new Vector2(136, compact ? 43 : 48);
            stateIcon.rectTransform.anchoredPosition = new Vector2(16, compact ? -6 : -9);
            nameText.fontSizeMin = compact ? 12 : 16; nameText.overflowMode = TextOverflowModes.Ellipsis;
            nameText.text = player.name; badge.SetActive(player.id == 0); highlight.SetActive(player.ready);
            voice.sprite = owner.Asset(player.voiceEnabled ? "03_PlayerCard/PlayerCard_Voice_On" : "03_PlayerCard/PlayerCard_Voice_Muted");
            stateBG.sprite = owner.Asset(player.ready ? "03_PlayerCard/PlayerCard_ReadyState_Ready_BG" : "03_PlayerCard/PlayerCard_ReadyState_Waiting_BG");
            stateIcon.sprite = owner.Asset(player.ready ? "03_PlayerCard/Icon_Ready_Check" : "03_PlayerCard/Icon_Waiting_Ring");
            stateText.text = player.developerDummy ? "자동 준비" : player.ready ? "준비 완료" : "대기 중"; stateText.color = player.ready ? new Color(.61f, .81f, .58f) : new Color(.67f, .69f, .65f);
            replacement.sprite = PortraitOverride; replacement.gameObject.SetActive(PortraitOverride != null); portrait.gameObject.SetActive(PortraitOverride == null);
        }
    }
    public sealed class WaitingButtonSkin : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        public Sprite DefaultSprite, DisabledSprite;
        public Color ActiveTextColor = new(.86f, .84f, .78f);
        Button button; Image image; TMP_Text label; GameObject highlight, disabled; bool over, down, selected;
        public void Initialize(Button control, Image background, TMP_Text text, GameObject focus, GameObject inactive, Sprite normal)
        { button = control; image = background; label = text; highlight = focus; disabled = inactive; DefaultSprite = normal; }
        void LateUpdate()
        {
            bool active = button.interactable; image.sprite = !active && DisabledSprite != null ? DisabledSprite : DefaultSprite;
            highlight.GetComponent<WaitingFocusGradient>().Strength = down ? .25f : .14f;
            highlight.SetActive(active && (over || selected)); disabled.SetActive(!active); label.color = active ? ActiveTextColor : new Color(.43f, .45f, .43f);
        }
        public void OnPointerEnter(PointerEventData e) { over = true; }
        public void OnPointerExit(PointerEventData e) { over = down = selected = false; }
        public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) down = true; }
        public void OnPointerUp(PointerEventData e) { down = false; }
        public void OnSelect(BaseEventData e) { selected = true; }
        public void OnDeselect(BaseEventData e) { selected = down = false; }
        void OnDisable() { over = down = selected = false; }
    }
    public sealed class WaitingFocusGradient : MaskableGraphic
    {
        float strength = .14f;
        public float Strength { get => strength; set { if (Mathf.Approximately(strength, value)) return; strength = value; SetVerticesDirty(); } }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var r = GetPixelAdjustedRect();
            mesh.AddVert(new Vector3(r.xMin, r.yMin), new Color(1, 1, 1, 0), Vector2.zero);
            mesh.AddVert(new Vector3(r.xMin, r.yMax), new Color(1, 1, 1, strength), Vector2.up);
            mesh.AddVert(new Vector3(r.xMax, r.yMax), new Color(1, 1, 1, strength), Vector2.one);
            mesh.AddVert(new Vector3(r.xMax, r.yMin), new Color(1, 1, 1, 0), Vector2.right);
            mesh.AddTriangle(0, 1, 2); mesh.AddTriangle(2, 3, 0);
        }
    }
    public sealed class WaitingIconFocus : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        Button button; WaitingFocusGradient effect; bool over, selected;
        public void Initialize(Button control, WaitingFocusGradient gradient) { button = control; effect = gradient; }
        void LateUpdate() { effect.gameObject.SetActive(button.interactable && (over || selected)); }
        public void OnPointerEnter(PointerEventData e) { over = true; }
        public void OnPointerExit(PointerEventData e) { over = selected = false; }
        public void OnSelect(BaseEventData e) { selected = true; }
        public void OnDeselect(BaseEventData e) { selected = false; }
        void OnDisable() { over = selected = false; }
    }
}
