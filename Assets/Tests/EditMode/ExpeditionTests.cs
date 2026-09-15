using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace EarthRecovery.Tests
{
    public sealed class ExpeditionTests
    {
        GameRules rules; Expedition game;
        [SetUp] public void Setup()
        { rules=ScriptableObject.CreateInstance<GameRules>(); game=new Expedition(rules);for(ulong i=0;i<4;i++){game.Join(i,"Agent"+i);game.Apply(i,new Command{action="ready",flag=true});} }
        [TearDown] public void Cleanup()=>UnityEngine.Object.DestroyImmediate(rules);
        SiteState Mission() {Assert.That(game.Start(42));return game.State.sites.First(s=>s.mission);}
        [Test] public void DataAssetsHaveAllRequiredRelationships()
        {var c=HumanContent.Load();Assert.That(c.zones.Length,Is.EqualTo(5));Assert.That(c.locations.Length,Is.EqualTo(15));Assert.That(c.artifacts.Length,Is.EqualTo(45));Assert.That(c.monsters.Length,Is.EqualTo(6));Assert.DoesNotThrow(c.Validate);}
        [Serializable] sealed class TextSource {public TextRow[] artifacts;}
        [Test] public void FacilityPrefabsContainNoCallBells()
        {
            foreach(var location in HumanContent.Load().locations)
                Assert.That(location.prefab.GetComponentsInChildren<WorldBehaviorAnchor>(true).Any(a=>a.tagId=="CounterBell"),Is.False,location.id);
        }
        [Test] public void RemovedBellCommandCannotProduceNoiseOrEvents()
        {
            Mission();var count=game.State.events.Count;
            var destination=game.State.monsters.Select(m=>m.destination).ToArray();
            foreach(var site in game.State.sites)
            {
                game.Player(1).position=site.position;
                Assert.That(game.Apply(1,new Command{action="bell",target=site.id}),Is.False);
            }
            Assert.That(game.State.events.Count,Is.EqualTo(count));
            CollectionAssert.AreEqual(destination,game.State.monsters.Select(m=>m.destination).ToArray());
        }
        [Serializable] sealed class TextRow {public string id,locationId,trueNameKo,trueNameEn,categoryTag,materialHint,functionHint,archiveDescription,marsComment;}
        [Test] public void All45ContentTextsMatchSourceTablesExactly()
        {
            var source=JsonUtility.FromJson<TextSource>(Resources.Load<TextAsset>("HumanThingsSeed").text);Assert.That(source.artifacts.Length,Is.EqualTo(45));
            foreach(var row in source.artifacts)
            {
                var asset=HumanContent.Load().artifacts.Single(a=>a.id==row.id);
                foreach(var field in typeof(TextRow).GetFields())Assert.That(typeof(ArtifactDefinition).GetField(field.Name).GetValue(asset),Is.EqualTo(field.GetValue(row)),row.id+" "+field.Name);
            }
        }
        [Test] public void DefaultAssetRequiresFourPlayersAndKeepsSpecifiedTimings()
        {Assert.That(Resources.Load<GameRules>("GameRules").minPlayers,Is.EqualTo(4));foreach(var l in HumanContent.Load().locations){Assert.That(l.recipe.scrapCost,Is.EqualTo(3));Assert.That(l.recipe.duration,Is.EqualTo(3));Assert.That(l.recipe.installDuration,Is.EqualTo(2));}}
        [Test] public void FourPlayersRequiredByDefault() {game.Disconnect(3);Assert.That(game.Start(1),Is.False);}
        [Test] public void DeveloperSoloStartsWithOneReadyPlayerWithoutChangingNormalMinimum()
        {
            rules.developerSolo = true;
            for (ulong i = 1; i < 4; i++) game.Disconnect(i);
            rules.Sanitize();
            Assert.That(rules.minPlayers, Is.EqualTo(4));
            Assert.That(rules.MinimumStartPlayers, Is.EqualTo(1));
            Assert.That(game.Start(42), Is.True);
            Assert.That(game.State.players.Count, Is.EqualTo(1));
            Assert.That(game.State.phase, Is.EqualTo(Phase.Expedition));
            Assert.That(game.State.sites.Count(s => s.mission), Is.EqualTo(3));
        }
        [Test] public void DeveloperSoloStillRequiresReadyAndDoesNotStartEmptyLobby()
        {
            rules.developerSolo = true;
            for (ulong i = 1; i < 4; i++) game.Disconnect(i);
            game.Apply(0, new Command { action = "ready", flag = false });
            Assert.That(game.Start(42), Is.False);
            game.Disconnect(0);
            Assert.That(game.Start(42), Is.False);
        }
        [Test] public void DisablingDeveloperSoloRestoresNormalMinimum()
        {
            rules.developerSolo = true;
            for (ulong i = 1; i < 4; i++) game.Disconnect(i);
            rules.developerSolo = false;
            Assert.That(rules.MinimumStartPlayers, Is.EqualTo(4));
            Assert.That(game.Start(42), Is.False);
        }
        [Test] public void CapacityIsSix() {Assert.That(game.Join(4,"5"));Assert.That(game.Join(5,"6"));Assert.That(game.Join(6,"7"),Is.False);}
        [Test] public void UnreadyPlayerBlocksStart() {game.Apply(1,new Command{action="ready",flag=false});Assert.That(game.Start(1),Is.False);}
        [Test] public void LateJoinRejected() {Mission();Assert.That(game.Join(8,"Late"),Is.False);}
        [TestCase(0)] [TestCase(1)] [TestCase(42)] [TestCase(777)] [TestCase(99999)]
        public void SeedReproducesThreeDistinctZoneMissionsAndAllLocations(int seed)
        {
            Assert.That(game.Start(seed));var c=game.Content;
            var first=game.State.sites.Select(s=>(s.id,s.position,s.quarterTurn,s.mission,s.product)).ToArray();
            var stations=game.State.stations.Select(s=>s.position).ToArray();
            Assert.That(first.Length,Is.EqualTo(15));var missions=game.State.sites.Where(s=>s.mission).ToArray();
            Assert.That(missions.Select(s=>c.locations[s.id].zone).Distinct().Count(),Is.EqualTo(3));
            Assert.That(game.State.loot.All(s=>s.material==0));Assert.That(game.State.stations.Count,Is.EqualTo(3));
            foreach(var a in game.State.stations) foreach(var b in game.State.stations.Where(s=>s!=a)) Assert.That(Vector3.Distance(a.position,b.position),Is.GreaterThanOrEqualTo(rules.stationMinDistance));
            game.ResetLobby();foreach(var p in game.State.players)p.ready=true;game.Start(seed);
            CollectionAssert.AreEqual(first,game.State.sites.Select(s=>(s.id,s.position,s.quarterTurn,s.mission,s.product)).ToArray());
            CollectionAssert.AreEqual(stations,game.State.stations.Select(s=>s.position).ToArray());
        }
        [Test] public void ScrapPickupChecksRangeCapacityAndDuplicates()
        {
            Mission();var p=game.Player(1);var l=game.State.loot[0];Assert.That(game.Apply(1,new Command{action="loot",target=0}),Is.False);
            p.position=l.position;Assert.That(game.Apply(1,new Command{action="loot",target=0}));Assert.That(game.Apply(1,new Command{action="loot",target=0}),Is.False);
            p.position=game.State.loot[1].position;p.inventory[0]=rules.resourceCapacity;Assert.That(game.Apply(1,new Command{action="loot",target=1}),Is.False);
        }
        [Test] public void P02InsufficientScrapDoesNotChangeInventory()
        {var s=Mission();var p=game.Player(1);p.position=game.State.stations[0].position;p.inventory[0]=2;Assert.That(game.Apply(1,new Command{action="moduleCraft",target=s.id,x=0}),Is.False);Assert.That(p.inventory[0],Is.EqualTo(2));}
        void Module(SiteState s)
        {
            var p=game.Player(1);p.position=game.State.stations[0].position;
            for(int i=0;i<3;i++){var l=game.State.loot.First(l=>!l.collected);l.position=p.position;Assert.That(game.Apply(1,new Command{action="loot",target=l.id}));}
            Assert.That(game.Apply(1,new Command{action="moduleCraft",target=s.id,x=0}));game.Tick(3.01f);Assert.That(p.modules.Contains(s.id));
        }
        void Install(SiteState s)
        {var p=game.Player(1);p.position=s.position;for(int i=0;i<22;i++){Assert.That(game.Apply(1,new Command{action="install",target=s.id,flag=true})||s.facility!=FacilityState.ModuleRequired);game.Tick(.1f);}Assert.That(s.facility,Is.EqualTo(FacilityState.Restored));}
        [Test] public void P03WrongModuleIsPreserved() {var s=Mission();Module(s);var other=game.State.sites.First(x=>x.id!=s.id);game.Player(1).position=other.position;Assert.That(game.Apply(1,new Command{action="install",target=other.id,flag=true}),Is.False);Assert.That(game.Player(1).modules.Contains(s.id));}
        [TestCase(.4f,3)] [TestCase(.5f,0)] [TestCase(.8f,0)]
        public void ModuleCancellationHonorsRefundBoundary(float fraction,int refund)
        {var s=Mission();var p=game.Player(1);p.position=game.State.stations[0].position;p.inventory[0]=3;game.Apply(1,new Command{action="moduleCraft",target=s.id,x=0});game.Tick(3*fraction);Assert.That(game.Apply(1,new Command{action="moduleCancel"}));Assert.That(p.inventory[0],Is.EqualTo(refund));}
        [Test] public void ModuleDuplicatedAcrossStationsIsRejected()
        {var s=Mission();var p=game.Player(1);p.position=game.State.stations[0].position;p.inventory[0]=3;game.Apply(1,new Command{action="moduleCraft",target=s.id,x=0});var q=game.Player(2);q.position=game.State.stations[1].position;q.inventory[0]=3;Assert.That(game.Apply(2,new Command{action="moduleCraft",target=s.id,x=1}),Is.False);}
        [Test] public void InstallationRequiresContinuousHold()
        {var s=Mission();Module(s);game.Player(1).position=s.position;game.Apply(1,new Command{action="install",target=s.id,flag=true});game.Tick(2.1f);Assert.That(s.facility,Is.EqualTo(FacilityState.ModuleRequired));}
        [Test] public void P05RevealAtLocation33And66Percent()
        {
            var s=Mission();Assert.That(game.ForClient(1).sites[s.id].displayName,Is.Empty);Assert.That(game.ForClient(1).sites[s.id].discovered,Is.False);
            Module(s);Install(s);var p=game.Player(1);p.viewingSite=s.id;Assert.That(s.reveal,Is.EqualTo(RevealLevel.OnLocation));
            for(int i=0;i<3;i++){game.Apply(1,new Command{action="craft",target=s.id});game.Tick(rules.skillCycleSeconds*.65f);game.Apply(1,new Command{action="check",target=s.id});}
            Assert.That(s.revealClues,Is.EqualTo(2));Assert.That(s.reveal,Is.EqualTo(RevealLevel.DuringCraft));Assert.That(game.ForClient(1).sites[s.id].displayName,Is.Empty);
        }
        [Test] public void PriorArchiveKnowledgeNeverRevealsUnrecoveredMissionName()
        {var s=Mission();game.State.archive.Add(new ArchiveEntry{artifactId=game.Content.Artifact(s).id,discovered=true});Assert.That(game.ForClient(0).sites[s.id].displayName,Is.Empty);rules.retainArchiveKnowledge=true;Assert.That(game.ForClient(0).sites[s.id].displayName,Is.Empty);}
        [Test] public void EveryFacilityModuleCanBeCraftedAndInstalledWithoutMissionAssignment()
        {
            Mission();
            foreach(var s in game.State.sites)
            {
                Module(s);Install(s);
                if(!s.mission) Assert.That(game.Apply(1,new Command{action="craft",target=s.id}),Is.False);
            }
        }
        [Test] public void EveryConsoleCanBeViewedWithoutModuleButOnlyWhenNearIt()
        {
            Mission();var p=game.Player(1);
            foreach(var s in game.State.sites)
            {
                p.position=s.position; Assert.That(game.Apply(1,new Command{action="view",target=s.id}));
                Assert.That(p.modules,Is.Empty); Assert.That(game.Apply(1,new Command{action="install",target=s.id,flag=true}),Is.False);
                p.position=s.position+Quaternion.Euler(0,s.quarterTurn*90,0)*new Vector3(0,0,-4);
                Assert.That(game.Apply(1,new Command{action="view",target=s.id}),Is.False);
            }
        }
        [Test] public void CampMissionDetailsContainNoFacilityOrZoneNamesAndMapHidesUnknownNames()
        {
            Mission();var view=game.ForClient(0);
            Assert.That(view.sites.Count,Is.EqualTo(15));
            foreach(var s in view.sites)
            {
                Assert.That(Catalog.MapName(s),Is.EqualTo("??"));
                var details=Catalog.MissionDetails(s);
                Assert.That(details,Does.Not.Contain(game.Content.locations[s.id].displayNameKnown));
                Assert.That(details,Does.Not.Contain(game.Content.zones[game.Content.locations[s.id].zone].displayName));
                s.discovered=true;Assert.That(Catalog.MapName(s),Is.EqualTo(Catalog.Sites[s.id]));
            }
        }
        [Test] public void LocationNamesAreUniqueSingleWordsAndRemainInsideAssignedZoneSockets()
        {
            Mission();var content=game.Content;
            CollectionAssert.AreEqual(new[]{"악기점","식당","전자상가","문방구","의류점","편의점","공구점","정비소","인쇄소","병원","실험실","관측소","도서관","학교","박물관"},Catalog.Sites);
            foreach(var s in game.State.sites)
            {
                var zone=content.zones[content.locations[s.id].zone];
                Assert.That(zone.spawnSockets.Any(p=>Vector3.Distance(zone.center+p,s.position)<.01f));
            }
        }
        [Test] public void AssignmentHasRoleSpecificInformation()
        {var s=Mission();game.Player(1).position=new Vector3(60,0,60);Assert.That(game.ForClient(0).sites[s.id].facilityHint,Is.Not.EqualTo(game.ForClient(1).sites[s.id].facilityHint));}
        [Test] public void FailedCheckEmitsNoiseAndReleasesLease()
        {var s=Mission();Module(s);Install(s);game.Player(1).viewingSite=s.id;game.Apply(1,new Command{action="craft",target=s.id});game.Apply(1,new Command{action="check",target=s.id});Assert.That(s.worker,Is.EqualTo(ulong.MaxValue));Assert.That(game.State.events.Any(e=>e.kind=="CraftFailed"));}
        [Test] public void CraftLeaseReleasesOnExit()
        {var s=Mission();Module(s);Install(s);game.Player(1).viewingSite=s.id;game.Apply(1,new Command{action="craft",target=s.id});game.Player(1).position=Vector3.zero;game.Tick(.1f);Assert.That(s.worker,Is.EqualTo(ulong.MaxValue));}
        [Test] public void P01FullMissionLoopAndP04DuplicateRecovery()
        {
            Mission();var p=game.Player(1);
            foreach(var s in game.State.sites.Where(s=>s.mission))
            {
                Module(s);Install(s);p.viewingSite=s.id;
                for(int i=0;i<rules.checksRequired;i++){Assert.That(game.Apply(1,new Command{action="craft",target=s.id}));game.Tick(rules.skillCycleSeconds*.65f);Assert.That(game.Apply(1,new Command{action="check",target=s.id}));}
                Solve(game,s);Assert.That(s.phase,Is.EqualTo(CraftPhase.Ready));Assert.That(game.State.archive.Any(e=>e.artifactId==game.Content.Artifact(s).id&&e.discovered),Is.False);
                p.position=game.ObjectPosition(s);Assert.That(game.Apply(1,new Command{action="pickup",target=s.id}));p.position=Vector3.zero;game.Tick(.1f);game.Tick(.1f);
                Assert.That(s.missionState,Is.EqualTo(MissionState.Recovered));Assert.That(game.State.archive.Single(e=>e.artifactId==game.Content.Artifact(s).id).recoverCount,Is.EqualTo(1));
            }
            Assert.That(game.State.phase,Is.EqualTo(Phase.Success));
        }
        public static void Solve(Expedition g,SiteState s)
        {
            var path=new List<Vector2Int>();
            if(s.puzzleKind==PuzzleKind.Maze)
            {
                var queue=new Queue<int>();var prev=new Dictionary<int,int>{{0,-1}};queue.Enqueue(0);
                while(queue.Count>0){int at=queue.Dequeue();if(at==35)break;foreach(var d in new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right}){int x=at%6+d.x,y=at/6+d.y,n=y*6+x;if(x<0||x>5||y<0||y>5||s.maze[n]==1||prev.ContainsKey(n))continue;prev[n]=at;queue.Enqueue(n);}}
                int current=35;while(prev[current]>=0){int before=prev[current];path.Add(new Vector2Int(current%6-before%6,current/6-before/6));current=before;}path.Reverse();
            }
            else {for(int i=0;i<s.targetX;i++)path.Add(Vector2Int.right);for(int i=0;i<s.targetY;i++)path.Add(Vector2Int.up);}
            foreach(var d in path){g.Tick(.2f);g.Apply(0,new Command{action="puzzle",target=s.id,x=d.x,y=d.y});}
            for(int i=0;s.phase==CraftPhase.Puzzle&&i<4;i++){g.Tick(.2f);g.Apply(0,new Command{action="puzzle",target=s.id,flag=true});}
        }
        [Test] public void DeadPlayersCannotMoveAndSoloCanHibernate()
        {Mission();game.Infect(1);game.Infect(2);game.Infect(3);Assert.That(game.Apply(1,new Command{action="move",move=Vector2.one}),Is.False);Assert.That(game.Apply(0,new Command{action="hibernate"}));}
        [Test] public void OxygenAndAllDeadTerminate() {Mission();game.Tick(2000);Assert.That(game.State.phase,Is.EqualTo(Phase.OxygenLost));}
        [Test] public void SnapshotCompressionRoundTrip() {Mission();var json=JsonUtility.ToJson(game.ForClient(0));Assert.That(SnapshotCodec.Decode(SnapshotCodec.Encode(json)),Is.EqualTo(json));Assert.That(SnapshotValidation.Valid(game.ForClient(0),0));}
    }
}
