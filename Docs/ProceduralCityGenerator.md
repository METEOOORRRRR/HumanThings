# Local Procedural City Generator

## 확정 구조

`CityLayoutTemplate -> Seed -> RuntimeCityGenerationConfig -> RoadNetworkGenerator -> BuildingLotGenerator -> BuildingUsageResolver -> CityLayoutAssets -> HumanThingsPlacementSolver -> CitySceneBuilder -> GeneratedCityResult`

도시 생성은 로컬 코드와 직접 참조된 프리팹만 사용한다. 네트워크 요청, 인증키, 자연어 입력, 비동기 Provider, 중간 계획 JSON UI를 제거했다. 기존 게임의 LAN 멀티플레이와 미션/회수 데이터는 변경하지 않았다.

이번 작업은 독립 도시 생성기와 런타임 진입점을 완성한 것이다. 기존 실제 탐사 맵/미션 배치를 자동으로 이 도시로 교체하지 않는다. 그 연결은 별도 게임 통합 단계다.

## 실행

1. `Tools > HumanThings > City Block Generator`.
2. Database: `Assets/CityGeneration/CityAssetDatabase.asset`.
3. Template: `Assets/CityGeneration/Templates`의 에셋을 선택.
4. Seed 입력 후 `Generate`. 중간 계획 입력 없이 바로 생성한다.
5. `Regenerate With New Seed`는 다른 Seed로 새 도시를 만든다.
6. `Clear Generated`는 `GeneratedCityRoot`가 붙은 루트만 삭제한다. 이름만 같은 일반 오브젝트는 보존한다.

Editor 생성/교체/Clear는 Undo를 지원한다. 실패하면 기존 생성 루트를 보존한다. Preview Ground, 카메라, 기존 게임 오브젝트는 Clear 대상이 아니다. Play Mode와 Prefab Mode에서는 편집 창 생성 버튼을 비활성화한다.

`Assets/Scenes/ProceduralCityPreview.unity`를 열고 Play하면 RuntimeCityGenerator가 직접 생성한다. 다른 씬에서는 빈 오브젝트에 RuntimeCityGenerator를 추가하고 Database/Template/Seed를 연결한다. Generate On Start 또는 Generate()로 실행한다. 씬의 바닥/플레이어/조명은 호출 측이 제공하며 예제 씬에 기본 바닥과 카메라가 있다.

## Template와 샘플링

`Assets > Create > HumanThings > City Layout Template`로 생성한다. `Tools > HumanThings > Create City Template Presets`는 없는 프리셋만 만든다. 이미 수정한 프리셋은 덮어쓰지 않는다.

프리셋: DenseCommercial, MixedDistrict, ResidentialAlleys, SubwayHub, WideAvenue.

| 그룹 | Inspector 필드 |
|---|---|
| Map | width/depth FloatRange |
| Roads | mainRoadCount/sideRoadCount/alleyCount/intersectionCount IntRange, width/blockSize FloatRange, sidewalkWidth |
| Buildings | density 및 commercial/residential/office/public 비율 FloatRange |
| POIs | shop/subwayEntrance/busStop/publicBuilding IntRange |
| Vehicles | parkedCount IntRange |
| Props | density FloatRange |

IntRange는 양 끝 정수를 포함하고 FloatRange는 연속값을 샘플링한다. min > max나 비유한 값은 오류다. System.Random만 사용하며 UnityEngine.Random 상태를 바꾸지 않는다. 비율 합은 1로 정규화하며 모두 0이면 오류다.

안전 상한: 맵 100~500m, 메인 도로 1~3, 사이드 0~8, 골목 0~10, 교차 목표 0~12, 도로 폭 6~30m, 필지 기준 크기 16~70m, 보도 2~12m, 밀도/비율 0~1, 차량 0~60. 샘플 수치 clamp와 연결도로 추가는 경고로 남긴다.

도로 개수/교차로 개수는 공간 제약을 받는 목표값이다. 교차 목표는 관통 가지와 막다른 가지 선택에 영향을 준다. 교차로 실제 개수는 도로 연결에서 유도되므로 목표와 정확히 같지 않을 수 있다. 도로 수가 공간에 들어가지 않으면 제한하고, 부족한 교차로·골목·POI 수를 경고한다.

## 생성 알고리즘

- Seed로 메인 축 방향, 평행 도로 위치, 도로 양 끝, 사이드 도로 연결점/길이, 관통 여부, 골목 위치/길이를 정한다.
- 여러 메인 도로는 최소 연결도로를 확보한다. 단순 개수뿐 아니라 실제 공간 구조가 달라진다.
- 축 정렬 직사각형으로 도로 영역을 합치고 보도에서 도로 영역을 뺀다. 같은 높이의 중복 표면을 만들지 않는다.
- 도로/보도를 맵에서 뺀 나머지 영역을 직사각형 블록으로 분할하고, 접근 가능한 전면을 기준으로 필지를 만든다. 막다른 도로 끝을 넘어선 전면은 제외한다.
- 각 필지는 연결 도로 ID/종류, 코너 여부, 용도, Bounds를 가진다. 너무 작은 잔여 영역과 전면 없는 블록은 경고 후 제외한다.
- Template의 비율로 건물 용도를 선택한다. DB에 주거 건물이 없다고 상점으로 바꾸지 않는다.
- 기존 DB 검색, 회전, footprint, 간격, 출입부 확보, companion prefab 조립을 재사용한다. 코너가 아닌 도로 표면을 우선 선택한다.
- 필수 POI를 먼저 배치하고 일반 건물, 차량, 소품 순으로 간격을 검사한다. 필수 POI 미충족은 REQUIRED 경고를 남긴다.

같은 Template 값, Seed, DB 값/프리팹, 코드 버전과 실행 환경이면 재현된다. 프리팹 추가/분류/Bounds/알고리즘 변경 후에는 같은 Seed라도 달라질 수 있다. 다른 플랫폼 간 비트 단위 일치는 이번에 검증하지 않았다.

## Runtime 결과와 미션 연결점

GeneratedCityResult: seed, template, config, root, roads, lots, buildings, vehicles, props, locations, warnings.

GeneratedLocation: id, type, bounds, tags, root. 실제 배치된 건물/POI와 도로에 대해서만 생성한다. 제외된 필지를 완성된 장소로 보고하지 않는다.

조회 API:

```csharp
GeneratedCityResult city = generator.Result;
var all = city.GetGeneratedLocations();
var shops = city.GetLocationsByType(LocationType.Shop);
var cornerLocations = city.GetLocationsByTag("Corner");
var target = city.GetRandomLocationByType(LocationType.Subway, new System.Random(missionSeed));
```

목표 유형이 없으면 random 조회는 null이다. 쿼리용 RNG는 도시 생성과 별도로 전달한다. 미션 시스템, 네트워크 Seed 동기화, 목표 선정 로직은 구현하지 않았다.

루트 구조:

```text
HumanThings_RuntimeGeneratedCity
  Roads
  Buildings
  Vehicles
  StreetProps
  POIs
  Debug
```

## 파일 이동과 변경

DB, Entry, Classifier를 `Assets/Editor/AssetScanner`에서 `Assets/Scripts/CityGeneration`으로 이동했다. 네임스페이스는 EarthRecovery, 어셈블리는 EarthRecovery다. DB 스크립트/에셋 .meta GUID는 유지했고 MovedFrom과 새 class identifier를 적용했다. 프리팹 GUID/335개 목록/수동 metadata는 보존했다.

CityLayoutAssets, HumanThingsPlacementSolver는 `Assets/Editor/CityLayout`에서 동일 런타임 폴더로 이동했다. 배치용 CompiledCityLayout/CityPlacementRequest는 의미 계획 Compiler 대신 로컬 생성기의 내부 배치 요청으로 재사용한다.

| 파일 | 역할 |
|---|---|
| Assets/Scripts/CityGeneration/CityLayoutTemplate.cs | SO, Range, 그룹 설정, 5개 프리셋 정의 |
| Assets/Scripts/CityGeneration/CityGenerationConfigBuilder.cs | Seed 샘플링과 실제 설정 DTO |
| Assets/Scripts/CityGeneration/RoadNetworkGenerator.cs | 도로/연결 및 사각형 차집합 |
| Assets/Scripts/CityGeneration/BuildingLotGenerator.cs | 필지, 전면, 코너, 용도 선택 |
| Assets/Scripts/CityGeneration/ProceduralCityGenerator.cs | 생성 조합, 배치 요청, 검증 |
| Assets/Scripts/CityGeneration/CityPlacementData.cs | 공통 배치 데이터와 runtime 후보 유효성 |
| Assets/Scripts/CityGeneration/CityLayoutAssets.cs | 의미적 DB 검색 |
| Assets/Scripts/CityGeneration/HumanThingsPlacementSolver.cs | 기존 Bounds/회전/간격 기반 배치 |
| Assets/Scripts/CityGeneration/CitySceneBuilder.cs | 런타임 Instantiate, 결과 작성, 소유 루트 Clear |
| Assets/Scripts/CityGeneration/GeneratedCityResult.cs | 결과 및 위치 조회 API |
| Assets/Scripts/CityGeneration/GeneratedCityRoot.cs | 생성 루트 소유 및 결과 보관 |
| Assets/Scripts/CityGeneration/RuntimeCityGenerator.cs | Generate/Regenerate/Clear 런타임 진입점 |
| Assets/Editor/AssetScanner/HumanThingsCityBlockGeneratorWindow.cs | Database/Template/Seed 중심으로 교체한 UI |
| Assets/Editor/AssetScanner/CityTemplateEditor.cs | 프리셋 에셋 생성, Prefab/Undo 편집 어댑터 |
| Assets/Editor/AssetScanner/HumanThingsPlacementMetadata.cs | 공통 Usable 검사를 runtime helper에 위임 |
| Assets/Editor/AssetScanner/ProceduralCityVerification.cs | 실제 렌더링/검증 Player 빌드 도구 |
| Assets/Tests/EditMode/ProceduralCityTests.cs | 결정성, 공간/접근/참조/Undo/실패 복구 검사 |
| Assets/Tests/CitySmoke/* | 검증 전용 Player 자동 실행 테스트 |
| Assets/Tests/EditMode/AssetScannerTests.cs | 이동된 DB 경로 사용 |
| Assets/Editor/EarthRecovery.Editor.asmdef | 제거한 의존성 정리, 검증 어셈블리 참조 |
| Assets/Tests/EditMode/EarthRecovery.Tests.asmdef | 불필요한 JSON DLL 참조 제거 |
| Packages/manifest.json, packages-lock.json | 이전 단계에서 추가한 JSON 패키지 제거 |
| Assets/CityGeneration/CityAssetDatabase.asset | GUID 유지하며 Editor 폴더 밖으로 이동 |
| Assets/CityGeneration/Templates/*.asset | Inspector에서 편집하는 프리셋 5개 |
| Assets/Scenes/ProceduralCityPreview.unity | 독립 runtime 생성 확인 씬 |
| Assets/CityGeneration/PreviewGround.mat | 검증 씬 바닥 머티리얼 |

이전 테스트 씬과 그 데이터용 HumanThingsGeneratedBlock/PlacementZone은 참조 보존을 위해 유지했다. 이전 씬을 새 생성기의 Clear로 임의 삭제하지 않는다. 기존 실제 게임 씬과 Synty 원본 에셋은 변경하지 않았다.

## 삭제 목록

아래는 삭제 작업을 설명하기 위한 이력이다. 해당 클래스/Provider/키 UI/호출 코드는 프로젝트에 남겨두지 않았다. .cs의 .meta도 함께 삭제했다.

- `Assets/Editor/CityLayout/LLM/CityAssetCapabilities.cs`
- `Assets/Editor/CityLayout/LLM/CityLayoutLLMDocumentation.cs`
- `Assets/Editor/CityLayout/LLM/CityLayoutLLMPrompt.cs`
- `Assets/Editor/CityLayout/LLM/CityLayoutPlanValidator.cs`
- `Assets/Editor/CityLayout/LLM/CityLayoutSchema.cs`
- `Assets/Editor/CityLayout/LLM/ILLMProvider.cs`
- `Assets/Editor/CityLayout/LLM/LLMCityLayoutPlanner.cs`
- `Assets/Editor/CityLayout/LLM/LLMEditorCredentials.cs`
- `Assets/Editor/CityLayout/LLM/OpenAICompatibleLLMProvider.cs`
- `Assets/Editor/CityLayout/CityLayoutPlan.cs`
- `Assets/Editor/CityLayout/CityLayoutJson.cs`
- `Assets/Editor/CityLayout/CityLayoutCompiler.cs`
- `Assets/Editor/CityLayout/MockCityLayoutPlanner.cs`
- `Assets/Editor/CityLayout/CityLayoutVerification.cs`
- `Assets/Editor/AssetScanner/HumanThingsCityBlockPlanner.cs`
- `Assets/Editor/AssetScanner/HumanThingsCityBlockVerification.cs`
- `Assets/Editor/AssetScanner/HumanThingsCityBlockGenerator.cs`
- `Assets/Editor/AssetScanner/HumanThingsPlacementZone.cs`
- `Assets/Tests/EditMode/LLMCityLayoutTests.cs`
- `Assets/Tests/EditMode/CityLayoutTests.cs`
- `Assets/Tests/EditMode/CityBlockTests.cs`
- `Docs/LLMCityLayoutPlanner.md`
- `Docs/CityLayoutPlanner.md`
- `Docs/CityBlockGenerator.md`
- `Docs/LLM/SystemPrompt.txt`
- `Docs/LLM/CityLayoutPlan.schema.json`
- `Docs/LLM/CapabilitySummary.example.json`
- `Docs/LLM/ValidatedMockResponse.example.json`
- `Docs/Examples/CityLayoutMixed.json`

추가로 이전 단계 전용 상위 폴더 로그/XML 3개, 사용하지 않는 빈 폴더와 .meta를 정리했다. 수정한 파일에만 연결되어 있던 Planner 인터페이스·DTO·enum·비동기 계약도 제거했다.

## 검증

- Unity 6000.3.21f1 EditMode: 362개 통과, 실패 0개. 제거한 이전 시스템의 테스트를 새 생성기 테스트로 교체했으므로 이전 테스트 개수와 직접 비교하지 않는다.
- 프리셋 5종 x Seed 12개: 동일 결과, 도로 연결, 표면/필지 겹침, 필지 접근 확인.
- 고정 개수/고정 맵에서도 서로 다른 Seed 15개의 도로 공간 구조가 서로 다름을 확인.
- 실제 DB 335개 로드 및 prefab 참조 보존, 결과 쿼리, 실제 Renderer Bounds, Undo/Clear, 부분 생성 실패 복구 확인.
- 실제 에셋 렌더 두 장을 생성하고 비어 있지 않은 픽셀 검사 및 화면 확인. 보도 코너 반복 선택 문제를 수정했다.
- 별도 검증 Player 폴더는 생성하지 않는다. 에디터에서 도시 검증/캡처를 수행하고, 실행 파일 검증은 정상 배포된 `Builds/Latest/EarthRecovery.exe`를 사용한다.
- Windows Player 실행 종료 코드 0: `LOCAL_CITY_PLAYER_PASS database=335 deterministic=true regenerate=true clear=true`. 마지막 재생성에서 건물 6개, 도로 5개가 생성되었다. 외부 API 설정 없이 실제 프리팹으로 실행했다.
- 최종 Player 빌드 성공, 생성 씬 missing script 0, 컴파일 오류/컴파일러 경고 0. 로그의 Unity 라이선스 초기화 메시지는 도시 생성 코드와 별개다.
- 창 객체 생성과 동작 메서드는 자동 검사했다. 직접 마우스 조작으로 모든 GUI 상태를 검사한 것은 아니다.
- Assets/Packages의 C#/asmdef/JSON 전체 검색에서 외부 AI 호출 관련 문자열/참조 없음. 런타임 생성 소스에 UnityEditor/EditorPrefs 참조 없음.
- Windows EditorPrefs의 이전 전용 키 이름 검사 결과 저장된 항목 0개. 다른 프로젝트가 사용할 수 있는 공용 환경변수는 변경하지 않았다.

검증 중 발견해 수정한 항목: 새 씬 생성 시 검증 도구의 DB 재로드, 직선 표면에 코너 조각이 반복 선택되는 문제, 실패한 재생성에서 이전 결과의 도로 root 참조를 공유하던 문제, Player 테스트와 일반 Start 자동 생성의 실행 순서. 검증 빌드 경로는 Unity의 내부 폴더 제한을 피하도록 변경했다.

테스트 재실행:

- Test Runner > EditMode > ProceduralCityTests 또는 전체 테스트.
- batchmode `-executeMethod EarthRecovery.Editor.ProceduralCityVerification.Run`으로 프리셋/렌더 검증을 수행한다. 별도 실행 파일은 빌드하지 않는다.
- 최신 게임 실행 파일에 `--city-smoke -logFile <로그경로>`를 전달하면 런타임 도시 검증을 수행한다. 최신 빌드 생성/보관 규칙은 `Builds.md`를 따른다.
- 상위 로그: EarthRecovery-local-city-tests.xml, EarthRecovery-local-city-player-build.log, EarthRecovery-local-city-player.log.
- 화면과 샘플 값/경고: Docs/ProceduralCity/Seed1234.png, Seed4321.png 및 같은 이름의 JSON/validation.txt.

## 알려진 한계

- 현 DB의 완성형 주거 건물 후보는 0개다. 주거 용도 필지는 비워지고 경고한다. 기존 아파트 조각을 완성 건물로 간주하지 않는다.
- 필지 크기와 후보 footprint가 맞지 않으면 실제 밀도/POI/소품 수는 목표보다 작다. 특히 좁은 보도의 지하철 입구는 배치 실패할 수 있다.
- 도로는 평면 직교 네트워크이며 곡선/경사/고가/교량/복잡한 다중 계층 도로는 없다.
- fitted 표면의 차선과 교차로 노면 표시는 근사치다. 제작 에셋 전용 타일 연결 규칙은 후속 작업이다.
- 작은 잔여 블록은 제외한다. 공간 부족을 보고하되 invalid seed 자동 재생성은 하지 않는다.
- 생성은 동기 작업이다. 안전 상한을 뒀지만 대형 맵 프레임 분할/풀링/대규모 성능 최적화는 아직 없다.
- 출입 가능성은 도로 전면/Bounds 기준이다. 내부 출입구, 실제 캐릭터 경로, NavMesh 접근성을 의미하지 않는다.
- 기존 prefab Collider 설정을 사용한다. 미션, 폐허화, 식생 침식, 간판 제작, 몬스터, NavMesh, 멀티플레이 월드 동기화는 추가하지 않았다.
