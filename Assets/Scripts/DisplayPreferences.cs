using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace EarthRecovery
{
    public sealed class DisplayPreferences : MonoBehaviour
    {
        const string Preference = "Display.Aspect";
        public static readonly string[] Labels = { "자동 (모니터)", "4:3", "5:4", "3:2", "16:10", "16:9", "18:9 (2:1)", "19.5:9", "20:9", "21:9", "24:10", "32:10", "32:9" };
        public static readonly float[] Ratios = { 0, 4f/3, 5f/4, 3f/2, 16f/10, 16f/9, 2, 19.5f/9, 20f/9, 21f/9, 24f/10, 32f/10, 32f/9 };
        public static int Selected { get; private set; }
        public static event Action Changed;
        public static bool PopupOpen => instance != null && instance.popup != null;
        static DisplayPreferences instance;
        GameObject popup;
        Font font;
        GameObject ownedEvents;
        RectTransform popupLayout;
        Vector2Int windowSize;
        Rect previousViewport;
        bool wasFullscreen;
        public static Rect Viewport => FitViewport(Screen.width, Screen.height, Ratios[Selected]);
        public static Rect FitViewport(float width, float height, float ratio)
        {
            width = Mathf.Max(1, width); height = Mathf.Max(1, height);
            if (!float.IsFinite(ratio) || ratio <= 0) return new Rect(0, 0, width, height);
            float w = Mathf.Floor(Mathf.Min(width, height * ratio)), h = Mathf.Floor(Mathf.Min(height, width / ratio));
            return new Rect(Mathf.Floor((width-w)/2), Mathf.Floor((height-h)/2), w, h);
        }
        public static Matrix4x4 GuiMatrix(float width, float height)
        {
            var r = Viewport; float scale = Mathf.Min(r.width / width, r.height / height);
            return Matrix4x4.TRS(new Vector3(r.center.x-width*scale/2, r.center.y-height*scale/2, 0), Quaternion.identity, Vector3.one*scale);
        }
        public static void FitCanvas(RectTransform layout, RectTransform canvas, float width=1672, float height=941)
        {
            var r = Viewport;
            // Text rasterization reads Canvas.scaleFactor, not a nested layout's localScale.
            float scale = Mathf.Max(.01f, Mathf.Min(r.width / width, r.height / height));
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = scale;
            var surface = canvas.GetComponent<Canvas>(); surface.scaleFactor = scale; surface.pixelPerfect = true;
            layout.localScale = Vector3.one;
        }
        public static void SetFullscreen(bool enabled)
        {
            if (enabled)
            {
                if (instance != null && !Screen.fullScreen) instance.windowSize = new Vector2Int(Screen.width, Screen.height);
                var display = Screen.mainWindowDisplayInfo;
                Screen.SetResolution(display.width, display.height, FullScreenMode.FullScreenWindow);
            }
            else
            {
                var size = instance != null && instance.windowSize.x > 0 ? instance.windowSize : new Vector2Int(1280, 720);
                Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
            }
        }
        public static void Select(int index, bool persist = true)
        {
            Selected = Mathf.Clamp(index, 0, Ratios.Length-1);
            if (persist) { PlayerPrefs.SetInt(Preference, Selected); PlayerPrefs.Save(); }
            Changed?.Invoke();
        }
        void Awake()
        {
            instance = this; Select(PlayerPrefs.GetInt(Preference, 0), false);
            wasFullscreen = Screen.fullScreen;
            if (wasFullscreen) SetFullscreen(true);
            else windowSize = new Vector2Int(Screen.width, Screen.height);
        }
        void Update()
        {
            // Cover OS/Alt+Enter transitions as well as the settings toggle.
            if (Screen.fullScreen && !wasFullscreen) SetFullscreen(true);
            wasFullscreen = Screen.fullScreen;
            if (!wasFullscreen) windowSize = new Vector2Int(Screen.width, Screen.height);
            var world = GetComponent<WorldView>();
            if (world != null && world.eye != null)
            {
                var r = Viewport;
                world.eye.rect = new Rect(r.x / Screen.width, r.y / Screen.height, r.width / Screen.width, r.height / Screen.height);
                if (r != previousViewport)
                {
                    world.eye.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().resetHistory = true;
                    previousViewport = r;
                }
            }
            if (popupLayout != null) FitCanvas(popupLayout, (RectTransform)popup.transform);
            if (PopupOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
        }
        void OnGUI()
        {
            var matrix = GUI.matrix; var color = GUI.color; int depth = GUI.depth;
            GUI.matrix = Matrix4x4.identity; GUI.color = Color.black; GUI.depth = -1000;
            var r = Viewport;
            GUI.DrawTexture(new Rect(0,0,Screen.width,r.y), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0,r.yMax,Screen.width,Screen.height-r.yMax), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0,r.y,r.x,r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax,r.y,Screen.width-r.xMax,r.height), Texture2D.whiteTexture);
            GUI.matrix=matrix; GUI.color=color; GUI.depth=depth;
        }
        public static Dropdown AddDropdown(Transform parent, Font font, Rect rect)
        {
            var root = MainMenu.Node("AspectRatio", parent, rect);
            var sync = root.gameObject.AddComponent<AspectDropdownSync>();
            if (font == null) { font = Font.CreateDynamicFontFromOSFont(new[] { "Batang", "Malgun Gothic", "Arial" }, 26); sync.OwnedFont = font; }
            var bg = root.gameObject.AddComponent<Image>(); bg.color = new Color(.12f,.16f,.14f);
            var dropdown = root.gameObject.AddComponent<Dropdown>(); dropdown.targetGraphic = bg;
            Text Label(string name, Transform p, Rect r, string value)
            {
                var t=MainMenu.Node(name,p,r).gameObject.AddComponent<Text>(); t.font=font; t.fontSize=22; t.text=value; t.alignment=TextAnchor.MiddleLeft; t.color=new Color(.87f,.86f,.81f); t.raycastTarget=false; return t;
            }
            dropdown.captionText = Label("Caption",root,new Rect(14,0,rect.width-58,rect.height),"");
            Label("Arrow",root,new Rect(rect.width-35,0,32,rect.height),"▾");
            var template = MainMenu.Node("Template",root,new Rect(0,rect.height+3,rect.width,240));
            template.gameObject.AddComponent<Image>().color = new Color(.055f,.075f,.065f);
            var scroll = template.gameObject.AddComponent<ScrollRect>(); scroll.horizontal=false; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=30;
            var viewport=MainMenu.Node("Viewport",template,new Rect(0,0,rect.width-16,240)); viewport.gameObject.AddComponent<RectMask2D>();
            var content=MainMenu.Node("Content",viewport,new Rect(0,0,rect.width-16,42));
            var item=MainMenu.Node("Item",content,new Rect(0,0,rect.width-16,42));
            item.anchorMin=new Vector2(0,.5f); item.anchorMax=new Vector2(1,.5f); item.pivot=new Vector2(.5f,.5f); item.sizeDelta=new Vector2(0,42); item.anchoredPosition=Vector2.zero;
            var itemBg=item.gameObject.AddComponent<Image>(); itemBg.color=new Color(.12f,.16f,.14f);
            var toggle=item.gameObject.AddComponent<Toggle>(); toggle.targetGraphic=itemBg;
            var check=MainMenu.Node("Selected",item,new Rect(0,0,4,42)).gameObject.AddComponent<Image>(); check.color=new Color(.8f,.74f,.55f); toggle.graphic=check;
            dropdown.itemText=Label("Label",item,new Rect(14,0,rect.width-46,42),"");
            scroll.viewport=viewport; scroll.content=content;
            var bar=MainMenu.Node("Scrollbar",template,new Rect(rect.width-12,4,8,232));bar.gameObject.AddComponent<Image>().color=new Color(.12f,.16f,.14f);
            var handle=MainMenu.Node("Handle",bar,new Rect(0,0,8,232));var handleImage=handle.gameObject.AddComponent<Image>();handleImage.color=new Color(.57f,.6f,.53f);
            handle.anchorMin=Vector2.zero;handle.anchorMax=Vector2.one;handle.pivot=new Vector2(.5f,.5f);handle.offsetMin=handle.offsetMax=Vector2.zero;
            var scrollbar=bar.gameObject.AddComponent<Scrollbar>();scrollbar.handleRect=handle;scrollbar.targetGraphic=handleImage;scrollbar.direction=Scrollbar.Direction.BottomToTop;scroll.verticalScrollbar=scrollbar;
            dropdown.template=template; dropdown.AddOptions(new System.Collections.Generic.List<string>(Labels)); dropdown.SetValueWithoutNotify(Selected);
            dropdown.onValueChanged.AddListener(i=>Select(i));
            sync.Initialize(dropdown);
            template.gameObject.SetActive(false);
            return dropdown;
        }
        public static void Show()
        {
            if (instance == null || PopupOpen) return;
            instance.Open();
        }
        void Open()
        {
            if (EventSystem.current == null)
            {
                ownedEvents=new GameObject("Display EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule)); ownedEvents.transform.SetParent(transform);
            }
            font=Font.CreateDynamicFontFromOSFont(new[] {"Batang","Malgun Gothic","Arial"},26);
            popup=new GameObject("DisplaySettings",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)); popup.transform.SetParent(transform,false);
            var canvas=popup.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=100;
            var scaler=popup.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1672,941);
            var block=MainMenu.Node("Backdrop",popup.transform,new Rect()); block.anchorMin=Vector2.zero; block.anchorMax=Vector2.one; block.offsetMin=block.offsetMax=Vector2.zero; block.gameObject.AddComponent<Image>().color=new Color(0,0,0,.65f);
            popupLayout=MainMenu.Node("Layout",popup.transform,new Rect(0,0,1672,941)); popupLayout.anchorMin=popupLayout.anchorMax=popupLayout.pivot=new Vector2(.5f,.5f); popupLayout.anchoredPosition=Vector2.zero;
            var panel=MainMenu.Node("Panel",popupLayout,new Rect(536,250,600,400)); panel.gameObject.AddComponent<Image>().color=new Color(.04f,.06f,.05f);
            var label=MainMenu.Node("Title",panel,new Rect(30,25,540,55)).gameObject.AddComponent<Text>(); label.font=font; label.fontSize=30; label.text="화면 비율"; label.color=Color.white;
            AddDropdown(panel,font,new Rect(30,100,540,48));
            var close=MainMenu.Node("Close",panel,new Rect(200,325,200,48)); var bg=close.gameObject.AddComponent<Image>(); bg.color=new Color(.18f,.22f,.19f); close.gameObject.AddComponent<Button>().onClick.AddListener(Close);
            var t=MainMenu.Node("Label",close,new Rect(0,0,200,48)).gameObject.AddComponent<Text>(); t.font=font; t.fontSize=24; t.text="닫기"; t.alignment=TextAnchor.MiddleCenter; t.color=Color.white; t.raycastTarget=false;
        }
        void Close() { if(popup!=null) Destroy(popup); popup=null; popupLayout=null; if(font!=null)Destroy(font); }
        void OnDestroy() { Close(); if(ownedEvents!=null)Destroy(ownedEvents); if(instance==this)instance=null; }
    }
    public sealed class AspectDropdownSync : MonoBehaviour
    {
        public Font OwnedFont;
        Dropdown dropdown;
        public void Initialize(Dropdown value) { dropdown=value; }
        void OnEnable() { DisplayPreferences.Changed+=Refresh; Refresh(); }
        void OnDisable() { DisplayPreferences.Changed-=Refresh; }
        void Refresh() { if(dropdown!=null)dropdown.SetValueWithoutNotify(DisplayPreferences.Selected); }
        void OnDestroy() { if(OwnedFont!=null)Destroy(OwnedFont); }
    }
}
