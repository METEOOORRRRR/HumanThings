# Revision2 전체 수정 재검토

## 실행 경로 불일치 원인

사용자가 실행한 경로는 Builds/Windows-HumanThings-v03-ThirdPerson이다. 이후 명칭/모듈/지도/호출벨 변경을 별도 Revision1 및 NoBell 폴더에만 배포해서 이 실행 경로에는 반영되지 않았다.

이번 배포는 사용자 기존 ThirdPerson 폴더를 백업한 뒤 같은 경로를 최신 Revision2로 갱신한다. 로비 제목에 Revision2를 표시하며, 상위 폴더 HumanThings-Latest.cmd도 이 실행 파일만 가리킨다. Unity Hub 등록 프로젝트는 현재 EarthRecovery와 일치함을 확인했다.

## 항목별 반영

| 요청 | 처리 |
|---|---|
| 없는 방 참가 시 준비 화면 노출 | 통신 시작 상태와 서버 연결/플레이어 정보 수신 완료 상태 분리. 완료 전에는 Online=false |
| 접속 대기/실패/취소 | 접속 중 상태와 취소 버튼, 최대 8초 후 실패 처리, 실패 후 재접속 가능. 준비/시작 버튼은 실제 연결 후에만 표시 |
| 장소명 통일 | 악기점, 식당, 전자상가, 문방구, 의류점, 편의점, 공구점, 정비소, 인쇄소, 병원, 실험실, 관측소, 도서관, 학교, 박물관 |
| 간판/지도/시설 UI | 같은 장소 데이터 사용. 실제 생성된 간판 15종도 회귀 검사 |
| 제작기 모듈 목록 | 탭으로 숨기지 않고 구역 5행 x 시설 3열, 총 15종을 한 화면에 표시. 임무 여부 필터/정답 강조 없음 |
| 미확인 임무 | 캠프/임무 화면 모두 회수 전 표본 코드와 ???, 회수 후 이름 공개 |
| 캠프 단말기 | 표본 3개의 정보와 진행 상태, 협동 연결을 표시. 시설 정답 직접 표시 없음 |
| 미니맵 | 시설 15개 위치 항상 표시, 미발견 ??, 발견 후 이름 공개. 구역별 색상 유지 |
| 시설 모듈 설치 | 내부 단말기 근처 E, 모듈 없어도 팝업 및 설치 가능 여부 표시. 서버에서 거리/보유 여부 확인 |
| 제작기 구분 | 주황색 제작기, 청록색 시설 단말기 유지 |
| 괴물 이동 | 독립 순찰, 경로 갱신 및 막힘 복구 유지 |
| 호출벨 | 오브젝트, 상호작용, 소음 이벤트, 괴물 반응 제거 유지 |
| 플레이어 시점 | 3인칭 및 카메라 충돌 처리 유지 |

## 검증

QA/Revision2-EditMode.xml, QA/Revision2-PlayMode.xml, QA/Revision2-build.log에 결과를 기록한다.

신규 회귀 범위: 없는 방 접속 전체 기간에 Online/LocalPlayer가 생기지 않는지, 접속 취소 후 정상 방 재접속, 탐사 중 방의 입장 거부, 실제 월드의 장소 간판 15종.

최종 결과:

- EditMode 52개 + PlayMode 30개 = 82개 통과, 실패 0.
- Windows 빌드 Succeeded, errors=0.
- QA/Revision2-final/missing-result.txt: 없는 방에 접속 시도 중 Online=false 유지, 준비 버튼 없는 접속 중 화면 및 실패 후 접속 화면 복귀 PASS.
- QA/Revision2-final: 실제 6인 실행 전원 PASS 및 종료 코드 0. 물건 3개 회수, 도감 3개 등록, 사망 후 무전 차단 확인.
- 실제 창 캡처 ui-missing-pending.png, ui-missing-failed.png: 연결 전 준비/시작 버튼이 없음을 직접 확인.
- 실제 창 캡처 ui-field-1-station.png: 구역별 15개 시설명과 모듈을 한 화면에서 확인. 병원/학교/인쇄소/문방구 등 승인된 명칭 사용.
- 최종 로그에 런타임 Exception 또는 QA_FAIL 없음.
- 사용자 실행 경로의 EarthRecovery.dll 및 resources.assets 해시가 검증 빌드와 일치.

1차 실행(QA/Revision2-runtime)은 자동 테스트 요원이 시설 중심점 근처에서 일찍 멈춰 단말기 반경 밖에 남는 문제가 있어 중단했다. 게임의 거리 규칙은 유지하고 테스트 접근 지점만 단말기 앞으로 보정했으며, 최종 재실행에서 전체 임무 완료를 확인했다.

이전 사용자 실행 파일 백업: Builds/Archive/ThirdPerson-before-Revision2-20260914-192138.

## 실행

Builds/Windows-HumanThings-v03-ThirdPerson/EarthRecovery.exe

폴더명은 사용자가 쓰던 경로를 유지하기 위한 이름이며, 실행 내용은 Revision2다. 프로토콜 5를 사용하므로 참가자 모두 Revision2를 사용해야 한다. 다른 이전 빌드 폴더는 과거 버전이다.
