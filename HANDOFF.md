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
