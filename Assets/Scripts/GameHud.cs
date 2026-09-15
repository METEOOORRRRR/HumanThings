using System;
using System.Linq;
using UnityEngine;

namespace EarthRecovery
{
    public sealed partial class GameHud : MonoBehaviour
    {
        public NetworkSession session;
        public WorldView world;
        public bool CampOpen, InventoryOpen;
        public int SiteOpen = -1;
        public int StationOpen = -1;
        bool archiveOpen;
        public bool ArchiveOpen => archiveOpen;
        public void OpenArchive() { archiveOpen = true; }
        public void CloseArchive() { archiveOpen = false; }
        public MainMenu TitleMenu;
        public ExpeditionLobby ExpeditionMenu;
        public WaitingRoom WaitingMenu;
        Vector2 archiveScroll;
        string toast = "";
        float toastUntil, nextInstall;
        string address = "127.0.0.1", port = "7777";
        int selectedPuzzle;
        bool confirmAbandon;
        Phase lastPhase;
        GUIStyle text, heading, small, button;
        Texture2D pixel;
        readonly Color panel = new(.08f, .105f, .11f, 1), accent = new(.42f, .8f, .67f), muted = new(.83f, .87f, .87f);
        void Start() { session.Presented += Present; }
        void Present(GameEvent e)
        {
            if(e.kind=="ArtifactRecovered"||e.kind=="LocationDiscovered") {toast=e.text;toastUntil=Time.unscaledTime+session.rules.recoveryToastSeconds;}
        }

        void Update()
        {
            if (session.View.phase != lastPhase)
            {
                lastPhase = session.View.phase; ClosePanels(); world.MenuOpen = lastPhase != Phase.Expedition;
            }
        }
        void Styles()
        {
            if (pixel == null) { pixel = new Texture2D(1, 1); pixel.SetPixel(0, 0, Color.white); pixel.Apply(); }
            GUI.skin.font = world.font;
            text = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true }; text.normal.textColor = Color.white;
            heading = new GUIStyle(text) { fontSize = 26, fontStyle = FontStyle.Bold };
            small = new GUIStyle(text) { fontSize = 14 }; small.normal.textColor = muted;
            button = new GUIStyle(GUI.skin.button) { fontSize = 18, fixedHeight = 0, wordWrap = true };
            GUI.skin.textField.fontSize = 18;
            GUI.skin.toggle.fontSize = 18;
        }
        void Fill(Rect rect, Color color) { var old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, pixel); GUI.color = old; }
        void Label(float x, float y, float w, float h, string value, GUIStyle style = null) => GUI.Label(new Rect(x, y, w, h), value, style ?? text);
        bool Button(float x, float y, float w, float h, string label, bool enabled = true)
        {
            bool previous = GUI.enabled; GUI.enabled = enabled;
            bool clicked = GUI.Button(new Rect(x, y, w, h), label, button); GUI.enabled = previous; return clicked;
        }
        void Panel(float x, float y, float w, float h, string title)
        {
            Fill(new Rect(x, y, w, h), panel); Fill(new Rect(x, y, w, 3), accent); Label(x + 20, y + 16, w - 60, 38, title, heading);
        }
        public void OpenSite(int id) { ClosePanels(); SiteOpen = id; session.Send(new Command { action = "view", target = id }); }
        public void ClosePanels()
        {
            if (SiteOpen >= 0) session.Send(new Command { action = "close" });
            SiteOpen = StationOpen = -1; CampOpen = false; InventoryOpen = false; confirmAbandon = false;
        }
        public void SkillPress()
        {
            if (SiteOpen < 0 || SiteOpen >= session.View.sites.Count) return;
            var s = session.View.sites[SiteOpen];
            session.Send(new Command { action = s.checkStarted >= 0 ? "check" : "craft", target = s.id });
        }
        void OnGUI()
        {
            if (session == null || world == null) return;
            if (WaitingMenu != null && WaitingMenu.Visible && !archiveOpen) return;
            if (ExpeditionMenu != null && ExpeditionMenu.Visible && !archiveOpen) return;
            if (TitleMenu != null && TitleMenu.Visible && !archiveOpen) return;
            Styles();
            GUI.matrix = DisplayPreferences.GuiMatrix(1280, 720);
            if (DisplayPreferences.PopupOpen) return;
            if(archiveOpen && session.View.phase!=Phase.Expedition) {ArchivePanel();return;}
            if (!session.Online || session.View.phase == Phase.Lobby) { Lobby(); return; }
            if (session.View.phase > Phase.Expedition) { Results(); RecoveryToast(); return; }
            Overlay();
            RecoveryToast();
            var p = session.LocalPlayer;
            if (p == null) return;
            if (!p.alive)
            {
                Panel(400, 540, 480, 135, "관전"); Label(420, 598, 250, 48, p.deathReason);
                if (Button(690, 602, 165, 42, "다음 요원 →")) world.SpectatorIndex++;
                return;
            }
            if (SiteOpen >= 0 && (SiteOpen >= session.View.sites.Count || !WorldGeometry.NearConsole(session.View.sites[SiteOpen], p.position))) ClosePanels();
            if (CampOpen && !WorldGeometry.InCamp(p.position)) ClosePanels();
            if(StationOpen>=0 && (StationOpen>=session.View.stations.Count || Vector3.Distance(p.position,session.View.stations[StationOpen].position)>4)) ClosePanels();
            if (SiteOpen >= 0) SitePanel();
            else if(StationOpen>=0) StationPanel();
            else if (CampOpen) CampPanel();
            else if (InventoryOpen) Inventory();
            else if (world.MenuOpen) PauseMenu();
            else
            {
                Fill(new Rect(638, 358, 4, 4), Color.white);
                if (!string.IsNullOrEmpty(world.Nearby)) { Fill(new Rect(430, 607, 420, 42), panel); Label(448, 613, 390, 34, world.Nearby); }
            }
        }
        void Lobby()
        {
            if (TitleMenu != null && session.CanConnect && Button(65, 8, 180, 40, "타이틀로")) TitleMenu.Show();
            Panel(65, 60, 520, 600, "HUMAN THINGS · " + NetworkSession.BuildLabel);
            Label(87, 122, 460, 34, "탐사 대기실", heading);
            if (session.IsConnecting)
            {
                Label(88, 220, 465, 48, "접속 중", heading);
                Label(88, 284, 465, 85, session.Status, small);
                if (Button(88, 400, 220, 50, "접속 취소")) session.Leave();
            }
            else if (!session.Online)
            {
                Label(88, 185, 100, 32, "요원명"); session.UserName = GUI.TextField(new Rect(195, 180, 355, 38), session.UserName, 16);
                Label(88, 242, 100, 32, "주소"); address = GUI.TextField(new Rect(195, 237, 355, 38), address, 64);
                Label(88, 299, 100, 32, "포트"); port = GUI.TextField(new Rect(195, 294, 140, 38), port, 5);
                bool valid = ushort.TryParse(port, out var number) && number > 0 && session.CanConnect;
                if (Button(88, 370, 220, 50, "방 만들기", valid)) session.Connect(true, "127.0.0.1", number);
                if (Button(325, 370, 225, 50, "참가", valid)) session.Connect(false, address, number);
                Label(88, 448, 465, 85, session.Status, small);
            }
            else
            {
                var me = session.LocalPlayer;
                for (int i = 0; i < session.View.players.Count; i++)
                {
                    var p = session.View.players[i]; float y = 185 + i * 43;
                    Fill(new Rect(88, y, 463, 35), new Color(.15f, .19f, .19f));
                    Label(100, y + 3, 280, 30, p.name + (p.id == 0 ? " · 호스트" : ""));
                    Label(397, y + 3, 145, 30, p.ready ? "준비 완료" : "준비 중", small);
                }
                if (Button(88, 476, 220, 45, me?.ready == true ? "준비 취소" : "준비 완료", me != null)) session.Send(new Command { action = "ready", flag = !me.ready });
                if (Button(326, 476, 225, 45, "탐사 시작", session.IsHost && session.View.players.Count >= session.rules.MinimumStartPlayers && session.View.players.All(p => p.ready))) session.StartMission();
                if (Button(88, 538, 220, 42, "나가기")) session.Leave();
                Label(88, 595, 462, 40, session.Status, small);
            }
            Panel(615, 60, 600, 600, "임무 설정");
            Label(640, 128, 540, 48, "문화·생활 표본 3개 회수", heading);
            Label(640, 198, 530, 35, "최대 인원   " + session.rules.maxPlayers + "명");
            Label(640, 248, 530, 35, "산소 예산   " + Mathf.RoundToInt(session.rules.oxygenSeconds / 60) + "분");
            bool editable = session.CanConnect || session.IsHost;
            GUI.enabled = editable;
            session.rules.oxygenSeconds = Mathf.Round(GUI.HorizontalSlider(new Rect(641, 306, 530, 25), session.rules.oxygenSeconds, 300, 1740) / 60) * 60;
            GUI.enabled = true;
            Label(640, 360, 530, 38, "통신", heading);
            RadioControls(640, 418);
            if(Button(640,535,250,45,"HumanThings Archive")) archiveOpen=true;
        }
        void RadioControls(float x, float y)
        {
            if (session.radio == null) return;
            if (Button(x, y, 250, 42, session.radio.Enabled ? "마이크 끄기" : "마이크 켜기"))
            { if (session.radio.Enabled) session.radio.StopMicrophone(); else session.radio.StartMicrophone(); }
            if (Button(x + 270, y, 250, 42, session.radio.Muted ? "수신 음소거 해제" : "수신 음소거")) session.radio.Muted = !session.radio.Muted;
            Label(x, y + 51, 520, 28, session.radio.Status, small);
        }
        void Overlay()
        {
            Fill(new Rect(20, 18, 360, 89), panel);
            Label(35, 30, 330, 31, "산소 " + TimeSpan.FromSeconds(session.View.oxygen).ToString(@"mm\:ss"));
            Fill(new Rect(35, 77, 325, 6), new Color(.2f, .25f, .25f));
            Fill(new Rect(35, 77, 325 * session.View.oxygen / Mathf.Max(1, session.View.oxygen + session.View.elapsed), 6), accent);
            Label(400, 28, 490, 35, session.View.message, small);
            bool showMissions = !CampOpen && SiteOpen < 0 && StationOpen < 0 && !InventoryOpen && !world.MenuOpen;
            if (showMissions) Fill(new Rect(936, 18, 324, 525), panel);
            if (showMissions)
            {
                int i = 0;
                foreach (var s in session.View.sites.Where(s => s.mission).OrderBy(s => s.assignedOrder))
                {
                    float y = 30 + i++ * 171;
                    Label(952, y, 292, 24, Catalog.Product(s), small);
                    Label(952, y + 24, 292, 22, Stage(s.phase), small);
                    var hint = Catalog.MissionDetails(s);
                    var style = FitText(small, hint, 292, 120);
                    Label(952, y + 46, 292, 120, hint, style);
                }
            }
            var p = session.LocalPlayer;
            if (p != null)
            {
                Fill(new Rect(20, 657, 570, 44), panel);
                Label(34, 666, 550, 30, (WorldGeometry.InCamp(p.position) ? "베이스캠프" : "외부 · 방독면 착용") + " | " + (p.carrying >= 0 ? Catalog.Product(session.View.sites[p.carrying]) + " 운반 중" : "고철 " + p.inventory.Sum() + "/" + session.rules.resourceCapacity), small);
            }
            if (session.radio.Talking) Label(1080, 664, 170, 30, "송신 중", small);
        }
        static string Stage(CraftPhase p) => p switch { CraftPhase.Repair => "복원 대기", CraftPhase.Crafting => "제작", CraftPhase.Puzzle => "협동 퍼즐", CraftPhase.Ready => "운반 대기", CraftPhase.Carried => "운반 중", _ => "회수 완료" };
        void CloseButton(float x, float y)
        {
            if (Button(x, y, 48, 38, "×")) { ClosePanels(); world.MenuOpen = false; }
        }
        void SitePanel()
        {
            var s = session.View.sites[SiteOpen];
            Panel(215, 130, 850, 500, Catalog.Sites[s.id] + " · 시설 단말기"); CloseButton(998, 143);
            Label(240, 192, 790, 34, s.facility==FacilityState.ModuleRequired?"복원 모듈 미설치":"시설 복원 완료");
            if (s.facility == FacilityState.ModuleRequired)
            {
                var definition=HumanContent.Load().locations[s.id];
                bool owned=session.LocalPlayer.modules.Contains(s.id);
                Label(240,260,750,80,definition.displayNameKnown+" · "+definition.moduleName+"\n"+(owned?"설치 가능 · 모듈 보유":"설치 불가 · 해당 모듈 미보유"));
                GUI.enabled=owned;
                bool held=GUI.RepeatButton(new Rect(240,380,360,50),session.LocalPlayer.installing==s.id?"설치 중":"모듈 설치",button);GUI.enabled=true;
                if(held && Time.unscaledTime>=nextInstall) {session.Send(new Command {action="install",target=s.id,flag=true});nextInstall=Time.unscaledTime+.08f;}
                if(session.LocalPlayer.installing==s.id) Fill(new Rect(240,450,360*Mathf.Clamp01((session.View.elapsed-session.LocalPlayer.installStarted)/definition.recipe.installDuration),7),accent);
            }
            else if (!s.mission) Label(240, 300, 750, 80, "설비 가동 중 · 제작 요청 없음");
            else if (s.phase == CraftPhase.Crafting)
            {
                Label(240, 255, 750, 40, "제작 진행 " + s.checks + "/" + session.rules.checksRequired);
                Label(240,292,750,36,s.missionHint.structure,small);
                Rect bar = new(240, 335, 790, 50); Fill(bar, new Color(.22f, .26f, .26f));
                Fill(new Rect(bar.x + bar.width * (.65f - session.rules.skillWindow / 2), bar.y, bar.width * session.rules.skillWindow, bar.height), accent);
                if (s.checkStarted >= 0)
                {
                    float t = Mathf.Clamp01((session.View.elapsed - s.checkStarted) / session.rules.skillCycleSeconds);
                    Fill(new Rect(bar.x + bar.width * t - 3, bar.y - 8, 6, 66), Color.white);
                }
                bool ownCheck = s.worker == session.LocalId;
                if (Button(240, 446, 360, 50, s.checkStarted >= 0 ? "판정" : "제작 시작", s.checkStarted < 0 || ownCheck)) SkillPress();
            }
            else if (s.phase == CraftPhase.Puzzle)
            {
                Label(240, 240, 335, 45, PuzzleName(s.puzzleKind), heading);
                Label(240, 316, 320, 120, session.View.players.Any(p => p.alive && p.connected && WorldGeometry.InCamp(p.position)) ? "캠프 연결됨" : "캠프 연결 대기");
                DrawPuzzle(s, new Rect(651, 237, 354, 354));
            }
            else Label(240, 300, 700, 90, Stage(s.phase), heading);
        }
        static string PuzzleName(PuzzleKind kind) => kind switch { PuzzleKind.Alignment => "회전 레버", PuzzleKind.Crane => "인형뽑기", _ => "미로찾기" };
        void DrawPuzzle(SiteState s, Rect r)
        {
            if (s.targetX < 0) { Label(r.x, r.y, r.width, 100, "현장 연결 중"); return; }
            float cell = r.width / 6;
            for (int y = 0; y < 6; y++) for (int x = 0; x < 6; x++)
            {
                bool wall = s.puzzleKind == PuzzleKind.Maze && s.maze.Length == 36 && s.maze[y * 6 + x] == 1;
                Fill(new Rect(r.x + x * cell + 2, r.y + y * cell + 2, cell - 4, cell - 4), wall ? new Color(.03f, .04f, .04f) : new Color(.25f, .3f, .3f));
            }
            Rect target = new(r.x + s.targetX * cell + 8, r.y + s.targetY * cell + 8, cell - 16, cell - 16);
            Fill(target, accent);
            Rect current = new(r.x + s.puzzleX * cell + 14, r.y + s.puzzleY * cell + 14, cell - 28, cell - 28);
            Fill(current, new Color(.93f, .7f, .32f));
            if (s.puzzleKind == PuzzleKind.Alignment)
            {
                var arrows = new[] { "↑", "→", "↓", "←" };
                GUI.Label(target, arrows[s.targetAngle], heading); GUI.Label(current, arrows[s.angle], text);
            }
            else if (s.puzzleKind == PuzzleKind.Crane)
            { GUI.Label(target, "●", heading); GUI.Label(current, "+", text); }
        }
        void Puzzle(SiteState s, int x, int y, bool flag = false) => session.Send(new Command { action = "puzzle", target = s.id, x = x, y = y, flag = flag });
        void DrawMap(Rect rect)
        {
            TerminalDraw(rect, "03_Map/Map_Grid");
            var size = session.View.mapSize;
            Vector2 Map(Vector3 p) => new(rect.x + (p.x / size.x + .5f) * rect.width, rect.y + (.5f - p.z / size.y) * rect.height);
            if (session.View.cityWorld && world.City != null)
                foreach (var chunk in world.City.chunks)
                {
                    var b = chunk.node.worldBounds;
                    var corner = Map(new Vector3(b.min.x, 0, b.max.z));
                    var tint = Color.HSVToRGB((int)chunk.node.district * .137f, .24f, .28f);
                    Fill(new Rect(corner.x + 1, corner.y + 1, b.size.x / size.x * rect.width - 2, b.size.z / size.y * rect.height - 2), tint);
                    foreach (var road in chunk.city.roads)
                    {
                        var rb = road.bounds; rb.center += chunk.node.worldBounds.center;
                        var at = Map(new Vector3(rb.min.x, 0, rb.max.z));
                        Fill(new Rect(at.x, at.y, rb.size.x / size.x * rect.width, rb.size.z / size.y * rect.height), new Color(.35f,.38f,.38f));
                    }
                }
            for (int d = 0; !session.View.cityWorld && d < HumanContent.Load().zones.Length; d++)
            {
                var zone = HumanContent.Load().zones[d];
                float minX = zone.spawnSockets.Min(p=>p.x)-6, maxX = zone.spawnSockets.Max(p=>p.x)+6;
                float minZ = zone.spawnSockets.Min(p=>p.z)-6, maxZ = zone.spawnSockets.Max(p=>p.z)+6;
                var corner = Map(zone.center + new Vector3(minX,0,maxZ));
                Color[] colors = { new(.28f,.39f,.32f), new(.40f,.30f,.38f), new(.38f,.36f,.24f), new(.25f,.36f,.44f), new(.38f,.30f,.26f) };
                Fill(new Rect(corner.x,corner.y,(maxX-minX)/150*rect.width,(maxZ-minZ)/150*rect.height),colors[d%colors.Length]);
                Label(corner.x,corner.y-22,110,22,zone.displayName,small);
            }
            var camp = Map(Vector3.zero); Fill(new Rect(camp.x - 6, camp.y - 6, 12, 12), accent);
            foreach (var s in session.View.sites)
            {
                var at = Map(s.position); Fill(new Rect(at.x - 5, at.y - 5, 10, 10), s.discovered ? accent : muted);
                Label(at.x - 32, at.y + 6, 64, 30, Catalog.MapName(s), new GUIStyle(small) { fontSize = 12, alignment = TextAnchor.UpperCenter });
            }
            foreach (var p in session.View.players.Where(p => p.connected && p.alive))
            { var at = Map(p.position); Fill(new Rect(at.x - 3, at.y - 3, 6, 6), Color.white); }
            foreach(var s in session.View.stations) {var at=Map(s.position);Fill(new Rect(at.x-4,at.y-4,8,8),Color.cyan);}
        }
        void Inventory()
        {
            Panel(270, 120, 740, 510, "휴대 재료"); CloseButton(943, 133);
            int row = 0; var p = session.LocalPlayer;
            for (int i = 0; i < p.inventory.Length; i++) if (p.inventory[i] > 0)
            {
                float x = 290 + (row / 6) * 350, y = 192 + (row % 6) * 63;
                Label(x, y, 230, 45, Catalog.Material(i) + " ×" + p.inventory[i]);
                if (Button(x + 235, y, 94, 39, "내려놓기")) session.Send(new Command { action = "dropMaterial", target = i });
                row++;
            }
            if (row == 0) Label(295, 201, 650, 42, "빈 가방", small);
            for(int i=0;i<p.modules.Count;i++)
            {
                int location=p.modules[i];float y=265+i*46;
                Label(295,y,500,40,HumanContent.Load().locations[location].moduleName,small);
                if(Button(830,y,125,38,"내려놓기")) session.Send(new Command {action="moduleDrop",target=location});
            }
        }
        void PauseMenu()
        {
            Panel(345, 176, 590, 360, "통신 / 세션"); CloseButton(868, 189);
            RadioControls(370, 250);
            if (Button(370, 390, 250, 48, "탐사 계속")) world.MenuOpen = false;
            if (Button(640, 390, 250, 48, "방 나가기")) session.Leave();
            if (Button(370, 455, 520, 48, "화면 비율 · " + DisplayPreferences.Labels[DisplayPreferences.Selected])) DisplayPreferences.Show();
        }
        void Results()
        {
            Panel(245, 140, 790, 440, session.View.phase == Phase.Success ? "임무 성공" : "임무 종료");
            Label(270, 212, 735, 65, session.View.message, heading);
            int recovered = session.View.sites.Count(s => s.mission && s.phase == CraftPhase.Delivered);
            Label(270, 298, 720, 44, "회수 표본 " + recovered + "/3");
            Label(270, 356, 720, 44, "생존 " + session.View.players.Count(p => p.alive && p.connected) + "명");
            if(Button(660,356,340,42,"HumanThings Archive")) archiveOpen=true;
            if (Button(270, 461, 340, 50, "대기실로", session.IsHost)) session.ReturnToLobby();
            if (Button(639, 461, 340, 50, "방 나가기")) session.Leave();
        }
        void StationPanel()
        {
            var station=session.View.stations[StationOpen];
            Panel(65,60,1150,600,"복원 제작기 "+(station.id+1));CloseButton(1148,73);
            Label(90,128,990,30,"고철 "+session.LocalPlayer.inventory[0]+" · 모듈 15종 · "+(station.phase==StationPhase.Available?"대기":station.phase==StationPhase.CraftingModule?"모듈 제작 중":"사용 불가"));
            var content = HumanContent.Load();
            for (int z=0;z<content.zones.Length;z++)
            {
                float y=178+z*82;
                Fill(new Rect(85,y+76,1110,1),new Color(.22f,.28f,.28f));
                Label(90,y+13,135,35,content.zones[z].displayName);
                int column=0;
                foreach(var site in session.View.sites.Where(s=>content.locations[s.id].zone==z).OrderBy(s=>s.id))
                {
                    var l=content.locations[site.id];float x=240+column++*308;
                    bool exists=site.facility!=FacilityState.ModuleRequired||site.moduleExists;
                    Label(x,y,192,28,l.displayNameKnown+" · 고철 "+l.recipe.scrapCost);
                    Label(x,y+30,192,42,l.moduleName,small);
                    string state=site.facility!=FacilityState.ModuleRequired?"복원 완료":site.moduleExists?"제작됨":"제작";
                    if(Button(x+198,y+11,94,42,state,!exists&&station.phase==StationPhase.Available&&session.LocalPlayer.inventory[0]>=l.recipe.scrapCost)) session.Send(new Command {action="moduleCraft",target=site.id,x=station.id});
                }
            }
            if(station.phase==StationPhase.CraftingModule)
            {
                float progress=Mathf.Clamp01((session.View.elapsed-station.started)/HumanContent.Load().locations[station.target].recipe.duration);
                Fill(new Rect(90,617,790*progress,8),accent);
                if(Button(930,600,235,42,"제작 취소",station.worker==session.LocalId)) session.Send(new Command {action="moduleCancel"});
            }
        }
        void RecoveryToast()
        {
            if (session.View.phase == Phase.Expedition && (CampOpen || SiteOpen >= 0 || StationOpen >= 0 || InventoryOpen || world.MenuOpen)) return;
            if(Time.unscaledTime<toastUntil) {Fill(new Rect(20,110,590,154),panel);Label(35,121,560,135,toast,small);}
        }
        void ArchivePanel()
        {
            Panel(80,55,1120,610,"HumanThings Archive");
            if(Button(1115,72,60,40,"×")) CloseArchive();
            var rows = session.View.archive.OrderBy(e => e.artifactId).Select(Catalog.ArchiveLines).ToArray();
            float total = rows.Sum(lines => lines.Select((line, i) => (i == 0 ? heading : text).CalcHeight(new GUIContent(line), 1000) + 6).Sum() + 20);
            archiveScroll=GUI.BeginScrollView(new Rect(105,125,1070,510),archiveScroll,new Rect(0,0,1040,Mathf.Max(500,total)));
            float y = 0;
            foreach(var lines in rows)
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    var style = i == 0 ? heading : text;
                    float height = style.CalcHeight(new GUIContent(lines[i]), 1000);
                    Label(10, y, 1000, height, lines[i], style); y += height + 6;
                }
                y += 20;
            }
            if(rows.Length==0) Label(10,20,1000,50,"등록된 표본 없음");
            GUI.EndScrollView();
        }
        static void ScrollText(Rect area, ref Vector2 scroll, string[] lines, GUIStyle style)
        {
            float width = area.width - 20;
            var heights = lines.Select(line => style.CalcHeight(new GUIContent(line), width) + 5).ToArray();
            scroll = GUI.BeginScrollView(area, scroll, new Rect(0, 0, width, Mathf.Max(area.height, heights.Sum())));
            float y = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                GUI.Label(new Rect(0, y, width, heights[i] - 5), lines[i], style);
                y += heights[i];
            }
            GUI.EndScrollView();
        }
        public static GUIStyle FitText(GUIStyle original, string value, float width, float height)
        {
            var style = new GUIStyle(original);
            while (style.fontSize > 10 && style.CalcHeight(new GUIContent(value), width) > height) style.fontSize--;
            return style;
        }
        void OnDestroy() { if(session!=null) session.Presented-=Present; if (pixel != null) Destroy(pixel); if (terminalFont != null) Destroy(terminalFont); }
    }
}
