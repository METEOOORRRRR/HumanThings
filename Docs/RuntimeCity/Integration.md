# In-game procedural city

## Entry point

The normal title -> lobby -> ready -> start expedition flow now uses the same
StandardCity template as the five-seed POI comparison. StartMission selects one
world seed on the host. RuntimeCitySettings is a Resources asset containing
references to the existing template and prefab database; no Editor APIs are
needed by the player.

## Gameplay binding

- The host applies the 15 POI transforms to SiteState by immutable location ID.
- The mission selection still chooses three locations from different content zones.
- Clients regenerate the same template/seed and validate facility transforms
  against the host snapshot before constructing their world.
- Existing gameplay prefabs remain unchanged. Their structural children are
  disabled only on instantiated objects and their consoles, lights, artifact
  anchors and behavior anchors are attached below the fixed POI exterior.
- The solid preview camp is disabled and replaced with the existing enterable
  camp at the origin, including its terminal, recovery area and hibernation pod.
- Supplies, restoration stations and monsters use deterministic road sockets.
  The host checks their navigation surfaces and paths after building the city.
- The minimap uses actual map dimensions, chunk areas, road bounds and replicated
  facility positions. Undiscovered facility names still remain hidden.
- Navigation, camera collision, map borders and patrol limits use the city scale.
- Unreadable third-party mesh collision uses local-bounds box proxies. Source
  package imports are untouched. These are deliberately approximate colliders.
- Transport protocol is now 9; both peers must use the updated build.

## Verification

`RuntimeCityTests` starts actual host expeditions with seeds 100, 200, 300, 400,
500, opens all 75 consoles, compares independently generated client layouts,
validates snapshots and returns to the lobby after each run.

`RuntimeCityLayoutTests` checks runtime references and rejects invalid map sizes.
The normal build is published to `Builds/Latest/EarthRecovery.exe` using the
existing latest-plus-two-previous retention policy.

Development builds also support `--city-smoke host` and `--city-smoke client`
with `--city-output <directory>` for a two-process localhost smoke test on port
17998. These flags are test-only and are not needed for normal play. The smoke
test does not write player archive progress.

## Remaining scope

Travel time and supply density have not been rebalanced for the larger city.
Korean sign occlusion by street props, observed in the comparison, is unchanged.
The preview tools and original prefab assets are retained.

## Recorded checks

- EditMode: 396 passed.
- PlayMode: 37 passed, including all five runtime seeds and 75 facility consoles.
- Windows player: host and client connected on localhost, seed 100, 15 matching facilities each.
- Player camera images are offscreen renders, not UI screenshots.
