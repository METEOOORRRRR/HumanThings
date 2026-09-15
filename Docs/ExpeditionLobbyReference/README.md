# HumanThings - Expedition Lobby Unity Assets

세 장의 이미지는 하나의 `탐사대 편성` 화면의 상태 변형으로 정리했다.

## Unity 구조
LobbyCanvas
- Background
- SharedHeader
- LeftNavigation
- ContentRoot
  - CreateRoomPanel
  - PublicRoomPanel
  - JoinCodePanel

좌측 메뉴는 `MenuItem_Default / Selected` 구조를 재사용하고,
ContentRoot 내부 패널 하나만 활성화하면 된다.

## 텍스트
텍스트는 이미지로 굽지 않았다.
`Spec/ExpeditionLobby_UI_Spec.json`의 문구를 TextMeshPro 등에 넣어 사용하면 된다.

## 9-slice
파일명에 `_9Slice`가 들어간 PNG는 9-slice용으로 만든 에셋이다.
기본 border 권장값은 12~18 px 범위에서 조정.

## 주의
원본이 완성 렌더 3장뿐이라 배경의 UI 아래 영역은 실제 원본 배경을 분리할 수 없다.
`Background_Artwork.png`는 JoinCode 화면 기준으로 UI를 제거하고 인페인팅한 best-effort 베이스다.
