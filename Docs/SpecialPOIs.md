# 고정 장소 15개 배치 시스템

## 범위

모든 월드에 15개 게임상 장소를 정확히 하나씩 배치한다. 각 ID는 고정된 전용 외관 Prefab reference를 사용하며, Seed는 위치/청크/회전만 바꾼다. 일반 장식 건물과 Special POI는 별도 데이터 및 결과다.

사용자 승인에 따라 기존 테스트 장소 프리팹의 Floor/Front/Rear/Side 외형만 별도 프리팹의 `Exterior` 자식으로 분리했다. 기존 게임플레이 프리팹은 수정하지 않았다. 새 프리팹은 장소별 고정 색상을 사용하고, MonoBehaviour·제작 단말기·아이템·괴물·행동 앵커를 포함하지 않는다. 완성 아트가 아닌 배치 검증용이다.

## 1. Definition / Database

`SpecialPOIDefinition` 필드:

| 필드 | 의미 |
| --- | --- |
| id / displayName | 기존 장소 ID와 이름 |
| prefab | 장소별 전용 고정 외관 |
| boundsCenter / boundsSize / footprint | 측정한 geometry 중심/크기 및 배치 footprint |
| frontFacesRoad / localForward | 도로 방향 정렬, 테스트 외형 정면은 -Z |
| minDistanceFromOtherPOI | 계산 기본값보다 크게 요구할 때 사용하는 최소 중심 거리 |
| minDistanceFromBaseCamp | 계산 기본값보다 크게 요구할 때 사용하는 캠프 중심~POI footprint 거리 |
| placementEnabled | required이므로 false면 생성 실패 |
| tags | 추가 검색 태그 |

`SpecialPOIDatabase`는 Definition 15개를 보관한다. 정확한 ID 집합, 개수, ID 중복, null, 같은 Prefab 공유, 잘못된 Bounds/방향/거리 값을 검증한다. 다른 Special POI ID를 추가할 수 없다.

### 등록된 장소

| ID | 이름 |
| --- | --- |
| COM_MUSIC_STORE | 악기점 |
| COM_RESTAURANT | 식당 |
| COM_ELECTRONICS | 전자상가 |
| RES_TOY_STORE | 문방구 |
| RES_CLOTHING | 의류점 |
| RES_CONVENIENCE | 편의점 |
| IND_TOOL_SHOP | 공구점 |
| IND_AUTO_SHOP | 정비소 |
| IND_PRINT_SHOP | 인쇄소 |
| RES_HOSPITAL | 병원 |
| RES_LAB | 실험실 |
| RES_SIGNAL_STATION | 관측소 |
| CUL_LIBRARY | 도서관 |
| CUL_SCHOOL | 학교 |
| CUL_MUSEUM | 박물관 |

ID는 계약 목록으로 고정한다. 표시 이름은 초기 에셋 생성 때 `Assets/Resources/HumanThingsSeed.json`의 장소 데이터에서 읽었다. 아티팩트나 3개 미션 선택은 수행하지 않는다.

## 2. 생성 순서 / 기존 생성기 변경

```text
World Layout + 중앙 캠프 경계 여유
 -> Chunk Road / 연결 도로
 -> 중앙 캠프와 겹치는 도로 우회
 -> Building Lots / placement requests 준비 (아직 일반 물체 배치 안 함)
 -> 캠프 예약 Bounds 확정
 -> Special POI 후보 수집 및 15개 예약
 -> 15/15 예약 검증
 -> 점유 Lot을 일반 요청에서 제거
 -> 기존 Solver로 일반 건물 / 차량 / 소품 배치
 -> 전체 도로·POI 검증
 -> Scene 생성 + 실제 POI 인스턴스 검증
```

`ProceduralCityGenerator.Prepare`와 `Complete`로 기존 준비/해결 단계를 분리했다. 기존 단독 `Generate` API는 두 단계를 연속 호출하므로 유지된다. World만 모든 청크 Prepare 이후 POI 예약을 수행한다.
일반 Solver에는 예약된 POI Bounds와 캠프 clear 영역을 전달한다. `TryPlace`가 예약 영역 충돌을 거부한다. `GeneratedLot.occupied / occupiedBySpecialPOI / occupantId`로 Lot 전체를 독점한다. 일반 건물 배치 후 남은 자리에 끼워 넣지 않는다.

## 3. 중앙 Base Camp

`CityWorldTemplate.baseCamp`에 별도 `BaseCampDefinition`을 연결한다. Base Camp는 15개 목록이나 일반 Lot에 포함되지 않는다. World Bounds 중앙 X/Z에 놓고 측정한 높이로 지면에 맞춘다.

```text
clearRadius = max(
  캠프 footprint 대각선 / 2 * footprintClearMultiplier,
  최소 청크 변 길이 * chunkClearRatio
)
```

기본 multiplier 1.3, ratio 0.08이며 Inspector에서 조정 가능하다.
충돌 예약은 원형 clear radius를 포함하는 보수적인 정사각형이다. 중앙이 청크 경계에 걸리지 않도록 분할선을 조정하고, 캠프 예약 영역을 가로지르는 도로를 잘라 바깥의 직각 순환 경로에 연결한다. 보도와 도로 폭도 여유에 포함한다. 청크가 너무 작아지면 Seed/설정 실패로 반환한다.
캠프 주변 일반 건물·소품도 예약 영역을 침범하지 않는다. 캠프 게임플레이/출입/귀환 기능은 없다.

## 4. Placement / 거리 / 분산 / Retry

1. 모든 청크의 Building Lot에서 후보를 수집한다.
2. footprint가 Lot 안에 들어가고, 도로/보도를 침범하지 않으며, 캠프 clear 영역 밖인 후보만 남긴다.
3. 큰 footprint 면적 순으로 POI를 처리한다. 동률은 ID 순으로 고정한다.
4. World Seed에서 파생한 로컬 System.Random으로 후보의 동률 순서를 섞는다.
5. 현재 POI가 적은 청크를 우선하되 거리·겹침·Lot 점유·청크 수용량을 모두 검사한다.
6. 실패하면 backtracking한다. 시도당 기본 10,000개 탐색 노드, 최대 10회로 제한한다.
7. 각 재시도는 다른 파생 placement seed를 사용한다. 거리 조건을 몰래 낮추거나 다른 외관으로 바꾸지 않는다.
8. 15개가 모두 예약된 경우만 점유 상태를 확정한다. 모두 실패하면 `World Generation Failed` 예외와 해결 방향을 반환한다.

```text
minPOIDistance = max(
  평균 POI footprint의 긴 변 * footprintDistanceFactor,
  최소 청크 변 길이 * chunkDistanceRatio
)
```

기본 factor 2, ratio 0.2. 두 POI 중 개별 최소 거리가 더 크면 그 값을 적용한다. 거리 조건 외에도 footprint overlap을 별도로 검사한다.

```text
maxPOIsPerChunk = ceil(15 / 청크 수) + (청크 수가 9 미만이면 1, 아니면 0)
```

3x3는 최대 2개/청크다. 균등한 고정 슬롯 배정이 아니라 후보와 Seed에 따라 위치/분포가 결정된다. 검증한 10개 Seed는 모두 8개 이상 청크에 분산됐다.

같은 코드/World Template/Asset Database/Special POI Database/Seed에 대해서 위치가 재현된다. UnityEngine.Random 글로벌 상태를 사용하지 않는다. 프리팹 크기나 데이터 변경 후에도 과거 위치를 보존하는 버전 저장 시스템은 아니다.

## 5. 생성 결과 / 검색 / Hierarchy

`GeneratedSpecialPOI`:

- definition / id / displayName
- instance / world-space bounds
- chunk / location

`GeneratedWorldResult.specialPOIs`에 저장하고 `locations`에도 등록한다. `LocationType.SpecialPOI`는 기존 enum 끝에 추가해 기존 숫자 값을 보존했다.

태그는 `special_poi`, 장소 ID, displayName, 청크 ID, Definition tags, ID의 의미 토큰을 포함한다. 예를 들어 학교는 `school`로도 찾을 수 있다.

```csharp
var all = world.GetLocationsByType(LocationType.SpecialPOI);
var school = world.GetLocationsByTag("CUL_SCHOOL");
var tagged = world.GetLocationsByTag("school");
```

```text
HumanThings_GeneratedWorld
  BaseCamp
    DebugLocationLabel
  SpecialPOIs
    COM_MUSIC_STORE
      Exterior
      DebugLocationLabel
    ... (15개)
  Chunk_0_0
    Roads / Buildings / Vehicles / StreetProps / POIs / Debug
  ...
  Global / Debug
```

기존 청크 루트 구조는 유지하고 SpecialPOIs만 별도 루트로 분리했다. 명시적으로 청크를 Unload하면 해당 POI 인스턴스와 Location도 제거되고, 같은 계획으로 Load하면 고정 외관/위치로 복원된다. 부분 스트리밍 상태에서 15개 인스턴스가 모두 로드돼 있다고 주장하지 않는다. 완전한 월드 생성 성공 시에는 반드시 15/15다.

## 6. 개발용 간판

TextMeshPro 3D Text를 사용한다. `POIDebugLabel`은 외관과 별도 자식이며, `displayName`을 `[학교]`처럼 표시한다. 정면 벡터와 Bounds로 건물 앞 위치를 계산하고, 지면에서 최소 2.5m 높이에 둔다. 바탕체 계열의 한글 glyph를 미리 구운 `POILabelFont.asset`을 사용해 실행 시 OS 글꼴 설치에 의존하지 않게 했다.

`Show POI Labels`는 신규 생성 설정과 현재 월드의 간판 표시 모두에 적용된다. 캠프 `[BASE CAMP]`도 같은 시스템이다. Debug label은 배치 footprint/실제 외관 Bounds 측정에서 제외한다. 향후 간판은 POI 정의나 배치 알고리즘 변경 없이 교체할 수 있다.

## 7. Unity에서 외관 연결 / 테스트

1. `Tools > HumanThings > City World Generator`를 연다.
2. 기본 `StandardCity`에는 SpecialPOIDatabase와 BaseCampDefinition이 연결돼 있다.
3. Seed 100으로 `Generate World`, Seed 200으로 다시 생성해 비교한다.
4. 창에 `Special POIs: 15 / 15 placed`, 각 장소의 청크·위치가 표시된다.
5. `Show POI Labels`, `Draw POI Bounds`, `Draw POI Min Distance`, `Draw Base Camp Clear Radius`를 켜서 확인한다.
6. `Validate 10 World Seeds`로 전체 월드/POI 검증을 실행한다.
7. 필요한 경우 `Tools > HumanThings > Create Special POI Test Assets`로 초기 에셋을 만든다. 기존 외형/Definition은 덮어쓰지 않는다.

외관을 바꾸려면 `Assets/CityGeneration/SpecialPOIs/Definitions/<장소ID>.asset`의 **Prefab** 슬롯만 새 외관 프리팹으로 바꾼다. Inspector가 Bounds/footprint를 자동 측정한다. 같은 프리팹 내부의 Mesh만 편집했다면 `Measure Prefab Bounds / Footprint` 버튼으로 다시 측정한다. 앞 방향이 다른 에셋은 `Local Forward`도 맞춘다. ID는 변경하지 않는다.
Base Camp도 Definition의 Prefab 슬롯 교체 시 자동 측정된다. 새 WorldTemplate은 SpecialPOIDatabase, BaseCampDefinition, 한글 labelFont를 직접 연결해야 한다. 값이 없으면 불완전한 월드를 생성하지 않고 실패한다.

## 8. Validation 결과

| Seed | Special POI | 배치 시도 | 도로 실패 | 빈 청크 | 일반 footprint overlap |
| --- | --- | --- | --- | --- | --- |
| 100 | 15/15 | 1 | 0 | 0 | 0 |
| 200 | 15/15 | 1 | 0 | 0 | 0 |
| 1234 | 15/15 | 1 | 0 | 0 | 0 |
| 5678 | 15/15 | 1 | 0 | 0 | 0 |
| 16847 | 15/15 | 1 | 0 | 0 | 0 |
| 24766 | 15/15 | 1 | 0 | 0 | 0 |
| 32685 | 15/15 | 1 | 0 | 0 | 0 |
| 40604 | 15/15 | 1 | 0 | 0 | 0 |
| 48523 | 15/15 | 1 | 0 | 0 | 0 |
| 56442 | 15/15 | 1 | 0 | 0 | 0 |

POI ID/lot 중복, POI 상호 겹침, 최소 거리 위반, 캠프 clear 영역 침범, 도로/보도 침범 모두 0. 100/200은 실제 프리팹 생성 후 instance 중복·실측 Renderer footprint·Location 등록도 검증했다. 100을 재생성하면 동일 위치, 200은 같은 프리팹에 다른 위치임을 테스트했다.

전체 EditMode 390개 통과, 실패 0. 테스트에는 4x4/5x5 기존 확장, 원본 프리팹 보존, 15개 외형의 게임플레이 컴포넌트 부재, 잘못된 DB, 최대 10회 실패, 취소, 재로드, 라벨 토글 및 Mesh 생성 검사가 포함된다.
발견 후 수정한 문제는 POI만 있는 청크의 잘못된 empty 판정과 비활성 부모에서 만든 TMP 간판을 활성화할 때의 Mesh 갱신 시점이다.

검증 파일: `Docs/SpecialPOIs/Validation.json`; 위치/고정 프리팹 경로: `Seed100.csv`, `Seed200.csv`; 실제 캡처: `Seed100-Top.png`, `Seed200-Top.png`, `Seed100-Label.png`, `Seed200-Label.png`.
GUI를 실제 마우스로 조작하는 검증은 하지 않았다. Unity batch 생성/렌더링 및 EditMode 테스트로 확인했다. Player 실행 파일 빌드는 생성하지 않았다.

## 9. 생성/수정 파일 목록

| 파일 | 역할 |
| --- | --- |
| Scripts/CityGeneration/World/SpecialPOIDefinition.cs | 고정 외관·Bounds·방향·거리 정의 |
| Scripts/CityGeneration/World/SpecialPOIDatabase.cs | 15개 계약 검증, 설정/배치/결과 DTO |
| Scripts/CityGeneration/World/BaseCampDefinition.cs | 중앙 캠프 외관·clearance 정의 |
| Scripts/CityGeneration/World/SpecialPOIPlacementSolver.cs | 후보/분산/거리/backtracking/제한 재시도 |
| Scripts/CityGeneration/World/SpecialPOIValidation.cs | 예약 및 실제 인스턴스 검증 |
| Scripts/CityGeneration/World/POIDebugLabel.cs | 독립 TMP 개발 간판 |
| Scripts/CityGeneration/World/CityWorldTemplate.cs | POI DB/캠프/설정 참조 |
| Scripts/CityGeneration/World/CityWorldData.cs | 예약/결과/Location 통합 필드 |
| Scripts/CityGeneration/World/CityWorldLayout.cs | 중앙 캠프용 청크 경계 여유 |
| Scripts/CityGeneration/World/CityChunkGenerator.cs | Prepare와 캠프 충돌 도로 우회 |
| Scripts/CityGeneration/World/CityWorldGenerator.cs | POI 우선 순서, 생성/부분 재로드 |
| Scripts/CityGeneration/World/CityWorldValidation.cs | 15/15 필수 검증, 점유 청크 집계 |
| Scripts/CityGeneration/ProceduralCityGenerator.cs | Prepare/Complete 분리 및 점유 Lot 제외 |
| Scripts/CityGeneration/BuildingLotGenerator.cs | Lot 점유 상태 |
| Scripts/CityGeneration/CityPlacementData.cs | 예약 영역 |
| Scripts/CityGeneration/HumanThingsPlacementSolver.cs | 예약 충돌 거부 |
| Scripts/CityGeneration/CityLayoutTemplate.cs | SpecialPOI LocationType 추가 |
| Editor/CityTemplates/SpecialPOIAssetBuilder.cs | 외형 분리본·Definition·DB·폰트 생성, 교체 외형 자동 측정 |
| Editor/CityTemplates/SpecialPOIVerification.cs | 10 Seed 및 실제 프리팹/캡처 검증 |
| Editor/CityTemplates/CityWorldGeneratorWindow.cs | POI Debug UI/Gizmo/간판 토글 |
| Editor/EarthRecovery.Editor.asmdef | 폰트 생성 도구용 TMP 참조 |
| Tests/EditMode/SpecialPOITests.cs | 새 POI 테스트 7개 |
| CityGeneration/SpecialPOIs/* | Definition 15개, 외형 15개, 캠프, DB, 소재, 글꼴 |
| CityGeneration/WorldTemplates/StandardCity.asset | 새 DB/캠프 참조 연결 |

위 표의 코드/에셋 경로는 `Assets/` 기준이다. 문서는 `Docs/SpecialPOIs.md`, 검증 산출물은 `Docs/SpecialPOIs/`에 있다. Unity가 생성한 `.meta`를 함께 유지한다.

## 10. 알려진 한계

- 외형은 기존 기본 도형 구조에 장소별 색상을 적용한 테스트용이다. 학교/병원 등의 완성 건축물은 아니다.
- 15개 고정 외관이 존재한다는 보장은 올바른 DB/설정과 배치 가능한 공간을 전제로 한다. 임의로 큰 프리팹·강한 거리 제약·작은 월드에서는 명확히 실패하며 누락된 성공 결과를 반환하지 않는다.
- 제한 탐색이므로 해가 있어도 예산 내에 못 찾으면 실패할 수 있다. 무한 재시도나 제약 자동 완화는 하지 않는다.
- 네비메시/실제 캐릭터 보행·캠프 출입 검증은 없다. 연결성은 기존 도로 그래프와 표면 기준이다. 캠프 진입로/게임플레이는 별도 작업이다.
- 캠프 도로 우회는 직각 직사각형 경로다. 곡선/높낮이/특수 지형을 지원하지 않는다.
- Inspector에서 바꾼 외형의 정면 축은 직접 지정해야 한다. Bounds/footprint는 Renderer 기반 보수적인 AABB이고 정밀 mesh 충돌 검사가 아니다.
- 한글 라벨 글꼴은 현재 15개 이름과 BASE CAMP에 필요한 glyph를 미리 구웠다. 이름에 새로운 글자를 추가하면 글꼴 에셋도 갱신해야 한다.
- 미션 3종 선택, 아티팩트 선택, 아이템/괴물 생성, 협동 퍼즐, 캠프 게임플레이, 탈출 로직은 수정/연결하지 않았다. 외부 AI/API/HTTP도 추가하지 않았다.
