#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace EarthRecovery
{
    public sealed class LobbySmoke : MonoBehaviour
    {
        public ExpeditionLobby lobby;
        string output;
        readonly StringBuilder report = new();
        Mouse testMouse, physicalMouse;
        NetworkSession auxiliary;
        IEnumerator Start()
        {
            output = Path.Combine(Application.dataPath, "..", "LobbyQA"); Directory.CreateDirectory(output);
            Screen.SetResolution(1672, 941, false); yield return new WaitForSecondsRealtime(2);
            physicalMouse = Mouse.current; if (physicalMouse != null) InputSystem.DisableDevice(physicalMouse);
            testMouse = InputSystem.AddDevice<Mouse>("Lobby QA Mouse");
            auxiliary = new GameObject("Lobby QA advertised host").AddComponent<NetworkSession>(); auxiliary.PersistArchive = false;
            var advertised = auxiliary.gameObject.AddComponent<LanRooms>(); advertised.Initialize(auxiliary);
            Check(advertised.Create("서울 생활권 기록 조사", 4, true, "123456"), "Real public test host created");
            GetComponent<MainMenu>().Activate(0); yield return new WaitForSecondsRealtime(.3f);
            Check(lobby.Visible && !GetComponent<MainMenu>().Visible, "Start opens expedition lobby");
            var nav = (RectTransform)lobby.Navigation[0].transform; var navPosition = nav.position;
            yield return Capture("01-create");
            for (int i = 0; i < 3; i++)
            {
                yield return Click((RectTransform)lobby.Navigation[i].transform);
                Check(lobby.Page == i && lobby.Panels.Count(p => p.activeSelf) == 1 && lobby.Panels[i].activeSelf, "Only selected panel active " + i);
                Check(nav.position == navPosition, "Navigation stays fixed " + i);
                var item = (RectTransform)lobby.Navigation[i].transform;
                Check(item.anchoredPosition == new Vector2(120, -(346 + 60 * i)) && item.sizeDelta == new Vector2(390, 58), "Navigation reference coordinates " + i);
                if (i == 1)
                {
                    yield return new WaitForSecondsRealtime(.7f);
                    Check(lobby.Rooms.Rooms.Any(r => r.id == advertised.Hosted.id), "Real public room appears");
                    var row = lobby.Panels[1].transform.Find("RoomViewport/RoomRows/Room_" + advertised.Hosted.id);
                    if (row != null) yield return Click((RectTransform)row);
                    var join = lobby.Panels[1].transform.Find("JoinSelected").GetComponent<UnityEngine.UI.Button>();
                    Check(join.interactable, "Selected waiting room enables join");
                }
                yield return Capture("02-panel-" + i);
            }
            Check(!lobby.JoinCodeButton.interactable, "Empty code cannot join");
            string nonexistent = LanRooms.EncodeCode("127.0.0.1", 7784, "1234567890abcdef1234567890abcdef");
            Check(lobby.PasteCode(ExpeditionLobby.FormatCode(nonexistent)), "Formatted code paste fills all fields"); yield return null;
            yield return Click((RectTransform)lobby.JoinCodeButton.transform);
            yield return new WaitForSecondsRealtime(3.5f);
            Check(!GetComponent<NetworkSession>().Online && !GetComponent<WaitingRoom>().Visible, "Missing room never shows ready screen");
            yield return Capture("03-missing-code");
            auxiliary.Leave(); yield return new WaitForSecondsRealtime(.5f); Destroy(auxiliary.gameObject); auxiliary = null;
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(1024, 768) })
            {
                Screen.SetResolution(size.x, size.y, false); yield return new WaitForSecondsRealtime(.6f);
                var corners = new Vector3[4]; lobby.Layout.GetWorldCorners(corners);
                Check(corners.All(p => p.x >= -1 && p.y >= -1 && p.x <= Screen.width + 1 && p.y <= Screen.height + 1), "Layout fits " + size);
                yield return Capture("04-size-" + size.x + "x" + size.y);
            }
            yield return Click((RectTransform)lobby.Navigation[0].transform);
            lobby.RoomName.text = "LAN 검증 방";
            yield return Click((RectTransform)lobby.CreateButton.transform);
            yield return new WaitForSecondsRealtime(.8f);
            Check(GetComponent<NetworkSession>().Online && GetComponent<WaitingRoom>().Visible && !lobby.Visible, "Created host shows independent waiting room after approval");
            Check(lobby.Rooms.Hosted != null && lobby.Rooms.Hosted.Code.Length == 16, "Host has join code");
            yield return Capture("05-waiting");
            yield return Click((RectTransform)GetComponent<WaitingRoom>().LeaveButton.transform); yield return new WaitForSecondsRealtime(.8f);
            Check(!GetComponent<NetworkSession>().Online && !GetComponent<WaitingRoom>().Visible, "Leaving removes waiting state");
            yield return Click((RectTransform)lobby.Navigation[3].transform);
            Check(!lobby.Visible && GetComponent<MainMenu>().Visible, "Back returns to title");
            File.WriteAllText(Path.Combine(output, "verification.txt"), report.ToString()); Debug.Log(report.ToString());
            Application.Quit(report.ToString().Contains("FAIL") ? 1 : 0);
        }
        void FillCode(string code) { for (int i = 0; i < 4; i++) lobby.CodeFields[i].SetTextWithoutNotify(code.Substring(i * 4, 4)); }
        IEnumerator Capture(string name) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png")); yield return new WaitForSecondsRealtime(.2f); }
        IEnumerator Click(RectTransform target)
        {
            var point = RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(target.rect.center));
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = point }); yield return new WaitForSecondsRealtime(.15f);
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = point }.WithButton(MouseButton.Left)); yield return new WaitForSecondsRealtime(.15f);
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = point }); yield return new WaitForSecondsRealtime(.2f);
        }
        void Check(bool value, string message) { report.AppendLine((value ? "PASS " : "FAIL ") + message); }
        void OnDestroy() { if (auxiliary != null) Destroy(auxiliary.gameObject); if (testMouse != null && testMouse.added) InputSystem.RemoveDevice(testMouse); if (physicalMouse != null && physicalMouse.added) InputSystem.EnableDevice(physicalMouse); }
    }
}
#endif
