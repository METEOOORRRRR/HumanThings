#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace EarthRecovery
{
    public sealed partial class DisplaySmoke : MonoBehaviour
    {
        string output;
        int saved, checks;
        NetworkSession session; GameHud hud; WorldView world;
        IEnumerator Start()
        {
            output=Path.Combine(Application.dataPath,"..","DisplayQA"); Directory.CreateDirectory(output);
            saved=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--display-reset-auto")>=0?0:DisplayPreferences.Selected; session=GetComponent<NetworkSession>(); hud=GetComponent<GameHud>(); world=GetComponent<WorldView>(); world.Automated=true;
            session.PersistArchive=false;
            Screen.SetResolution(1596,896,false); yield return new WaitForSecondsRealtime(1);
            Screen.SetResolution(1600,900,false); yield return new WaitForSecondsRealtime(2);
            var title=GetComponent<MainMenu>(); var lobby=GetComponent<ExpeditionLobby>(); var waiting=GetComponent<WaitingRoom>();
            for(int i=0;i<DisplayPreferences.Ratios.Length;i++)
            {
                DisplayPreferences.Select(i,false); yield return Capture("title-"+i,title.Layout);
            }
            yield return NativeSizes("title",title.Layout);
            if (!Application.isBatchMode) yield return Fullscreen("title", title.Layout);
            DisplayPreferences.Select(5,false);
            title.Activate(2); yield return null;
            if (!Application.isBatchMode) yield return Fullscreen("settings", title.Layout);
            var dropdown=title.Layout.Find("Settings/AspectRatio").GetComponent<Dropdown>(); dropdown.Show();
            yield return Capture("title-dropdown",title.Layout); yield return VerifyDropdown(dropdown,"title",title.Layout); dropdown.Hide();
            title.Layout.Find("Settings/Close").GetComponent<Button>().onClick.Invoke(); yield return null;
            lobby.Show();
            for(int p=0;p<3;p++) {lobby.SelectPage(p); yield return Capture("lobby-"+p,lobby.Layout);}
            lobby.Rooms.Create("화면 비율 검증",6,false,"");
            float timeout=Time.realtimeSinceStartup+20;
            while(!session.Online || session.LocalPlayer==null) { if(Time.realtimeSinceStartup>timeout) {Fail("connect");yield break;} yield return null; }
            for(ulong id=1;id<6;id++)session.HostGame.State.players.Add(new PlayerState{id=id,name="테스트 요원 "+id,ready=true});
            yield return new WaitForSecondsRealtime(.5f);
            for(int i=0;i<DisplayPreferences.Ratios.Length;i++) {DisplayPreferences.Select(i,false);yield return Capture("waiting-"+i,waiting.Layout);}
            yield return NativeSizes("waiting",waiting.Layout);
            DisplayPreferences.Select(5,false);
            waiting.ToggleOptions(); yield return null;
            var waitingDropdown=waiting.Layout.Find("OptionsPanel/AspectRatio").GetComponent<Dropdown>(); waitingDropdown.Show(); yield return Capture("waiting-dropdown",waiting.Layout);
            yield return VerifyDropdown(waitingDropdown,"waiting",waiting.Layout); waitingDropdown.Hide(); waiting.ToggleOptions();
            if (Application.isBatchMode)
            {
                File.WriteAllText(Path.Combine(output,"result.txt"),"PASS: " + checks + " canvas bounds/dropdown checks and offscreen menu captures. IMGUI HUD and OS fullscreen transitions require visible-window QA.");
                DisplayPreferences.Select(saved); Debug.Log("DISPLAY_CANVAS_QA_PASS " + checks); Application.Quit(0); yield break;
            }
            foreach(var p in session.HostGame.State.players)p.ready=true;
            if(!session.HostGame.Start(20260915)){Fail("start");yield break;}
            session.HostGame.State.monsters.Clear(); yield return new WaitForSecondsRealtime(.5f);
            hud.ClosePanels(); world.MenuOpen=false;
            yield return Fullscreen("hud", null);
            for(int i=0;i<DisplayPreferences.Ratios.Length;i++)
            {
                DisplayPreferences.Select(i,false);hud.ClosePanels();hud.CampOpen=true;world.MenuOpen=true;
                yield return Capture("terminal-"+i,null);
            }
            yield return NativeSizes("terminal",null);
            yield return Fullscreen("terminal", null);
            foreach(int aspect in new[]{2,5,12})
            {
                DisplayPreferences.Select(aspect,false); hud.ClosePanels();world.MenuOpen=false;yield return Capture("hud-"+aspect,null);
                hud.InventoryOpen=true;world.MenuOpen=true;yield return Capture("inventory-"+aspect,null);hud.ClosePanels();
                world.MenuOpen=true;yield return Capture("pause-"+aspect,null);
                DisplayPreferences.Show();yield return Capture("display-popup-"+aspect,null);
                var display=transform.Find("DisplaySettings/Layout/Panel/Close");display.GetComponent<Button>().onClick.Invoke();yield return null;
                var state=session.HostGame.State; var player=state.players[0]; var site=state.sites.First(s=>s.mission);
                Warp(site.position+Quaternion.Euler(0,site.quarterTurn*90,0)*HumanContent.Load().locations[site.id].prefab.GetComponent<LocationController>().console.localPosition); yield return new WaitForSecondsRealtime(.3f); hud.OpenSite(site.id);
                yield return Capture("facility-"+aspect,null);if(hud.SiteOpen!=site.id){Fail("facility not open");yield break;}
                site.facility=FacilityState.Restored;site.phase=CraftPhase.Crafting;yield return Capture("craft-"+aspect,null);
                site.maze=new int[36]; site.phase=CraftPhase.Puzzle;
                foreach(var kind in new[]{PuzzleKind.Alignment,PuzzleKind.Crane,PuzzleKind.Maze}){site.puzzleKind=kind;yield return Capture("puzzle-"+kind+"-"+aspect,null);}
                hud.ClosePanels();
                Warp(state.stations[0].position);yield return new WaitForSecondsRealtime(.3f);hud.StationOpen=0;
                yield return Capture("station-"+aspect,null);if(hud.StationOpen!=0){Fail("station not open");yield break;}hud.ClosePanels();
            }
            session.HostGame.State.phase=Phase.Success;yield return new WaitForSecondsRealtime(.4f);yield return Capture("results",null);
            hud.OpenArchive();yield return Capture("archive",null);hud.CloseArchive();
            File.WriteAllText(Path.Combine(output,"result.txt"),"PASS: "+checks+" viewport checks; runtime captures with actual screen "+Screen.width+"x"+Screen.height+"; fixture peers, not network gameplay test.");
            DisplayPreferences.Select(saved);Debug.Log("DISPLAY_QA_PASS "+checks);Application.Quit();
        }
        IEnumerator Capture(string name, RectTransform layout)
        {
            if (!Application.isBatchMode && name.StartsWith("terminal-")) yield return VerifyScrollPixels();
            yield return new WaitForSecondsRealtime(.3f); Canvas.ForceUpdateCanvases();
            if(layout!=null && !Application.isBatchMode)
            {
                var c=new Vector3[4];layout.GetWorldCorners(c);var r=DisplayPreferences.Viewport;
                var canvas=layout.GetComponentInParent<Canvas>();
                if(canvas.renderMode!=RenderMode.ScreenSpaceOverlay)
                    for(int i=0;i<c.Length;i++)c[i]=RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,c[i]);
                if(c.Any(p=>p.x<r.xMin-1||p.x>r.xMax+1||p.y<r.yMin-1||p.y>r.yMax+1)){Fail("bounds "+name);yield break;}
                checks++;
            }
            yield return new WaitForEndOfFrame();
            var screenshot=Application.isBatchMode ? CaptureCanvas(layout) : ScreenCapture.CaptureScreenshotAsTexture();
            var pixels=screenshot.GetPixels32();
            if(!pixels.Any(p=>p.r>40 || p.g>40 || p.b>40)){Destroy(screenshot);Fail("blank capture "+name);yield break;}
            File.WriteAllBytes(Path.Combine(output,name+".png"),screenshot.EncodeToPNG());Destroy(screenshot);
            yield return null;
        }
        IEnumerator NativeSizes(string name,RectTransform layout)
        {
            DisplayPreferences.Select(0,false);
            foreach(var size in new[]{new Vector2Int(1280,1024),new Vector2Int(1200,900),new Vector2Int(1440,900),new Vector2Int(1600,900),new Vector2Int(1600,750),new Vector2Int(1600,500),new Vector2Int(1600,450)})
            {
                Screen.SetResolution(size.x,size.y,false);yield return new WaitForSecondsRealtime(.4f);
                yield return Capture(name+"-native-"+Screen.width+"x"+Screen.height,layout);
            }
            Screen.SetResolution(1600,900,false);yield return new WaitForSecondsRealtime(.4f);
        }
        IEnumerator Fullscreen(string name, RectTransform layout)
        {
            DisplayPreferences.Select(0, false);
            var window = new Vector2Int(Screen.width, Screen.height);
            DisplayPreferences.SetFullscreen(true); yield return new WaitForSecondsRealtime(1);
            var display = Screen.mainWindowDisplayInfo;
            if (Screen.width != display.width || Screen.height != display.height) { Fail("fullscreen is not native resolution"); yield break; }
            foreach (int aspect in new[] { 0, 1, 5, 12 })
            { DisplayPreferences.Select(aspect, false); yield return Capture(name + "-fullscreen-" + aspect, layout); }
            DisplayPreferences.SetFullscreen(false); yield return new WaitForSecondsRealtime(1);
            if (Screen.width != window.x || Screen.height != window.y) { Fail("window size not restored"); yield break; }
            checks += 2;
        }
        void Warp(Vector3 position)
        {
            var body=GameObject.Find("Agent "+session.LocalId);var controller=body.GetComponent<CharacterController>();
            if(controller!=null)controller.enabled=false;body.transform.position=position;
            if(controller!=null)controller.enabled=true;session.HostGame.State.players[0].position=position;
        }
        void OnEnable() { Application.logMessageReceived+=Log; Invoke(nameof(Timeout),180); }
        void OnDisable() { Application.logMessageReceived-=Log; }
        void Log(string message,string trace,LogType type) { if(type==LogType.Exception){if(output!=null)File.WriteAllText(Path.Combine(output,"result.txt"),"FAIL exception: "+message);DisplayPreferences.Select(saved);Application.Quit(2);} }
        void Timeout() { Fail("timeout"); }
        IEnumerator VerifyDropdown(Dropdown dropdown,string name,RectTransform layout)
        {
            if(dropdown.captionText.font==null || dropdown.itemText.font==null || dropdown.options.Count!=DisplayPreferences.Ratios.Length){Fail("dropdown font/options");yield break;}
            var list=dropdown.transform.Find("Dropdown List");
            if(list==null){Fail("dropdown not expanded");yield break;}
            list.GetComponent<ScrollRect>().verticalNormalizedPosition=0;
            yield return Capture(name+"-dropdown-bottom",layout);
            var toggles=list.GetComponentsInChildren<Toggle>();toggles.Last().isOn=true;
            if(DisplayPreferences.Selected!=12){Fail("dropdown selection");yield break;}
            DisplayPreferences.Select(5,false); checks++;
        }
        void Fail(string message) {File.WriteAllText(Path.Combine(output,"result.txt"),"FAIL "+message);Debug.LogError("DISPLAY_QA_FAIL "+message);DisplayPreferences.Select(saved);Application.Quit(2);}
    }
}
#endif
