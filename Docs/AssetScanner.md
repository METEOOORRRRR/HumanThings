# City Pack Asset Scanner

## 현재 데이터베이스

- 원본: `Assets/Synty/PolygonCity`
- 생성 에셋: `Assets/CityGeneration/CityAssetDatabase.asset`
- City 프리팹 335개. 같은 `Synty` 폴더의 `PolygonGeneric`은 별도 팩이므로 제외했다.
- 기존 게임 씬, 맵 배치, 런타임 기능은 변경하지 않았다. Synty 원본 파일도 수정하지 않았다.
- 데이터베이스와 검색 데이터는 런타임 어셈블리로 이동했다. 스캔 및 편집 창만 Editor 전용이다.

| Category | 개수 |
|---|---:|
| Building | 62 |
| Road | 49 |
| Vehicle | 9 |
| StreetProp | 112 |
| Transit | 6 |
| Nature | 12 |
| Structure | 26 |
| Unknown | 59 |

Unknown에는 캐릭터, FX, 일부 일반 소품과 지형 조각이 포함된다. 이름·경로 기반 초벌 분류이며 모델의 형상을 분석하지 않는다. 예를 들어 상점 내부 부품은 Shop 태그에 걸릴 수 있으므로 사용 전 확인이 필요하다.

## 사용 방법

1. Unity에서 `Tools > HumanThings > Asset Scanner`를 연다.
2. 프로젝트에 데이터베이스가 하나뿐이면 자동 선택된다. 아니면 Database 필드에 기존 에셋을 지정한다.
3. Scan Folder에 `Assets/Synty/PolygonCity`를 지정한다.
4. Scan을 누른다. 하위 폴더의 `.prefab` 파일을 모두 읽는다.
5. Search에 `도로`, `상점`, `차량`, `버스정류장` 또는 영문 이름·태그를 입력한다.
6. Category Filter에서 Unknown을 선택하면 미분류 항목만 표시한다.
7. Category, Subcategory, Tags를 수정한다. Tags는 쉼표로 구분하고 Enter 또는 포커스 이동으로 확정한다.
8. Save Database로 저장한다.

새 카탈로그는 Create Database에서 프로젝트 내부 `.asset` 저장 경로를 고른다. Player에서 사용할 데이터는 `Assets/CityGeneration` 등 Editor 밖에 저장한다. 목록은 페이지당 50개이며 상단에 전체/필터 결과/카테고리별 개수를 표시한다.

## 재스캔과 안전성

- GUID를 키로 사용하므로 원본 프리팹의 이름이나 경로가 바뀌어도 기존 분류·하위분류·태그가 유지된다.
- 새 프리팹만 자동 분류한다. 기존 자동 분류도 사람이 수정한 값처럼 보존한다.
- 삭제되거나 선택 폴더 밖으로 옮겨진 프리팹은 다음 스캔 결과에서 제외된다.
- 스캔 중 취소/실패하면 기존 목록을 덮어쓰지 않는다.
- 다른 폴더로 변경하면 기존 목록 범위가 교체된다는 확인창을 표시한다.
- 수정과 스캔은 Undo를 지원한다. Prefab 참조는 잘못 연결되지 않도록 읽기 전용으로 표시한다.
- Play Mode나 씬에 프리팹을 생성하지 않고 AssetDatabase API로 읽는다.

## 코드 구성

데이터/검색은 `EarthRecovery`, Editor 도구는 `EarthRecovery.Editor` 네임스페이스를 사용한다.

| 파일 | 역할 |
|---|---|
| `Assets/Scripts/CityGeneration/HumanThingsAssetEntry.cs` | Category enum, 이름·경로·GUID·Prefab 참조·분류·태그 데이터 |
| `Assets/Scripts/CityGeneration/HumanThingsAssetDatabase.cs` | ScriptableObject 목록 및 문자열/태그/카테고리 검색 API |
| `Assets/Scripts/CityGeneration/HumanThingsAssetClassifier.cs` | CamelCase/구분자 정규화, 키워드 우선순위, 한글/영문 태그 |
| `Assets/Editor/AssetScanner/HumanThingsAssetScanner.cs` | 재귀 스캔, GUID 보존, 배치 실행 지원 |
| `Assets/Editor/AssetScanner/HumanThingsAssetScannerWindow.cs` | EditorWindow UI, 수동 수정, 생성·저장·Undo·필터·페이지 |
| `Assets/CityGeneration/CityAssetDatabase.asset` | 실제 City Pack 스캔 결과 |
| `Assets/Tests/EditMode/AssetScannerTests.cs` | 분류, 한글 검색, 저장, 재스캔, 이동/삭제/취소 회귀 테스트 |
| `Assets/Tests/EditMode/EarthRecovery.Tests.asmdef` | Editor 도구 테스트를 위한 어셈블리 참조 추가 |
| `Docs/AssetScanner.md` | 사용 및 검증 안내 |

Bounds 및 placement metadata를 사용한다. Template + Seed 기반 로컬 도시 생성은 `Docs/ProceduralCityGenerator.md`를 참고한다. Thumbnail 및 임베딩 검색은 구현하지 않았다.

## 분류 방식

- 이름을 먼저 검사하고, 이름에 분류 키워드가 없을 때 경로를 사용한다.
- CamelCase, underscore, 공백을 정규화해 `BusStop`과 `bus_stop`을 같은 방식으로 처리한다.
- 단어 경계를 사용하므로 `Carpet`이 Car로, `Canvas`가 Van으로 분류되지 않는다.
- 구체적인 키워드를 먼저 처리한다: BusStop, TrafficLight, FireEscape, 긴급 차량 등.
- 실제 City 이름의 `Car_Ambo`, `Car_Police`, `Trashbin`, `CityHall` 등의 표현도 지원한다.
- 여러 규칙에 해당하면 대표 카테고리는 하나지만 관련 태그는 여러 개 저장한다.
- 검색은 입력 단어 모두가 이름·경로·분류·태그의 조합에 포함되는지 검사한다. 임베딩 의미 검색은 아니다.

## 테스트 방법

- Unity Test Runner의 EditMode에서 `AssetClassifierTests`, `AssetScannerTests` 실행.
- 기존 데이터베이스에서 Unknown 필터 → 태그 수정 → Save → 창 재열기 → 수정 유지 확인.
- Scan을 다시 실행해 수동 값과 335개 프리팹 참조가 유지되는지 확인.
- 다른 테스트 폴더를 지정하거나 스캔 취소 시 원본 프리팹/씬이 바뀌지 않는지 확인.
- 실제 스캔 로그: 프로젝트 상위 `EarthRecovery-city-scan.log`.
- 전체 테스트 결과: 프로젝트 상위 `EarthRecovery-scanner-tests.xml` 및 `.log`.

머티리얼 참조 누락 검사는 수행했지만, 모든 모델의 외형·URP 셰이더 렌더링을 개별 시각 검수한 것은 아니다.
