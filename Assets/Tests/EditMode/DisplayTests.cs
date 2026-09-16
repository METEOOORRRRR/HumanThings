using System.Collections;
using NUnit.Framework;
using UnityEngine;

namespace EarthRecovery.Tests
{
    public sealed class DisplayTests
    {
        public static IEnumerable Cases()
        {
            foreach(var size in new[] {new Vector2(800,600),new Vector2(1280,720),new Vector2(1280,1024),new Vector2(1600,900),new Vector2(1920,1080),new Vector2(1920,1200),new Vector2(2560,1440),new Vector2(2560,1080),new Vector2(3440,1440),new Vector2(3840,2160),new Vector2(3840,1600),new Vector2(5120,1440),new Vector2(5120,1600),new Vector2(7680,2160)})
                for(int i=0;i<DisplayPreferences.Ratios.Length;i++)yield return new TestCaseData(size.x,size.y,i);
        }
        [TestCaseSource(nameof(Cases))]
        public void EveryAspectFitsAndPreservesBothUiCoordinateSystems(float w,float h,int index)
        {
            float ratio=DisplayPreferences.Ratios[index]; var r=DisplayPreferences.FitViewport(w,h,ratio);
            Assert.That(r.xMin,Is.GreaterThanOrEqualTo(-.001f)); Assert.That(r.yMin,Is.GreaterThanOrEqualTo(-.001f));
            Assert.That(r.xMax,Is.LessThanOrEqualTo(w+.001f)); Assert.That(r.yMax,Is.LessThanOrEqualTo(h+.001f));
            Assert.That(r.center.x,Is.EqualTo(w/2).Within(.5f)); Assert.That(r.center.y,Is.EqualTo(h/2).Within(.5f));
            float requested = ratio == 0 ? w/h : ratio;
            Assert.That(Mathf.Abs(r.width - r.height * requested), Is.LessThanOrEqualTo(Mathf.Max(1, requested)));
            Assert.That(r.x, Is.EqualTo(Mathf.Floor(r.x))); Assert.That(r.y, Is.EqualTo(Mathf.Floor(r.y)));
            foreach(var design in new[]{new Vector2(1280,720),new Vector2(1672,941)})
            {
                float scale=Mathf.Min(r.width/design.x,r.height/design.y);
                Assert.That(design.x*scale,Is.LessThanOrEqualTo(r.width+.001f));
                Assert.That(design.y*scale,Is.LessThanOrEqualTo(r.height+.001f));
                Assert.That((design.x*scale)/(design.y*scale),Is.EqualTo(design.x/design.y).Within(.00001f));
            }
        }
        [Test] public void InvalidPreferenceIsClamped()
        {
            int saved=DisplayPreferences.Selected;
            try { DisplayPreferences.Select(-100,false); Assert.That(DisplayPreferences.Selected,Is.Zero); DisplayPreferences.Select(100,false); Assert.That(DisplayPreferences.Selected,Is.EqualTo(DisplayPreferences.Ratios.Length-1)); }
            finally { DisplayPreferences.Select(saved,false); }
        }
        [Test] public void InvalidRatioFallsBackToFullViewport() => Assert.That(DisplayPreferences.FitViewport(1920,1080,float.NaN),Is.EqualTo(new Rect(0,0,1920,1080)));
        [TestCase(18, 1.5f, 27)]
        [TestCase(18, 3, 54)]
        [TestCase(18, .625f, 11)]
        public void HudGlyphsUseOutputPixels(int size, float scale, int expected) => Assert.That(PixelGui.FontPixels(size, scale), Is.EqualTo(expected));

        [Test] public void CanvasOwnsTextScaleInsteadOfStretchingItsChildren()
        {
            var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            var layout = new GameObject("Layout", typeof(RectTransform)).GetComponent<RectTransform>(); layout.SetParent(go.transform, false);
            try
            {
                DisplayPreferences.FitCanvas(layout, (RectTransform)go.transform);
                Assert.That(layout.localScale, Is.EqualTo(Vector3.one));
                Assert.That(go.GetComponent<UnityEngine.UI.CanvasScaler>().uiScaleMode, Is.EqualTo(UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize));
                Assert.That(go.GetComponent<Canvas>().scaleFactor, Is.EqualTo(Mathf.Min(DisplayPreferences.Viewport.width/1672, DisplayPreferences.Viewport.height/941)).Within(.001f));
            }
            finally { Object.DestroyImmediate(go); }
        }
        [Test] public void LargeUiImagesHaveMinificationFiltering()
        {
            foreach (string path in new[] { "MainMenu/MainMenu_BG_Clean_Base", "MainMenu/Logo_HumanThings2", "WaitingRoom/01_Background/Background_Artwork", "BaseCampTerminal/01_Hardware/Terminal_Background" })
            {
                var texture = Resources.Load<Texture2D>(path);
                Assert.That(texture, Is.Not.Null, path);
                Assert.That(texture.mipmapCount, Is.GreaterThan(1), path);
                Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Trilinear), path);
            }
        }
        [TestCase(1, 256, 320)]
        [TestCase(2.3f, 416, 520)]
        [TestCase(4.6f, 832, 1040)]
        public void PortraitResolutionTracksUiPixels(float scale, int width, int height) => Assert.That(WaitingRoom.PortraitResolution(scale), Is.EqualTo(new Vector2Int(width, height)));
    }
}
