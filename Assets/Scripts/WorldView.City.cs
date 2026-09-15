using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace EarthRecovery
{
    public sealed partial class WorldView
    {
        public GeneratedWorldResult City { get; private set; }
        void BuildCity(Snapshot state)
        {
            var plan = session.IsHost ? session.CityPlan : RuntimeCitySettings.Load().Generate(state.seed);
            if (plan == null || plan.seed != state.seed || Mathf.Abs(plan.bounds.size.x - state.mapSize.x) > .01f
                || Mathf.Abs(plan.bounds.size.z - state.mapSize.y) > .01f)
                throw new InvalidOperationException("City seed or dimensions mismatch.");
            foreach (var s in state.sites)
            {
                var p = plan.specialPOIs.Single(p => p.definition.id == HumanContent.Load().locations[s.id].id);
                if (Vector3.Distance(p.position, s.position) > .01f || Mathf.Abs(Mathf.DeltaAngle(p.rotation.eulerAngles.y, s.quarterTurn * 90)) > .01f)
                    throw new InvalidOperationException("City facility transform mismatch.");
            }
            City = CityWorldGenerator.Build(plan, root.gameObject.scene);
            City.root.transform.SetParent(root, true);
            // The preview shelter is solid. The playable camp and its interaction geometry are created by Building().
            City.baseCampInstance.SetActive(false);
            foreach (var t in City.root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 8;
            foreach (var mesh in City.root.GetComponentsInChildren<MeshFilter>())
            {
                if (mesh.sharedMesh == null || mesh.GetComponent<Collider>() != null || mesh.GetComponent<TMPro.TMP_Text>() != null) continue;
                if (mesh.sharedMesh.isReadable) mesh.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
                else AddBox(mesh.gameObject, mesh.sharedMesh.bounds);
            }
            foreach (var collider in City.root.GetComponentsInChildren<MeshCollider>())
                if (collider.sharedMesh != null && !collider.sharedMesh.isReadable)
                {
                    AddBox(collider.gameObject, collider.sharedMesh.bounds);
                    collider.enabled = false; Destroy(collider);
                }
            eye.farClipPlane = Mathf.Max(180, state.mapSize.magnitude);
        }
        static void AddBox(GameObject target, Bounds bounds)
        {
            var box = target.AddComponent<BoxCollider>(); box.center = bounds.center; box.size = bounds.size;
        }

        void ValidateCitySpawns(Snapshot state)
        {
            Vector3 Walkable(Vector3 at, float radius)
            {
                if (!NavMesh.SamplePosition(at, out var hit, radius, NavMesh.AllAreas) || WorldGeometry.Safe(state, hit.position))
                    throw new InvalidOperationException("City spawn has no outdoor navigation surface: " + at);
                return hit.position;
            }
            var path = new NavMeshPath();
            var origin = Walkable(new Vector3(0, 0, -10), 5);
            Vector3 Reachable(Vector3 at, float maxRadius)
            {
                // Closest surface can be a car roof or the station itself, so search connected ground around it.
                for (int ring = 0; ring <= maxRadius; ring++)
                    for (int i = 0; i < (ring == 0 ? 1 : 12); i++)
                    {
                        var candidate = at + new Vector3(Mathf.Cos(i * Mathf.PI / 6), 0, Mathf.Sin(i * Mathf.PI / 6)) * ring;
                        candidate.y = 0;
                        if (!NavMesh.SamplePosition(candidate, out var hit, .8f, NavMesh.AllAreas) || WorldGeometry.Safe(state, hit.position)
                            || hit.position.y > .8f || Vector3.Distance(at, hit.position) > maxRadius) continue;
                        if (NavMesh.CalculatePath(origin, hit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete) return hit.position;
                    }
                throw new InvalidOperationException("No connected ground near city spawn: " + at);
            }
            foreach (var item in state.loot) item.position = Reachable(item.position, 5);
            foreach (var monster in state.monsters) monster.position = monster.home = monster.destination = Reachable(monster.position, 5);
            foreach (var station in state.stations) Reachable(station.position, 3);
            foreach (var site in state.sites)
            {
                var entrance = Walkable(WorldGeometry.Entrance(site), 5);
                if (!NavMesh.CalculatePath(origin, entrance, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                    throw new InvalidOperationException("Unreachable city facility: " + HumanContent.Load().locations[site.id].id);
            }
        }
    }
}
