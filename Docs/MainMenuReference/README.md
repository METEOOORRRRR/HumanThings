# HumanThings Main Menu - Unity Asset Extraction

이 패키지는 제공된 최종 이미지를 Unity에서 다시 조립하기 쉽도록 분리한 버전이다.

## 폴더
- 01_Background: 배경
- 02_Branding: 로고
- 03_Menu: 9-slice 버튼, 구분선, 선택 액센트, 화살표
- 04_Decorative: 폴라로이드 사진 / 프레임
- 05_FX: 화면 외곽 장식
- 06_Spec: 원본 레퍼런스 + JSON 구조/텍스트/좌표
- 07_Preview: 에셋 시트

## 중요
원본이 이미 한 장으로 합쳐진 래스터 이미지였기 때문에,
배경에서 UI를 제거한 `MainMenu_BG_Clean_Base.png`는 인페인팅으로 복원한 베이스다.
최종 출시 전 배경만 별도로 원본 생성/리터치하면 가장 깔끔하다.

## Unity 권장 구조
MainMenu
  BG
    MainMenu_BG_Clean_Base
  FX
    FX_ScreenEdge_Frame
  Branding
    Logo_HumanThings
    Subtitle_Text (TMP)
  Menu
    Button_Start
    Button_Continue
    Button_Archive
    Button_Settings
    Button_Quit
  Decorative
    Polaroid
    Text layers...

텍스트는 PNG로 굽지 않았다. `MainMenu_UI_Spec.json`의 text_layers 값을 TMP에 넣으면 된다.

## 9-slice
`Button_Default_BG_9Slice.png`
`Button_Selected_BG_9Slice.png`
권장 Border: Left/Right/Top/Bottom = 18 px
