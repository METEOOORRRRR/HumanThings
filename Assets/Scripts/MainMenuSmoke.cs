#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace EarthRecovery
{
    public sealed class MainMenuSmoke : MonoBehaviour
    {
        public MainMenu menu;
        string output;
        readonly StringBuilder report = new();
        Mouse testMouse, physicalMouse;
        IEnumerator Start()
        {
            output = Path.Combine(Application.dataPath, "..", "TitleQA"); Directory.CreateDirectory(output);
            Screen.SetResolution(1672, 941, false);
            yield return new WaitForSecondsRealtime(2);
            physicalMouse = Mouse.current;
            if (physicalMouse != null) InputSystem.DisableDevice(physicalMouse);
            testMouse = InputSystem.AddDevice<Mouse>("Title QA Mouse");
            menu.Focus(0);
            yield return Capture("01-reference-size");
            Check(menu.Items.Count == 4, "Four menu buttons");
            Check(menu.Items.All(x => x.transform.Find("Label").GetComponent<UnityEngine.UI.Text>().text != "이어하기"), "Continue removed");
            Check(menu.Layout.Find("Logo") == null && menu.Layout.Find("Foreground/Logo") != null, "Separate logo layer");
            var logo = (RectTransform)menu.Layout.Find("Foreground/Logo");
            Check(logo.anchoredPosition == new Vector2(95, -48) && Mathf.Abs(logo.rect.width / logo.rect.height - 2169f / 725) < .001f, "Logo 2 keeps source aspect ratio");
            Check(logo.GetComponent<UnityEngine.UI.Image>().sprite.texture.name == "Logo_HumanThings2", "Logo 2 asset selected");
            var subtitle = (RectTransform)menu.Layout.Find("Foreground/Subtitle");
            Check(-logo.anchoredPosition.y + logo.rect.height < -subtitle.anchoredPosition.y, "Logo and subtitle do not overlap");
            foreach (var item in menu.Items)
            {
                var rect = (RectTransform)item.transform;
                int index = menu.Items.IndexOf(item);
                Check(rect.anchoredPosition == new Vector2(204, -(405 + 71 * index)) && rect.sizeDelta == new Vector2(473, 66), "Button " + index + " reference coordinates");
                var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
                InputSystem.QueueStateEvent(testMouse, new MouseState { position = point });
                yield return new WaitForSecondsRealtime(.3f);
                Check(menu.FocusedIndex == index, "Pointer hover " + index);
                Check(menu.Items.Count(x => x.Highlighted) == 1, "Single selection " + index);
                Check(item.transform.Find("MenuArrow").gameObject.activeSelf, "Arrow state " + index);
                var label = item.transform.Find("Label").GetComponent<UnityEngine.UI.Text>();
                var arrow = (RectTransform)item.transform.Find("MenuArrow");
                Check(arrow.anchoredPosition.x == 20 && arrow.anchoredPosition.x + arrow.rect.width <= label.rectTransform.anchoredPosition.x, "Arrow left of label without overlap " + index);
                Check(label.fontSize == 42 && label.fontStyle == FontStyle.Bold, "Selected typography " + index);
                Check(label.preferredWidth <= label.rectTransform.rect.width && label.preferredHeight <= label.rectTransform.rect.height, "Selected label fits " + index);
                yield return Capture("02-hover-" + index);
            }
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = new Vector2(Screen.width - 20, 20) });
            yield return new WaitForSecondsRealtime(.3f);
            Check(menu.FocusedIndex == -1 && menu.Items.All(x => !x.Highlighted), "Pointer exit clears all effects");
            Check(menu.Items.All(x => { var text = x.transform.Find("Label").GetComponent<UnityEngine.UI.Text>(); return text.fontSize == 36 && text.fontStyle == FontStyle.Normal; }), "Pointer exit restores typography");
            yield return Capture("03-no-focus");
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(1024, 768) })
            {
                Screen.SetResolution(size.x, size.y, false); yield return new WaitForSecondsRealtime(.6f);
                var corners = new Vector3[4]; menu.Layout.GetWorldCorners(corners);
                Check(corners.All(p => p.x >= -1 && p.y >= -1 && p.x <= Screen.width + 1 && p.y <= Screen.height + 1), "Layout inside " + size);
                report.AppendLine("Canvas " + Screen.width + "x" + Screen.height + ": " + string.Join(", ", corners.Select(p => p.ToString())));
                yield return Capture("04-size-" + size.x + "x" + size.y);
            }
            yield return Click((RectTransform)menu.Items[2].transform);
            yield return Capture("05-settings");
            var settings = menu.Layout.Find("Settings"); Check(settings != null, "Settings opens");
            if (settings != null) yield return Click((RectTransform)settings.Find("Close"));
            yield return null;
            Check(menu.Layout.Find("Settings") == null, "Settings close click");
            yield return Click((RectTransform)menu.Items[1].transform);
            yield return Capture("06-archive");
            Check(GetComponent<GameHud>().ArchiveOpen, "Archive opens");
            GetComponent<GameHud>().CloseArchive(); yield return new WaitForSecondsRealtime(.2f);
            Check(menu.Visible && menu.Layout.gameObject.activeInHierarchy, "Archive returns to title");
            yield return Click((RectTransform)menu.Items[0].transform);
            Check(!menu.Visible && !GetComponent<NetworkSession>().Online, "Start opens offline lobby, not a joined room");
            yield return Capture("07-lobby");
            menu.Show(); yield return new WaitForSecondsRealtime(.2f);
            Check(menu.Visible, "Lobby returns to title");
            File.WriteAllText(Path.Combine(output, "verification.txt"), report.ToString());
            Debug.Log(report.ToString());
            Application.Quit(report.ToString().Contains("FAIL") ? 1 : 0);
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png"));
            yield return new WaitForSecondsRealtime(.15f);
        }
        IEnumerator Click(RectTransform target)
        {
            var point = RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(target.rect.center));
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = point });
            yield return new WaitForSecondsRealtime(.15f);
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = point }.WithButton(MouseButton.Left));
            yield return new WaitForSecondsRealtime(.15f);
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = point });
            yield return new WaitForSecondsRealtime(.2f);
        }
        void Check(bool value, string message) { report.AppendLine((value ? "PASS " : "FAIL ") + message); }
        void OnDestroy()
        {
            if (testMouse != null && testMouse.added) InputSystem.RemoveDevice(testMouse);
            if (physicalMouse != null && physicalMouse.added) InputSystem.EnableDevice(physicalMouse);
        }
    }
}
#endif
