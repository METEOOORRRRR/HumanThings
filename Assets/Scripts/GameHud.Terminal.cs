using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EarthRecovery
{
    public sealed partial class GameHud
    {
        Font terminalFont;
        Vector2 terminalMissionScroll;
        readonly Dictionary<string, Texture2D> terminalImages = new();
        Texture2D TerminalImage(string name)
        {
            if (!terminalImages.TryGetValue(name, out var image))
            {
                image = Resources.Load<Texture2D>("BaseCampTerminal/" + name);
                terminalImages[name] = image;
            }
            return image;
        }
        void TerminalDraw(Rect rect, string image)
        {
            var texture = TerminalImage(image);
            if (texture != null) GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
        }
        GUIStyle TerminalText(int size, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            var style = new GUIStyle(GUI.skin.label) { font = terminalFont, fontSize = size, alignment = alignment, wordWrap = true, padding = new RectOffset() };
            style.normal.textColor = new Color(.85f, .85f, .8f);
            return style;
        }
        bool TerminalButton(Rect rect, string label, bool enabled = true, string icon = null, bool selected = false, bool confirm = false)
        {
            var old = GUI.enabled;
            GUI.enabled = old && enabled;
            string family = icon != null && rect.width == rect.height ? "Square_" : "Button_";
            var style = new GUIStyle { alignment = TextAnchor.MiddleCenter, font = terminalFont, fontSize = 20, wordWrap = true, border = new RectOffset(8, 8, 8, 8), padding = new RectOffset(10, 10, 4, 4) };
            style.normal.background = TerminalImage(confirm ? "08_Hibernate/Confirm_Button" : selected ? "06_Missions/Mission_Selected" : "04_Controls/" + family + (enabled ? "Normal" : "Disabled"));
            style.hover.background = TerminalImage("04_Controls/" + family + "Hover");
            style.active.background = TerminalImage("04_Controls/" + family + "Pressed");
            style.normal.textColor = style.hover.textColor = style.active.textColor = new Color(.85f, .85f, .8f);
            bool clicked = PixelGui.Button(rect, new GUIContent(icon == null ? label : "", icon == null ? "" : label), style);
            if (icon != null)
            {
                var color = GUI.color; GUI.color = new Color(1, 1, 1, enabled ? 1 : .3f);
                TerminalDraw(new Rect(rect.center.x - 20, rect.center.y - 20, 40, 40), "05_Icons/" + icon);
                GUI.color = color;
            }
            GUI.enabled = old;
            return clicked;
        }
        void CampPanel()
        {
            if (terminalFont == null) terminalFont = Font.CreateDynamicFontFromOSFont(new[] { "Batang", "Malgun Gothic", "Arial" }, 30);
            var matrix = GUI.matrix;
            var oldSmall = small;
            GUI.matrix = DisplayPreferences.GuiMatrix(1672, 941);
            try
            {
                // Only the display is backed by an opaque fill; the exterior stays transparent over the world.
                Fill(new Rect(170, 160, 1380, 650), new Color(.025f, .045f, .04f));
                TerminalDraw(new Rect(0, 0, 1672, 941), "01_Hardware/Terminal_Background");
                PixelGui.Label(new Rect(212, 194, 1000, 44), "베이스캠프 단말기", TerminalText(30));
                PixelGui.Label(new Rect(1230, 205, 230, 30), "HUMAN THINGS", TerminalText(17));
                TerminalDraw(new Rect(212, 250, 1296, 4), "02_Screen/Divider");
                if (TerminalButton(new Rect(1466, 196, 44, 44), "닫기", icon: "Close")) { ClosePanels(); world.MenuOpen = false; return; }
                PixelGui.Label(new Rect(212, 270, 450, 36), "주변 지역 지도", TerminalText(22));
                PixelGui.Label(new Rect(696, 270, 440, 36), "회수 임무", TerminalText(22));
                PixelGui.Label(new Rect(1170, 270, 338, 36), "협동 연결", TerminalText(22));
                small = TerminalText(14);
                DrawMap(new Rect(212, 316, 450, 450));
                var missions = session.View.sites.Where(s => s.mission).OrderBy(s => s.assignedOrder).Take(3).ToArray();
                int alive = session.View.players.Count(p => p.connected && p.alive);
                if (alive != 1) confirmAbandon = false;
                PixelGui.Label(new Rect(1194, 708, 290, 40), "생존 요원 " + alive + "명", TerminalText(18));
                if (missions.Length == 0) PixelGui.Label(new Rect(696, 316, 440, 60), "회수 임무 대기", TerminalText(20));
                else
                {
                    selectedPuzzle = Mathf.Clamp(selectedPuzzle, 0, missions.Length - 1);
                    for (int i = 0; i < missions.Length; i++)
                        if (TerminalButton(new Rect(696, 316 + i * 88, 440, 76), Catalog.Product(missions[i]) + "\n" + Stage(missions[i].phase), selected: i == selectedPuzzle)) { selectedPuzzle = i; terminalMissionScroll = Vector2.zero; }
                    var site = missions[selectedPuzzle];
                    ScrollText(new Rect(696, 590, 440, 116), ref terminalMissionScroll, Catalog.MissionLines(site), TerminalText(18));
                    bool linked = site.phase == CraftPhase.Puzzle && session.View.players.Any(p => p.alive && p.connected && p.id != session.LocalId && p.viewingSite == site.id);
                    TerminalDraw(new Rect(1170, 327, 24, 24), "07_Coop/Connection_" + (linked ? "Linked" : "Waiting"));
                    PixelGui.Label(new Rect(1206, 324, 302, 58), linked ? PuzzleName(site.puzzleKind) + " · 연결됨" : "협동 연결 대기", TerminalText(19));
                    if (TerminalButton(new Rect(1298, 394, 64, 64), "위", linked, "Arrow_Up")) Puzzle(site, 0, -1);
                    if (TerminalButton(new Rect(1222, 470, 64, 64), "왼쪽", linked, "Arrow_Left")) Puzzle(site, -1, 0);
                    if (TerminalButton(new Rect(1374, 470, 64, 64), "오른쪽", linked, "Arrow_Right")) Puzzle(site, 1, 0);
                    if (TerminalButton(new Rect(1298, 546, 64, 64), "아래", linked, "Arrow_Down")) Puzzle(site, 0, 1);
                    if (site.puzzleKind != PuzzleKind.Maze && TerminalButton(new Rect(1210, 634, 240, 54), site.puzzleKind == PuzzleKind.Crane ? "집게 내리기" : "회전", linked, site.puzzleKind == PuzzleKind.Crane ? "Claw" : "Rotate")) Puzzle(site, 0, 0, true);
                }
                if (alive == 1 && TerminalButton(new Rect(696, 722, 440, 44), confirmAbandon ? "동면 확정 · 임무 포기" : "동면 장치 가동", confirm: confirmAbandon))
                { if (confirmAbandon) session.Send(new Command { action = "hibernate" }); else confirmAbandon = true; }
                if (!string.IsNullOrEmpty(GUI.tooltip)) PixelGui.Label(new Rect(1170, 775, 338, 30), GUI.tooltip, TerminalText(16, TextAnchor.MiddleCenter));
            }
            finally { GUI.matrix = matrix; small = oldSmall; }
        }
    }
}
