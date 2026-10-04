# Tetris WebGL (Unity 2022.3.62f3)

## 열기
1. Unity Hub > Open > 이 폴더 선택 (`TETRIS_WEBGL`)
2. 에디터 버전: 2022.3.62f3 (설치됨 확인)

## 실행
1. 빈 Scene 생성 (File > New Scene)
2. Hierarchy 우클릭 > Create Empty > 이름 `Game` > Add Component > `TetrisGame` 추가
3. Play 버튼
- 조작: ←→이동, Up/Z회전, ↓소프트드롭, Space하드드롭, R재시작

## 구조
- `Assets/Scripts/TetrisCore.cs`: 순수 로직 (이동/회전/매칭/파괴). 테스트 가능
- `Assets/Scripts/TetrisGame.cs`: MonoBehaviour. 카메라/조명/보드/큐브를 런타임에 생성. 씬 세팅 불필요. WebGL 대응 (쓰레드 없음, 외부 파일 없음)

## WebGL 빌드
1. File > Build Settings > WebGL > Switch Platform
2. Player Settings:
   - Resolution: 960x600
   - Compression: Gzip
   - Template: Default
3. Build And Run

## 다음 확장
- Tilemap 렌더러로 교체 (2D 저사양)
- Hold/Next UI, 고스트 피스
- 효과음 + 라인 클리어 플래시 코루틴
## UI / Touch (2026-10-05)
- OnGUI 제거, Canvas + TextMeshPro + 터치 패드로 교체
- 좌 하단: 이동 패드 (왼쪽/아래/위... 아니라 왼쪽/소프트드롭/오른쪽, 88px 버튼, 홀드 반복 DAS 0.18s/ARR 0.06s)
- 우 하단: 반시계/시계 회전 + DROP(하드드롭)
- 첫 실행 시 필수: Unity 메뉴 Window > TextMeshPro > Import TMP Essential Resources
## Korean Font (2026-10-05)
- UI 한글화 적용 (점수/레벨/줄, 게임 오버, 다시 시작)
- 첫 실행 순서: (1) Window > TextMeshPro > Import TMP Essential Resources
  (2) 메뉴 Tetris > Create Korean Font Asset (NotoSansKR 자동 다운로드 후 SDF 생성)
- NotoSansKR SDF(Dynamic) 우선, LiberationSans 폴백으로 기호 커버
## Static Font + itch.io (2026-10-05)
- 한글 폰트 static bake: 121 glyph, 64.5KB. 원본 TTF(10MB)는 빌드에서 제외됨
- 심볼 4개는 LiberationSans 폴백이 커버
- 첫 실행 순서: (1) TMP Essential Resources import (2) Tetris > Create Korean Font Asset (Static)
- WebGL: Gzip + DecompressionFallback 적용됨. itch.io 업로드 시 Build 폴더 zip 그대로 업로드
## WebGL Build (2026-10-05)
- Unity 6000.3.13f1 (2022.3에 WebGL 모듈 미설치라 6000으로 빌드)
- 메뉴 Tetris > Build WebGL (itch.io + Desktop): 씬 자동생성 + Gzip/폴백 + 빌드 일괄 수행
- 출력 환경변수 TETRIS_BUILD_OUT (기본 Build/WebGL)
- 바탕화면 TetrisWebGL_Play: 빌드 산출물 + TetrisPlay.bat(로컬 서버 실행). index.html 직접 열기 불가, bat로 실행
## Black Screen Fix (2026-10-05)
- 원인: Shader.Find로 찾는 셰이더는 빌드에서 스트리핑되어 null → Awake 예외 → 검은 화면
- 수정: Resources/Materials에 블록7색+배경+테두리 Material 에셋 사전 생성 (빌드 메뉴에서 자동 생성)
- 추가: CreatePrimitive 대신 내장 메시(Cube.fbx/Quad.fbx) 사용, MeshCollider 경고 제거
## Font Atlas Fix (2026-10-05)
- 증상: 한글 깨짐. 원인: 글리프 위치만 굽고 아틀라스 텍스처를 에셋에 포함하지 않음
- 수정: CreateAsset 후 atlasTextures + material을 AddObjectToAsset로 서브에셋 포함
## Buttons Fix (2026-10-05)
- 이동 버튼 기호(◄▼►)를 NotoSansSymbols(2)에서 추가 bake, 삼각형 정상 표시
- 회전 기호(↺↻)는 두 폰트 모두 없어 CCW/CW 텍스트 유지 (기능 정상)
- 힌트 문구 우상단 2줄로 이동, 보드 겹침 해소
- 조작 버튼은 PointerDown 기반이라 마우스 클릭·터치 모두 동작 (추가 코드 불필요)
## Button Click Fix (2026-10-05)
- 증상: 마우스·터치 클릭 무반응. 원인: 패드 버튼에 Image가 없어 GraphicRaycaster 판정 불가
- 수정: MakeButtonVisual에 Image 추가 + Button.targetGraphic 연결
## Clear+Ghost+TopButtons (2026-10-05)
- 줄 삭제 버그 수정: 인접 복수 줄 삭제 시 인덱스 어긋남 → 한 번에 압축 방식으로 교체, 결정적 테스트 9개 ALL PASS
- 고스트를 옅은 회색 단일색으로 변경 (떨어지는 블록 진한색과 구분)
- II/PAD/SND를 우상단 모서리로, 힌트는 한 줄로 그 아래에
