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
- 불확실성 사전 고지: 검증 안 된 추측(못 읽은 악보·귀 재현·외부 출처 미확인)으로 작업할 땐 결과물보다 먼저 `확실한 부분 / 추정한 부분`을 구분해서 밝힐 것. 사후 해명 금지 (2026-10-08 합의)
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
- 보충(동일 세션 재검증): 화면 밖 창(`--window-position` 음수) CDP 캡처는 합성 중단된 스플래시 잔상만 나옴 → headless(`--remote-debugging-port`)+실시간 90초 대기 후 `Page.captureScreenshot`이 정답. 최종 캡처 `tetris17g.png` = 게임오버 화면 정상 (무입력 방치 귀결, 아티팩트 아님). 스크립트 `Temp/opencode/cdp-shot.ps1`
- 다음 세션: 디테일 폴리싱 (프리팹 비주얼 사용자 측) → 수 세션 후 itch.io 서비스 배포

## 세션 기록 (2026-10-08, V2 첫 플레이어 빌드)
- 범위 확정: v1 동결(참고용). 폴리싱·배포는 v2로
- `685cf58` `BuildV2Player` (`TetrisV2Builder`): V2.unity 재생성+WebGL, 출력 `Build/WebGL_V2` (v1 산출물 분리). 사전 스모크 `PASS (frames=90, audio=True)`
- 결과: `Succeeded totalBytes=10360328` (`v2player1.log:2212`, v1빌드17 대비 +115B)
- 검증: CDP `bar:none` + 과일 블럭·한글 UI·NEXT·패드 정상 렌더 (`v2player1.png` = 무입력 방치 게임오버). 폴리싱 후보: `CCW` 버튼 글자 줄바꿈(`CC/W`), `HOLD` 좌우 중복 (v1 공통 레이아웃)
- 배포: 바탕화면 `TetrisWebGL_V2_Play` 신규 + `TetrisWebGL_V2_itchio.zip` (10,086,170B). v1 폴더 손대지 않음
- 함정: 검증 서버 종료 시 powershell 전수열거 kill은 도구 러너까지 죽여 exit255. `CommandLine like *serve*`로 PID 특정 후 개별 kill할 것
- 다음: V2 폴리싱 (프리팹 비주얼 사용자 측) → itch.io 서비스 배포

## 세션 기록 (2026-10-08, V2 폴리싱 1차 — 코드 3건)
- `0358ed8` (main, 미푸시): `TetrisGameV2.cs` + `TetrisKoreanFontSetup.cs` + `TetrisUiPrefabBuilder.cs`
- ① CCW 줄바꿈: CCW/CW 44pt→26pt + `AddPadButton`에 `NoWrap` (88px 버튼에 폴백 3글자가 2줄로 깨지던 원인)
- ② HOLD 중복: 우측 패드 `HOLD`→`SWAP` (동작=동사형, 월드 좌측 HOLD=영역명 유지). v1은 동결이라 손대지 않음
- ③ 힌트 모바일 기준: `"◄► 이동 / ▼ 소프트드롭 / ↺↻ 회전 / DROP 하드드롭 / SWAP 홀드"` — V2 런타임 지정이라 WORK 프리팹 비주얼 작업과 충돌 없음. `홀` 1글리프를 `KoreanChars`에 추가 (static bake 네모박스 방지)
- ④ 과일 비주얼: 사용자 WORK 브랜치에서 직접 처리 예정
- 미검증: 컴파일·폰트 bake는 다음 `BuildV2Player` 때 일괄 확인 (홀 bake 후 CDP 캡처로 SWAP/힌트 렌더 확인 예정)

## 세션 기록 (2026-10-08, V2 BGM)
- `c7450d8` (main, 미푸시): 절차적 8비트 코로베이니키 루프 (`BuildBgm`, 에셋 없음, 약 7.24초)
- 선율 검증: Unity 빌드 없이 동일 수식을 WAV 렌더 (`Temp/opencode/bgm_test.wav`) — 피크 0.223 무클리핑, 루프 이음새 0.018 무클릭. 사용자 시聴 OK
- 연동: BGM 전용 소스(루프) + SND 음소거/일시정지/레벨별 피치 상승. WebGL 첫 제스처 오디오 잠금 대비 입력 시 재생시도
- 템포 0.24 확정 + 참고용 샘플 `Reference/bgm_sample.wav` 커밋 (게임은 코드 합성 재생, 파일은 빌드 미포함)
- 확정 스펙(시청 3회): 템포 0.27 + 리드 소프트(기본파+3배음/6) + A-A-B-A' 64박 17.28초. GB판처럼 반복으로 길이 확보 (피크 0.232, 이음새 0.022)
- 다음 빌드(`BuildV2Player`)에 폴리싱 1차 + BGM 일괄 포함 예정

## 세션 기록 (2026-10-08, V2 빌드2 — 폴리싱+BGM 실기)
- 결과: 빌드2 Succeeded (v2player2.log, BGM 코드·SWAP·힌트 포함. 총 10373412B)
- 사전 스모크: `v2smoke3.log` (컴파일·런타임 통과 후 빌드 진입)
- 검증: CDP 로딩바 사라짐 + 경고 없음 + 960x600 + 과일블럭·한글UI·SWAP·CCW/CW 한줄·모바일힌트 정상 렌더 (무입력 방치 게임오버)
- 배포: 바탕화면 `TetrisWebGL_V2_Play` 갱신 + itch zip 재생성 (10097366B)
- BGM 참고: 빌드 도중 BGM 코드 교체가 겹쳐 내장 버전 불확정 (마피아노版 or 네이버版). 확실한 부분: 폴리싱 라벨·파이프라인. 빌드3에서 확정版으로 통일 예정
- 다음: 네이버145版 시聴 확정 후 BGM 교체 빌드3

## 세션 기록 (2026-10-08, V2 빌드3 — 트림BGM 실기)
- 결과: `Succeeded totalBytes=14006555` (MP3 포함으로 +3.6MB)
- 검증: CDP `bar:none` + 경고空 + 960x600 + 폴리싱 전부 정상 렌더 (SWAP·CCW 한줄·모바일힌트·과일블럭)
- 배포: 바탕화면 `TetrisWebGL_V2_Play` 갱신 + itch zip 재생성 (13,731,262B)
- BGM: 2:50 트림版 내장. 소리는 헤드리스에서 확인 불가 → 실기 플레이 시 확인 필요
- 운용 합의: 빌드는 요청 있을 때만 돌림 (WORK 에디터 확인 → 빌드 요청 순). 독단 빌드 금지

## 세션 기록 (2026-10-08, BGM 파일 확정 + 게임밸런스)
- NES 리믹스 MP3 원본 → 2:50(8루프 170.37초) 프레임컷 → `Resources/Audio/bgm_nes.mp3` 내장 (코드 로드+절차예비)
- 코드 변환 시도했으나 리드+베이스 뒤섞임으로 탈락. 원본 루프 결함(3분마다 끊김)은 사용자 확인 후 감수
- 출처 주의: 외부 리믹스, 연습용 팬메이드. 정식 배포 전 교체 필요
- BGM 중복재생 버그 수정 (`80f6652`): 첫 입력 시 재생중 Play 재시작이 원인. isPlaying 가드 2곳 + GameV2 중복 감지
- 밸런스: 낙하 `0.5/1.1^lv` 복리 (`7836c36`), BGM 피치 0.7 시작+10% 복리 상한1.4 (`e8c498a`)
- 점수 체계 유지 (100/300/500/800×레벨 + 콤보50×콤보×레벨, 낙하점수 없음)
- 상태: main=work=`e8c498a`, 원격 0/0. 미검증: 빌드3 이후 코드 변경분(중복수정·속도·피치)은 다음 빌드에서 실기 확인 필요
- 다음: itch.io 배포 / 과일 비주얼 / 용어 정리 (사용자가 범용 용어로 정리 예정)

## 세션 기록 (2026-10-08, BGM C/D 반복 + 게임오버곡)
- BGM 반복 운용 합의: 마음에 드는 C/D 나올 때까지 교체 반복. 롤백은 사용자가 시점 지정, 체크포인트 커밋으로 복구
- 스퀄160 샘플: A-A-B-B @160 드라이브 8분베이스 (`Temp/opencode/sq_test.wav`). C/D는 mp 중간부 채보 후 완성형 샘플済
- NES MP3 분석: 21.3초 루프 8회 겹침多数결로 코드 변환 시도 → 리드+베이스 뒤섞임으로 탈락, 원본 파일 사용 확정
- MP3 2:50 트림 (프레임컷 6522프레임, 이음새 검증済) → 빌드3 내장
- BGM 중복재생 확정 수정: 첫 입력 시 Play 재시작이 원인. isPlaying 가드 + 중복 감지. 사용자 실기 확인済
- 밸런스: 낙하 10%→20% 복리 (`11c01cb`, BGM 피치는 10% 유지), 점수 체계 유지
- 게임오버곡: ABAB @0.62 순수사인 슬로우 루프 + 화면전환 연동 (`f1583ac`, 시聴 확정済, 빌드 미반영)
- 상태: main=work=`f1583ac`, 원격 0/0
- 다음: 게임오버곡 포함 빌드 (요청 시) / itch.io / 과일 비주얼 / 용어 정리

## 세션 기록 (2026-10-08, V2 BGM 원본파일 확정)
- 사용자 결정: NES 리믹스 MP3 원본 사용 (`Assets/UI/Resources/Audio/bgm_nes.mp3`, 2.9MB)
- 확실한 부분: 코드 로드 (`Resources.Load`, 절차版은 예비로 유지). 일시정지·음소거·레벨피치는 소스 레벨이라 그대로 동작
- 추정한 부분 겸 결함 고지: 180.7초가 21.3초 루프 배수가 아니라 매 3분마다 중간에 끊기고, MP3 패딩으로 미세 공백 가능. 코드 변환본(`nes_code.wav`, 리드 단독)은 밋밋해서 탈락
- 출처 주의: mp3hamster 경유 외부 리믹스. 연습용 팬메이드로 사용자 확인済. 정식 배포 전 교체 필요
- 다음: 빌드3에서 실기 확인

## 세션 기록 (2026-10-08, V2 빌드4 — 중복수정+밸런스 실기+배포)
- 결과: `Succeeded totalBytes=14006596` (`v2player4.log:2212`, 빌드3 대비 +41B)
- 사전 스모크: `v2smoke5.log` `PASS (frames=90, audio=True)`. 함정: 스모크에 `-quit` 금지 (스크립트가 직접 종료. `-quit` 주면 90프레임 전 종료돼 SMOKE줄 없음). 셸 즉시복귀 → `Exiting batchmode` 전 로그 확인 필수
- 검증: CDP 에러 0 + `bar:none` + 960x600 + 과일·한글UI·SWAP·패드 정상 (무입력 방치 게임오버, `v2b4.png`). 스크립트 `Temp/opencode/verify-v2b4.ps1` 신규 (에러수집+상태+캡처 일체형, 기존 cdp_verify+verify-v2build2 조합)
- 배포: 바탕화면 `TetrisWebGL_V2_Play` 갱신 (ALL-MATCH 바이트 대조済) + itch zip 재생성 (13,732,887B, 17엔트리). v1 손대지 않음. 서버 8080 정리済 (PID 특정 kill)
- 함정(신규): `Copy-Item srcDir destDir`는 dest 존재 시 중첩 복사됨 (`Build\Build` 사고). 내용은 `Build\WebGL_V2\Build\*` 와일드카드로 복사할 것
- 부산물: `EditorSettings.asset` 스모크 흔적은 원복 (커밋 제외 유지). `V2.unity`·폰트SDF는 재생성물이라 커밋 (폰트 diff는 Thin Atlas 순서만 변경, 글리프 정상)
- 미결: BGM 소리는 헤드리스 확인 불가 → 실기 플레이 시 확인. 푸시는 사용자 몫 (미푸시)

## 세션 기록 (2026-10-08, V2 빌드5 — 영어UI+굵은폰트+게임오버곡 실기)
- 결과: `Succeeded totalBytes=14007365` (`v2player5.log:2214`, 빌드4 대비 +769B)
- 사전 스모크: `v2smoke6.log` `PASS (frames=90, hud=True, SCORE 영어, blocks=16, audio=True)` + MissingReference/NullReference 0건
- 포함분(빌드4 이후 전부): 키보드전용 영어UI(패드/Hint/SND 제거·PAUSE 1종), 미리보기 알파 0.1→0.25, 굵은폰트(Bold+외곽선 0.15), 게임오버곡 첫 내장. BGM·블럭색·과일은 동결 유지
- 함정(신규): TMP `outlineWidth` setter는 끊어진 공유 머티리얼에서 MissingReference(1619)/NullReference(1621). 패키지 원본(`TextMeshProUGUI.cs:1609-1622`)이 무방비 역참조라 `== null` 가드로 못 막음. 우회: 직접 복제한 인스턴스 머티리얼에 셰이더 값 기록 후 교체 (`ThickenTMP`)
- 함정(재확인): `& Unity.exe` 셸 즉시복귀 → 실프로세스 별도 진행. `Exiting batchmode` 대기자로 확인 후 복사. CDP 스크린샷 base64는 64KB 초과 분할 수신 → EndOfMessage까지 이어붙일 것. 검증 스크립트 `Temp/opencode/verify-v2b5.ps1` (에러수집+캡처 일체형)
- 검증: CDP 에러 0 + 로딩바 사라짐 + GAME OVER/SCORE/RESTART·HOLD/NEXT 영어 + 과일블럭 + 굵은폰트 정상 (무입력 방치 게임오버, `Temp/opencode/v2b5.png`)
- 배포: 바탕화면 `TetrisWebGL_V2_Play` 갱신 + itch zip 재생성 (13,734,242B, 17엔트리). 서버 8080 정리済 (PID 특정 kill)
- 다음: itch.io 서비스 배포

## 세션 기록 (2026-10-08, V2 빌드6 — FruiTetris 타이틀 실기)
- 결과: `Succeeded totalBytes=14007400` (`v2player6.log:2201`, 빌드5 대비 +35B)
- 사전 스모크: `v2smoke7.log:409` `PASS (frames=90, hud=True, SCORE, blocks=16, audio=True)`
- 포함분: `productName FruiTetris` (`da71a3d`, index.html 타이틀 확인済). 게임 내용물은 빌드5와 동일
- 함정(신규): 스모크 대기자는 `SMOKE frames`로 (자체 종료라 `Exiting batchmode` 없음, `-quit` 금지 유지). 빌드 인라인 `-Command`의 `\"` 이스케이프 파서 에러 → 파일 스크립트(`Temp/opencode/v2build6b.ps1`)로 우회
- 검증: CDP 에러 0 + 로딩바 사라짐 + GAME OVER/SCORE/RESTART·HOLD/NEXT·과일블럭 정상 (무입력 방치 게임오버)
- 배포: 바탕화면 `TetrisWebGL_V2_Play` 갱신 (index.html+Build+TemplateData 내용물 복사) + itch zip 재생성 (13,734,242B, 17엔트리, zip 내 타이틀 확인済). 서버 8080 정리済
- 다음: itch.io 서비스 배포 (페이지 설정값은 전 세션 전달済)
