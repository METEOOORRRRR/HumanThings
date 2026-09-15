using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
namespace EarthRecovery
{
    public sealed class MonsterBrain : MonoBehaviour
    {
        System.Random random;
        int lastEvent;
        float nextMemoryCheck;
        float navigationCheck, progressAt, destinationUntil, routeRetryAt;
        Vector3 progressPosition, requestedDestination;
        bool navigationStarted, hasRequest;
        NavMeshPath patrolPath;
        public void Navigate(Expedition game, MonsterState m, NavMeshAgent agent)
        {
            if (!agent.enabled || game.State.elapsed < navigationCheck) return;
            navigationCheck = game.State.elapsed + .25f;
            if (!agent.isOnNavMesh)
            {
                if (!NavMesh.SamplePosition(m.position, out var recovery, 12, agent.areaMask)) return;
                agent.Warp(recovery.position); m.position = recovery.position; hasRequest = false;
            }
            if (!navigationStarted)
            { navigationStarted = true; progressPosition = m.position; progressAt = game.State.elapsed; }
            if (Vector3.Distance(progressPosition, m.position) > .4f)
            { progressPosition = m.position; progressAt = game.State.elapsed; }
            bool resting = m.alert == AlertState.MemoryBehavior && Vector3.Distance(m.position,m.destination) < 1.2f;
            agent.isStopped = resting;
            if (resting) { progressAt = game.State.elapsed; return; }
            bool free = m.alert == AlertState.Idle || m.alert == AlertState.Return;
            bool blocked = !NavMesh.SamplePosition(WorldGeometry.OutsideSafety(game.State,m.destination),out _,4,agent.areaMask)
                || (!agent.pathPending && hasRequest && agent.pathStatus != NavMeshPathStatus.PathComplete);
            bool stuck = game.State.elapsed - progressAt > 3;
            bool arrived = Vector3.Distance(m.position, m.destination) < 1.5f;
            bool hasRoute = m.kind == MonsterKind.Patrol && game.Content.monsters[m.definition].routeTags.Length > 0;
            if (free && (blocked || stuck || (m.alert == AlertState.Idle && (!hasRoute && (arrived || game.State.elapsed >= destinationUntil))) || (m.alert == AlertState.Return && arrived)))
            {
                if (ChoosePatrol(game, m, agent))
                {
                    m.alert = AlertState.Idle; progressAt = game.State.elapsed; destinationUntil = game.State.elapsed + 15;
                    if (hasRoute) { m.routeIndex++; routeRetryAt = game.State.elapsed + 8; }
                }
            }
            var destination = WorldGeometry.OutsideSafety(game.State, m.destination);
            if (NavMesh.SamplePosition(destination, out var hit, 4, agent.areaMask)
                && (!hasRequest || Vector3.Distance(requestedDestination,hit.position) > .4f || (!agent.hasPath && !agent.pathPending)))
            { hasRequest = agent.SetDestination(hit.position); requestedDestination = hit.position; }
        }
        bool ChoosePatrol(Expedition game, MonsterState m, NavMeshAgent agent)
        {
            patrolPath ??= new NavMeshPath();
            random ??= new System.Random(game.State.seed ^ (m.id * 4001 + 1213));
            for (int attempt=0;attempt<24;attempt++)
            {
                float angle=(float)random.NextDouble()*Mathf.PI*2, distance=8+(float)random.NextDouble()*18;
                var candidate=m.position+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*distance;
                var half = game.State.mapSize * .5f - Vector2.one * 6;
                candidate.x=Mathf.Clamp(candidate.x,-half.x,half.x); candidate.z=Mathf.Clamp(candidate.z,-half.y,half.y);
                if (!NavMesh.SamplePosition(candidate,out var hit,4,agent.areaMask) || WorldGeometry.Safe(game.State,hit.position)
                    || Vector3.Distance(hit.position,m.position)<5) continue;
                if (agent.CalculatePath(hit.position,patrolPath) && patrolPath.status==NavMeshPathStatus.PathComplete)
                { m.destination=hit.position; return true; }
            }
            return false;
        }
        public void Tick(Expedition game, MonsterState m, IReadOnlyList<WorldBehaviorAnchor> anchors, float dt)
        {
            var state = game.State; var def = game.Content.monsters[m.definition]; random ??= new System.Random(state.seed ^ (m.id * 4001 + 1213));
            if (state.elapsed < m.nextPerception) return;
            float step = Mathf.Max(.02f, def.perceptionTick); m.nextPerception = state.elapsed + step;
            var signals = state.events.Where(e => e.sequence > lastEvent).ToArray();
            if (state.events.Count > 0) lastEvent = state.events.Last().sequence;
            foreach (var s in state.sites.Where(s => s.phase == CraftPhase.Ready || s.phase == CraftPhase.Carried))
            {
                var a = game.Content.Artifact(s);
                foreach (var r in def.reactions.Where(r => r.artifactId == a.id))
                {
                    if (r.once && m.usedReactions.Contains(r.observationId) || Vector3.Distance(m.position, game.ObjectPosition(s)) > r.radius
                        || r.contextTags.Contains("Rain") && !state.rainy || m.alert == AlertState.Chase && !r.overrideChase) continue;
                    bool triggered = r.trigger switch
                    {
                        ReactionTrigger.Flash => signals.Any(e => e.kind == "ArtifactActivated" && e.target == s.id),
                        ReactionTrigger.Activated or ReactionTrigger.Heard => s.activated,
                        ReactionTrigger.Equipped => s.activated && s.phase == CraftPhase.Carried,
                        _ => true
                    };
                    if (!triggered) continue;
                    if (r.once) m.usedReactions.Add(r.observationId);
                    if (random.NextDouble() > r.probability)
                    {
                        if (r.trigger == ReactionTrigger.Flash) { m.alert = AlertState.Chase; m.aggroUntil = state.elapsed + def.investigateTimeout; m.destination = game.ObjectPosition(s); }
                        continue;
                    }
                    Memory(game, m, def, r.behavior, r.duration, game.ObjectPosition(s));
                    m.protectedMemory=true;
                    if (state.players.Any(p => p.alive && p.connected && Vector3.Distance(p.position, m.position) <= game.Rules.observationDistance))
                        game.Observe(a.id, r.observationId + ": " + def.id + " / " + r.behavior + " / " + r.duration.ToString("F1") + "s");
                    return;
                }
            }
            if (m.alert == AlertState.MemoryBehavior)
            {
                bool interrupted=!m.protectedMemory&&state.players.Any(p=>p.alive&&p.connected&&!WorldGeometry.Safe(state,p.position)&&Vector3.Distance(p.position,m.position)<2);
                if (state.elapsed < m.stateUntil && !interrupted) return;
                if (m.anchor >= 0 && m.anchor < anchors.Count && anchors[m.anchor].occupant == m.id) { anchors[m.anchor].occupant = -1; anchors[m.anchor].cooldownUntil = state.elapsed + def.memoryGlobalCooldown; }
                m.anchor = -1; m.alert = AlertState.Return; m.destination = m.home;
            }
            PlayerState target = null;
            float nearest = float.MaxValue;
            foreach (var p in state.players.Where(p => p.alive && p.connected && !WorldGeometry.Safe(state, p.position)))
            {
                float d = Vector3.Distance(p.position, m.position), risk = Mathf.Clamp01(game.Pollution.Risk(p.id));
                if (d > def.senseRange * (m.kind == MonsterKind.Pollution ? 1 + risk : 1)) continue;
                bool seen = !Physics.Linecast(m.position + Vector3.up, p.position + Vector3.up, 1 << 8);
                bool sensed = m.kind switch
                {
                    MonsterKind.Sound => d < 3 || m.aggroUntil > state.elapsed && seen && d < 12,
                    MonsterKind.Light => p.flashlight && seen && Vector3.Dot(Quaternion.Euler(0,p.yaw,0)*Vector3.forward,(m.position-p.position).normalized) > .65f,
                    MonsterKind.Patrol => d < 4 && seen,
                    MonsterKind.Echo => signals.Any(e => e.kind == "Communication" && Vector3.Distance(e.position,m.position) < def.senseRange),
                    MonsterKind.Pollution => risk > 0 && seen,
                    MonsterKind.Stillness => p.stationary >= game.Rules.stillnessSeconds,
                    _ => false
                };
                if (sensed && d < nearest) { target = p; nearest = d; }
            }
            if (target != null)
            {
                m.lastSeen = state.elapsed; m.target = target.id; m.destination = target.position;
                m.lightExposure = m.kind == MonsterKind.Light ? m.lightExposure + step : def.lightChase;
                m.alert = m.lightExposure >= def.lightChase ? AlertState.Chase : AlertState.Suspicious;
                m.aggroUntil = state.elapsed + def.lostTargetGrace + (m.kind == MonsterKind.Pollution ? Mathf.Clamp01(game.Pollution.Risk(target.id)) * 10 : 0);
            }
            else
            {
                m.lightExposure = Mathf.Max(0, m.lightExposure - step);
                if (m.alert == AlertState.Chase && state.elapsed > m.aggroUntil) { m.alert = AlertState.Return; m.target = ulong.MaxValue; m.destination = m.home; }
                else if (m.alert == AlertState.Suspicious) { m.alert = AlertState.Investigate; m.stateUntil = state.elapsed + def.investigateTimeout; }
                else if (m.alert == AlertState.Investigate && state.elapsed > Mathf.Max(m.stateUntil,m.aggroUntil)) { m.alert = AlertState.Return; m.destination = m.home; }
                else if (m.alert == AlertState.Return && Vector3.Distance(m.position,m.home) < 2) m.alert = AlertState.Idle;
            }
            if (m.kind == MonsterKind.Echo && signals.Any(e => e.kind == "Communication" && Vector3.Distance(e.position,m.position) < def.senseRange) && state.elapsed > m.nextMemory)
            { game.Emit("EchoMimic",m.id,position:m.position); m.nextMemory = state.elapsed + def.memoryGlobalCooldown; }
            if ((m.alert == AlertState.Chase || m.alert == AlertState.Suspicious) && m.kind!=MonsterKind.Pollution) return;
            if (m.kind == MonsterKind.Patrol && m.alert == AlertState.Idle && def.routeTags.Length > 0 && state.elapsed >= routeRetryAt)
            {
                string tag = def.routeTags[m.routeIndex % def.routeTags.Length];
                var destination = anchors.Where(a => a.tagId == tag).OrderBy(a => Vector3.Distance(a.transform.position,m.position)).FirstOrDefault();
                if (destination != null) { m.destination = destination.transform.position; if (Vector3.Distance(m.position,m.destination) < 2) m.routeIndex++; }
            }
            if (state.elapsed < nextMemoryCheck || state.elapsed < m.nextMemory || m.memoryCount >= def.memorySessionMax) return;
            nextMemoryCheck = state.elapsed + def.memoryCheckInterval;
            if (m.kind == MonsterKind.Echo && def.legacyLines.Length > 0 && random.NextDouble() < .3)
            { Memory(game,m,def,"LegacyVoice",6,m.position); return; }
            foreach (var rule in def.memories)
            {
                if (m.memoryCount >= rule.sessionCap || random.NextDouble() > rule.probability) continue;
                var anchor = anchors.Select((a,i)=>(a,i)).Where(x => x.a.tagId == rule.requiredWorldTag && x.a.Allows(def.id) && x.a.occupant < 0 && x.a.cooldownUntil <= state.elapsed
                    && Vector3.Distance(x.a.transform.position,m.position) <= rule.maxDistance
                    && state.players.All(p => !p.alive || Vector3.Distance(p.position,x.a.transform.position) >= rule.minDistance))
                    .OrderBy(x => Vector3.Distance(x.a.transform.position,m.position)).FirstOrDefault();
                if (anchor.a == null) continue;
                anchor.a.occupant = m.id; m.anchor = anchor.i; Memory(game,m,def,rule.animationId,rule.duration,anchor.a.transform.position);m.nextMemory=Mathf.Max(m.nextMemory,state.elapsed+rule.cooldown);return;
            }
        }
        static void Memory(Expedition game, MonsterState m, MonsterDefinition def, string behavior, float duration, Vector3 at)
        {
            m.alert = AlertState.MemoryBehavior; m.behavior = behavior; m.stateUntil = game.State.elapsed + duration;
            m.protectedMemory=false;
            m.nextMemory = game.State.elapsed + def.memoryGlobalCooldown; m.memoryCount++; m.destination = WorldGeometry.OutsideSafety(game.State,at);
            game.Emit("MonsterMemoryBehaviorStarted",m.id,behavior,m.position);
            if (behavior == "LegacyVoice" || m.kind == MonsterKind.Echo) game.Emit("EchoLegacy",m.id,position:m.position);
        }
    }
}
