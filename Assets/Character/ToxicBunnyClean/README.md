# Toxic Bunny R-31 — Clean texture variant

2026-09-16. 원본과 별개로 제작한 텍스처 수정 사본입니다.

- `ToxicBunny_R31_Clean_All_Animations.glb`: 수정 텍스처가 내장된 모델. 원본의 메시, UV, 버텍스 노멀, 리깅, 스키닝 및 Running / Walking / restpose 애니메이션 데이터를 그대로 보존했습니다.
- `ToxicBunny_R31_BaseColor_4K.png`: 같은 UV에 사용하는 4096 × 4096 sRGB 베이스 컬러.
- `ToxicBunny_R31_Clean_All_Animations.validation.json`: 원본 해시 및 구조 보존 검사 결과.

옷의 색 얼룩, 노란 스트랩, 아이보리 소매, 얼굴의 눈과 머리카락을 정리했습니다. 재질은 Metallic 0, Roughness 0.82이며, 원래의 노멀 맵은 내장 데이터를 보존하되 적용 강도를 0으로 설정했습니다. 광택과 노멀 맵 때문에 생기던 불필요한 반점을 줄이는 설정입니다.

이 사본은 원본 GLB의 기존 바이너리를 그대로 유지하고 새 컬러 텍스처를 추가하는 방식으로 저장했습니다. 원본 파일과 원본 텍스처는 변경하지 않았습니다.

인게임 Toxic Bunny는 기존 리깅·프리팹을 유지하면서 `HT_ToxicBunny.mat`의 베이스 컬러로 이 4K PNG를 사용합니다. 캐릭터 전용 프로필의 추가 오염·먼지·마모·노멀 효과는 0으로 설정했습니다. `ToxicBunnyCharacterBuilder.Build()`도 재생성 마지막에 이 텍스처를 다시 연결합니다. 게임 빌드는 `ProjectBuilder.Build()`를 통해 `Builds/Latest`에 게시합니다.

텍스처만 바꾸므로 원본의 낮은 폴리곤 수에 따른 각진 윤곽과 접힌 면의 음영은 남습니다. 4K 출력은 확대 투영한 아틀라스 해상도이며, 모든 디테일이 원래부터 4K로 생성된 것은 아닙니다. 여러 방향에서 가려지는 면에는 인접한 3D 표면의 색을 이어주었고, 가장 깊이 가려진 약 6.8%의 UV 표면은 원래 색을 유지했습니다.

실제 3D 렌더 비교 및 Blender 검토 파일은 프로젝트 루트의 `QA/ToxicBunnyTexture/final/` 폴더에 있습니다. `review.blend`에는 회전해서 검토할 수 있는 텍스처 내장 모델과 조명이 있습니다. 배포용 애니메이션의 기준 파일은 위 GLB입니다.

내장 imagegen으로 4면 투영용 이미지, 얼굴 확대 이미지, 정면 의상 개선 이미지를 만든 뒤 기존 UV로 베이킹했습니다. 실제 모델에 총 5차례 적용하고 각 버전을 여러 각도에서 검토했습니다. 마지막 GLB의 걷기·달리기 중간 포즈도 렌더했습니다. 상세 프롬프트와 과정은 프로젝트 루트의 `QA/ToxicBunnyTexture/WORKLOG.md`에 기록했습니다.
