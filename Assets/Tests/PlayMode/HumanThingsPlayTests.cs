using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace EarthRecovery.Tests
{
    public sealed class HumanThingsPlayTests
    {
        GameRules rules; HumanContent content; Expedition game;
        readonly List<GameObject> objects=new(); readonly List<WorldBehaviorAnchor> anchors=new();
        [SetUp] public void Setup()
        {
            rules=ScriptableObject.CreateInstance<GameRules>();rules.minPlayers=2;
            content=UnityEngine.Object.Instantiate(HumanContent.Load());content.monsters=content.monsters.Select(m=>UnityEngine.Object.Instantiate(m)).ToArray();
            game=new Expedition(rules,content);game.Join(0,"Camp");game.Join(1,"Field");foreach(var p in game.State.players)p.ready=true;Assert.That(game.Start(42));
        }
        [TearDown] public void Cleanup()
        {foreach(var go in objects)UnityEngine.Object.DestroyImmediate(go);objects.Clear();anchors.Clear();foreach(var m in content.monsters)UnityEngine.Object.DestroyImmediate(m);UnityEngine.Object.DestroyImmediate(content);UnityEngine.Object.DestroyImmediate(rules);}
        MonsterBrain Brain() {var go=new GameObject("Test brain");objects.Add(go);return go.AddComponent<MonsterBrain>();}
        WorldBehaviorAnchor Anchor(string tag,Vector3 position)
        {var go=new GameObject(tag);go.transform.position=position;objects.Add(go);var anchor=go.AddComponent<WorldBehaviorAnchor>();anchor.tagId=tag;anchors.Add(anchor);return anchor;}
        void Step(MonsterBrain brain,MonsterState m) {game.Tick(.3f);brain.Tick(game,m,anchors,.3f);}
        SiteState Artifact(int location,int product,MonsterState m)
        {var s=game.State.sites[location];s.product=product;s.phase=CraftPhase.Carried;game.Player(1).carrying=location;game.Player(1).position=m.position+Vector3.right;s.activated=true;return s;}
        [UnityTest] public IEnumerator P07SoundAndRadioReaction()
        {
            var brain=Brain();var m=game.State.monsters[0];m.position=new Vector3(20,0,20);game.Noise(m.position,20);Assert.That(m.alert,Is.EqualTo(AlertState.Investigate));
            Artifact(2,2,m);Step(brain,m);Assert.That(m.alert,Is.EqualTo(AlertState.MemoryBehavior));Assert.That(m.behavior,Is.EqualTo("Listen"));
            Assert.That(game.State.archive.Any(e=>e.observations.Count>0));yield return null;
        }
        [UnityTest] public IEnumerator P08RouteAndRainUmbrellaReaction()
        {
            var brain=Brain();var m=game.State.monsters[2];m.position=new Vector3(20,0,20);m.home=m.position;m.nextMemory=float.MaxValue;
            var home=Anchor("Apartment",m.position);Anchor("Station",m.position+Vector3.right*5);Anchor("Office",m.position+Vector3.forward*5);Anchor("Convenience",m.position-Vector3.right*5);
            Step(brain,m);Assert.That(m.routeIndex,Is.EqualTo(1));Step(brain,m);Assert.That(m.destination,Is.EqualTo(anchors[1].transform.position));
            Artifact(4,2,m);game.State.rainy=true;Step(brain,m);Assert.That(m.behavior,Is.EqualTo("WaitUnderUmbrella"));yield return null;
        }
        [UnityTest] public IEnumerator P09LegacySpeechHasBankCooldownAndSessionCap()
        {
            var brain=Brain();var m=game.State.monsters[3];m.position=new Vector3(20,0,20);m.home=m.position;
            Assert.That(content.monsters[3].legacyClips.Length,Is.EqualTo(3));
            content.monsters[3].memories[0].probability=1;content.monsters[3].memories[0].maxDistance=100;Anchor("HomeAnchor",m.position);
            Step(brain,m);Assert.That(game.State.events.Any(e=>e.kind=="EchoLegacy"));int count=m.memoryCount;
            for(int i=0;i<10;i++)Step(brain,m);Assert.That(m.memoryCount,Is.EqualTo(count));Assert.That(m.memoryCount,Is.LessThanOrEqualTo(3));yield return null;
        }
        [UnityTest] public IEnumerator P10QueuePointsHaveExclusiveOccupancy()
        {
            var first=Brain();var second=Brain();var a=game.State.monsters[5];a.position=new Vector3(20,0,20);a.home=a.position;
            var b=new MonsterState{id=6,definition=5,kind=MonsterKind.Stillness,position=a.position,home=a.home};game.State.monsters.Add(b);
            content.monsters[5].memories[0].probability=1;content.monsters[5].memories[0].maxDistance=100;
            Anchor("QueuePoint",a.position);Anchor("QueuePoint",a.position+Vector3.right*2);Anchor("QueuePoint",a.position+Vector3.right*4);
            Step(first,a);Step(second,b);Assert.That(a.anchor,Is.Not.EqualTo(b.anchor));Assert.That(anchors[a.anchor].occupant,Is.EqualTo(a.id));Assert.That(anchors[b.anchor].occupant,Is.EqualTo(b.id));yield return null;
        }
        [UnityTest] public IEnumerator CameraAndPhotographChangeAIState()
        {
            var light=game.State.monsters[1];light.position=new Vector3(20,0,20);content.monsters[1].reactions[0].probability=1;
            var camera=Artifact(2,1,light);game.Emit("ArtifactActivated",camera.id);Step(Brain(),light);Assert.That(light.behavior,Is.EqualTo("CoverFace"));
            var returning=game.State.monsters[4];returning.position=light.position;Artifact(14,2,returning);Step(Brain(),returning);Assert.That(returning.behavior,Is.EqualTo("LookAtPhotograph"));yield return null;
        }
        [UnityTest] public IEnumerator EveryMonsterCanPerformItsMemoryBehavior()
        {
            foreach(var m in game.State.monsters)
            {
                m.position=new Vector3(20,0,20);m.home=m.position;
                var rule=content.monsters[m.definition].memories[0];rule.probability=1;rule.maxDistance=100;
                Anchor(rule.requiredWorldTag,m.position+Vector3.right*m.id);
                Step(Brain(),m);Assert.That(m.alert,Is.EqualTo(AlertState.MemoryBehavior),content.monsters[m.definition].id);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator P02P03ModuleAuthorityAndHold()
        {
            var s=game.State.sites.First(s=>s.mission);var p=game.Player(1);p.position=game.State.stations[0].position;p.inventory[0]=2;
            Assert.That(game.Apply(1,new Command{action="moduleCraft",target=s.id,x=0}),Is.False);Assert.That(p.inventory[0],Is.EqualTo(2));
            p.inventory[0]=3;Assert.That(game.Apply(1,new Command{action="moduleCraft",target=s.id,x=0}));game.Tick(3.1f);
            var other=game.State.sites.First(o=>o.id!=s.id);p.position=other.position;Assert.That(game.Apply(1,new Command{action="install",target=other.id,flag=true}),Is.False);
            Assert.That(p.modules.Contains(s.id));p.position=s.position;for(int i=0;i<22;i++){game.Apply(1,new Command{action="install",target=s.id,flag=true});game.Tick(.1f);}
            Assert.That(s.facility,Is.EqualTo(FacilityState.Restored));yield return null;
        }
        [UnityTest] public IEnumerator P04RecoveryIsIdempotentAndP05RevealIsStaged()
        {
            var s=game.State.sites.First(s=>s.mission);Assert.That(game.ForClient(1).sites[s.id].displayName,Is.Empty);
            game.Player(1).position=s.position;game.Tick(.1f);Assert.That(s.reveal,Is.EqualTo(RevealLevel.OnLocation));
            s.phase=CraftPhase.Crafting;s.facility=FacilityState.Restored;game.Player(1).viewingSite=s.id;
            for(int i=0;i<3;i++){game.Apply(1,new Command{action="craft",target=s.id});game.Tick(rules.skillCycleSeconds*.65f);game.Apply(1,new Command{action="check",target=s.id});}
            Assert.That(s.revealClues,Is.EqualTo(2));Assert.That(game.ForClient(1).sites[s.id].displayName,Is.Empty);
            s.phase=CraftPhase.Carried;game.Player(1).carrying=s.id;game.Player(1).position=Vector3.zero;game.Tick(.1f);game.Tick(.1f);
            Assert.That(game.State.archive.Single(e=>e.artifactId==content.Artifact(s).id).recoverCount,Is.EqualTo(1));Assert.That(s.reveal,Is.EqualTo(RevealLevel.Recovered));yield return null;
        }
        [UnityTest] public IEnumerator P06StationsAreSeparated()
        {foreach(var a in game.State.stations)foreach(var b in game.State.stations.Where(b=>b!=a))Assert.That(Vector3.Distance(a.position,b.position),Is.GreaterThanOrEqualTo(rules.stationMinDistance));yield return null;}
        sealed class Pollution : IPollutionProvider { public float Risk(ulong player)=>1; }
        [UnityTest] public IEnumerator PollutionProviderControlsM05WithoutInventingFilterRules()
        {
            var m=game.State.monsters[4];m.position=new Vector3(20,0,20);m.home=m.position;m.nextMemory=float.MaxValue;
            game.Player(1).position=m.position+Vector3.right*5;var brain=Brain();Step(brain,m);Assert.That(m.alert,Is.Not.EqualTo(AlertState.Chase));
            game.Pollution=new Pollution();Step(brain,m);Assert.That(m.alert,Is.EqualTo(AlertState.Chase));yield return null;
        }
        [UnityTest] public IEnumerator LightExposureAccumulatesAndStationarySensorDetects()
        {
            var m=game.State.monsters[1];m.position=new Vector3(20,0,20);m.home=m.position;m.nextMemory=float.MaxValue;
            var p=game.Player(1);p.position=m.position+Vector3.right*5;p.yaw=270;p.flashlight=true;var brain=Brain();Step(brain,m);Assert.That(m.alert,Is.EqualTo(AlertState.Suspicious));
            for(int i=0;i<8;i++)Step(brain,m);Assert.That(m.alert,Is.EqualTo(AlertState.Chase));
            var waiting=game.State.monsters[5];waiting.position=m.position;waiting.nextMemory=float.MaxValue;p.stationary=30;Step(Brain(),waiting);Assert.That(waiting.alert,Is.EqualTo(AlertState.Chase));yield return null;
        }
    }
}
