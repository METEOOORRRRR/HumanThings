using NUnit.Framework;
using UnityEngine;

namespace EarthRecovery.Tests
{
    public sealed class RuntimeCityLayoutTests
    {
        [Test]
        public void RuntimeAssetsAreIncludedAndPointToValidatedTemplate()
        {
            var settings = RuntimeCitySettings.Load();
            Assert.That(settings.template.templateName, Is.EqualTo("StandardCity"));
            Assert.That(settings.template.specialPOIDatabase.definitions.Count, Is.EqualTo(15));
            Assert.That(settings.database.entries.Count, Is.GreaterThan(0));
        }
        [TestCase(float.NaN, 600)][TestCase(600, float.PositiveInfinity)][TestCase(0, 600)][TestCase(600, 3000)]
        public void InvalidMapDimensionsAreRejected(float x, float z)
        {
            var state = new Snapshot { mapSize = new Vector2(x, z) };
            Assert.That(SnapshotValidation.Valid(state, 0), Is.False);
        }
        [Test]
        public void LegacyLobbyBoundsRemainUnchanged()
        {
            Assert.That(new Snapshot().mapSize, Is.EqualTo(new Vector2(150, 150)));
        }
    }
}
