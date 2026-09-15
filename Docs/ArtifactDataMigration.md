# 아티팩트 데이터 개편 적용 결과

## 데이터 흐름
`Assets/Resources/HumanThingsSeed.json` → `HumanContentSource.Parse` → `HumanContentBuilder.UpdateTextData` → `ArtifactDefinition / LocationDefinition` → `HumanContent.asset`

미션: `Expedition.State` → `Expedition.ForClient` → `SiteState.missionHint / missionLocationHint` → `NetworkSession.View` → `Catalog.MissionLines` → 우측 임무창·캠프 단말기.

기록실: 기존 `ArchiveStore.Save(version=1)` → `ArchiveEntry.artifactId / discovered` → `Catalog.ArchiveLines` → 현재 `ArtifactDefinition`의 텍스트 → 기존 기록실 스크롤 영역.

## 구조 변경
- 원본은 첨부 humanthings_artifacts_reworked.json과 구조·문자열이 모두 동일하다. 줄바꿈 인코딩 차이만 가능하다.
- 삭제: categoryTag, materialHint, functionHint, facilityHint, level1Clues, level2Clues, location.displayNameUnknown.
- 기존 파생 필드 dataTags와 clueText도 제거했다.
- 신규: MissionHint(category, structure, function), ArtifactDefinition.archiveDetails[], LocationDefinition.missionLocationHint.
- 기존 아티팩트 ID, locationId, 장소 순서, 구역 소속, 장소별 물품 연결, 프리팹·레시피·퍼즐 참조를 유지한다.
- 기존 전체 생성 임포터도 새 DTO를 사용한다. 이번 적용은 텍스트 전용 임포터로 수행하여 프리팹과 게임 규칙을 재생성하지 않았다.

## UI 바인딩
- 미션 정보: missionHint.category, missionHint.structure, missionHint.function, missionLocationHint 원문을 순서대로 표시한다.
- 캠프 안팎과 장소 발견 여부에 따라 힌트 문장을 축약·대체하지 않는다.
- 미회수 물품은 기존 표본 코드와 ???를 사용한다. 이전 기록실에서 회수했더라도 이번 미션 회수 전 이름은 숨긴다.
- 미션 설명에는 displayNameKnown을 사용하지 않는다. 발견 후 지도와 실제 시설 단말기 등 기존 장소명 용도는 유지한다.
- 기존 회수 완료 이벤트·팝업은 trueNameKo, trueNameEn, archiveDescription, marsComment를 기존 흐름으로 사용한다. 새 팝업은 만들지 않았다.
- 기록실은 discovered가 true인 기록에만 실제 이름, missionHint.category, archiveDetails 전체, archiveDescription, marsComment를 표시한다.
- 기록실의 회수 횟수와 관찰 기록은 유지한다. 미회수 기록은 기존 미식별 제목과 관찰 기록만 표시한다.
- 창 위치·크기·배경·버튼 배치를 유지한다. 우측 미션 텍스트는 기존 영역에 맞춰 줄바꿈 및 글자 크기를 조정한다.
- 단말기는 기존 설명 영역 내부에 스크롤을 사용한다. 기록실은 기존 스크롤 영역 안에서 텍스트의 실제 높이와 배열 길이에 따라 행 높이를 계산한다.

## 저장 및 네트워크
- ArchiveEntry와 저장 파일 버전은 변경하지 않는다. 기존 저장 데이터의 ID·횟수·발견 여부·관찰 기록은 수정하지 않는다.
- 최신 설명은 회수된 기록의 artifactId로 현재 콘텐츠를 조회한다. 따라서 오래된 저장 파일에 새 배열 필드가 없어도 표시된다.
- 알 수 없는 과거 ID는 저장된 기존 설명을 사용하는 방식을 유지한다.
- 미션 DTO가 변경되어 NGO ProtocolVersion을 7에서 8로, LAN BuildLabel을 Revision2에서 Revision3로 변경했다. 이전 빌드와 혼합 접속하지 않는다.
- 개발자 1인 모드는 유지한다.

## 검증
- 첨부 JSON과 프로젝트 원본의 구조·문자열 일치 확인.
- Unity 임포트: 15개 장소, 45개 물품 정상 처리. 모든 ID·소속 연결 유지.
- 5개 구역마다 장소 3개, 장소마다 물품 3개. 기록실 상세 문장 135개.
- EditMode 292개 통과. PlayMode 36개 통과. 실패 0개.
- 전체 45개 물품에 대해 회수 전 이름 숨김과 원문 힌트 바인딩 검증.
- 실제 LAN 클라이언트 수신 스냅샷 및 캠프/외부의 미션 힌트 일치 검증.
- 기존 저장 형식, 미회수 잠금, 상세 배열 0/1/5개 표시 검증.
- 1280×720, 1920×1080 실행 화면 11장 캡처. 전체 45개 미션 텍스트 영역 측정 통과.
- 런타임 화면 확인은 테스트용 기록실 데이터를 사용하며 사용자 저장 파일을 쓰지 않는다. 실제 회수 로직은 기존 전체 루프 자동 테스트로 검증했다.
- 컴파일 및 Windows 빌드 성공, 빌드 오류 0개. 런타임 QA 예외 없음.
- 기존 '캠프/외부 힌트가 달라야 한다' 테스트는 새 요구사항에 맞춰 '같아야 한다'로 변경했다.
- 기존 고정 높이 UI는 긴 힌트와 가변 길이 상세 배열을 담을 수 없어 내부 높이 계산·스크롤·글자 맞춤으로 해결했다.
- 빌드 후 무관한 씬 오브젝트 ID 재생성 차이만 원복했다. 씬 동작은 바꾸지 않았다.
- 최신 실행 파일: Builds/Latest/EarthRecovery.exe. 최신 포함 3개 버전 유지.

## 수정 파일 전체
아래 목록에는 새 파일과 Unity가 생성한 신규 메타 파일을 포함한다. 60개 콘텐츠 에셋의 변경은 텍스트 스키마와 문자열이다.

- `Assets/Editor/HumanContentBuilder.cs`
- `Assets/Resources/HumanThings/COM_ELECTRONICS.asset`
- `Assets/Resources/HumanThings/COM_MUSIC_STORE.asset`
- `Assets/Resources/HumanThings/COM_RESTAURANT.asset`
- `Assets/Resources/HumanThings/CUL_LIBRARY.asset`
- `Assets/Resources/HumanThings/CUL_MUSEUM.asset`
- `Assets/Resources/HumanThings/CUL_SCHOOL.asset`
- `Assets/Resources/HumanThings/HT_A01_01.asset`
- `Assets/Resources/HumanThings/HT_A01_02.asset`
- `Assets/Resources/HumanThings/HT_A01_03.asset`
- `Assets/Resources/HumanThings/HT_A02_01.asset`
- `Assets/Resources/HumanThings/HT_A02_02.asset`
- `Assets/Resources/HumanThings/HT_A02_03.asset`
- `Assets/Resources/HumanThings/HT_A03_01.asset`
- `Assets/Resources/HumanThings/HT_A03_02.asset`
- `Assets/Resources/HumanThings/HT_A03_03.asset`
- `Assets/Resources/HumanThings/HT_B01_01.asset`
- `Assets/Resources/HumanThings/HT_B01_02.asset`
- `Assets/Resources/HumanThings/HT_B01_03.asset`
- `Assets/Resources/HumanThings/HT_B02_01.asset`
- `Assets/Resources/HumanThings/HT_B02_02.asset`
- `Assets/Resources/HumanThings/HT_B02_03.asset`
- `Assets/Resources/HumanThings/HT_B03_01.asset`
- `Assets/Resources/HumanThings/HT_B03_02.asset`
- `Assets/Resources/HumanThings/HT_B03_03.asset`
- `Assets/Resources/HumanThings/HT_C01_01.asset`
- `Assets/Resources/HumanThings/HT_C01_02.asset`
- `Assets/Resources/HumanThings/HT_C01_03.asset`
- `Assets/Resources/HumanThings/HT_C02_01.asset`
- `Assets/Resources/HumanThings/HT_C02_02.asset`
- `Assets/Resources/HumanThings/HT_C02_03.asset`
- `Assets/Resources/HumanThings/HT_C03_01.asset`
- `Assets/Resources/HumanThings/HT_C03_02.asset`
- `Assets/Resources/HumanThings/HT_C03_03.asset`
- `Assets/Resources/HumanThings/HT_D01_01.asset`
- `Assets/Resources/HumanThings/HT_D01_02.asset`
- `Assets/Resources/HumanThings/HT_D01_03.asset`
- `Assets/Resources/HumanThings/HT_D02_01.asset`
- `Assets/Resources/HumanThings/HT_D02_02.asset`
- `Assets/Resources/HumanThings/HT_D02_03.asset`
- `Assets/Resources/HumanThings/HT_D03_01.asset`
- `Assets/Resources/HumanThings/HT_D03_02.asset`
- `Assets/Resources/HumanThings/HT_D03_03.asset`
- `Assets/Resources/HumanThings/HT_E01_01.asset`
- `Assets/Resources/HumanThings/HT_E01_02.asset`
- `Assets/Resources/HumanThings/HT_E01_03.asset`
- `Assets/Resources/HumanThings/HT_E02_01.asset`
- `Assets/Resources/HumanThings/HT_E02_02.asset`
- `Assets/Resources/HumanThings/HT_E02_03.asset`
- `Assets/Resources/HumanThings/HT_E03_01.asset`
- `Assets/Resources/HumanThings/HT_E03_02.asset`
- `Assets/Resources/HumanThings/HT_E03_03.asset`
- `Assets/Resources/HumanThings/IND_AUTO_SHOP.asset`
- `Assets/Resources/HumanThings/IND_PRINT_SHOP.asset`
- `Assets/Resources/HumanThings/IND_TOOL_SHOP.asset`
- `Assets/Resources/HumanThings/RES_CLOTHING.asset`
- `Assets/Resources/HumanThings/RES_CONVENIENCE.asset`
- `Assets/Resources/HumanThings/RES_HOSPITAL.asset`
- `Assets/Resources/HumanThings/RES_LAB.asset`
- `Assets/Resources/HumanThings/RES_SIGNAL_STATION.asset`
- `Assets/Resources/HumanThings/RES_TOY_STORE.asset`
- `Assets/Resources/HumanThingsSeed.json`
- `Assets/Scripts/ArtifactDefinition.cs`
- `Assets/Scripts/Bootstrap.cs`
- `Assets/Scripts/Expedition.cs`
- `Assets/Scripts/GameData.cs`
- `Assets/Scripts/GameHud.Terminal.cs`
- `Assets/Scripts/GameHud.cs`
- `Assets/Scripts/HumanContent.cs`
- `Assets/Scripts/LocationDefinition.cs`
- `Assets/Scripts/NetworkSession.cs`
- `Assets/Scripts/SnapshotValidation.cs`
- `Assets/Tests/EditMode/ExpeditionTests.cs`
- `Assets/Tests/PlayMode/LanConnectionTests.cs`
- `Assets/Scripts/ArtifactSmoke.cs`
- `Assets/Scripts/ArtifactSmoke.cs.meta`
- `Assets/Scripts/HumanContentSource.cs`
- `Assets/Scripts/HumanContentSource.cs.meta`
- `Assets/Tests/EditMode/ArtifactDataTests.cs`
- `Assets/Tests/EditMode/ArtifactDataTests.cs.meta`
- `Docs/ArtifactDataMigration.md` (이 문서)

## 검증 파일
프로젝트 상위 폴더:
- EarthRecovery-artifacts-import.log
- EarthRecovery-artifacts-tests.xml / .log
- EarthRecovery-artifacts-play-tests.xml / .log
- EarthRecovery-artifacts-build.log
- EarthRecovery-artifacts-runtime.log

최신 빌드 폴더:
- ArtifactQA/result.txt
- ArtifactQA/*.png (11장)

