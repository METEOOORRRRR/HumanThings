using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EarthRecovery
{
    public sealed class ExpeditionLobby : MonoBehaviour
    {
        public bool Visible { get; private set; }
        public int Page { get; private set; }
        public RectTransform Layout { get; private set; }
        public LanRooms Rooms { get; private set; }
        public readonly List<Button> Navigation = new();
        public readonly List<GameObject> Panels = new();
        public InputField RoomName { get; private set; }
        public InputField[] CodeFields { get; private set; }
        public Button CreateButton { get; private set; }
        public Button JoinCodeButton { get; private set; }
        NetworkSession session;
        GameHud gameHud;
        MainMenu title;
        GameObject canvasObject, busy, passwordModal;
        RectTransform roomList;
        Font font;
        Text status, capacityText, detailTitle, detailInfo, listEmpty, connectionText;
        InputField password, userName, joinPassword;
        Button joinSelected, publicButton, privateButton;
        readonly List<GameObject> navSelected = new();
        readonly List<Button> controls = new();
        readonly Dictionary<string, Sprite> sprites = new();
        readonly Color ink = new(.87f, .85f, .79f), muted = new(.59f, .6f, .57f), cyan = new(.5f, .84f, .82f);
        LanRoom selected;
        string listSignature = "", displayStatus = "", observedStatus = "";
        int capacity = 4;
        bool publicRoom = true;
        float nextRefresh;
        string chosenRegion = ExpeditionRegions.DefaultId, previewRegion;
        UnityEngine.UI.Image createPreview, selectedPreview;
        Text regionChoice, previewHeading, previewName;
        GameObject regionChoices;
        readonly Dictionary<string, Sprite> regionSprites = new();

        public Sprite RegionSprite(string id)
        {
            if (!regionSprites.TryGetValue(id, out var sprite)) regionSprites.Add(id, sprite = ExpeditionRegions.CreateSprite(id));
            return sprite;
        }
        public void ChooseRegion(string id)
        {
            var region = ExpeditionRegions.Find(id); if (region == null) return;
            chosenRegion = id; regionChoice.text = region.name; previewHeading.text = region.title; previewName.text = "탐사 구역     " + region.name;
            createPreview.sprite = RegionSprite(id); regionChoices.SetActive(false);
        }

        public void Initialize(NetworkSession network, GameHud hud, MainMenu mainMenu)
        {
            session = network; gameHud = hud; title = mainMenu; hud.ExpeditionMenu = this;
            Rooms = gameObject.AddComponent<LanRooms>(); Rooms.Initialize(session); Rooms.Resolved += OnResolved;
            Rooms.Changed += DirectoryChanged;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Batang", "Malgun Gothic", "Arial" }, 30);
            canvasObject = new GameObject("ExpeditionLobbyCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1672, 941); scaler.matchWidthOrHeight = .5f;
            var black = Node("Letterbox", canvasObject.transform, new Rect(0, 0, 1672, 941));
            black.anchorMin = Vector2.zero; black.anchorMax = Vector2.one; black.offsetMin = black.offsetMax = Vector2.zero; black.gameObject.AddComponent<Image>().color = Color.black;
            Layout = Node("ReferenceCanvas", canvasObject.transform, new Rect(0, 0, 1672, 941));
            Layout.anchorMin = Layout.anchorMax = Layout.pivot = new Vector2(.5f, .5f); Layout.anchoredPosition = Vector2.zero;
            Image("Background", Layout, "Shared_Common/Background_Artwork", new Rect(0, 0, 1672, 941));
            Image("Noise", Layout, "Shared_Common/Global_Noise_Overlay", new Rect(0, 0, 1672, 941)).color = new Color(1, 1, 1, .08f);
            Image("Logo", Layout, "Shared_Common/Logo_HumanThings", new Rect(72, 60, 465, 102));
            Label("LogoSubtitle", Layout, "화성 세대의 지구 문화 복원 탐사", new Rect(170, 163, 395, 30), 17);
            Label("Heading", Layout, "탐사대 편성", new Rect(123, 228, 320, 53), 36);
            Image("HeadingRule", Layout, "Shared_Common/Decorative_Line", new Rect(120, 274, 350, 8));
            Label("Subtitle", Layout, "함께, 잊힌 세계를 다시 걷는다.", new Rect(123, 292, 400, 35), 22);
            Label("Version", Layout, "v0.1.0", new Rect(43, 900, 150, 24), 14, muted);
            Label("Network", Layout, "LAN", new Rect(43, 871, 100, 24), 14, muted);
            Label("RightTop", Layout, "잊힌 세계를 걷는 일.\n그것은 여전히, 인간의 일이다.", new Rect(1467, 28, 193, 48), 13, muted);
            Label("Date", Layout, "2147. 03", new Rect(1535, 90, 110, 24), 13, muted, TextAnchor.MiddleRight);
            Label("Poem", Layout, "SOME\nTHINGS\nSTILL\nWAIT", new Rect(45, 510, 70, 95), 12, muted);
            string[] names = { "방 생성", "공개 방 찾기", "참가 코드 입력", "뒤로" };
            for (int i = 0; i < 4; i++)
            {
                int page = i;
                var root = Node("Navigation_" + i, Layout, new Rect(120, 346 + i * 60, 390, 58));
                var normal = Image("Default", root, "LeftNavigation/MenuItem_Default_BG_9Slice", new Rect(0, 0, 390, 58), true);
                normal.raycastTarget = true;
                var highlight = Node("Selected", root, new Rect(0, 0, 390, 58));
                Image("Background", highlight, "LeftNavigation/MenuItem_Selected_BG_9Slice", new Rect(0, 0, 390, 58), true);
                Image("Frame", highlight, "LeftNavigation/MenuItem_Selected_Frame", new Rect(0, 0, 390, 58));
                Image("Arrow", highlight, "LeftNavigation/MenuItem_Selected_Arrow", new Rect(20, 14, 26, 30));
                Label("Label", root, names[i], new Rect(57, 0, 320, 58), 29, ink, TextAnchor.MiddleLeft);
                var button = root.gameObject.AddComponent<Button>(); button.targetGraphic = normal;
                root.gameObject.AddComponent<CanvasGroup>(); controls.Add(button);
                button.onClick.AddListener(() => { if (page == 3) Back(); else SelectPage(page); });
                Navigation.Add(button); navSelected.Add(highlight.gameObject);
            }
            BuildCreate(); BuildPublic(); BuildCode();
            status = Label("Status", Layout, "", new Rect(605, 882, 1000, 45), 18, cyan);
            Image("Edge", Layout, "Shared_Common/Edge_Scratch_Overlay", new Rect(0, 0, 1672, 941));
            BuildBusy();
            canvasObject.SetActive(false);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--lobby-qa") >= 0) gameObject.AddComponent<LobbySmoke>().lobby = this;
#endif
        }
        static RectTransform Node(string name, Transform parent, Rect rect) => MainMenu.Node(name, parent, rect);
        Image Image(string name, Transform parent, string asset, Rect rect, bool sliced = false)
        {
            var image = Node(name, parent, rect).gameObject.AddComponent<Image>();
            if (!sprites.TryGetValue(asset, out var sprite))
            {
                var texture = Resources.Load<Texture2D>("ExpeditionLobby/" + asset);
                if (texture == null) throw new InvalidOperationException("Missing lobby asset: " + asset);
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, asset.Contains("9Slice") ? Vector4.one * 12 : Vector4.zero);
                sprites.Add(asset, sprite);
            }
            image.sprite = sprite; image.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.raycastTarget = false; return image;
        }
        Text Label(string name, Transform parent, string value, Rect rect, int size = 22, Color? color = null, TextAnchor align = TextAnchor.UpperLeft)
        {
            var text = Node(name, parent, rect).gameObject.AddComponent<Text>();
            text.font = font; text.fontSize = size; text.text = value; text.color = color ?? ink; text.alignment = align;
            text.raycastTarget = false; text.supportRichText = false; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate; return text;
        }
        Button Button(string name, Transform parent, string text, Rect rect, Action click, string icon = null)
        {
            var root = Node(name, parent, rect);
            var bg = Image("Background", root, "CommonControls/Button_BG_9Slice", new Rect(0, 0, rect.width, rect.height), true); bg.raycastTarget = true;
            Image("Frame", root, "CommonControls/Button_Frame_9Slice", new Rect(0, 0, rect.width, rect.height), true);
            if (icon != null) Image("Icon", root, "CommonControls/" + icon, new Rect(15, (rect.height - 26) / 2, 26, 26));
            Label("Label", root, text, new Rect(icon == null ? 0 : 40, 0, rect.width - (icon == null ? 0 : 40), rect.height), 23, ink, TextAnchor.MiddleCenter);
            var button = root.gameObject.AddComponent<Button>(); button.targetGraphic = bg;
            root.gameObject.AddComponent<CanvasGroup>(); controls.Add(button);
            var colors = button.colors; colors.highlightedColor = new Color(1.45f, 1.45f, 1.35f); colors.selectedColor = colors.highlightedColor; button.colors = colors;
            button.onClick.AddListener(() => click()); return button;
        }
        InputField Field(string name, Transform parent, Rect rect, int limit, string placeholder, bool secret = false, string assetPrefix = "CommonControls/Input")
        {
            var root = Node(name, parent, rect);
            var bg = Image("Background", root, assetPrefix + "_BG_9Slice", new Rect(0, 0, rect.width, rect.height), true); bg.raycastTarget = true;
            Image("Frame", root, assetPrefix + "_Frame_9Slice", new Rect(0, 0, rect.width, rect.height), true);
            var text = Label("Text", root, "", new Rect(12, 0, rect.width - 24, rect.height), 21, ink, TextAnchor.MiddleLeft);
            var hint = Label("Placeholder", root, placeholder, new Rect(12, 0, rect.width - 24, rect.height), 19, muted, TextAnchor.MiddleLeft);
            var field = root.gameObject.AddComponent<InputField>(); field.textComponent = text; field.placeholder = hint; field.targetGraphic = bg; field.characterLimit = limit;
            field.lineType = InputField.LineType.SingleLine; field.contentType = secret ? InputField.ContentType.Password : InputField.ContentType.Standard;
            return field;
        }
        RectTransform Panel(string name, string prefix, Rect rect)
        {
            var panel = Node(name, Layout, rect);
            var shade = Node("Shade", panel, new Rect(0, 0, rect.width, rect.height)).gameObject.AddComponent<Image>(); shade.color = new Color(.015f, .02f, .02f, .72f); shade.raycastTarget = false;
            Image("Background", panel, prefix + "_BG_9Slice", new Rect(0, 0, rect.width, rect.height), true);
            Image("Frame", panel, prefix + "_Frame_9Slice", new Rect(0, 0, rect.width, rect.height), true);
            return panel;
        }
        void BuildCreate()
        {
            var p = Panel("CreateRoomPanel", "ContentPanel/CreateRoom/CreateRoom_Panel", new Rect(586, 214, 1024, 636)); Panels.Add(p.gameObject);
            Label("Heading", p, "방 생성", new Rect(40, 22, 500, 48), 34);
            Label("Description", p, "함께할 새로운 대원을 모집합니다.", new Rect(40, 78, 800, 38), 22);
            Label("RoomNameLabel", p, "방 이름", new Rect(40, 131, 140, 40));
            RoomName = Field("RoomName", p, new Rect(172, 124, 356, 44), 20, "방 이름"); RoomName.text = "탐사대의 방";
            Label("RegionLabel", p, "탐사 구역", new Rect(40, 193, 140, 40));
            var region = Button("Region", p, "", new Rect(172, 186, 356, 44), () => regionChoices.SetActive(!regionChoices.activeSelf));
            regionChoice = region.GetComponentInChildren<Text>();
            Image("Dropdown", region.transform, "CommonControls/Icon_DropdownArrow", new Rect(324, 14, 22, 18));
            Label("CapacityLabel", p, "최대 인원", new Rect(590, 131, 140, 40));
            Image("CapacityFrame", p, "CommonControls/Input_Frame_9Slice", new Rect(716, 124, 270, 44), true);
            capacityText = Label("Capacity", p, "4명", new Rect(775, 124, 153, 44), 23, ink, TextAnchor.MiddleCenter);
            Button("LessPlayers", p, "", new Rect(718, 125, 48, 42), () => ChangeCapacity(-1), "Icon_StepperLeft");
            Button("MorePlayers", p, "", new Rect(934, 125, 48, 42), () => ChangeCapacity(1), "Icon_StepperRight");
            Label("VisibilityLabel", p, "공개 설정", new Rect(590, 193, 140, 40));
            publicButton = Button("Public", p, "공개", new Rect(716, 186, 132, 44), () => SetPublic(true));
            privateButton = Button("Private", p, "비공개", new Rect(860, 186, 126, 44), () => SetPublic(false));
            var publicSelection = Image("Selection", publicButton.transform, "CommonControls/Button_Selected_BG_9Slice", new Rect(0, 0, 132, 44), true); publicSelection.transform.SetAsFirstSibling();
            var privateSelection = Image("Selection", privateButton.transform, "CommonControls/Button_Selected_BG_9Slice", new Rect(0, 0, 126, 44), true); privateSelection.transform.SetAsFirstSibling();
            Label("PasswordLabel", p, "방 비밀번호", new Rect(590, 255, 140, 40));
            password = Field("Password", p, new Rect(716, 248, 270, 44), 6, "비밀번호 (선택)", true);
            Label("NameLabel", p, "요원명", new Rect(40, 255, 130, 40));
            userName = Field("UserName", p, new Rect(172, 248, 356, 44), 16, "요원명"); userName.text = session.UserName;
            Image("PreviewFrame", p, "ContentPanel/CreateRoom/RegionPreview_Frame_9Slice", new Rect(14, 324, 998, 299), true);
            createPreview = Image("Preview", p, "ContentPanel/CreateRoom/RegionPreview_Seoul", new Rect(42, 344, 390, 250)); createPreview.preserveAspect = true;
            previewHeading = Label("PreviewHeading", p, "", new Rect(461, 344, 518, 48), 28);
            previewName = Label("PreviewRegion", p, "", new Rect(461, 401, 500, 38));
            Button("Cancel", p, "취소", new Rect(662, 555, 136, 48), Back);
            CreateButton = Button("Create", p, "생성", new Rect(822, 555, 164, 48), Create);
            regionChoices = Node("RegionChoices", p, new Rect(172, 234, 356, 44 * ExpeditionRegions.All.Length)).gameObject;
            regionChoices.AddComponent<UnityEngine.UI.Image>().color = new Color(.02f, .025f, .025f, 1);
            for (int i = 0; i < ExpeditionRegions.All.Length; i++)
            {
                var entry = ExpeditionRegions.All[i];
                Button("Region_" + entry.id, regionChoices.transform, entry.name, new Rect(0, i * 44, 356, 44), () => ChooseRegion(entry.id));
            }
            ChooseRegion(chosenRegion);
            SetPublic(true);
        }
        void ChangeCapacity(int delta) { capacity = Mathf.Clamp(capacity + delta, 4, 6); capacityText.text = capacity + "명"; }
        void SetPublic(bool value)
        {
            publicRoom = value;
            publicButton.GetComponentInChildren<Text>().color = value ? cyan : muted;
            privateButton.GetComponentInChildren<Text>().color = value ? muted : cyan;
            publicButton.transform.Find("Selection").gameObject.SetActive(value);
            privateButton.transform.Find("Selection").gameObject.SetActive(!value);
        }
        public void Create()
        {
            if (!session.CanConnect) return;
            session.UserName = userName.text;
            Rooms.Create(RoomName.text, capacity, publicRoom, password.text, chosenRegion);
        }
        void BuildPublic()
        {
            var p = Panel("PublicRoomPanel", "ContentPanel/PublicRooms/PublicRooms_Panel", new Rect(600, 195, 1008, 689)); Panels.Add(p.gameObject);
            Label("Heading", p, "공개 방 목록", new Rect(32, 18, 570, 48), 34);
            Button("Refresh", p, "새로고침", new Rect(814, 12, 150, 44), () => { listSignature = ""; Rooms.Refresh(); }, "Icon_Refresh");
            Label("ColumnName", p, "방 이름", new Rect(46, 78, 308, 34), 20);
            Label("ColumnRegion", p, "탐사 구역", new Rect(364, 78, 185, 34), 20);
            Label("ColumnPlayers", p, "인원", new Rect(568, 78, 135, 34), 20);
            Label("ColumnStatus", p, "상태", new Rect(717, 78, 155, 34), 20);
            Label("ColumnLock", p, "잠금", new Rect(882, 78, 75, 34), 20);
            var viewport = Node("RoomViewport", p, new Rect(32, 112, 940, 308)); viewport.gameObject.AddComponent<RectMask2D>();
            roomList = Node("RoomRows", viewport, new Rect(0, 0, 930, 308));
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = roomList; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            var bar = Image("Scrollbar", p, "ContentPanel/PublicRooms/Scrollbar_Track", new Rect(982, 112, 12, 308));
            var sliding = Node("SlidingArea", bar.transform, new Rect(0, 0, 12, 308));
            var thumb = Image("Thumb", sliding, "ContentPanel/PublicRooms/Scrollbar_Thumb", new Rect(0, 0, 12, 60)); thumb.raycastTarget = true;
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>(); scrollbar.handleRect = thumb.rectTransform; scrollbar.targetGraphic = thumb; scrollbar.direction = Scrollbar.Direction.BottomToTop;
            thumb.rectTransform.sizeDelta = Vector2.zero; thumb.rectTransform.anchoredPosition = Vector2.zero;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            listEmpty = Label("Empty", p, "공개 방을 찾는 중입니다.", new Rect(60, 225, 870, 60), 23, muted, TextAnchor.MiddleCenter);
            selectedPreview = Image("SelectedPreview", p, "ContentPanel/CreateRoom/RegionPreview_Seoul", new Rect(40, 450, 343, 216)); selectedPreview.preserveAspect = true; selectedPreview.gameObject.SetActive(false);
            detailTitle = Label("SelectedName", p, "선택된 방 없음", new Rect(415, 452, 555, 52), 28);
            detailTitle.resizeTextForBestFit = true; detailTitle.resizeTextMinSize = 18; detailTitle.resizeTextMaxSize = 28;
            var badge = Image("StatusBadge", p, "ContentPanel/PublicRooms/StatusBadge_BG_9Slice", new Rect(870, 514, 96, 30), true);
            Label("Label", badge.transform, "", new Rect(0, 0, 96, 30), 17, cyan, TextAnchor.MiddleCenter);
            detailInfo = Label("SelectedInfo", p, "", new Rect(415, 515, 550, 112), 22);
            joinSelected = Button("JoinSelected", p, "참가", new Rect(796, 617, 168, 46), () => { if (selected != null) JoinRoom(selected); }); joinSelected.interactable = false;
        }
        void RenderRooms()
        {
            string signature = string.Join(";", Rooms.Rooms.Select(r => r.id + r.name + r.regionId + r.players + r.capacity + r.playing + r.locked));
            if (signature != listSignature)
            {
                listSignature = signature;
                foreach (Transform child in roomList) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
                roomList.sizeDelta = new Vector2(930, Mathf.Max(308, Rooms.Rooms.Count * 44));
                for (int i = 0; i < Rooms.Rooms.Count; i++)
                {
                    var room = Rooms.Rooms[i];
                    var row = Image("Room_" + room.id, roomList, "ContentPanel/PublicRooms/RoomRow_BG_9Slice", new Rect(0, i * 44, 930, 44), true); row.raycastTarget = true;
                    var highlight = Image("Selected", row.transform, "ContentPanel/PublicRooms/RoomRow_Selected_Highlight_9Slice", new Rect(0, 0, 930, 44), true); highlight.gameObject.SetActive(selected?.id == room.id);
                    var name = Label("Name", row.transform, room.name, new Rect(14, 0, 308, 44), 21, ink, TextAnchor.MiddleLeft);
                    name.resizeTextForBestFit = true; name.resizeTextMinSize = 14; name.resizeTextMaxSize = 21;
                    Label("Region", row.transform, ExpeditionRegions.Find(room.regionId).name, new Rect(332, 0, 185, 44), 21, ink, TextAnchor.MiddleLeft);
                    Label("Players", row.transform, room.players + " / " + room.capacity, new Rect(536, 0, 135, 44), 21, ink, TextAnchor.MiddleLeft);
                    Label("Status", row.transform, room.playing ? "탐사 중" : "대기 중", new Rect(685, 0, 155, 44), 21, room.playing ? muted : cyan, TextAnchor.MiddleLeft);
                    if (room.locked) Image("Lock", row.transform, "CommonControls/Icon_Lock", new Rect(865, 8, 20, 26));
                    var button = row.gameObject.AddComponent<Button>(); button.targetGraphic = row;
                    button.onClick.AddListener(() => { selected = room; listSignature = "invalidate"; RenderRooms(); });
                }
            }
            if (selected != null) selected = Rooms.Rooms.Find(r => r.id == selected.id);
            listEmpty.gameObject.SetActive(Rooms.Rooms.Count == 0); listEmpty.text = "표시할 공개 방이 없습니다.";
            detailTitle.text = selected?.name ?? "선택된 방 없음";
            detailInfo.text = selected == null ? "" : "탐사 구역    " + ExpeditionRegions.Find(selected.regionId).name + "\n인원           " + selected.players + " / " + selected.capacity + "\n" + (selected.playing ? "탐사 중" : "대기 중");
            selectedPreview.gameObject.SetActive(selected != null);
            if (selected != null && previewRegion != selected.regionId) { previewRegion = selected.regionId; selectedPreview.sprite = RegionSprite(previewRegion); }
            var badge = Panels[1].transform.Find("StatusBadge"); badge.gameObject.SetActive(selected != null);
            badge.GetComponentInChildren<Text>(true).text = selected?.playing == true ? "탐사 중" : "대기 중";
            joinSelected.interactable = selected != null && !selected.playing && selected.players < selected.capacity && session.CanConnect;
        }
        void BuildCode()
        {
            var p = Panel("JoinCodePanel", "ContentPanel/JoinCode/JoinCode_Panel", new Rect(644, 280, 838, 350)); Panels.Add(p.gameObject);
            Label("Heading", p, "참가 코드 입력", new Rect(30, 20, 540, 52), 34);
            Button("Help", p, "도움말", new Rect(676, 18, 138, 44), () => ShowMessage("같은 네트워크의 호스트에게 참가 코드를 받으세요.\n인터넷 중계 연결은 지원하지 않습니다."), "Icon_Help");
            Label("Description", p, "친구에게 받은 참가 코드를 입력하여 방에 참여합니다.", new Rect(50, 103, 745, 36), 21);
            CodeFields = new InputField[4];
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                var field = Field("Code_" + i, p, new Rect(123 + i * 157, 162, 122, 52), 4, "----", false, "ContentPanel/JoinCode/CodeField");
                field.textComponent.alignment = TextAnchor.MiddleCenter; ((Text)field.placeholder).alignment = TextAnchor.MiddleCenter;
                field.onValidateInput = (text, position, c) => Uri.IsHexDigit(c) ? char.ToUpperInvariant(c) : '\0';
                field.onValueChanged.AddListener(value => { if (value.Length == 4 && index < 3 && field.isFocused) CodeFields[index + 1].ActivateInputField(); });
                CodeFields[i] = field;
                if (i < 3) Image("Separator_" + i, p, "ContentPanel/JoinCode/Code_Separator", new Rect(254 + i * 157, 179, 18, 18));
            }
            JoinCodeButton = Button("Join", p, "참가", new Rect(260, 248, 310, 62), () => Rooms.Resolve(string.Concat(CodeFields.Select(f => f.text))));
        }
        public static string FormatCode(string code) => code != null && code.Length == 16 ? string.Join("-", Enumerable.Range(0, 4).Select(i => code.Substring(i * 4, 4))) : code;
        void BuildBusy()
        {
            var p = Node("Connecting", Layout, new Rect(586, 214, 1024, 636)); busy = p.gameObject;
            p.gameObject.AddComponent<Image>().color = new Color(.025f, .03f, .03f, .98f);
            connectionText = Label("Status", p, "접속 중", new Rect(80, 160, 864, 160), 28, ink, TextAnchor.MiddleCenter);
            Button("Cancel", p, "접속 취소", new Rect(382, 360, 260, 52), () => { Rooms.StopSearch(); if (session.IsConnecting) session.Leave(); }); busy.SetActive(false);
        }
        void ShowMessage(string message) { ShowModal(message, false, null); }
        void JoinRoom(LanRoom room)
        {
            if (room.playing || room.players >= room.capacity) { ShowMessage("참가할 수 없는 방입니다."); return; }
            if (room.locked) ShowModal("방 비밀번호", true, () => Rooms.Join(room, joinPassword.text));
            else Rooms.Join(room, "");
        }
        void OnResolved(LanRoom room) { if (Visible && Page == 2) JoinRoom(room); }
        void DirectoryChanged() { displayStatus = Rooms.Status; }
        public bool PasteCode(string value)
        {
            if (!LanRooms.DecodeCode(value, out _, out _, out _)) return false;
            var code = new string(value.Where(c => c != '-' && !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
            for (int i = 0; i < 4; i++) CodeFields[i].SetTextWithoutNotify(code.Substring(i * 4, 4));
            return true;
        }
        void ShowModal(string message, bool secret, Action submit)
        {
            CloseModal();
            var overlay = Node("Modal", Layout, new Rect(0, 0, 1672, 941)); passwordModal = overlay.gameObject;
            overlay.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .7f);
            var p = Node("Panel", overlay, new Rect(575, 310, 640, 320));
            Image("Background", p, "CommonControls/Panel_BG_9Slice", new Rect(0, 0, 640, 320), true);
            Image("Frame", p, "CommonControls/Panel_Frame_9Slice", new Rect(0, 0, 640, 320), true);
            Label("Heading", p, message, new Rect(35, 32, 570, 130), 23);
            if (secret) joinPassword = Field("Password", p, new Rect(40, 119, 560, 50), 6, "비밀번호", true);
            Button("Close", p, "닫기", new Rect(70, 235, 220, 50), CloseModal);
            if (secret) Button("Join", p, "참가", new Rect(340, 235, 220, 50), () => { submit?.Invoke(); CloseModal(); });
        }
        void CloseModal() { if (passwordModal != null) { passwordModal.SetActive(false); Destroy(passwordModal); passwordModal = null; } }
        public void Show() { Visible = true; title.Hide(); canvasObject.SetActive(true); SelectPage(0); }
        public void Hide() { Visible = false; Rooms.StopSearch(); CloseModal(); canvasObject.SetActive(false); }
        void Back() { if (!session.CanConnect) return; Hide(); title.Show(); }
        public void SelectPage(int page)
        {
            if (page < 0 || page > 2 || session.Online || session.IsConnecting || Rooms.Resolving) return;
            CloseModal(); Page = page; Rooms.StopSearch();
            for (int i = 0; i < Panels.Count; i++) Panels[i].SetActive(i == page);
            for (int i = 0; i < navSelected.Count; i++) navSelected[i].SetActive(i == page);
            status.text = displayStatus = ""; observedStatus = session.Status;
            if (page == 1) { listSignature = "invalidate"; Rooms.BeginSearch(); RenderRooms(); }
        }
        void Update()
        {
            if (session == null) return;
            if (session.Online) { if (Visible) Hide(); return; }
            if (!Visible && !title.Visible && session.View.phase == Phase.Lobby) Show();
            if (!Visible) return;
            controls.RemoveAll(b => b == null);
            foreach (var button in controls) button.GetComponent<CanvasGroup>().alpha = button.interactable ? 1 : .4f;
            canvasObject.SetActive(!gameHud.ArchiveOpen);
            if (observedStatus != session.Status) { observedStatus = session.Status; displayStatus = session.Status; }
            var canvas = (RectTransform)canvasObject.transform; DisplayPreferences.FitCanvas(Layout, canvas);
            if (session.Online && session.View.phase != Phase.Lobby) { Hide(); return; }
            bool connecting = session.IsConnecting || Rooms.Resolving;
            busy.SetActive(connecting);
            connectionText.text = session.IsConnecting ? session.Status : Rooms.Status;
            foreach (var button in Navigation) button.interactable = session.CanConnect && !Rooms.Resolving;
            CreateButton.interactable = session.CanConnect && !string.IsNullOrWhiteSpace(RoomName.text);
            JoinCodeButton.interactable = session.CanConnect && !Rooms.Resolving && CodeFields.All(f => f.text.Length == 4);
            status.text = displayStatus;
            if (Page == 1 && Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + .25f; RenderRooms(); }
        }
        void LateUpdate()
        {
            if (!Visible) return;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;
            if (Page == 2 && CodeFields.Any(f => f.isFocused) && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed) && keyboard.vKey.wasPressedThisFrame)
                PasteCode(GUIUtility.systemCopyBuffer);
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (passwordModal != null) CloseModal();
                else if (Rooms.Resolving || session.IsConnecting) { Rooms.StopSearch(); if (session.IsConnecting) session.Leave(); }
                else if (!session.Online) Back();
            }
        }
        void OnDestroy()
        {
            if (Rooms != null) Rooms.Resolved -= OnResolved;
            if (Rooms != null) Rooms.Changed -= DirectoryChanged;
            if (canvasObject != null) Destroy(canvasObject);
            foreach (var sprite in sprites.Values) if (sprite != null) Destroy(sprite);
            foreach (var sprite in regionSprites.Values) if (sprite != null) Destroy(sprite);
            if (font != null) Destroy(font);
        }
    }
}
