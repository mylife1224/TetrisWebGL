# HANDOFF — 새 세션 인수인계 (2026-10-05)

## 프로젝트 개요
- Unity 6000.3.13f1 (2022.3도 열림) 테트리스 WebGL. 경로: `C:\Dev\TETRIS_WEBGL`
- 원격: https://github.com/mylife1224/TetrisWebGL.git (`main` 동기화済)
- 실행본: 바탕화면 `TetrisWebGL_Play` (`TetrisPlay.bat`으로 실행, index.html 직접 열기 불가)
- itch.io용: 위 폴더의 `TetrisWebGL_itchio.zip` (Build/TemplateData/index.html만)
- 네이티브版(별도, git 미포함): 바탕화면 `TetrisWeb_Native/index.html` 단일 파일 (6.6KB)

## 핵심 파일
- `Assets/Scripts/TetrisCore.cs` — 순수 로직 (이동/회전/삭제). `ClearLines`는 한 번에 압축 방식 (逐行 삭제 버그 수정済)
- `Assets/Scripts/TetrisGame.cs` — MonoBehaviour 전부 (UI·연출·입력·사운드). 빈 씬 + Game 오브젝트 1개면 동작
- `Assets/Scripts/TouchHoldButton.cs` — 누름 유지/탭 버튼
- `Assets/Editor/TetrisKoreanFontSetup.cs` — 메뉴: 폰트 생성(Static/Dynamic), 머티리얼 생성, `TestClearLines`, `ApplyItchio`, `BuildItchioPlayer`
- 폰트: `Assets/UI/Resources/Fonts/NotoSansKR SDF.asset` (static, 135 glyph). 원본 TTF는 `Assets/Fonts/`
- 머티리얼: `Assets/UI/Resources/Materials/` Block 7색+배경+테두리 (빌드 시 자동 생성)

## 빌드 명령 (PowerShell, 프로젝트 경로에서)
```powershell
$u6 = "C:\Program Files\Unity\Hub\Editor\6000.3.13f1\Editor\Unity.exe"
$env:TETRIS_BUILD_OUT = "<출력경로>"
& $u6 -batchmode -quit -projectPath "C:\Dev\TETRIS_WEBGL" -executeMethod TetrisKoreanFontSetup.BuildItchioPlayer -logFile "C:\Dev\TETRIS_WEBGL\build.log"
```
- `BuildItchioPlayer`가 순서대로 수행: Gzip/폴백 세팅 → 머티리얼 생성 → 폰트 static bake → 씬 자동생성 → WebGL 빌드
- 소요 10~20분. `exit:0` + 로그 `[Tetris] build result: Succeeded` 확인

## 알려진 함정 (다시 밟지 말 것)
1. **Documents 폴더는 터미널/git 쓰기 차단.** agent 파일도구만 됨. 프로젝트는 `C:\Dev`에 둘 것
2. **Shader.Find는 빌드에서 null.** 셰이더는 반드시 Material 에셋으로 참조 포함 (ตอน 검은화면 원인)
3. **TMP SDF 생성 시 아틀라스·머티리얼을 AddObjectToAsset로 포함.** 안 하면 글자 깨짐
4. **`TryAddCharacters`는 Static에서 거부.** Dynamic으로 채운 뒤 Static 전환
5. **TMP 3.0.9 + 6000:** `ProjectVersion.txt`가 2022.3이면 마이그레이션 모드에서 TMP 제거됨. 6000으로 고정되어 있음 (현재 6000.3.13f1). 건드리지 말 것
6. **버튼에 Image 필수.** 없으면 클릭 판정 없음 (마우스·터치 공통)
7. **Space 하드드롭은 낙하 후 고정.** `hardDrop()` 호출 순서 주의 (네이티브版에서 실수 이력)
8. **자동화 탭 포커스 클릭 주의.** `tabs.focus`가 II 버튼을 눌러 일시정지 아티팩트 발생 가능. 검증은 reload 후 무입력 캡처로

## 검증 절차 (빌드 후)
1. 바탕화면 폴더에 복사 → `play-server.ps1` 실행 → 브라우저 탭 열기
2. 콘솔 에러 0건 + 로딩바 사라짐 + 캡처로 화면 확인
3. 서버 종료 (포트 8080 정리) → zip 재생성

## 코웍 규칙
- 에이전트는 로컬 커밋까지, 푸시는 사용자가 SourceTree에서 (단, 사용자가 직접 푸시 요청하면 에이전트가 수행)
- `.gitignore`: Library/Temp/Logs/Build 산출물/*.log 제외. TTF·essentials는 포함 (재현성)
- Temp 복사본은 삭제済. `C:\Dev`가 유일 진실

## 코웍 체제 (2026-10-05~, worktree)
- 내 영역: `C:\Dev\TETRIS_WEBGL` (`main`) — 빌드/테스트 전용
- 사용자 영역: `C:\Dev\TETRIS_WORK` (`work` 브랜치, worktree) — 에디터 작업용
- 에디터 버전 통일: 6000.3.13f1 (양쪽 동일 버전으로 열 것)
- 자동생성 파일은 에이전트 소유: `Main.unity`(빌드 시 재생성), `NotoSansKR SDF.asset`(rebake). 사용자 브랜치에서 씬 하이어라키 직접 편집 금지 (빌드 때 증발)
- 사용자 변경 범위: 코드 수치·에셋 추가·새 스크립트 (씬 배치는 이 프로젝트에 없는 개념 — 전부 코드 생성)
- 머지 절차: main 커밋은 사용자가 `merge main`으로 가져가기 / work는 합칠 때 알리면 에이전트가 main에 merge 후 빌드 검증
- 동시 빌드 금지. 에이전트는 batchmode 시작/종료 신호를 매번 알림
- 상시 요청: 3년 공백 후 복귀 중인 유저라 각 과정마다 `이 과정을 하는 이유:` 형태로 첨삭. 중단 요청 전까지 유지
- 학습 Q&A 다룬 주제: 코드생성 vs 씬배치, Additive 씬, UI프리팹 vs UI씬, 화면전환 구조(Single+상시씬), 웹UI 하이브리드. 다음 세션 학습 이어가기용

## 빌드16 (2026-10-05, P0+P1+P2 반영)
- 결과: `Succeeded totalBytes=10328183` (`webgl_build16.log:2156`)
- 변경: 큐브 4셀 풀링(프레임당 Destroy 제거), 머티리얼 Resources 복제(CloneWithAlpha), PAD 토글 held 리셋, 게임오버 고스트 숨김, O회전 false
- 검증: `TestClearLines ALL PASS` + 에디터 컴파일 에러 0건 + 헤드리스 Chrome 실기 확인 (콘솔 에러 0, 로딩바 사라짐, 한글 UI·보드·NEXT/HOLD·패드 정상 렌더)
- 배포: 바탕화면 `TetrisWebGL_Play` 갱신 + `TetrisWebGL_itchio.zip` 재생성 (10.05MB)
- 참고: 에이전트 브라우저 탭은 localhost 502로 검증 불가 → 로컬 Chrome CDP로 대체. 서버는 분리 프로세스로 띄울 것 (shell Job은 세션 종료 시 함께 죽음)

## 세션 기록 (2026-10-06, worktree 복원 + SourceTree 기억더듬기)
- 증상: `C:\Dev\TETRIS_WORK` 폴더 소실 + `TETRIS_WEBGL`이 `work`에 체크아웃된 상태로 발견 (main=work 동일 4f8d8cc)
- 복원: `TETRIS_WEBGL`에서 `git checkout main` → `git worktree add "C:\Dev\TETRIS_WORK" work`. 현재 `WEBGL=[main]` / `WORK=[work]` 정상
- SourceTree 교훈: 한 브랜치는 한 worktree에만 체크아웃 가능. `WEBGL` 탭에서 `work` 체크아웃 시도는 `already used`가 정상. 해결은 새 탭 → 추가(Add) → `C:\Dev\TETRIS_WORK` 등록. 클론/생성 아님
- 다음 세션 시작점: worktree 2탭 구조 확인 (`git worktree list`) 후 작업 이어가기

## 세션 기록 (2026-10-06, 블럭 프리팹 + 과일 블럭)
- `823b6a8` 블럭 프리팹: `Assets/UI/Resources/Prefabs/Block.prefab` (MeshFilter 내장Cube + MeshRenderer Block_I, 콜라이더 없음) + 생성기 `Assets/Editor/TetrisBlockPrefabBuilder.cs`. 단일 프리팹 + sharedMaterial 교체 방식
- `6cef379` 과일 블럭: `Assets/UI/Resources/Textures/Fruit_7종.png` (64px 도트, 코드 생성 후 실물 커밋) + `Assets/Editor/TetrisFruitBuilder.cs` (Ensure=없을 때만 생성 / Apply=부착). 매핑 I사과 O딸기 T바나나 S오렌지 Z배 J복숭아 L수박, 배경은 기존 블럭색 유지
- `EnsureRuntimeMaterials`가 머티리얼 재생성 직후 과일 부착 → 빌드해도 유지. 색상표 단일 진실 공급원 = `TetrisFruitBuilder.Blocks`
- batchmode 주의 2건: (1) `& Unity.exe` 실행 후 셸이 대기 없이 복귀 → 검증 전 로그 `Exiting batchmode` 확인 필수. (2) 죽은 프로세스의 `Temp/UnityLockfile` 잔재로 1회 거부 → 소멸 후 자동 해결. 유저 WORK 에디터와 간섭 없음 실증
- 원격 동기화済 (`main`/`work` 모두 origin과 0/0). 다음: 사용자 측 프리팹 디테일 수정 커밋 → 알리면 에이전트가 work→main 머지 + 빌드 검증

## 세션 기록 (2026-10-07, V2 게임규칙 이전)
- `c0f04a3` V2 껍데기: `TetrisGameV2` + `V2.unity` + `Fruit_*` 분리 (`Block_*` plain 복원 → 구씬 빌드16 외형)
- `7e3bc22` 게임규칙 이전: `TetrisGame.cs` 통째 복사 후 프리팹 패치 (HUD 프리팹/Block 프리팹/`Fruit_*`, 고스트·예고·플래시 텍스처 제거). 로직 1:1, 필드명 유지
- 스모크: `TetrisV2SmokeTest` (playmode 90프레임, HUD/점수/블럭≥4) PASS. 함정 2건: (1) `EditorApplication.EnterPlayMode()` 없음 → `isPlaying` setter 사용. (2) 테스트 카운터 이름은 `Block` (MakeCube가 개명) — `(Clone)` 아님. 대기 패턴은 `SMOKE frames`로 (출력 형식과 일치시킬 것)
- `EditorSettings.asset`의 enterPlayModeOptions 변경은 커밋 제외 (런타임에 매번 설정하므로). 다음: V2 실기 조작 확인 + `BuildV2Player`

## 세션 기록 (2026-10-07, 무음 수정 + 푸시)
- 원인: `AudioListener` 0개 → Unity 전체 음소거 (구·V2 공통, 빌드16부터). V2(`a06f0f4`) + 구버전(`9ac4950`) 모두 카메라에 리스너 보장 패치. 구 코드는 사용자 요청으로 예외 수정
- 검증: `TestClearLines ALL PASS`, V2 스모크 `PASS (audio=True)`. `EditorSettings.asset` 부산물은 별도 원복 커밋 (`f644bd6`)
- 푸시 플로우 확립: 에이전트 요청 시 푸시 수행. `main` 탭 푸시(작업물) + `work` 탭 병합·푸시(작업환경) → 현재 전체 0/0 동기화
- 미결: 바탕화면 실행본은 빌드16 무음 그대로. 소리 나는 실행본 원하면 빌드17 필요
- 다음 세션: 디테일 폴리싱 (프리팹 비주얼 사용자 측) → 수 세션 후 itch.io 서비스 배포 (`BuildItchioPlayer` → `TetrisWebGL_itchio.zip` 파이프라인 그대로 사용)

## 세션 기록 (2026-10-08, 빌드17 소리나는 실행본)
- 결과: `Succeeded totalBytes=10360213` (`build17.log:2219`)
- 함정 재확인 2건: (1) `& Unity.exe` 셸 즉시 복귀 → PID 17356 실빌드 중. `Exiting batchmode` 확인 전 복사 금지. (2) 헤드리스 일회성 스크린샷은 로딩바에서 멈춤 (가상시간이 비동기 로딩을 못 밀어냄) → CDP 실시간 대기(50초)+`Page.captureScreenshot`으로 대체. 검증 스크립트: Temp/opencode `cdp_verify.ps1` (Temp라 커밋 안 됨, 필요시 재생성)
- 헤드리스 Chrome 플래그: `--enable-unsafe-swiftshader` 필수 (없으면 WebGL 컨텍스트 실패로 로딩 정지)
- 검증: CDP 콘솔 에러 0 + 로딩바 사라짐 + 한글 UI·보드·NEXT/HOLD·패드 정상 렌더 (verify17d.png)
- 배포: 바탕화면 `TetrisWebGL_Play` 갱신 + `TetrisWebGL_itchio.zip` 재생성 (10.09MB). 서버 정리済 (8080)
- 다음 세션: 디테일 폴리싱 (프리팹 비주얼 사용자 측) → 수 세션 후 itch.io 서비스 배포
