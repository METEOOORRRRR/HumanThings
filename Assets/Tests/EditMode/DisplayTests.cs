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
            Assert.That(r.center.x,Is.EqualTo(w/2).Within(.001f)); Assert.That(r.center.y,Is.EqualTo(h/2).Within(.001f));
            Assert.That(r.width/r.height,Is.EqualTo(ratio==0?w/h:ratio).Within(.00001f));
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
    }
}
