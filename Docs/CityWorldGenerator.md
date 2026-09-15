# HumanThings: Hierarchical City World Generator

## 1. 구조와 기존 생성기 재사용

```text
CityWorldTemplate + World Seed
  -> CityWorldLayout: 월드 크기 / 가변 열·행 크기
  -> 관통 대로 경로 / 연결 그래프 / Connector pair
  -> 가중치 + 인접 규칙으로 기존 CityLayoutTemplate 배정
  -> CityChunkGenerationRequest (청크별)
  -> 기존 RoadNetworkGenerator
  -> 경계 Connector 도로 제약 반영
  -> 기존 BuildingLotGenerator / BuildRequests / PlacementSolver
  -> CityWorldValidation
  -> 기존 CitySceneBuilder (청크별)
  -> GeneratedWorldResult / 통합 Location API
```

기존 `RuntimeCityGenerator` 및 단독 블록 생성 메뉴는 유지한다. 이름을 변경하지 않아 기존 씬/컴포넌트 참조를 보존한다.
`CityChunkGenerator`는 기존 생성기 앞의 어댑터다. 도로를 경계 안쪽으로 수축하고, 지정된 경계에서 내부 주도로의 중심까지 직각 연결 경로를 추가한다. 이 작업은 Lot 분할 **이전**에 수행하므로 건물이 연결 도로 위에 먼저 배치되는 구조가 아니다.
각 청크에 기존 Solver를 독립 실행한다. 월드 전체를 하나의 배치 Solver에 넣지 않는다.

## 2. WorldTemplate

생성된 기본 에셋: `Assets/CityGeneration/WorldTemplates/StandardCity.asset`

| 설정 | 초기값 / 의미 |
| --- | --- |
| worldWidthRange / worldDepthRange | 540~600m |
| chunkCountXRange / chunkCountZRange | 3~3 |
| columnVariation | 0.12, 열/행 크기에 변동을 주고 전체 길이에 정규화 |
| chunkTemplates | 기존 CityLayoutTemplate 참조 + district + weight |
| adjacency | 두 district 간 선호 배율, 기본 2 |
| maxSameDistrictRun | 행/열에서 동일 타입 최대 연속 2개 |
| extraConnectionChance | 0.22, 연결 보장 이후 추가 루프 확률 |
| mainRoadWidth / sideRoadWidth | 14m / 8m |
| sidewalkWidth | 청크 전체 공통 6m |

프리팹과 개별 좌표는 저장하지 않는다. 새로운 블록 템플릿 체계를 복제하지 않고, Auto Builder가 생성한 기존 6종을 참조한다.

| 기존 Chunk Template | World district | 선택 가중치 |
| --- | --- | --- |
| CatalogMixed | Mixed | 2 |
| SparseBlocks | Mixed | 1 |
| MarketBranches | Commercial | 2 |
| OfficeBlocks | Office | 1.5 |
| TransitCorridor | Transit | 1 |
| WideAvenue | Downtown | 1 |

현재 라이브러리에 맞지 않는 Residential/Backstreet 전용 프리셋은 임의로 만들지 않았다. enum 및 규칙은 지원하므로 적합한 기존/추가 CityLayoutTemplate을 연결할 수 있다.
월드가 청크 경계를 결정하므로 청크 템플릿의 `map.width/depth` 샘플값은 해당 청크 크기로 대체한다. 나머지 건물/차량/소품/POI 설정과 Solver는 유지한다. 경계 계약을 위해 보도 폭은 월드 공통값을 사용한다. 원본 ScriptableObject는 수정하지 않는다.

## 3. Connector와 Global Main Road

- 방향은 North/South/East/West. North/South 위치는 서쪽에서 동쪽으로, East/West 위치는 남쪽에서 북쪽으로 0~1이다.
- pair는 한 번에 만들며 같은 ID, 타입, normalizedPosition, 도로 폭, 보도 폭을 양쪽에 부여한다.
- 가변 열/행 분할을 사용하므로 인접 청크의 공유 경계 길이는 같다. 정규화 위치뿐 아니라 최종 world 좌표 일치도 검사한다.
- World Seed로 대로 방향을 동서 또는 남북 중 선택한다. 진행 축을 따라 전진하며 일부 단계에서 인접 행/열로 한 칸 꺾을 수 있다.
- 관통 경로에는 MainRoad pair를 강제하고, 양 끝에는 월드 외곽 Entry/Exit를 만든다.
- 대로 경로에서 시작하는 무작위 spanning graph로 나머지 모든 청크에 SideRoad를 연결한다. 이후 선택적으로 루프를 추가한다.
- 각 청크 내부에서 모든 MainRoad port는 같은 내부 주도로 지점에 연결되므로 대로가 SideRoad를 거쳐야만 연결되는 경우를 피한다.
- 표면 도로/보도는 기존 직사각형 합집합과 차집합 타일링을 사용한다. 중복 표면을 제외하고 월드 경계에서 정확히 자른다.
- 건물·차량·소품의 배치 영역을 경계에서 2m 안쪽으로 제한한다. 건물은 기존 Lot의 도로/보도 회피 규칙도 적용된다.

## 4. 타입 배정과 Seed

타입 기본 가중치에 이미 배정된 인접 구역의 선호 배율을 곱한다. 같은 타입 인접에는 0.4배, 대로 경로의 Downtown/Transit에는 1.8배를 적용한다. 동일 타입이 행/열에서 설정값보다 연속하는 선택은 제외한다. 만족 가능한 후보가 없으면 잘못된 설정/Seed로 실패 처리한다.

`DeriveChunkSeed(worldSeed, x, z)`는 명시적인 uint 혼합 해시를 사용한다. 문자열 GetHashCode, 시간, UnityEngine.Random 글로벌 상태에 의존하지 않는다. World 레이아웃은 로컬 System.Random을 사용한다.
재현성 범위는 동일 코드, 동일 템플릿/DB, 동일 Seed다. DB/프리팹이나 생성 알고리즘을 변경한 뒤까지 이전 월드 재현을 보장하는 저장 버전 시스템은 아직 없다.

## 5. 결과 / Location / Mission 연결 지점

`GeneratedWorldResult`:

- worldSeed / worldTemplate / worldBounds / root
- chunks: GeneratedCityChunk(node + 기존 GeneratedCityResult)
- globalRoads: 청크 간 GeneratedRoadConnection 목록, globalMainRoute 플래그
- locations: 로드된 모든 청크에서 수집한 world-space Location 목록
- GetAllLocations / GetLocationsByType / GetLocationsByTag

청크 내부 결과는 local 좌표를 유지한다. 월드 조회 결과는 청크 offset을 더한 Bounds를 반환하고, ID에 `Chunk_x_z/` 접두사를 붙여 충돌을 피한다. 원래 tags와 함께 청크 ID와 district도 제공한다.

```csharp
var plan = CityWorldGenerator.Generate(worldTemplate, 1234, database);
var world = CityWorldGenerator.Build(plan, scene);
var shops = world.GetLocationsByType(LocationType.Shop);
var tagged = world.GetLocationsByTag("Commercial");

var node = plan.nodes[0];
CityWorldGenerator.UnloadChunk(world, node.gridPosition);
CityWorldGenerator.LoadChunk(world, node, plan.chunkPlans[node.gridPosition], scene);
```

향후 미션은 이 월드 Location 쿼리에서 조건에 맞는 후보를 수집한 뒤 별도 Mission Seed로 선택하면 된다. 특정 청크에 고정하지 않았다. 이번 작업에서 미션 자체나 기존 아티팩트 데이터/UI는 수정하지 않았다.

## 6. Hierarchy와 편집 안전성

```text
HumanThings_GeneratedWorld
  Chunk_0_0
    Roads
    Buildings
    Vehicles
    StreetProps
    POIs
    Debug
  Chunk_1_0 ...
  Global
    Debug
```

새 계획 생성과 검증, 프리팹 인스턴스 생성이 성공한 뒤에만 이전 월드를 제거한다. Editor 생성/삭제는 Undo를 지원하고 다른 씬 오브젝트나 독립 블록 생성 결과는 지우지 않는다. 취소는 씬 변경 전 단계에서 처리한다. 실패한 새 월드의 부분 생성물은 정리한다.
`LoadChunk` / `UnloadChunk`는 독립 생성·삭제 API이며 Location 집계도 갱신한다. 자동 스트리밍 스케줄러는 없다. 월드 루트는 원점/회전 0/scale 1 상태로 사용하는 좌표 계약이다. 생성 후 루트/청크를 수동 이동하는 편집기는 별도 지원하지 않는다.

## 7. Unity 실행 방법

1. `Tools > HumanThings > City World Generator`를 연다.
2. Asset Database에 `CityAssetDatabase`, World Template에 `StandardCity`를 지정한다. 기본 자동 로드된다.
3. World Seed를 1234로 입력하고 `Generate World`를 누른다.
4. 5678 입력 후 생성하거나 `Regenerate With New Seed`로 비교한다.
5. `Draw Chunk Bounds / Draw Connectors / Draw Global Road Graph / Draw District Types`를 Scene View에서 켜고 확인한다.
6. `Validate 10 World Seeds`로 씬 변경 없이 검증한다. UI에 청크/연결/배치 결과, Console에 상세 JSON이 표시된다.
7. `Clear World`는 생성 월드만 지운다. Undo로 되돌릴 수 있다.

`Create / Select StandardCity`는 없는 경우에만 기본 템플릿을 만들고, 이미 존재하는 사용자 설정은 덮어쓰지 않는다. 새 WorldTemplate은 Project의 `Create > HumanThings > City World Template`에서도 생성할 수 있다.
4x4는 월드 너비/깊이 720~800m, 5x5는 900~1000m와 함께 지정한다. 개별 청크는 기존 생성기 안전범위인 100~500m 밖이면 명시적으로 실패한다. 초기 grid 지원 범위는 축당 2~8이다.

## 8. 자동 검증과 실제 확인 결과

- 고정 10개 World Seed: 1234, 5678, 16847, 24766, 32685, 40604, 48523, 56442, 64361, 72280.
- 90/90 청크 생성, 84/84 내부 Connector pair 일치, 모든 Seed에서 9/9 청크 접근 가능.
- 도로/보도 연결 실패 0, 빈 청크 0, solid footprint overlap 0, 경계 침범 0.
- 건물 배치 755/914 = 약 82.6%. 나머지는 기존 크기·간격·후보 제한으로 생략되며 경고에 남는다. 모든 건물 배치 성공을 의미하지 않는다.
- 검증은 BFS, 쌍 좌표/폭/타입, 대로 경로, 내부 도로 연결, 실제 Solver가 배치한 경계 표면 샘플, 로컬/청크 간 solid footprint 중첩을 검사한다.
- 1234, 5678 월드를 실제 씬에 생성하고 위/사선 시점으로 렌더링했다. Missing Script와 빈 렌더 검사 통과.
- 동일 Seed 재현, 다른 Seed의 타입/연결 변경, 템플릿/DB/Unity Random 불변성, 4x4/5x5, Location ID/좌표, 개별 unload/load, 잘못된 입력/연결/겹침 검출 테스트를 추가했다.
- 최종 EditMode 전체 383개 통과, 실패 0. 신규 World 테스트는 9개 테스트 케이스다. Unity 컴파일 오류 없음.
- EditorWindow 초기화는 테스트했지만 실제 마우스 클릭을 통한 GUI 조작 검증은 수행하지 않았다. 생성·삭제·조회와 렌더링은 Unity batch/EditMode에서 실행했다.

상세 결과: `Docs/CityWorld/Validation.json`, 요약: `Validation.csv`, 캡처: `Seed1234-Top.png`, `Seed1234-Perspective.png`, `Seed5678-Top.png`, `Seed5678-Perspective.png`.
Editor 테스트는 Test Runner의 EditMode 전체 또는 `CityWorldTests`를 실행한다. Batch 렌더 검증 entry point는 `EarthRecovery.Editor.CityWorldVerification.Run`이며 캡처에는 `-nographics`를 사용하지 않는다.

## 9. 생성/수정 파일 목록

| 파일 | 역할 |
| --- | --- |
| Assets/Scripts/CityGeneration/World/CityWorldTemplate.cs | 월드 SO, 가중치, adjacency 설정 |
| Assets/Scripts/CityGeneration/World/CityWorldData.cs | Node, Connector, request, plan, result, Location 쿼리 |
| Assets/Scripts/CityGeneration/World/CityWorldLayout.cs | 거시 레이아웃, 대로, spanning graph, 타입 배정, Seed 해시 |
| Assets/Scripts/CityGeneration/World/CityChunkGenerator.cs | 기존 생성기 어댑터와 Connector 제약 |
| Assets/Scripts/CityGeneration/World/CityWorldGenerator.cs | 청크 순차 생성, 씬 빌드, 부분 load/unload/clear |
| Assets/Scripts/CityGeneration/World/GeneratedWorldRoot.cs | 생성 월드 식별 및 결과 보관 |
| Assets/Scripts/CityGeneration/World/CityWorldValidation.cs | 10+ Seed 검증, 그래프/표면/경계/배치 검사 |
| Assets/Scripts/CityGeneration/ProceduralCityGenerator.cs | 기존 API 유지, 설정/도로 제약 overload와 경계 여백 추가 |
| Assets/Scripts/CityGeneration/HumanThingsPlacementSolver.cs | 표면 배치에도 requestId 기록, 도로/보도 검증 추적 |
| Assets/Editor/CityTemplates/CityWorldGeneratorWindow.cs | Editor UI, Undo, Scene View debug, StandardCity 생성 |
| Assets/Editor/CityTemplates/CityWorldVerification.cs | batch 검증, 실제 월드 생성 및 렌더 |
| Assets/Tests/EditMode/CityWorldTests.cs | 신규 회귀/불변성/확장/실패 경로 테스트 |
| Assets/CityGeneration/WorldTemplates/StandardCity.asset | 기존 6개 Chunk Template을 참조하는 기본 3x3 설정 |
| Docs/CityWorldGenerator.md | 사용/설계/검증/한계 문서 |
| Docs/CityWorld/* | 검증 JSON/CSV와 실제 캡처 |

Unity가 생성한 위 코드/폴더/에셋의 `.meta`도 함께 유지한다.

## 10. 현재 한계

- 월드 수준 연결은 추가됐지만, 청크 내부는 기존 저밀도 생성기다. 큰 빈 공간, 긴 직선/직각 길, 작은 상점 프리팹 반복은 남는다. 완성된 도시 아트/레벨 디자인이 아니다.
- 청크는 가변 크기 직사각형 grid다. 비정형 구역, 곡선 도로, 높낮이/교량 연결은 아직 없다.
- 도로/보도 접합의 기하 연결은 확인했지만, 타일 UV·차선·교차로 표시는 기존 근사 방식을 유지한다. 건물로 경계를 충분히 가리는 아트 보강도 아직 없다.
- 연결성은 도로/청크 그래프와 표면 기준이다. NavMesh Bake, 캐릭터 반경에 따른 전체 보행 경로 검증, 교통 AI는 이번 작업에 포함되지 않았다.
- 심각한 겹침은 DB의 Bounds/footprint 기준 검사다. 모든 개별 triangle/collider의 정밀 충돌 검사는 아니다.
- 자동 Streaming/Culling/LOD/비동기 생성/멀티플레이 동기화는 아직 없다. 해당 확장을 위한 청크별 루트와 API만 제공한다.
- 템플릿의 모든 필수 POI가 항상 배치되는 것을 보장하지 않는다. 배치 실패는 상세 warnings에 남으며 미션 목표는 실제 생성된 Location을 조회해야 한다.
- 이번 단계에서 게임의 실제 탐사 씬과 시작 흐름에 월드를 자동 연결하지 않았다. Editor와 런타임 API에서 생성하는 상위 시스템을 추가했다.
- 외부 AI/LLM/API/HTTP 호출을 추가하지 않았다. 새 Player 빌드나 기존 게임 씬 덮어쓰기도 하지 않았다.
