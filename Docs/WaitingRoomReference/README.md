# HumanThings - Expedition Waiting Room Unity Assets

이 패키지는 첨부된 `탐사대 대기실` 최종 UI 이미지를 바탕으로,
Unity에서 실제 최대 6인 멀티플레이 대기실로 구현하기 좋게 분리한 에셋 세트다.

## 포함 내용
- 배경 / 글로벌 노이즈 / 가장자리 스크래치
- 재사용 가능한 PlayerCard 프리팹용 에셋
- 1~6인 카드 배치 프리셋 가이드 이미지
- 탐사 지역 패널 에셋
- 방 나가기 / 준비 완료 / 탐사 시작 버튼 상태 에셋
- 채팅 바 에셋
- 장식선 / 소형 장식 이미지
- JSON 스펙 (권장 파일명, 텍스트, 프리팹 구조, 레이아웃 규칙)

## 중요한 제한
원본이 이미 한 장으로 합쳐진 래스터 이미지라서,
기능 UI 뒤에 가려진 '실제' 배경 픽셀을 완벽하게 추출할 수는 없다.
`Background_Artwork.png`는 UI를 제거하고 자동 복원한 best-effort 베이스다.

## 추천 Unity 구조
ExpeditionLobby
- Background
- Title
- PlayerArea
- ExpeditionRegionPanel
- BottomActions
- Chat
- Decorations

PlayerArea 내부는 PlayerCard Prefab 하나를 재사용하고,
플레이어 수에 따라 Layout_1~6 규칙만 바꿔 적용한다.

## 텍스트
런타임에 바뀌는 모든 텍스트는 PNG에 굽지 않았다.
반드시 TextMeshPro로 구현하고, 문구는 `09_Spec/ExpeditionWaitingRoom_UI_Spec.json`을 참고한다.
