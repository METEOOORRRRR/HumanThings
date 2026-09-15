# HumanThings City Visual Pass

## 1. Rendering 환경

Unity 6000.3.21f1, URP, PC_RPAsset / PC_Renderer, Forward+, Linear Color Space를 유지했다. HDR 활성, MSAA 비활성 상태에서 게임 카메라에 FXAA와 후처리를 적용했다. 기존 SSAO(intensity 0.4, radius 0.3)는 그대로 사용한다. 기존 그림자 설정은 2048, 4 cascades, distance 50이다.

기존 WorldView는 평면 환경광, ExpSquared fog 0.013, 방향광 1.05, 후처리 비활성 카메라를 사용했다. 생성 도시의 재질 슬롯에서 Synty/Generic_Basic 2323개, URP/Lit 94개, TMP 16개를 확인했다. 이는 고유 재질 수가 아니다. Synty Shader Graph의 팔레트 텍스처 구조를 유지했다.

## 2. 생성 및 수정 파일

| 파일 | 역할 |
|---|---|
| Assets/Scripts/Visual/HumanThingsVisualProfile.cs | 비주얼 설정 ScriptableObject |
| Assets/Scripts/Visual/HumanThingsVisualTreatment.cs | 재질 복제, 조명/안개/Volume 적용과 복원 |
| Assets/HumanThings/Visual/Shaders/WeatheredEnvironment.shader | URP 환경 표면 셰이더 |
| Assets/HumanThings/Visual/Resources/HumanThingsVisualProfile.asset | 런타임 공통 프리셋 |
| Assets/HumanThings/Visual/Textures/SurfaceDetail.asset | 교체 가능한 128x128 procedural detail texture |
| Assets/Editor/CityTemplates/HumanThingsVisualTools.cs | 프로필 생성, 원본 전환, 비교 렌더링 |
| Assets/Scripts/WorldView.cs | 도시 완성 후 비주얼 프로필 적용 호출 추가 |
| Assets/Tests/EditMode/VisualTreatmentTests.cs | 원본 보존, 복원, 셰이더/프로필 검사 |
| Assets/Tests/EditMode/EarthRecovery.Tests.asmdef | 테스트용 URP/Core 참조 추가 |
| Docs/VisualPass | 비교 PNG 24장, 갤러리, 설정 JSON, 측정 및 QA 도구, 이 보고서 |

새 Unity 파일의 meta도 함께 생성했다. 이번 작업에서 generator, placement solver, world layout, POI, 임무, 몬스터, 아이템 스폰 로직과 원본 Synty 에셋은 수정하지 않았다.

## 3. Material 처리

원본 Material과 Renderer 연결을 저장하고, 원본 재질과 표면 종류 조합별로 공유 복제본을 만든다. 복제본은 DontSave이며 원본 Asset을 덮어쓰지 않는다. 도시 제거 및 Editor Original 전환 시 원래 참조와 환경 설정으로 복원한다. 텍스트 재질은 제외한다. 투명 재질은 원본 셰이더를 유지하여 유리 표현을 일괄 불투명 처리하지 않는다.

## 4. Shader 구조

원본 팔레트 UV/albedo + 카테고리 tint/desaturation + 작은 packed detail texture의 world-space triplanar 표본 + 수직 물때 + 하부 dirt/moss + 금속 rust + 약한 normal 변화 + 수평면 wet mask 구조다. URP PBR, 그림자, depth, depth-normal 및 SSAO를 지원한다. 메시 변형이나 새로운 메시/데칼 배치는 없다.

불투명 표면 환경광은 프로필의 하늘/수평/지면 색을 법선에 따라 보간한다. 즉시 생성 씬에서 SH 캐시가 검게 출력되던 문제를 피하기 위한 단순한 hemisphere GI이며, 실제 baked GI를 대체할 고급 조명 시스템은 아니다. 기존 셰이더에는 RenderSettings ambient probe를 제공한다.

## 5. Palette

재질 saturation 0.30. 콘크리트는 냉회색, 금속은 청회색, 벽돌은 탁한 갈색, 식생은 낮은 채도의 녹색이다. 차량은 원래 색을 낮은 채도로 유지한다. 종류 판단은 renderer 이름 기반이므로 예외적인 에셋 이름은 추후 개별 override가 필요할 수 있다.

## 6. Roughness / Wetness

Smoothness는 콘크리트 0.08, 도로 0.12, 금속/차량 0.24로 낮췄다. 도로 wetness 0.45, 콘크리트는 그 20%만 적용한다. 젖음 마스크가 있는 수평면만 약간 어두워지고 smoothness가 0.48 방향으로 증가한다. 전면 거울 반사, SSR, 실제 물웅덩이는 구현하지 않았다. Dirt 0.35, detail 0.12, normal 0.012로 로우폴리 형태와 충돌하지 않도록 억제했다.

## 7. Lighting

완전한 밤 대신 흐린 황혼을 선택했다. 방향광 Euler(28,-35,0), intensity 1.05, RGB(0.86,0.90,0.92), shadow strength 0.8, bias 0.035, normal bias 0.25, soft shadows. 환경광은 하늘(0.42,0.46,0.48), 수평(0.28,0.30,0.31), 지면(0.13,0.14,0.15), reflection intensity 0.25다. Skybox 대신 안개색 배경을 사용한다.

캠프 및 일부 고정 순서 POI에 최대 4개의 그림자 없는 약한 포인트 라이트만 추가한다. intensity 1.8, range 7. 이는 조명만 추가하며 POI 위치나 게임 로직을 바꾸지 않는다.

## 8. Fog

URP 거리 안개 ExpSquared, density 0.0065, RGB(0.395,0.410,0.415). 가까운 도로와 외벽을 읽을 수 있게 유지하고 먼 건물의 대비를 줄인다. Height fog와 volumetric fog는 사용하지 않았다.

## 9. Post Processing

ACES, exposure +0.1, saturation -40, contrast 9, temperature -2. Bloom 0.06/threshold 1.4/scatter 0.35, vignette 0.10/smoothness 0.65. 기존 SSAO는 유지한다. Film Grain, Depth of Field, SSR, volumetric 효과는 추가하지 않았다.

## 10. Profile 및 사용법

HumanThingsVisualProfile에 재질 색감/오염/젖음/환경광/방향광/안개/노출/후처리/국소 조명 값을 모았다. AO는 기존 Renderer 설정을 유지하므로 이 프로필에서 중복 제어하지 않는다. Resources 참조를 통해 실제 인게임 도시에도 자동 적용한다.

Editor에서 생성된 도시 씬을 열고 `Tools > HumanThings > Visual > Compare Original and HumanThings`를 선택한다. `Select Profile`로 값을 편집한 뒤 `HumanThings Visual / Refresh`를 누른다. `Original Visual`로 복원한다. Play Mode는 인게임 자동 적용을 사용한다. Editor 프리뷰는 창을 닫거나 저장할 때 복원되어 임시 재질을 씬에 저장하지 않는다.

## 11. 화면 확인과 반복 조정

같은 StandardCity seed 100, 같은 네 카메라로 원본 및 5단계 누적 적용을 렌더링했다. PNG는 Unity 카메라 출력이며 외부 색 보정을 하지 않았다. 단계 1에도 새 셰이더의 hemisphere 환경광 모델은 포함되므로 조명 변화의 완벽한 단일 변수 실험은 아니다.

| 문제 | 변경 및 이유 |
|---|---|
| 초기 PNG가 실제보다 어두움 | linear half-float 캡처를 sRGB ARGB32로 수정. 미술 설정 문제가 아닌 캡처 문제로 구분 |
| 전경의 검은 뭉침 | 환경광 전달 수정, 중간 밝기 복원 |
| 원경 가시성 과도한 감소 | fog 0.013에서 0.0065로 완화 |
| 외벽의 얼룩/노이즈 과다 | detail 0.28에서 최종 0.12, normal 0.05에서 0.012로 감소 |
| 도로 시점 바닥이 너무 어두움 | 도로 reflectance floor 0.045에서 0.105, road tint 밝기 증가 |
| 푸른 기운과 색이 예상보다 강함 | 후처리 saturation -12에서 -40, temperature -5에서 -2, 안개색 중립화 |

하단 60% 픽셀을 일정 간격으로 측정한 평균 saturation은 Street 0.08→0.06, Boulevard 0.10→0.04, Detail 0.09→0.05, Overview 0.08→0.04다. 마지막 보정 전후 도로 시점의 어두운 픽셀 비율(luma<0.06)은 약 47%에서 11%로 감소했다. 이 수치는 미술 품질 점수가 아니라 검은 뭉침과 채도 확인용이다. 최종 네 시점에서 magenta 픽셀은 검출되지 않았다.

## 12. 가장 큰 차이

팔레트 저채도화와 후처리가 원본의 장난감 같은 색감을 가장 크게 바꾼다. 측면 명암과 거리 안개가 그 다음으로 공간 깊이를 만든다. 거칠기와 오염은 가까이서 보조적으로 작용한다. 젖음은 절제되어 있으며 특정 각도에서만 드러난다.

## 13. Self Review 및 한계

| 질문 | 실제 화면 판단 |
|---|---|
| 장난감 느낌이 남는가? | 색감/광택은 감소했으나 반복 로우폴리 건물 형태는 남음 |
| 길이 안 보일 만큼 어두운가? | 최종 보정 후 테스트 시점의 도로/보도 구분 가능. 모든 상황의 적 식별까지 검증한 것은 아님 |
| 색이 단조로운가? | 청회색 중심이며 벽돌/식생 색을 약하게 남김. 일부 시점은 여전히 회색 비중이 높음 |
| 안개가 깊이를 만드는가? | 전경 외벽과 원경 실루엣의 대비 분리 확인 |
| 표면이 너무 깨끗한가? | 하부 얼룩/미세 오염 추가. 큰 파손이나 국소 누수 표현은 부족 |
| 실사 디테일과 충돌하는가? | 노이즈/normal 강도를 낮춰 충돌을 줄임 |
| 평평한 시점이 있는가? | 넓고 빈 부지를 향한 시점은 여전히 평평함. 이번 범위에서는 배치 변경하지 않음 |
| 근거리 재질이 단순한가? | 원본보다 변화가 있지만 공통 procedural 마스크의 한계가 있음 |

정밀 GPU 성능 비교는 수행하지 않았다. 동적 조명은 최대 4개, 그림자 없는 방식으로 제한하고 공유 복제 재질, 작은 공통 텍스처를 사용했다. 저사양 하드웨어에서 추가 비용을 별도 측정해야 한다.

## 14. 다음 Visual Pass

우선순위는 외벽별 누수/녹 위치를 의도적으로 정리한 마스크, 선택적 창문 emissive, 입구 조명 위치의 수동 미술 검수다. 그 뒤 실제 플레이 중 적/상호작용 가독성과 GPU profiler를 확인하는 편이 좋다. 건물 밀도와 반복성 개선은 별도 승인된 배치 작업에서 다룬다.

## 검증

EditMode 398개 통과. 최종 셰이더 비교 렌더링 성공. PlayMode RuntimeCityTests 통과: 씨드 5개, 장소 15개씩, 75개 시설 단말 접근 및 로비 복귀 검사. 갤러리 데스크톱/모바일 레이아웃과 이미지 렌더링 확인.

Windows 빌드 성공: `Builds/Latest/EarthRecovery.exe`. 최신 포함 3개 빌드 유지 확인. 실제 실행 파일의 호스트/클라이언트 양쪽에서 `CITY_PLAYER_PASS seed=100 players=2 facilities=15` 확인. 실행 로그에서 예외/셰이더 오류는 발견하지 못했다. `Player/host-ingame.png`, `Player/client-ingame.png`는 실행 파일의 3D 카메라 렌더 출력이며 HUD가 포함된 전체 화면 캡처는 아니다. 테스트 프로세스는 정상 종료했다. 전체 임무 완료 및 모든 하드웨어/시점에서의 가독성 검증까지 포함하는 결과는 아니다.
