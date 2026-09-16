# 배경 건물 후보 확장 및 조립 검토

## 결과

사용 가능한 건물 후보 **13개 → 33개**. 기존 완성형 13개를 보존하고 검토한 조립 프리팹 20개를 추가했다. 구형 오피스만 반복되던 고층 후보에 사각/원형/팔각 오피스와 3~5층 아파트가 추가된다. 메시를 절단하거나 새 텍스처를 만들지 않고 원본 프리팹을 자식으로 조립했다. 원본 Synty 파일은 수정하지 않았다.

| 추가 계열 | 개수 | 조합 |
|---|---:|---|
| Apartment | 6 | 3가지 외벽, 10m/15m 폭, 3/4/5층 |
| OfficeSquare | 4 | 큰 창/작은 창, 저층/고층 외벽에 출입층과 지붕 |
| OfficeRound | 3 | 저층 2종, 고층 1종에 출입층과 지붕 |
| OfficeOctagon | 3 | 중간층 반복 2/4/6개, 출입층 및 상부 지붕층 |
| OfficeOld | 4 | Small/Large 각각 중간층 반복 2/4개 |

Office 계열 이름의 F는 조립한 중간 모듈 수이며 출입층/지붕층을 포함한 건축학적 총 층수를 뜻하지 않는다. 원본 치수와 스케일을 유지했다.

## 실제 원본 확인

아파트/오피스 관련 원본 45개를 네 방향에서 렌더링했다. `Sources.txt`에 각 이미지의 행과 실제 bounds가 있으며 `Source-00.png`부터 `Source-11.png`가 근거다.

- 구형 OfficeOld 완성형 4개는 지붕과 출입층이 포함되어 있었고 기존 등록을 유지했다.
- 사각/원형 오피스는 큰 건물처럼 보여도 외벽만 있는 모듈이었다. 지붕/출입층 없이 단독 후보로 복귀시키지 않았다.
- 팔각 오피스도 층 외벽과 기둥 모듈이어서 기단 및 지붕층이 필요했다.
- 아파트는 5m 격자의 단층/코너/문/지붕 모듈이었다. 외벽 코너를 네 방향으로 돌려 닫힌 외곽을 만들고, 폭이 넓은 조합은 전후 중앙에 직선 모듈을 채웠다. 출입문은 지상층에만 있다.
- 이번에 제외 목록에서 그대로 복귀시킬 추가 완성형은 발견하지 못했다. 모듈 원본은 계속 배치 불가이며, 완성된 조립 프리팹만 추가했다.

`Joints.txt`에는 모델 정점 높이와 원본 Demo 씬의 조립 위치를 기록했다. Square 출입층 위 3.5m, Round 6m, Octagon 3.75m가 실제 데모의 접합 기준이다. Apartment는 3m 격자다. 단순 renderer bounds의 최대 높이를 기단 접합 높이로 잘못 사용하는 것을 피했다.

## 완성도 검토

20종 모두 정면/후면 사선, 상부, 하부 방향 캡처를 직접 확인했다. 검사 대상은 빈 지붕, 떠 있는 층, 어긋난 모서리, 잘못된 문 높이, 돌출된 미완성 기둥, 과한 비율 변형이다. 내부 장식 경계의 의도된 소량 겹침은 원본 조립 규격을 따랐다.

실제 MeshCollider를 임시 생성해 외벽 16방향에서 여러 높이와 각 층 접합부 ±0.025m를 검사했다. 지붕 안쪽은 9개 위치에서 하향 검사했다. 최종 20종 모두 표본 외벽/지붕 ray miss 0. 최종 로그는 `Registration.txt`에 있다. 초기 `AssemblyAudit.txt`의 일부 roof miss는 검사 길이 3m가 지붕 난간에서 옥상 바닥까지 닿지 못한 검사 문제였으며, 최종 검사는 실제 최상부 모듈의 하단까지 측정한다.

이 검사는 표본 외피 검사이지 모든 삼각형에 대한 수학적 수밀성 증명은 아니다. 배경용으로 내부 진입은 지원하지 않으며, 지하에서 보는 일부 원본의 바닥 뒷면은 렌더링되지 않을 수 있다. 지상 외관과 닫힌 상부를 검토했다. 원형 건물도 보수적인 박스 충돌을 사용하므로 원형 외곽의 모서리 빈 공간 일부가 충돌 영역에 포함된다.

## 프리팹별 네 방향 캡처

| 계열 | 검토 이미지 |
|---|---|
| 아파트 1 | [10m](HT_Building_Apartment_1_10m_3F.png), [15m](HT_Building_Apartment_1_15m_3F.png) |
| 아파트 2 | [10m](HT_Building_Apartment_2_10m_4F.png), [15m](HT_Building_Apartment_2_15m_4F.png) |
| 아파트 3 | [10m](HT_Building_Apartment_3_10m_5F.png), [15m](HT_Building_Apartment_3_15m_5F.png) |
| 사각 오피스 | [01](HT_Building_OfficeSquare_01.png), [02](HT_Building_OfficeSquare_02.png), [03](HT_Building_OfficeSquare_03.png), [04](HT_Building_OfficeSquare_04.png) |
| 원형 오피스 | [01](HT_Building_OfficeRound_01.png), [02](HT_Building_OfficeRound_02.png), [04](HT_Building_OfficeRound_04.png) |
| 팔각 오피스 | [2](HT_Building_OfficeOctagon_2F.png), [4](HT_Building_OfficeOctagon_4F.png), [6](HT_Building_OfficeOctagon_6F.png) |
| 구형 Small | [2](HT_Building_OfficeOld_Small_2F.png), [4](HT_Building_OfficeOld_Small_4F.png) |
| 구형 Large | [2](HT_Building_OfficeOld_Large_2F.png), [4](HT_Building_OfficeOld_Large_4F.png) |

## 배치 결과

| World Seed | 배경 건물 수 | 사용한 프리팹 종류 | 미션 POI |
|---|---:|---:|---:|
| 100 | 58 | 19 | 15 |
| 200 | 62 | 18 | 15 |
| 300 | 66 | 17 | 15 |
| 400 | 86 | 22 | 15 |
| 500 | 67 | 18 | 15 |

동일 씨드를 두 번 생성해 건물 프리팹 GUID와 좌표 순서가 동일함을 확인했다. 기존 World/POI 배치 검증을 통과했다. 새 20종 모두가 매 판 나오는 규칙은 아니며, 필지 크기에 맞는 후보 중 랜덤 선택한다. 큰 원형은 seed 200에서, 팔각은 100/500에서 선택됐다. 아파트는 다섯 씨드 모두 나왔다. 통계 전체는 `WorldValidation.txt`에 있다.

`World-100-Aerial.png`부터 `World-500-Aerial.png`는 배치 확인용 중립 조명이다. 같은 이름의 `Street.png`는 기존 게임 비주얼과 폐허 표면을 적용한 시점이다. HUD 없는 Editor 카메라 렌더이며 실제 플레이 화면 전체 캡처는 아니다.

## 변경 범위와 유지 관리

- 신규: `Assets/CityGeneration/Buildings`의 완성 프리팹 20개 및 meta, `CityBuildingAssembly.cs`, `BuildingCatalogAudit.cs`, `CityBuildingCatalogBuilder.cs`, `BuildingCatalogTests.cs`.
- DB: `CityAssetDatabase.asset`에 20개 추가. 원본 335개 항목의 ID와 참조 유지, 총 355개.
- Metadata/Eligibility: 검토 완료 표시가 있는 조립 프리팹만 사용할 수 있다. 미검토 초안은 수동으로 placementEnabled를 켜도 배치되지 않는다.
- Scanner: Synty 폴더를 재스캔해도 폴더 밖의 승인된 조립 프리팹은 보존한다. 일반 에셋의 삭제/이동 처리는 기존대로다.
- Template: CatalogMixed/SparseBlocks에 주거 가중치 0.18~0.22를 추가하고 기존 용도 가중치를 0.8배로 조절했다. 도로/필지 치수/건물 밀도/미션 POI/맵 구조는 변경하지 않았다.
- Test: 과거 335개/완성 건물 13개/주거 0개를 가정한 검사를 새 카탈로그에 맞게 갱신했다. 후보 누락 경고 검사는 주거 후보를 제거한 테스트 DB로 계속 검증한다.
- 원본 재질과 메시, 미션 장소, 기존 폐허 프로필은 수정하지 않았다. 새 배경 건물에도 기존 폐허 표면이 적용된다.

Editor 메뉴 `Tools > HumanThings > Buildings > Build Review Drafts`는 검토용 초안을 만든다. 이미 승인된 같은 이름의 프리팹은 덮어쓰지 않는다. 새로운 조합은 별도 이름으로 만든 뒤 이미지와 검사 결과를 검토하고 등록해야 한다.

## 최종 테스트 및 빌드

- EditMode: 443/443 통과. 조립 외피, 미검토 후보 차단, 원본 폴더 재스캔 시 조립 후보 보존 포함.
- PlayMode: RuntimeCityTests 1/1 통과. 씨드 100/200/300/400/500의 미션 장소 15곳씩, 총 75곳의 단말기 접근과 위치 동기화 검증.
- 정식 ProjectBuilder.Build 성공, 컴파일/빌드 오류 0. `Builds/Latest/EarthRecovery.exe`에 반영. Latest와 이전 빌드 3개 보존 검사 통과.
- 실제 Latest 실행 파일에 `--dev-solo --city-smoke`를 사용한 별도 호스트/클라이언트 연결 테스트 모두 통과. seed=100, players=2, facilities=15. 결과와 카메라 렌더는 `Player` 폴더에 있다.
- 두 플레이어 로그에 D3D12 업로드 버퍼 크기 경고(16MB 버퍼에 64MB 요청)가 각 1회 발생했다. 테스트 완료를 막지는 않았으며 런타임 예외는 없었다. 장시간 GPU 메모리/프레임 성능 검증은 이번 외관 및 배치 검증에 포함하지 않았다.
- 초기 테스트 실패 3개는 이전 DB 개수/주거 후보 부재를 가정한 검사와 신규 검사의 구조물 필터 범위였다. 새 후보 구조에 맞게 고친 뒤 전체를 재실행해 통과했다.
