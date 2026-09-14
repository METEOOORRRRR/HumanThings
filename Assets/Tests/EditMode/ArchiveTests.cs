using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
namespace EarthRecovery.Tests
{
    public sealed class ArchiveTests
    {
        string folder;
        [SetUp] public void Setup()=>folder=Path.Combine(Path.GetTempPath(),"HumanThingsTest-"+Guid.NewGuid().ToString("N"));
        [TearDown] public void Cleanup(){if(Directory.Exists(folder))Directory.Delete(folder,true);}
        [Test] public void AtomicArchiveRoundTripAndCorruptPrimaryFallback()
        {
            var path=Path.Combine(folder,"archive.json");var store=new ArchiveStore(path);
            var entries=new List<ArchiveEntry>{new ArchiveEntry{artifactId="HT_A01_01",discovered=true,recoverCount=1,observations=new List<string>{"M01 observation"}}};
            Assert.That(store.Write(entries));Assert.That(store.Load()[0].recoverCount,Is.EqualTo(1));entries[0].recoverCount=2;Assert.That(store.Write(entries));
            File.WriteAllText(path,"broken");Assert.That(store.Load()[0].recoverCount,Is.EqualTo(1));
        }
    }
}
