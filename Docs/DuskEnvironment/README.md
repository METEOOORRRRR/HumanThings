# 진한 황혼 환경

## 현재 방향: 넓은 주황 노을

사용자가 얇은 지평선 띠 방향을 취소했다. 현재는 참고 이미지처럼 한쪽 하늘의 아래 영역에 선명한 주황 노을이 넓게 펼쳐지고, 위쪽 및 반대 방향에는 어두운 청남색이 남는 구성이다. 지상 환경광, 고정 노출 -0.2, 안개, 조명, 맵 배치는 유지했다. 태양 원반은 없다. 아래의 '후속 조정' 절들은 이전 시안의 작업 이력이며 현재 수치는 주요 값 표를 따른다.

세로 감쇠 폭은 0.3, 방위 폭은 한쪽 85도로 확대했다. 면적이 넓어진 대신 최고 잔광 강도는 0.85에서 0.48로 낮췄다. 구름은 노이즈 4단계와 길게 늘어진 형태로 세분화하고 혼합 강도 0.8로 조정하여 넓은 주황 배경을 어두운 구름이 가리도록 했다. 이는 실제 체적 구름이 아닌 기존 Skybox 표현이다.

[이전 띠 방식](Before-Broad-Sunset.png) / [현재 넓은 노을](Dusk-Afterglow.png).

이번 변경 검증: 같은 씨드/시점 전후 캡처 5쌍 및 메시/좌표 불변 검사 통과. 노을 방향 이미지의 지상(y>=552) 4픽셀 간격 표본 34,800개 모두 동일. VisualTreatmentTests 4/4 통과. Latest 빌드 오류 0, 최신 + 이전 3개 유지 확인. 아래 445개 전체 테스트 및 2인 실행 기록은 직전 채도 보정 버전의 결과이며, 이번 변경에서는 관련 4개 테스트와 렌더/빌드를 재검증했다.

## 구현 범위

이 프로젝트는 Unity 6000.3 / URP다. Unreal 액터나 Blueprint를 추가하지 않고 기존 `HumanThingsVisualTreatment`와 `HumanThingsVisualProfile`을 확장했다. 게임 진입 시 기존 WorldView의 Directional Light, 카메라, 환경 처리와 글로벌 Volume을 재사용한다. 재적용/해제 시 하늘 머터리얼을 해제하고 원래 환경 및 카메라 설정을 복원한다.

- 하늘: 태양 원반이 없는 URP Skybox 셰이더. 위쪽 청회색/남색과 한쪽 아래 하늘의 넓은 주황 노을.
- 구름: 고정된 월드 방향의 절차적 하늘 레이어. Unreal Volumetric Clouds에 해당하는 실제 체적 구름은 아니다.
- 안개: 기존 URP ExponentialSquared 안개. 푸른 원경 혼탁감을 주되, 체적 안개나 높이별 안개 시뮬레이션은 추가하지 않았다.
- 노출: 기존 ColorAdjustments.postExposure의 고정값. 적응형/자동 노출을 추가하지 않았다. 카메라가 어두운 곳을 보아도 노출 값은 변하지 않는다.
- 국소 조명: 기존 입구 조명 예산 4개, 강도 1.8, 범위 7m, 색과 위치 및 플레이어 조명을 유지했다. 꺼진 모든 가로등/간판을 새로 점등하는 기능은 추가하지 않았다.
- 원본 메시/재질, 폐허 프로필, POI, 맵 생성, 프리팹 후보, 씨드, 배치, UI 및 게임플레이는 변경하지 않았다. 표면 셰이더에 전달하는 환경광만 기존 경로로 갱신한다.

## 주요 값

`Assets/HumanThings/Visual/Resources/HumanThingsVisualProfile.asset`을 Inspector에서 선택한다. 변경 후 다음 탐사 시작 시 적용된다. Play Mode 밖의 기존 `Tools > HumanThings > Visual > Compare Original and HumanThings` 창에서도 다시 적용할 수 있다.

캐릭터 기본 머터리얼은 환경광 값의 저장본도 가지고 있다. 환경광 색상을 변경한 뒤 `Tools > HumanThings > Visual > Sync Character Ambient Lighting`을 실행하면 환경광 3개만 동기화한다. 캐릭터 텍스처/색감/풍화 파라미터는 변경하지 않는다.

| 항목 | 현재 값 | 의미 |
|---|---|---|
| lightRotation | (-4, -35, 0) | 태양 고도 -4도, 잔광 방향과 연동 |
| lightIntensity | 0 | 지평선 아래 직사광 차단. 땅 아래에서 비추는 현상 방지 |
| lightColor | (0.92, 0.87, 0.82) | 보존된 약한 따뜻한 색. 직사광 0이므로 지상을 물들이지 않음 |
| ambientSky | (0.40, 0.44, 0.53) | 위쪽 환경광 |
| ambientEquator | (0.38, 0.40, 0.46) | 벽면 가독성을 지키는 차가운 환경광 |
| ambientGround | (0.16, 0.18, 0.23) | 아래쪽 환경광 |
| exposure | -0.2 EV | 고정 노출 |
| skyBrightness | 1 | 하늘만의 밝기 배율 |
| afterglowStrength | 0.48 | 넓은 노을의 최고 강도, 이전 띠보다 낮춤 |
| horizonSaturation | 1 | 후처리 이후 노을 영역의 채도 보정. 0이면 기존 흐린 표현 |
| afterglowHeight | 0.3 | 방향 벡터 높이 기준 세로 감쇠 폭, 약 17.5도 규모 |
| afterglowWidth | 85도 | 잔광 중심에서 한쪽까지의 각도 범위 |
| afterglowColor | (1.00, 0.56, 0.00) | 후처리를 고려한 선명한 주황 잔광 색 |
| fogDensity | 0.003 | 기존 0.0065보다 얇은 원경 안개 |
| fogColor | (0.14, 0.17, 0.225) | 차가운 저녁 대기 |
| cloudCoverage / cloudOpacity | 0.5 / 0.8 | 구름 분포와 혼합 강도 |

환경광 수치만 비교하면 일부는 기존보다 높다. 태양 직사광 1.05를 0으로 제거했으므로, 벽면/도로가 검게 뭉개지지 않게 확산광을 보완한 것이다. 결과 지상은 이전보다 어둡다.

## 조절 방법

- 전체를 조금 밝게: `exposure`를 -0.2에서 0~0.2로. 더 어둡게: -0.4~-0.6으로. 먼저 0.2 EV 단위로 조절한다.
- 하늘은 유지하고 건물/도로만 밝게: `ambientEquator`, `ambientSky`의 RGB를 같은 비율로 약 10% 올린다. 더 어둡게는 반대로 줄인다. 원본 표면 밝기나 폐허 강도는 변경하지 않는다.
- 주황 노을만 강하게/약하게: `afterglowStrength`를 0.48에서 0.6 / 0.35로. 0이면 주황 노을이 사라진다. 면적은 `afterglowHeight`와 `afterglowWidth`로 별도 조절한다.
- 더 밤처럼: 잔광 강도를 0.15, `skyBrightness`를 0.8 정도로 내린 뒤 필요할 때만 노출을 -0.4로 낮춘다. 가독성이 부족하면 환경광은 유지한다.
- 잔광 방향: `lightRotation.y`. 태양 고도 X는 -2~-6도, 직사광 강도는 0 유지 권장. 하늘은 물리 산란 시뮬레이션이 아니므로 고도만 내려서 자동으로 야간이 되지는 않는다.
- 원경을 더 흐리게: `fogDensity`를 0.0035~0.004로 소폭 올린다. 노출 대신 안개를 크게 올려 밝기를 조절하지 않는다.

## 수정 파일

| 파일 | 역할 |
|---|---|
| Assets/Scripts/Visual/HumanThingsVisualProfile.cs | 기존 프로필에 하늘/잔광/구름 제어값 추가 |
| Assets/Scripts/Visual/HumanThingsVisualTreatment.cs | 기존 환경 적용/복원 경로에 Skybox 연결 |
| Assets/HumanThings/Visual/Resources/HumanThingsVisualProfile.asset | 실제 황혼 파라미터 및 셰이더 참조 |
| Assets/HumanThings/Visual/Shaders/DuskSky.shader | 태양 원반 없는 황혼 및 구름 렌더링 |
| Assets/Editor/CityTemplates/HumanThingsDuskTools.cs | 동일 지형/좌표/카메라 전후 캡처 및 불변성 검사 |
| Assets/Tests/EditMode/VisualTreatmentTests.cs | 셰이더/고정노출/중복 방지/복원 회귀 검사 |
| Assets/Scripts/Visual/HumanThingsHorizonSaturation.cs | URP 기본 FullScreenPass 확장. 처리 중인 카메라만 채도 보정, 종료 시 임시 재질 해제 |
| Assets/HumanThings/Visual/Shaders/HorizonSaturation.shader | 깊이/방위/높이 마스크로 하늘 잔광만 휘도를 보존하며 채도 보정 |
| Assets/HumanThings/Visual/HorizonSaturation.mat | 보정 패스의 원본 머터리얼 |
| Assets/Settings/PC_Renderer.asset | 기존 SSAO 유지, 후처리 이후 채도 보정 패스 1개 추가 |
| Assets/HumanThings/Characters/Visual/Materials/HT_NeonVanguard.mat | 저장된 환경광 색상 3개만 동기화 |
| Assets/HumanThings/Characters/Visual/Materials/HT_ToxicBunny.mat | 저장된 환경광 색상 3개만 동기화 |

새 셰이더와 Editor 도구의 Unity meta 파일도 포함한다. 별도 환경 컨트롤러나 새 Directional Light는 추가하지 않았다.

## 시각적 비교

씨드 200, 같은 월드 인스턴스와 같은 카메라의 전후 렌더. 캡처에만 보조 바닥을 사용했으며 맵 에셋에는 저장하지 않았다. 이는 HUD 없는 Editor 검증 렌더다. 표면/조명 외의 메시와 변환 행렬이 동일함을 확인했다.

| 시점 | 이전 | 황혼 |
|---|---|---|
| 건물과 도로 | [이전](Before-Street.png) | [황혼](Dusk-Street.png) |
| 도로 방향 | [이전](Before-Road.png) | [황혼](Dusk-Road.png) |
| 잔광 방향 | [이전](Before-Afterglow.png) | [황혼](Dusk-Afterglow.png) |
| 반대 방향 | [이전](Before-Opposite.png) | [황혼](Dusk-Opposite.png) |
| 캠프 주변 | [이전](Before-Camp.png) | [황혼](Dusk-Camp.png) |

첫 시안은 지상이 과도하게 어두워 폐기하고, 환경광과 하늘 밝기를 조정한 결과로 위 이미지를 갱신했다.

후속 조정: 지상/노출/환경광/안개/구름은 유지하고 잔광 색상, 강도, 두께만 변경했다. 띠는 0.035에서 0.012로 좁히고 강도는 0.35에서 0.5로 올려 지평선 바로 위에서만 주황빛을 조금 더 식별할 수 있게 했다. [직전 황혼](Previous-Dusk-Afterglow.png)과 [현재 황혼](Dusk-Afterglow.png)을 같은 시점에서 비교할 수 있다.

콘셉트 이미지 반영 후속 조정: 띠의 두께 0.012와 방위 폭 40도는 유지하고 색상을 (1, 0.56, 0), 강도를 0.85로 변경했다. 기존 전체 채도 -40/노출 -0.2를 그대로 두고 잔광 자체의 색과 강도만 조정했다. [직전의 옅은 잔광](Muted-Horizon.png)과 [선명한 주황 잔광](Dusk-Afterglow.png) 비교. 새 태양 원반, 조명, 배치 변경은 없다.

### 채도만 복원한 최종 보정

전체 채도 -40과 ACES가 잔광에도 적용되어 살구/회색빛으로 변하는 원인을 확인했다. 하늘 전체 후처리나 노출을 바꾸는 대신 URP 기본 FullScreenPass를 후처리 뒤에 추가했다. 카메라 깊이로 지형/건물/차량을 제외하고, 기존 잔광 방향 및 높이 마스크 안에서만 RGB의 무채색 성분을 제거한다. Rec.709 선형 휘도를 원래 값으로 정규화하고 색역 초과도 휘도를 유지하며 제한한다. 띠 크기/잔광 강도/노출은 직전 버전과 같다. 적용 카메라 등록을 해제하면 패스가 실행되지 않는다.

- 채도 조절: `horizonSaturation` 0~1. 현재 1. 밝기나 노출 대신 이 값으로 채도를 조절한다.
- [보정 직전](Before-Saturation-Afterglow.png) / [최종](Dusk-Afterglow.png).
- 1600x900 이미지에서 4픽셀 간격 비교: 변화 1,217개 표본, 지상(y>550) 0개, 상단 하늘(y<440) 0개. 하늘 띠 영역에만 변화가 있었다.
- 픽셀 (700,503): RGB (173,123,97) -> (200,112,26). HSV 채도 약 44% -> 87%. sRGB를 선형으로 변환한 Rec.709 휘도 0.239132 -> 0.239423, 약 0.12% 차이(8비트 양자화 포함).
- 픽셀 (900,504): 채도 44% -> 약 94%, 휘도 차이 약 0.52%. 보정 공식은 휘도를 보존하며 캡처 수치는 양자화/마지막 필터링 오차를 포함한다.
- 보정 패스 추가 후 전체 EditMode 445/445 통과. 등록/해제, 중복 패스 없음, 깊이 요구, 후처리 뒤 실행, 셰이더 컴파일 검사 포함.
- 보정 패스를 포함한 Latest 빌드 성공(오류 0), Latest + 이전 3개 유지 확인. 실제 호스트/클라이언트 모두 CITY_PLAYER_PASS, 런타임 예외/셰이더 오류 없음. 이번 실행 결과 및 카메라 렌더는 `SaturationPlayer` 폴더에 있다.
- 화면 색상 복사 및 전체화면 패스 1개가 추가된다. 장시간 GPU 성능 측정은 별도 수행하지 않았다.

### 잔광 영역과 그라데이션 확대

현재 주황색/채도/중심 강도는 유지하고 좌우 범위만 40도에서 52도로, 세로 그라데이션 폭은 0.012에서 0.02로 소폭 확대했다. 기존 좌우 smoothstep 및 세로 Gaussian 감쇠를 더 넓은 영역에 적용하므로 양 끝과 위쪽이 자연스럽게 사라진다. 하늘 셰이더와 후처리 채도 마스크 모두 같은 프로필 값을 사용한다. 지상/노출/안개/맵 배치는 그대로다. [직전의 가는 띠](Before-Wider-Gradient.png) / [현재](Dusk-Afterglow.png).

## 최종 검증

- 전체 EditMode 444/444 통과. 초기 캐릭터 환경광 불일치 1건은 저장된 환경광 색상만 동기화해 해결했다. 기존 검사를 삭제하거나 완화하지 않았다.
- 황혼 셰이더 컴파일, 반복 적용 시 Volume/조명 중복 방지, 환경 해제 시 Skybox/카메라/환경광 복원 검사 통과.
- 비교 렌더 5쌍 생성 및 직접 확인. 태양 원반 없음, 한쪽의 잔광과 반대쪽의 차이, 건물/도로 식별성을 검토했다. 환경 적용 전후 메시와 변환 행렬 불변 검사 통과.
- 정식 빌드 오류 0. `Builds/Latest/EarthRecovery.exe` 갱신 및 Latest + 이전 3개 보존 검사 통과.
- 실제 Latest 호스트/클라이언트 실행 둘 다 `CITY_PLAYER_PASS`, seed=100, players=2, facilities=15. [호스트 렌더](Player/host-ingame.png), [클라이언트 렌더](Player/client-ingame.png). 숨김 테스트 창의 게임 카메라 렌더이므로 HUD는 포함하지 않는다.
- 플레이어 로그에서 런타임 예외 및 셰이더 오류 없음. 이전 빌드 검증에서도 발생했던 D3D12 업로드 버퍼 경고(16MB에 64MB 요청)는 각 1회 남았으며 테스트는 완료됐다. 별도 장시간 성능 측정은 하지 않았다.
- 검증 도구는 배치 프로세스의 깨끗하고 빈 시작 씬인 경우만 교체한다. 다른 씬은 Additive 방식으로 보존한다. 작업 중인 사용자 씬을 저장하거나 덮어쓰지 않는다.
