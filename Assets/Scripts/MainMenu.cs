using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace EarthRecovery
{
    public sealed class MainMenu : MonoBehaviour
    {
        public const float Width = 1672, Height = 941;
        public bool Visible { get; private set; } = true;
        public int FocusedIndex { get; private set; } = -1;
        public readonly List<MainMenuItem> Items = new();
        public RectTransform Layout { get; private set; }
        NetworkSession session;
        GameHud hud;
        GameObject canvasObject, foreground, settings, ownedEvents;
        Font serif;
        readonly List<Sprite> sprites = new();
        readonly Color ink = new(.86f, .84f, .79f), quiet = new(.43f, .44f, .42f);

        public void Initialize(NetworkSession network, GameHud gameHud)
        {
            session = network; hud = gameHud; hud.TitleMenu = this;
            serif = Font.CreateDynamicFontFromOSFont(new[] { "Batang", "Noto Serif CJK KR", "Malgun Gothic", "Arial" }, 36);
            canvasObject = new GameObject("MainMenu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.GetComponent<Canvas>().sortingOrder = -10;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(Width, Height); scaler.matchWidthOrHeight = .5f;
            var black = Node("Letterbox", canvasObject.transform, new Rect(0, 0, Width, Height));
            black.anchorMin = Vector2.zero; black.anchorMax = Vector2.one; black.offsetMin = black.offsetMax = Vector2.zero;
            black.gameObject.AddComponent<Image>().color = Color.black;
            Layout = Node("ReferenceCanvas", canvasObject.transform, new Rect(0, 0, Width, Height));
            Layout.anchorMin = Layout.anchorMax = new Vector2(.5f, .5f); Layout.pivot = new Vector2(.5f, .5f); Layout.anchoredPosition = Vector2.zero;
            Picture("Background", Layout, "MainMenu_BG_Clean_Base", new Rect(0, 0, Width, Height));
            foreground = Node("Foreground", Layout, new Rect(0, 0, Width, Height)).gameObject;
            var parent = foreground.transform;
            Picture("Logo", parent, "Logo_HumanThings2", new Rect(95, 48, 755, 755f * 725 / 2169));
            Text("Subtitle", parent, "화성 세대의 지구 문화 복원 탐사", new Rect(300, 306, 390, 34), 23);
            Picture("SubtitleLeft", parent, "Menu_Separator", new Rect(202, 324, 85, 8));
            Picture("SubtitleRight", parent, "Menu_Separator", new Rect(682, 324, 80, 8));
            Text("ArchivePoem", parent, "ARCHIVE\nTHE PLACES\nTHE PEOPLE\nTHE THINGS\nTHAT REMAIN", new Rect(73, 39, 200, 100), 14, quiet);
            Text("WaitingPoem", parent, "SOME\nTHINGS\nSTILL\nWAIT", new Rect(73, 460, 115, 95), 12, quiet);
            Text("RightTop", parent, "잊힌 세계를 걷는 일.\n그것은 여전히, 인간의 일이다.", new Rect(1467, 30, 192, 48), 13, quiet);
            Picture("DateRule", parent, "Menu_Separator", new Rect(1467, 78, 170, 8));
            Text("Date", parent, "2147. 03", new Rect(1555, 90, 82, 23), 13, quiet, TextAnchor.MiddleRight);
            Text("Quote", parent, "\"사라진 것들은 끝이 아니라,\n다른 어딘가에 남아 있다.\"", new Rect(1467, 846, 195, 46), 13, quiet);
            Text("Version", parent, "v0.1.0", new Rect(35, 900, 150, 25), 14, quiet);
            var photo = Node("Polaroid", parent, new Rect(-15, 611, 205, 255));
            photo.localRotation = Quaternion.Euler(0, 0, 7);
            Picture("Frame", photo, "Deco_Polaroid_Frame", new Rect(0, 0, 205, 255));
            Picture("Photo", photo, "Deco_Polaroid_Photo", new Rect(15, 20, 175, 171));
            Text("Note", photo, "여기에도, 사람이 있었다", new Rect(13, 204, 183, 26), 15, new Color(.16f, .16f, .15f));
            string[] names = { "탐사 시작", "기록 보관소", "설정", "종료" };
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                var row = Node("Button_" + i, parent, new Rect(204, 405 + 71 * i, 473, 66));
                var normal = Picture("Default", row, "Button_Default_BG_9Slice", new Rect(0, 0, 473, 66), true);
                // The supplied defaults are much more opaque than the reference's inactive rows.
                normal.color = new Color(1, 1, 1, .06f);
                var selection = Picture("Selected", row, "Button_Selected_BG_9Slice", new Rect(0, 0, 473, 66), true);
                var accent = Picture("LeftAccent", row, "Button_Selected_LeftAccent", new Rect(-13, 0, 28, 66));
                var arrow = Picture("MenuArrow", row, "Icon_MenuArrow", new Rect(20, 17, 32, 32));
                var label = Text("Label", row, names[i], new Rect(58, 0, 344, 66), 36, ink, TextAnchor.MiddleLeft);
                var button = row.gameObject.AddComponent<Button>();
                button.targetGraphic = normal; button.transition = Selectable.Transition.None;
                button.interactable = true;
                var item = row.gameObject.AddComponent<MainMenuItem>();
                item.Configure(this, i, selection.gameObject, accent.gameObject, arrow.gameObject, label, button);
                Items.Add(item); button.onClick.AddListener(() => Activate(index));
                Picture("Separator_" + i, parent, "Menu_Separator", new Rect(210, 470 + 71 * i, i == 0 ? 462 : 225, 8));
            }
            Picture("ScreenEdge", Layout, "FX_ScreenEdge_Frame", new Rect(0, 0, Width, Height));
            if (EventSystem.current == null)
            {
                ownedEvents = new GameObject("MainMenu EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                ownedEvents.transform.SetParent(transform, false);
            }
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat("MainMenu.MasterVolume", 1));
            EventSystem.current.SetSelectedGameObject(Items[0].gameObject);
            Focus(0); Resize();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--title-qa") >= 0) gameObject.AddComponent<MainMenuSmoke>().menu = this;
#endif
        }

        public static RectTransform Node(string name, Transform parent, Rect rect)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(rect.x, -rect.y); rt.sizeDelta = rect.size; return rt;
        }
        Image Picture(string name, Transform parent, string asset, Rect rect, bool sliced = false)
        {
            var image = Node(name, parent, rect).gameObject.AddComponent<Image>();
            var texture = Resources.Load<Texture2D>("MainMenu/" + asset);
            if (texture == null) { Debug.LogError("Missing main menu asset: " + asset); return image; }
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, sliced ? Vector4.one * 18 : Vector4.zero);
            sprites.Add(sprite); image.sprite = sprite; image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = sliced; return image;
        }
        Text Text(string name, Transform parent, string value, Rect rect, int size, Color? color = null, TextAnchor align = TextAnchor.UpperLeft)
        {
            var text = Node(name, parent, rect).gameObject.AddComponent<Text>();
            text.font = serif; text.text = value; text.fontSize = size; text.color = color ?? ink;
            text.alignment = align; text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate; text.supportRichText = false; return text;
        }
        void Resize()
        {
            var canvas = (RectTransform)canvasObject.transform;
            DisplayPreferences.FitCanvas(Layout, canvas, Width, Height);
        }
        void Update()
        {
            if (canvasObject == null) return;
            if (session.Online && Visible) Hide();
            if (!Visible) return;
            canvasObject.SetActive(!hud.ArchiveOpen);
            Resize(); foreground.SetActive(!hud.ArchiveOpen && settings == null);
            if (!DisplayPreferences.PopupOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && settings != null) CloseSettings();
        }
        public void Focus(int index)
        {
            if (index < 0 || index >= Items.Count || !Items[index].Button.interactable) index = -1;
            FocusedIndex = index;
            for (int i = 0; i < Items.Count; i++) Items[i].Highlight(i == index);
        }
        public void Activate(int index)
        {
            if (!Visible || hud.ArchiveOpen || settings != null || index < 0 || index >= Items.Count || !Items[index].Button.interactable) return;
            Focus(-1);
            switch (index)
            {
                case 0: if (hud.ExpeditionMenu != null) hud.ExpeditionMenu.Show(); else Hide(); break;
                case 1: hud.OpenArchive(); foreground.SetActive(false); break;
                case 2: OpenSettings(); break;
                case 3: Application.Quit(); break;
            }
        }
        public void Hide() { Visible = false; Focus(-1); canvasObject.SetActive(false); }
        public void Show()
        {
            if (!session.CanConnect) return;
            if (hud.ExpeditionMenu != null && hud.ExpeditionMenu.Visible) hud.ExpeditionMenu.Hide();
            Visible = true; canvasObject.SetActive(true); foreground.SetActive(true); Focus(-1);
        }
        void OpenSettings()
        {
            foreground.SetActive(false);
            settings = Node("Settings", Layout, new Rect(536, 230, 600, 470)).gameObject;
            settings.AddComponent<Image>().color = new Color(.045f, .055f, .055f, .98f);
            Text("Heading", settings.transform, "설정", new Rect(30, 22, 490, 50), 32);
            Text("Volume", settings.transform, "전체 음량", new Rect(30, 96, 300, 40), 24);
            var sliderNode = Node("VolumeSlider", settings.transform, new Rect(250, 100, 300, 30));
            var slider = sliderNode.gameObject.AddComponent<Slider>();
            var track = Node("Track", sliderNode, new Rect(0, 12, 300, 6)); track.gameObject.AddComponent<Image>().color = quiet;
            var handle = Node("Handle", sliderNode, new Rect(0, 0, 20, 30)); handle.gameObject.AddComponent<Image>().color = ink;
            handle.anchorMin = Vector2.zero; handle.anchorMax = Vector2.up; handle.sizeDelta = new Vector2(20, 0); handle.anchoredPosition = Vector2.zero;
            slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<Image>(); slider.minValue = 0; slider.maxValue = 1; slider.value = AudioListener.volume;
            slider.onValueChanged.AddListener(value => { AudioListener.volume = value; PlayerPrefs.SetFloat("MainMenu.MasterVolume", value); });
            var toggleNode = Node("Fullscreen", settings.transform, new Rect(30, 169, 500, 40));
            var toggle = toggleNode.gameObject.AddComponent<Toggle>();
            var box = Node("Box", toggleNode, new Rect(0, 4, 30, 30)); box.gameObject.AddComponent<Image>().color = quiet;
            var check = Node("Check", box, new Rect(6, 6, 18, 18)); check.gameObject.AddComponent<Image>().color = ink;
            toggle.targetGraphic = box.GetComponent<Image>(); toggle.graphic = check.GetComponent<Image>(); toggle.isOn = Screen.fullScreen;
            Text("Label", toggleNode, "전체 화면", new Rect(50, 0, 400, 40), 24);
            toggle.onValueChanged.AddListener(value => Screen.fullScreen = value);
            Text("AspectLabel", settings.transform, "화면 비율", new Rect(30, 240, 190, 45), 24);
            DisplayPreferences.AddDropdown(settings.transform, serif, new Rect(230, 240, 320, 45));
            var close = Node("Close", settings.transform, new Rect(200, 383, 200, 50));
            var bg = close.gameObject.AddComponent<Image>(); bg.color = new Color(.2f, .23f, .22f);
            var button = close.gameObject.AddComponent<Button>(); button.targetGraphic = bg;
            Text("Label", close, "닫기", new Rect(0, 0, 200, 50), 24, ink, TextAnchor.MiddleCenter);
            button.onClick.AddListener(CloseSettings);
        }
        void CloseSettings() { if (settings != null) Destroy(settings); settings = null; foreground.SetActive(true); Focus(-1); PlayerPrefs.Save(); }
        void OnApplicationFocus(bool focused) { if (!focused) Focus(-1); }
        void OnDestroy()
        {
            if (hud != null && hud.TitleMenu == this) hud.TitleMenu = null;
            if (canvasObject != null) Destroy(canvasObject);
            if (ownedEvents != null) Destroy(ownedEvents);
            foreach (var sprite in sprites) if (sprite != null) Destroy(sprite);
            if (serif != null) Destroy(serif);
        }
    }

    public sealed class MainMenuItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public Button Button { get; private set; }
        public bool Highlighted { get; private set; }
        MainMenu owner;
        int index;
        GameObject selection, accent, arrow;
        Text label;
        public void Configure(MainMenu menu, int id, GameObject selected, GameObject leftAccent, GameObject menuArrow, Text text, Button button)
        { owner = menu; index = id; selection = selected; accent = leftAccent; arrow = menuArrow; label = text; Button = button; Highlight(false); }
        public void Highlight(bool value)
        {
            Highlighted = value && Button.interactable;
            selection.SetActive(Highlighted); accent.SetActive(Highlighted); arrow.SetActive(Highlighted);
            label.fontSize = Highlighted ? 42 : 36;
            label.fontStyle = Highlighted ? FontStyle.Bold : FontStyle.Normal;
            label.color = Button.interactable ? (Highlighted ? new Color(.96f, .93f, .85f) : new Color(.83f, .81f, .76f)) : new Color(.4f, .41f, .39f);
        }
        public void OnPointerEnter(PointerEventData eventData) { owner.Focus(index); }
        public void OnPointerExit(PointerEventData eventData) { if (owner.FocusedIndex == index) owner.Focus(-1); }
        public void OnSelect(BaseEventData eventData) { owner.Focus(index); }
        public void OnDeselect(BaseEventData eventData) { if (owner.FocusedIndex == index) owner.Focus(-1); }
    }
}
