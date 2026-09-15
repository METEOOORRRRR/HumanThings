# 실측 기반 도시 템플릿

## 사용 방법

1. Tools > HumanThings > Build City Templates From Asset Database.
2. Database에 Assets/CityGeneration/CityAssetDatabase.asset을 지정.
3. Analyze Database: 통계와 의심 분류를 읽기 전용으로 분석.
4. Build Templates: 실측 설계, 보정 및 20 Seed 검증을 통과한 후보를 저장.
5. Validate Templates: 저장된 템플릿을 별도 Seed 10개로 다시 검사. 값은 수정하지 않음.
6. Rebuild All: 현재 DB로 다시 설계·검증 후 변경되지 않은 자동 에셋 갱신. 수동/수정된 에셋은 보존.

저장 폴더는 Assets/CityGeneration/AutoTemplates이다. City Block Generator의 기본 선택은 CatalogMixed이며, Measured Template Builder 버튼으로 분석 도구를 열 수 있다. 기존 수동 템플릿과 이전 시험용 프리셋은 삭제하지 않았다. 이전 고정 프리셋 생성 메뉴 대신 실측 도구를 사용한다.

원본 DB, 분류, 태그, 배치 metadata는 하나도 덮어쓰지 않았다. 명백한 의심 항목도 변경 이력과 작성 의도를 알 수 없어 경고만 남겼다.

## 실제 분석

| 분류 | 등록 | Metadata 유효 후보 |
|---|---:|---:|
| Building | 62 | 13 |
| Road | 49 | 38 |
| Vehicle | 9 | 9 |
| StreetProp | 112 | 61 |
| Transit | 6 | 6 |
| Nature | 12 | 10 |
| Structure | 26 | 11 |
| Unknown | 59 | 49 |

Metadata 유효는 최종 생성 대상이라는 뜻이 아니다. Unknown은 사용하지 않으며 교량 부품도 현재 평면 도로 생성에서 사용하지 않는다. 실제 도로 표면 후보는 5개, 차량 후보는 8개다.

- 건물 전체 평균 footprint: 8.698 x 8.279m. 유효 13개 평균: 9.283 x 8.958m.
- 유효 건물 평균 Bounds: 9.283 x 10.334 x 8.958m. footprint 축별 최소 5.000 x 5.224m, 최대 20.086 x 17.634m.
- 유효 건물 cornerCompatible: 2/13. frontFacesRoad: 100%. 전체 건물 cornerCompatible은 7/62.
- 실제 건물 용도 후보: 상점 8, 사무실 4, 공공 1, 주거 0.
- 도로 분류: Sidewalk 29, Other 15, Bridge 5. Straight/Corner/Intersection/TIntersection으로 직접 분류된 도로는 각각 0개다. Other 중 5x5m 표면 5개를 기존 fitted-surface 생성기가 사용한다.
- 차량: Car 5, Van 1, Emergency 2, Other 1, Bus/Truck 0. 전체 평균 Bounds 2.089 x 1.676 x 4.546m. Other 1개는 차량 배치 쿼리에서 제외된다.
- StreetProp: Lamp 6, Sign 53, Trash 9, TrafficLight 5, Utility 33, Barrier 5, Bench 1.
- Transit: BusStop 1, SubwayEntrance 2, Other 3.
- Structure: Fence 2, Stair 6, FireEscape 3, Rooftop 15.
- 지하철 입구의 도로 방향 정렬 후 깊이는 8.257m / 15.400m. 후자는 현재 최대 보도 폭 12m에 들어가지 않는다.

모든 도로 49개의 이름/Bounds/footprint, Unknown 59개 이름, 세부 통계와 원본 태그/방향/표면/참조는 MeasuredCityTemplates/Build.md 및 Build.json에 기록한다. Read-only 분석 단계에서도 Analysis.json을 별도로 출력한다.

의심 사례: SM_Prop_SidewalkPoles와 작은 Sidewalk_Panel이 Road로 분류됨, Station 건물에 Sidewalk 표면이 지정됨. 이는 정상적인 수동 설정일 수 있으므로 자동 수정하지 않았다.

## 산정 규칙

절대 길이를 임의로 정한 것이 아니라 실제 수량/크기를 입력으로 사용한다. 단, “어떤 성격의 도시를 만들 것인가”는 데이터만으로 결정되지 않으므로 아래 무차원 배율은 명시적인 설계 정책이다.

- 건물은 기존 CityLayoutAssets 및 runtime Usable 조건과 동일하게 조회한다. 꺼진 아파트 조각을 주거 후보로 계산하지 않는다.
- 기존 Solver의 회전 함수를 사용해 localForward/frontFacesRoad가 반영된 폭과 깊이를 구한다.
- 기본 도로 모듈: 사용 가능한 도로 표면의 짧은 변 중앙값, 현재 5m. 일반 도로는 2~2.5모듈, 대로는 3~3.5모듈이며 차량 폭 P90 + 양측 여유와 비교한다.
- 기본 보도: 비코너 보도 표면의 크기 중앙값 + 1m 여유. 교통형은 실제로 들어갈 수 있는 지하철 깊이 + 1m로 넓힌다.
- 건물 여유 1m는 기존 Solver의 0.5m 전면 여유와 0.4m containment margin을 올림한 값이다.
- 필지 기준 길이: max(건물 변 P80 + 2m, 평균 폭 + 평균 깊이/2 + 2m). 현재 16.154m.
- 기존 blockSize는 여러 건물이 들어가는 도시 블록 전체 길이가 아니라 필지 전면 분할 간격으로 사용된다. 이에 맞춰 기준 길이에 0.9~1.35를 적용한다. 전체 맵은 한쪽에 3~5개의 필지가 들어가는 길이와 도로/보도 단면을 비교해 계산한다.
- 밀도: 평균 건물 면적 / 여유 포함 면적을 기준으로 성격별 점유 배율을 적용하고 Seed 검증으로 확인한다.
- 용도 비율: 유효 후보 수 8:0:4:1을 기본으로 한다. Market은 상업 가중치 2, Office는 사무실 가중치 2. 없는 용도는 항상 0.
- 차량: 실제 길이 P90 + 2m를 한 대의 주차 길이로 삼아 도로 길이 대비 점유율을 계산한다.
- 소품: 해당 종류의 실제 후보 수와 건물 후보 수 비율로 초기 밀도를 정한 뒤 실제 배치 결과로 검증한다.
- 길이/개수 상한은 기존 Config Builder 안전 한도를 따른다. 새로운 도로 생성기를 만들지 않았다.

## 생성한 6종

| 템플릿 | 설계 이유 |
|---|---|
| CatalogMixed | 상점·사무실·공공 후보 비율을 따른 혼합 업무 구역. 주거 혼합으로 표시하지 않음 |
| SparseBlocks | 낮은 점유율, 골목 없음, 적은 차량·소품 |
| WideAvenue | 실제 도로 모듈을 더 사용한 넓은 도로, 큰 맵, 메인 1~2개, 많은 주차 |
| MarketBranches | 상점 후보 8종에 근거한 상업 가중치, 작은 필지, 높은 점유율과 골목 |
| OfficeBlocks | 사무실 4종에 근거한 업무 가중치, 넓은 필지, 평행 메인 도로 허용 |
| TransitCorridor | 짧은 지하철 입구가 들어가는 9.257m 보도, 지하철 최소 1개·버스정류장 최소 1개 |

| 템플릿 | 맵 폭 m | 도로 폭 m | 필지 기준 m | 점유 밀도 | 메인/사이드/골목 개수 |
|---|---|---|---|---|---|
| CatalogMixed | 109.6~125.8 | 10~12.5 | 16.2~18.6 | .54~.67 | 1 / 1~2 / 1~2 |
| SparseBlocks | 109.6~125.8 | 10~12.5 | 16.2~18.6 | .36~.44 | 1 / 1~2 / 0 |
| WideAvenue | 179.4~195.6 | 15~17.5 | 16.2~21.8 | .54~.67 | 1~2 / 2~3 / 0 |
| MarketBranches | 122.4~138.5 | 10~12.5 | 16.0~18.6 | .67~.81 | 1 / 1~2 / 1~3 |
| OfficeBlocks | 115.7~131.9 | 10~12.5 | 16.2~21.8 | .54~.67 | 1~2 / 1~2 / 1~2 |
| TransitCorridor | 122.6~138.8 | 10~12.5 | 16.2~18.6 | .54~.67 | 1 / 1~2 / 0~2 |

모든 실제 Range, 맵 깊이, 비율, POI/차량/소품 설정은 Build.json의 templates.values와 Inspector에 있다.

## Seed 검증

초기/보정 검증: 1009 + i*7919, i=0..9.
독립 검증: 1000003 + i*7919, i=0..9.

건물·차량·소품은 각 10 Seed 집계 성공률 80% 이상, 필수 POI는 95% 이상, 겹침/연결 오류 0을 통과 조건으로 사용했다. 개별 Seed 하나하나에 80%를 보장한다는 뜻은 아니다. 건물 요청에는 건물형 POI도 포함되고, StreetProps에는 Transit을 섞지 않는다.

| 템플릿 | 건물 | 차량 | StreetProps | 필수 POI | 비어 있는 필지 | 겹침 |
|---|---:|---:|---:|---:|---:|---:|
| CatalogMixed | 67/76, 88.2% | 31/31 | 369/375, 98.4% | 9/9 | 66/133 | 0 |
| SparseBlocks | 55/57, 96.5% | 11/11 | 121/121, 100% | 9/9 | 106/161 | 0 |
| WideAvenue | 175/194, 90.2% | 120/120 | 484/485, 99.8% | 9/9 | 170/345 | 0 |
| MarketBranches | 109/125, 87.2% | 41/41 | 544/612, 88.9% | 27/27 | 52/161 | 0 |
| OfficeBlocks | 91/110, 82.7% | 41/41 | 460/473, 97.3% | 9/9 | 100/191 | 0 |
| TransitCorridor | 77/88, 87.5% | 41/41 | 339/339, 100% | 49/49 | 77/154 | 0 |

빈 필지는 의도적인 낮은 밀도와 배치 실패를 모두 포함하므로 실패율과 다르다. 요청 0건은 N/A로 표시한다. 이 6종은 첫 설계로 통과해 자동 보정이 필요하지 않았다.

실패 시 최대 3회 보정 후 다시 검사한다. 건물 실패는 맵/필지 크기를 확대하고, 차량/소품 실패는 수요를 낮춘다. 차량 수요를 0으로 없애거나 교통형의 핵심 POI 최소치를 삭제해 통과시키지 않는다. 독립 검증까지 실패하면 저장하지 않으며 결과와 이유를 남긴다.

제외: ResidentialAlleys(완성형 주거 0개), PublicDistrict(공공 후보 1개). 15.4m 지하철 입구는 보도 최대 폭에 맞지 않지만 8.26m 입구가 있으므로 TransitCorridor 자체는 유효하다. DB에서 교통 에셋을 제거하면 교통형 생성도 제외한다.

최종 Unity 6000.3.21f1 EditMode 전체 테스트: 374개 통과, 실패 0개. 6개 자동 템플릿 각각을 임시 씬에 실제 생성해 건물/Renderer 생성 및 Missing Script 0을 확인했다. 직접 마우스로 Editor 창을 조작하거나 이번에 새 Player 빌드를 만든 것은 아니다.

테스트에는 DB 무변경, footprint 변경 시 계산값 변경, 교통 에셋 제거 시 후보 제외, 같은 분석의 fingerprint 재현, 수동 에셋 보존, 직접 수정한 자동 에셋 보존, 재생성 시 GUID 보존, 취소 시 파일 무변경, 보정 횟수 상한과 핵심 POI 최소치 보존을 포함한다. 로그는 프로젝트 상위 EarthRecovery-measured-tests.xml 및 EarthRecovery-measured-tests.log, 생성 로그는 EarthRecovery-measured-templates.log에 있다.

## 보존 및 제한

- autoGenerated, 생성기 ID, 원본 DB GUID, 원본 데이터 fingerprint, 생성 내용 hash를 저장한다.
- Rebuild All은 변경되지 않은 자동 템플릿만 갱신한다. 같은 에셋은 GUID를 유지해 참조를 보존하고, 불필요해진 자동 템플릿만 제거한다.
- 수동 템플릿 또는 생성 이후 수정한 템플릿은 덮어쓰거나 삭제하지 않는다. 이름이 충돌하면 별도 경로에 새 자동 에셋을 만든다.
- 분석/검증은 씬을 변경하지 않는다. 생성은 기존 City Block Generator에서 수행한다.
- 이번 결과는 dry run과 자동 테스트다. 새로운 실제 게임 빌드나 맵/미션 연결 변경은 하지 않았다.
- Renderer 모델을 의미적으로 이해하는 분류기는 아니다. 의심 분류를 원본에 자동 적용하지 않았으며 겹침은 기존 Bounds 기준이다.
- 작은 필지에 큰 사무실/공공건물이 들어가지 않는 실패는 남아 있다. 통과율은 명시된 Seed 집합 기준이며 모든 미래 Seed의 성공을 보장하지 않는다.
- 외부 AI/API/HTTP 호출을 추가하지 않았다.

## 파일

신규 Editor/CityTemplates:
CityAssetAnalysis.cs(통계·경고), CityTemplateDesigner.cs(측정 기반 수식·보정),
CityTemplateValidation.cs(Seed별 요청/실제 배치 지표),
CityTemplateAutoBuilder.cs(저장·소유권·보고서), CityTemplateAutoBuilderWindow.cs(UI).

수정: Scripts/CityGeneration/CityLayoutTemplate.cs(설명·근거·자동 생성 식별),
Editor/AssetScanner/HumanThingsCityBlockGeneratorWindow.cs(실측 템플릿 기본 선택·도구 연결),
Editor/AssetScanner/CityTemplateEditor.cs(기존 고정 프리셋 메뉴 비노출, 시험용 호환 메서드 유지).

신규: Tests/EditMode/MeasuredCityTemplateTests.cs, AutoTemplates 에셋 6개와 .meta, 본 문서 및 MeasuredCityTemplates/Build.json·Build.md.

기존 Config Builder, Road Generator, Lot Generator, Placement Solver, Scene Builder는 변경하지 않았다.
