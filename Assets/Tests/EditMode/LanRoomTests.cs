using NUnit.Framework;
namespace EarthRecovery.Tests
{
    public sealed class LanRoomTests
    {
        const string Id = "1234567890abcdef1234567890abcdef";
        [Test] public void CodeRoundTripsEndpointAndRoomTag()
        {
            string code = LanRooms.EncodeCode("192.168.0.12", 7780, Id);
            Assert.That(code.Length, Is.EqualTo(16));
            Assert.That(LanRooms.DecodeCode(ExpeditionLobby.FormatCode(code), out var address, out var port, out var tag));
            Assert.That(address, Is.EqualTo("192.168.0.12")); Assert.That(port, Is.EqualTo(7780)); Assert.That(tag, Is.EqualTo("1234"));
        }
        [TestCase("")][TestCase("1234")][TestCase("GGGGGGGGGGGGGGGG")][TestCase("FFFFFFFF1E611234")][TestCase("7F00000100001234")]
        public void InvalidCodesAreRejected(string code) => Assert.That(LanRooms.DecodeCode(code, out _, out _, out _), Is.False);
        [Test] public void PasswordProofIsRoomSpecific()
        {
            Assert.That(LanRooms.PasswordProof(Id, "123456"), Is.Not.EqualTo(LanRooms.PasswordProof(Id, "654321")));
            Assert.That(LanRooms.PasswordProof(Id, "123456"), Is.Not.EqualTo(LanRooms.PasswordProof("abcdef1234567890abcdef1234567890", "123456")));
        }
        [Test] public void DiscoveryRejectsInvalidMetadata()
        {
            var room = new LanRoom { id = Id, name = "방", port = 7777, players = 1, capacity = 4 };
            Assert.That(LanRooms.ValidRoom(room)); room.players = 7; Assert.That(LanRooms.ValidRoom(room), Is.False);
            room.players = 1; room.name = new string('x', 21); Assert.That(LanRooms.ValidRoom(room), Is.False);
        }
    }
}
