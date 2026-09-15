using NUnit.Framework;
using UnityEngine;

namespace EarthRecovery.Tests
{
    public sealed class TerminalTests
    {
        [TestCase("01_Hardware/Terminal_Background")]
        [TestCase("03_Map/Map_Grid")]
        [TestCase("04_Controls/Button_Normal")]
        [TestCase("04_Controls/Button_Hover")]
        [TestCase("04_Controls/Button_Pressed")]
        [TestCase("04_Controls/Button_Disabled")]
        [TestCase("05_Icons/Arrow_Up")]
        [TestCase("05_Icons/Arrow_Down")]
        [TestCase("05_Icons/Arrow_Left")]
        [TestCase("05_Icons/Arrow_Right")]
        [TestCase("05_Icons/Rotate")]
        [TestCase("05_Icons/Claw")]
        [TestCase("08_Hibernate/Confirm_Button")]
        public void RequiredAssetExists(string path) => Assert.That(Resources.Load<Texture2D>("BaseCampTerminal/" + path), Is.Not.Null);
        [Test] public void UnknownSampleAndLocationRemainHidden()
        {
            var site = new SiteState { sampleCode = "H-001", discovered = false };
            Assert.That(Catalog.Product(site), Is.EqualTo("표본 H-001 / ???"));
            Assert.That(Catalog.MapName(site), Is.EqualTo("??"));
        }
    }
}
