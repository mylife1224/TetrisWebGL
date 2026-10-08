using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// V2: 구 TetrisGame.cs 복사본 + 프리팹 패치 (로직 1:1, 비교용 구 코드 유지).
// 변경점: UI 코드생성→HudCanvas/PadButton 프리팹, 블럭 생성→Block 프리팹, 머티리얼→Fruit_*.
// 게임 규칙(낙하/회전/고정/삭제/점수/연출/오디오)은 구버전과 동일.
// UI: Canvas + TextMeshPro. 조작: 키보드 + 터치 패드 (좌 하단 이동 / 우 하단 회전)
// 연출: 고스트+라인예고, Next/Hold, 플래시+파티클+쉐이크, 콤보, 일시정지, 절차적 효과음
public class TetrisGameV2 : MonoBehaviour
{
    TetrisCore core = new();
    Queue<TetrominoType> bag = new();
    readonly List<TetrominoType> nextQueue = new();
    TetrominoType? holdType;
    bool canHold = true;

    [Header("WebGL 권장: 960x600")]
    public float dropInterval = 0.5f;
    float timer;
    float lockTimer;
    bool inLockDelay;
    bool gameOver;
    bool paused;
    bool inputLocked;

    int score;
    int level = 1;
    int totalLines;
    int combo;

    GameObject[,] fixedCubes = new GameObject[TetrisCore.Width, TetrisCore.Height];
    List<GameObject> activeCubes = new();
    readonly List<GameObject> popups = new();
    List<GameObject> ghostCubes = new();
    List<GameObject> previewQuads = new();
    List<GameObject> nextMinis = new();
    List<GameObject> holdMinis = new();
    Material[] blockMats = new Material[7];
    GameObject blockPrefab; // Prefabs/Block (V2 과일 블럭)
    Material ghostGrayMat;
    Material flashMat;
    Material previewMat;
    GameObject boardRoot;
    Material bgMat;
    Camera mainCam;
    Vector3 camBasePos;

    // 파티클 풀
    struct BurstPart { public GameObject go; public Vector3 vel; public float life; public float maxLife; }
    readonly List<BurstPart> parts = new();
    int partIdx;
    const float PartSize = 0.16f;

    // 화면 흔들림
    float shakeT, shakeDur, shakeMag;

    // 오디오 (절차적 합성, 에셋 없음)
    AudioSource sfx;
    AudioSource bgm; // BGM 전용 (루프, SFX와 분리)
    AudioClip sMove, sRotate, sLock, sHold, sPause, sLevel, sOver;
    AudioClip sClear1, sClear2, sClear3, sTetris;
    AudioClip bgmClip;
    bool muted;

    // UI
    Canvas uiCanvas;
    TMP_FontAsset uiFont;
    TextMeshProUGUI scoreText;
    TextMeshProUGUI comboText;
    GameObject gameOverPanel;
    TextMeshProUGUI finalScoreText;
    GameObject pausePanel;
    GameObject leftPadGO, rightPadGO;
    Image padImg, sndImg;
    bool padsOn = true;

    // 터치 홀드 상태 (버튼이 누르고 있는 동안 true)
    TouchHoldButton btnLeft, btnRight, btnDown;
    readonly List<TouchHoldButton> padButtons = new();

    // 홀드 반복 (DAS: 첫 반복까지 대기, ARR: 이후 반복 간격)
    float repLeft, repRight;
    bool prevLeft, prevRight;
    const float DAS = 0.18f, ARR = 0.06f;

    static readonly Color[] BlockColors = new[]
    {
        new Color(0.2f,0.8f,0.9f), // I cyan
        new Color(0.95f,0.8f,0.2f), // O yellow
        new Color(0.7f,0.4f,0.9f), // T purple
        new Color(0.3f,0.85f,0.4f), // S green
        new Color(0.95f,0.3f,0.3f), // Z red
        new Color(0.3f,0.5f,0.95f), // J blue
        new Color(0.95f,0.55f,0.2f), // L orange
    };

    void Awake()
    {
        Application.targetFrameRate = 60;
        SetupCamera();
        SetupMaterials();
        SetupBoardFrame();
        SetupEffects();
        EnsureAudio();
        BuildClips();
        BuildHudFromPrefabs();
        SetupWorldSidePanels();
        Restart();
    }

    void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
        }
        cam.transform.position = new Vector3(TetrisCore.Width / 2f - 0.5f, TetrisCore.Height / 2f - 0.5f, -16f);
        cam.orthographic = true;
        cam.orthographicSize = TetrisCore.Height / 2f + 1.2f;
        cam.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        mainCam = cam;
        camBasePos = cam.transform.position;
        // 무음 수정: 리스너가 씬에 하나도 없으면 Unity가 전체 음소거함.
        // 구버전 공통 버그이나 구 코드는 비교용으로 동결 → V2만 수정.
#if UNITY_6000_0_OR_NEWER
        if (cam.GetComponent<AudioListener>() == null && FindFirstObjectByType<AudioListener>() == null)
#else
        if (cam.GetComponent<AudioListener>() == null && FindObjectOfType<AudioListener>() == null)
#endif
            cam.gameObject.AddComponent<AudioListener>();
    }

    void SetupMaterials()
    {
        blockPrefab = Resources.Load<GameObject>("Prefabs/Block");
        for (int i = 0; i < 7; i++)
            blockMats[i] = Resources.Load<Material>($"Materials/Fruit_{(TetrominoType)i}") ?? CreateLitMaterial(BlockColors[i]);
        bgMat = Resources.Load<Material>("Materials/BoardBg") ?? CreateLitMaterial(new Color(0.13f, 0.15f, 0.2f));
        // 빌드에서 Shader.Find가 null일 수 있어 Resources 머티리얼을 복제 (셰이더 참조 유지)
        ghostGrayMat = CloneWithAlpha(blockMats[0], new Color(0.78f, 0.82f, 0.9f), 0.14f);
        previewMat = CloneWithAlpha(blockMats[0], Color.white, 0.1f);
        flashMat = new Material(blockMats[0]);
        flashMat.color = Color.white;
        flashMat.mainTexture = null; // 순백 플래시 (과일 그림 제거)
        ghostGrayMat.mainTexture = null; // 반투명 고스트 깔끔하게
        previewMat.mainTexture = null; // 예고 쿼드 깔끔하게
        if (flashMat.HasProperty("_EmissionColor")) flashMat.SetColor("_EmissionColor", Color.white * 0.9f);
        flashMat.EnableKeyword("_EMISSION");
    }

    // 기존 머티리얼 복제 + 투명화. Shader.Find를 호출하지 않아 WebGL 스트리핑에 안전
    static Material CloneWithAlpha(Material src, Color c, float alpha)
    {
        var m = new Material(src);
        m.color = new Color(c.r, c.g, c.b, alpha);
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f); // URP 투명
        else
        {
            m.SetFloat("_Mode", 3f); // Built-in Transparent
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
        }
        return m;
    }

    // Resources 예비 경로 전용 (빌드 정상 시 호출되지 않음). 빌드에서는 Resources 에셋이 셰이더를 포함
    Material CreateLitMaterial(Color c)
    {
        var m = new Material(LitShader());
        m.color = c;
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.35f);
        else if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.65f);
        return m;
    }

    // 구 런타임 생성 경로 (미사용, WebGL 스트리핑 회피를 위해 CloneWithAlpha로 대체됨)
    static Material MakeGhostMaterialUnused(Color c, float alpha)
    {
        var m = new Material(LitShader());
        m.color = new Color(c.r, c.g, c.b, alpha);
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f); // URP 투명
        else
        {
            m.SetFloat("_Mode", 3f); // Built-in Transparent
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
        }
        return m;
    }

    // Built-in RP(Standard/Unlit) + URP(Lit/Unlit) 모두 대응. WebGL 포함
    static Shader LitShader() =>
        Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

    static Shader UnlitShader() =>
        Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");

    void SetupBoardFrame()
    {
        boardRoot = new GameObject("Board");
        // 뒷판
        var quad = QuadMesh();
        if (quad != null)
        {
            var bg = NewBlockObject("BoardBg", quad, bgMat,
                new Vector3(TetrisCore.Width / 2f - 0.5f, TetrisCore.Height / 2f - 0.5f, 0.6f),
                new Vector3(TetrisCore.Width + 0.4f, TetrisCore.Height + 0.4f, 1));
            bg.transform.parent = boardRoot.transform;
        }
        else
        {
            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(bg.GetComponent<Collider>());
            bg.transform.position = new Vector3(TetrisCore.Width / 2f - 0.5f, TetrisCore.Height / 2f - 0.5f, 0.6f);
            bg.transform.localScale = new Vector3(TetrisCore.Width + 0.4f, TetrisCore.Height + 0.4f, 1);
            bg.GetComponent<MeshRenderer>().material = bgMat;
            bg.transform.parent = boardRoot.transform;
        }

        // 테두리
        var lineMat = Resources.Load<Material>("Materials/BoardLine");
        if (lineMat == null)
        {
            lineMat = new Material(bgMat);
            lineMat.color = new Color(0.3f, 0.35f, 0.45f);
        }
        for (int i = 0; i < 4; i++)
        {
            Vector3 pos = Vector3.zero, scl = Vector3.one;
            if (i == 0) { pos = new Vector3(-0.65f, TetrisCore.Height / 2f - 0.5f, 0); scl = new Vector3(0.3f, TetrisCore.Height + 1, 0.5f); }
            if (i == 1) { pos = new Vector3(TetrisCore.Width - 0.35f, TetrisCore.Height / 2f - 0.5f, 0); scl = new Vector3(0.3f, TetrisCore.Height + 1, 0.5f); }
            if (i == 2) { pos = new Vector3(TetrisCore.Width / 2f - 0.5f, -0.65f, 0); scl = new Vector3(TetrisCore.Width + 1, 0.3f, 0.5f); }
            if (i == 3) { pos = new Vector3(TetrisCore.Width / 2f - 0.5f, TetrisCore.Height - 0.35f, 0); scl = new Vector3(TetrisCore.Width + 1, 0.3f, 0.5f); }
            GameObject wall;
            var cm = CubeMesh();
            if (cm != null)
            {
                wall = NewBlockObject("Wall", cm, lineMat, pos, scl);
            }
            else
            {
                wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(wall.GetComponent<Collider>());
                wall.GetComponent<MeshRenderer>().material = lineMat;
                wall.transform.position = pos;
                wall.transform.localScale = scl;
            }
            wall.transform.parent = boardRoot.transform;
        }

        // 방향광 (WebGL 저비용)
        var light =
#if UNITY_6000_0_OR_NEWER
            FindFirstObjectByType<Light>();
#else
            FindObjectOfType<Light>();
#endif
        if (light == null)
        {
            var lgo = new GameObject("Dir Light");
            light = lgo.AddComponent<Light>();
            light.type = LightType.Directional;
        }
        light.transform.rotation = Quaternion.Euler(30, -30, 0);
        light.intensity = 1.1f;
    }

    void SetupEffects()
    {
        // 삭제 예고 하이라이트 풀 (최대 4줄)
        var quad = QuadMesh();
        for (int i = 0; i < 4; i++)
        {
            GameObject q = quad != null
                ? NewBlockObject("Preview", quad, previewMat, Vector3.zero, new Vector3(TetrisCore.Width, 0.85f, 1))
                : new GameObject("Preview");
            q.transform.parent = boardRoot.transform;
            q.SetActive(false);
            previewQuads.Add(q);
        }
        // 파티클 풀
        var cube = CubeMesh();
        for (int i = 0; i < 64; i++)
        {
            GameObject go = blockPrefab != null
                ? Instantiate(blockPrefab, Vector3.zero, Quaternion.identity)
                : (cube != null
                    ? NewBlockObject("P", cube, blockMats[0], Vector3.zero, Vector3.one * PartSize)
                    : GameObject.CreatePrimitive(PrimitiveType.Cube));
            go.name = "P";
            go.transform.localScale = Vector3.one * PartSize;
            go.transform.parent = boardRoot.transform;
            go.SetActive(false);
            parts.Add(new BurstPart { go = go, vel = Vector3.zero, life = 0, maxLife = 0.6f });
        }
    }

    // ---------- 오디오 (절차적 합성) ----------

    void EnsureAudio()
    {
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        muted = PlayerPrefs.GetInt("tetris_mute", 0) == 1;
        sfx.mute = muted;
        bgm = gameObject.AddComponent<AudioSource>();
        bgm.playOnAwake = false;
        bgm.loop = true;
        bgm.volume = 0.5f;
        bgm.mute = muted;
    }

    AudioClip MakeTone(float freq, float dur, float vol = 0.35f)
    {
        int rate = 22050, n = Mathf.Max(1, (int)(rate * dur));
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            d[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * vol * Mathf.Exp(-3f * t / dur);
        }
        var c = AudioClip.Create("tone", n, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }

    AudioClip MakeSeq(float[] freqs, float noteDur, float vol = 0.35f)
    {
        int rate = 22050, per = Mathf.Max(1, (int)(rate * noteDur));
        var d = new float[per * freqs.Length];
        for (int k = 0; k < freqs.Length; k++)
            for (int i = 0; i < per; i++)
            {
                float t = (float)i / rate;
                d[k * per + i] = Mathf.Sin(2f * Mathf.PI * freqs[k] * t) * vol * Mathf.Exp(-2.5f * t / noteDur);
            }
        var c = AudioClip.Create("seq", d.Length, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }

    // 8비트 BGM (테트리스 Theme A 편곡식 루프, 코드 합성이라 에셋·용량 부담 없음).
    // 네이버 낭만피아노 악보(♩=145) 기반: A-B-A-B-코랄-런, 104박 약 43초 루프.
    // 확실한 부분: 템포 145, A/B 표준선율, 코랄 리듬(느린 2분음표), 런 존재+올림음(#) 사용.
    // 추정한 부분: 코랄 겹음 중 내성, 런의 정확한 음높이 (윤곽만 채보, 시聴 후 교정).
    // ff 코랄대편(17-24)·후반부(33-)는 단성 신스로 옮기기 어려워 생략. 게임 루프엔 불필요.
    bool bgmNeedStart = true;
    AudioClip BuildBgm()
    {
        const float beat = 60f / 145f; // 악보 지정 템포
        const int rate = 22050;
        // (midi, 박). -1 = 쉼표. E5=76 B4=71 C5=72 D5=74 A4=69 F5=77 A5=81 G5=79
        // 구조 A-B-A-B-코랄-런 반복 (네이버 악보 1-16·25-32마디). A/B=민요 원형, 코랄/런=편곡부.
        var mel = new (int m, float b)[]
        {
            // A
            (76,1),(71,.5f),(72,.5f),(74,1),(72,.5f),(71,.5f),(69,1),(69,.5f),(72,.5f),
            (76,1),(74,.5f),(72,.5f),(71,1.5f),(72,.5f),(74,1),(76,1),(72,1),(69,1),(69,1),(-1,.5f),
            // B
            (74,1.5f),(77,.5f),(81,1),(79,.5f),(77,.5f),(76,1.5f),(72,.5f),
            (76,1),(74,.5f),(72,.5f),(71,1),(71,.5f),(72,.5f),(74,1),(76,1),(72,1),(69,1),(69,1),(-1,1),
            // A
            (76,1),(71,.5f),(72,.5f),(74,1),(72,.5f),(71,.5f),(69,1),(69,.5f),(72,.5f),
            (76,1),(74,.5f),(72,.5f),(71,1.5f),(72,.5f),(74,1),(76,1),(72,1),(69,1),(69,1),(-1,.5f),
            // B
            (74,1.5f),(77,.5f),(81,1),(79,.5f),(77,.5f),(76,1.5f),(72,.5f),
            (76,1),(74,.5f),(72,.5f),(71,1),(71,.5f),(72,.5f),(74,1),(76,1),(72,1),(69,1),(69,1),(-1,1),
            // 코랄 (네이버 10-15마디 윤곽: 느린 2분음표, E장조 색채. 겹음 내성은 추정)
            (76,2),(79,2),
            (80,2),(76,2),
            (81,2),(79,2),
            (76,4),
            (80,2),(76,2),
            (76,2),(74,2),
            // 런 (네이버 25-32마디 윤곽: 16분음표 상행, E화성단음계. 정확한 음높이는 추정)
            (76,.25f),(78,.25f),(80,.25f),(81,.25f),(83,.25f),(84,.25f),(83,.25f),(81,.25f),
            (80,.25f),(78,.25f),(80,.25f),(81,.25f),(83,.25f),(81,.25f),(80,.25f),(78,.25f),
            (76,.25f),(78,.25f),(80,.25f),(81,.25f),(83,.25f),(84,.25f),(86,.25f),(84,.25f),
            (83,.25f),(81,.25f),(80,.25f),(81,.25f),(83,.25f),(81,.25f),(80,.25f),(78,.25f),
            (80,.25f),(81,.25f),(83,.25f),(84,.25f),(83,.25f),(81,.25f),(80,.25f),(81,.25f),
            (76,.25f),(78,.25f),(80,.25f),(81,.25f),(83,.25f),(81,.25f),(80,.25f),(78,.25f),
            (79,.25f),(81,.25f),(83,.25f),(81,.25f),(80,.25f),(81,.25f),(80,.25f),(78,.25f),
            (76,.25f),(78,.25f),(80,.25f),(78,.25f),(76,.25f),(74,.25f),(76,.25f),(76,.25f),
        };
        float totalBeats = 0;
        foreach (var n in mel) totalBeats += n.b;
        int total = (int)(rate * totalBeats * beat);
        var d = new float[total];
        float t = 0;
        foreach (var n in mel)
        {
            int len = (int)(rate * n.b * beat);
            int start = (int)(rate * t * beat);
            if (n.m >= 0)
            {
                float f = 440f * Mathf.Pow(2f, (n.m - 69) / 12f);
                for (int i = 0; i < len && start + i < total; i++)
                {
                    float tt = (float)i / rate;
                    float env = Mathf.Min(1f, tt / 0.008f) * Mathf.Min(1f, (len - i) / (rate * 0.03f)) * Mathf.Exp(-0.4f * tt / (n.b * beat));
                    float ph = 2f * Mathf.PI * f * tt;
                    d[start + i] += 0.16f * env * (Mathf.Sin(ph) + Mathf.Sin(3f * ph) / 6f);
                }
            }
            t += n.b;
        }
        // 베이스: 파트별 루트 (악보 베이스 라인 기준. Am E / Dm G C Am / E Am C Am E / Am E E C E / Am E Dm E)
        // 이 과정을 하는 이유: 전역 순환은 G# 선율과 F 루트가 충돌해 틀린 화음이 나기 때문.
        float[] secBeats = { 15.5f, 16f, 15.5f, 16f, 24f, 16f };
        int[][] secRoots = {
            new[]{45,40,45,40}, new[]{38,43,36,45}, new[]{45,40,45,40},
            new[]{38,43,36,45}, new[]{40,40,36,40,40,45}, new[]{40,40,40,40},
        };
        const float barBeats = 4f;
        float secStart = 0;
        for (int s = 0; s < secBeats.Length; s++)
        {
            int nbars = Mathf.CeilToInt(secBeats[s] / barBeats);
            for (int bar = 0; bar < nbars; bar++)
            {
                float f = 440f * Mathf.Pow(2f, (secRoots[s][bar % secRoots[s].Length] - 69) / 12f);
                int start = (int)(rate * (secStart + bar * barBeats) * beat);
                int len = (int)(rate * barBeats * beat);
            for (int i = 0; i < len && start + i < total; i++)
            {
                float tt = (float)i / rate;
                float env = Mathf.Min(1f, tt / 0.01f) * Mathf.Exp(-1.2f * tt / (barBeats * beat));
                float ph = 2f * Mathf.PI * f * tt;
                d[start + i] += 0.10f * env * (Mathf.Sin(ph) + Mathf.Sin(3f * ph) / 3f);
            }
        }
            secStart += secBeats[s];
        }
        // 루프 이음새 클릭 방지: 끝 50ms 페이드아웃 (시작은 어택이 0부터라 불필요).
        // 이 과정을 하는 이유: 베이스가 마디 중간에 잘리면 매 루프마다 틱 소리가 나기 때문.
        int fade = (int)(rate * 0.05f);
        for (int i = 0; i < fade && i < total; i++)
            d[total - 1 - i] *= Mathf.Min(1f, (float)i / fade);
        var c = AudioClip.Create("bgm", total, 1, rate, false);        c.SetData(d, 0);
        return c;
    }

    void BuildClips()
    {
        sMove = MakeTone(660f, 0.05f, 0.22f);
        sRotate = MakeTone(520f, 0.07f);
        sLock = MakeTone(200f, 0.09f);
        sHold = MakeTone(440f, 0.08f);
        sPause = MakeTone(880f, 0.05f, 0.3f);
        sClear1 = MakeSeq(new[] { 523f, 659f }, 0.09f);
        sClear2 = MakeSeq(new[] { 523f, 659f, 784f }, 0.09f);
        sClear3 = MakeSeq(new[] { 523f, 659f, 784f, 880f }, 0.09f);
        sTetris = MakeSeq(new[] { 523f, 659f, 784f, 1046f, 1318f }, 0.09f);
        sLevel = MakeSeq(new[] { 440f, 554f, 659f, 880f }, 0.1f);
        sOver = MakeSeq(new[] { 392f, 330f, 262f, 196f }, 0.16f);
        // BGM 음원 파일 우선 (없으면 절차 합성 예비).
        // 이 과정을 하는 이유: 씬이 매번 재생성돼서 인스펙터 참조가 안 살아남으니 Resources 로드가 필요하고,
        // 파일이 빠져도 무음으로 죽지 않게 예비가 있기 때문.
        // 출처 주의: bgm_nes.mp3는 외부 리믹스 음원 (연습용 팬메이드로 사용자 확인 후 사용, 정식 배포 전 교체 필요).
        bgmClip = Resources.Load<AudioClip>("Audio/bgm_nes");
        if (bgmClip == null) bgmClip = BuildBgm();
        if (bgm != null) { bgm.clip = bgmClip; bgm.Play(); }
    }

    void Play(AudioClip c, float v = 1f)
    {
        if (c != null && sfx != null) sfx.PlayOneShot(c, v);
    }

    // ---------- 월드 사이드 패널 (Next/Hold 미니) ----------

    void SetupWorldSidePanels()
    {
        MakeWorldLabel("NEXT", new Vector3(12.2f, 15.2f, 0), 64, 0.125f, Color.white);
        MakeWorldLabel("HOLD", new Vector3(-2.6f, 15.2f, 0), 64, 0.125f, Color.white);
    }

    TextMeshPro MakeWorldLabel(string text, Vector3 pos, float fontSize, float scale, Color color)
    {
        var go = new GameObject("WLabel");
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * scale;
        go.transform.parent = boardRoot.transform;
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.font = uiFont;
        tmp.color = color;
        return tmp;
    }

    void BuildMini(List<GameObject> list, TetrominoType t, Vector3 center, float cell)
    {
        var cells = TetrisCore.BaseCells(t);
        float minx = 99, maxx = -99, miny = 99, maxy = -99;
        foreach (var c in cells)
        {
            if (c.x < minx) minx = c.x; if (c.x > maxx) maxx = c.x;
            if (c.y < miny) miny = c.y; if (c.y > maxy) maxy = c.y;
        }
        Vector3 off = center - new Vector3((minx + maxx) / 2f * cell, (miny + maxy) / 2f * cell, 0);
        foreach (var c in cells)
        {
            var go = MakeCube((int)t, off + new Vector3(c.x * cell, c.y * cell, 0));
            go.transform.localScale = Vector3.one * (0.92f * cell);
            go.transform.parent = boardRoot.transform;
            list.Add(go);
        }
    }

    void ClearMinis(List<GameObject> list)
    {
        foreach (var g in list) Destroy(g);
        list.Clear();
    }

    void RefreshNextPreview()
    {
        ClearMinis(nextMinis);
        for (int i = 0; i < nextQueue.Count && i < 3; i++)
            BuildMini(nextMinis, nextQueue[i], new Vector3(12.2f, 13.2f - 3.4f * i, 0), 0.45f);
    }

    void RefreshHoldPreview()
    {
        ClearMinis(holdMinis);
        if (holdType != null)
            BuildMini(holdMinis, holdType.Value, new Vector3(-2.6f, 13.2f, 0), 0.45f);
    }

    // ---------- UI (Canvas + TextMeshPro + 터치 패드) ----------

    void BuildHudFromPrefabs()
    {
        uiFont = Resources.Load<TMP_FontAsset>("Fonts/NotoSansKR SDF");
        if (uiFont == null) uiFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (uiFont == null)
            Debug.LogError("[TetrisV2] Korean font missing.");

        var hudPrefab = Resources.Load<GameObject>("Prefabs/UI/HudCanvas");
        var btnPrefab = Resources.Load<GameObject>("Prefabs/UI/PadButton");
        if (hudPrefab == null || btnPrefab == null)
        {
            Debug.LogError("[TetrisV2] UI prefab missing. Run: Tetris > Build UI Prefabs");
            return;
        }
        var inst = Instantiate(hudPrefab);
        inst.name = "HudCanvas";
        uiCanvas = inst.GetComponent<Canvas>();
        scoreText = inst.transform.Find("ScoreText")?.GetComponent<TextMeshProUGUI>();
        comboText = inst.transform.Find("ComboText")?.GetComponent<TextMeshProUGUI>();
        gameOverPanel = inst.transform.Find("GameOverPanel")?.gameObject;
        finalScoreText = gameOverPanel?.transform.Find("PanelSubLabel")?.GetComponent<TextMeshProUGUI>();
        pausePanel = inst.transform.Find("PausePanel")?.gameObject;
        // 힌트는 모바일(터치 패드) 기준 문구로 런타임 지정.
        // 이 과정을 하는 이유: 프리팹 기본값은 키보드 전제(방향키/UP/SPACE)라
        // 모바일에서 안내가 어긋나고, 코드 지정이면 WORK 쪽 비주얼 작업과 충돌이 없기 때문.
        // 사용 글리프(홀)는 TetrisKoreanFontSetup.KoreanChars에 포함되어 static bake에 구워짐.
        var hintText = inst.transform.Find("HintText")?.GetComponent<TextMeshProUGUI>();
        if (hintText != null) hintText.text = "◄► 이동 / ▼ 소프트드롭 / ↺↻ 회전 / DROP 하드드롭 / SWAP 홀드";
        leftPadGO = inst.transform.Find("MovePad")?.gameObject;
        rightPadGO = inst.transform.Find("ActionPad")?.gameObject;
        var sysPad = inst.transform.Find("SysPad");

        btnLeft = AddPadButton(btnPrefab, leftPadGO?.transform, Pick("◄", "<"), 44, null);
        btnDown = AddPadButton(btnPrefab, leftPadGO?.transform, Pick("▼", "v"), 44, null);
        btnRight = AddPadButton(btnPrefab, leftPadGO?.transform, Pick("►", ">"), 44, null);

        // 우측 패드: 동작 버튼은 동사형(SWAP/DROP), 월드 좌측 HOLD는 영역명.
        // 이 과정을 하는 이유: 양쪽이 같은 "HOLD"라 영역 표시인지 동작 버튼인지 헷갈리고,
        // CCW/CW를 44pt에 두면 폴백 텍스트(CCW)가 88px 버튼에서 줄바꿈되기 때문.
        AddPadButton(btnPrefab, rightPadGO?.transform, "SWAP", 26, OnHold);
        AddPadButton(btnPrefab, rightPadGO?.transform, Pick("↺", "CCW"), 26, OnRotateCCW);
        AddPadButton(btnPrefab, rightPadGO?.transform, Pick("↻", "CW"), 26, OnRotateCW);
        AddPadButton(btnPrefab, rightPadGO?.transform, "DROP", 26, OnHardDrop);

        AddPadButton(btnPrefab, sysPad, "II", 44, TogglePause);
        var padTap = AddPadButton(btnPrefab, sysPad, "PAD", 26, TogglePads);
        var sndTap = AddPadButton(btnPrefab, sysPad, "SND", 26, ToggleMute);
        padImg = padTap?.GetComponent<Image>();
        sndImg = sndTap?.GetComponent<Image>();

        WirePanelButton(gameOverPanel?.transform, "RestartBtn", Restart);
        WirePanelButton(pausePanel?.transform, "ResumeBtn", TogglePause);

        // 저장된 설정 적용
        padsOn = PlayerPrefs.GetInt("tetris_pads", 1) == 1;
        ApplyPadVisibility();
        ApplyMuteVisual();
    }

    TouchHoldButton AddPadButton(GameObject prefab, Transform parent, string label, int fontSize, UnityAction act)
    {
        if (prefab == null || parent == null) return null;
        var go = Instantiate(prefab, parent, false);
        go.name = "PadBtn_" + label;
        var tmp = go.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) { tmp.text = label; tmp.fontSize = fontSize; tmp.textWrappingMode = TextWrappingModes.NoWrap; }
        var hold = go.GetComponent<TouchHoldButton>();
        if (hold != null)
        {
            padButtons.Add(hold);
            if (act != null) hold.onTap.AddListener(act);
        }
        return hold;
    }

    void WirePanelButton(Transform root, string path, UnityAction act)
    {
        var btn = root?.Find(path)?.GetComponent<Button>();
        if (btn != null) btn.onClick.AddListener(act);
        else Debug.LogError("[TetrisV2] panel button missing: " + path);
    }

    // 기본 폰트+폴백에 없는 글리프면 ASCII 대체 (네모박스 방지)
    string Pick(string main, string fallback)
    {
        if (uiFont != null && uiFont.HasCharacters(main)) return main;
        var lib = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (lib != null && lib != uiFont && lib.HasCharacters(main)) return main;
        return fallback;
    }

    void UpdateScoreUI()
    {
        if (scoreText != null)
            scoreText.text = $"점수 {score}   레벨 {level}   줄 {totalLines}";
        if (comboText != null)
        {
            comboText.gameObject.SetActive(combo >= 2);
            if (combo >= 2) comboText.text = $"콤보 x{combo}";
        }
    }

    void ShowGameOver()
    {
        if (finalScoreText != null)
            finalScoreText.text = $"점수 {score}";
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);
        Play(sOver);
    }

    // ---------- 게임 로직 ----------

    TetrominoType NextFromBag()
    {
        if (bag.Count == 0)
        {
            var arr = new List<TetrominoType> { TetrominoType.I, TetrominoType.O, TetrominoType.T, TetrominoType.S, TetrominoType.Z, TetrominoType.J, TetrominoType.L };
            for (int i = 0; i < arr.Count; i++)
            {
                int j = Random.Range(i, arr.Count);
                (arr[i], arr[j]) = (arr[j], arr[i]);
            }
            foreach (var t in arr) bag.Enqueue(t);
        }
        return bag.Dequeue();
    }

    void RefillQueue()
    {
        while (nextQueue.Count < 3) nextQueue.Add(NextFromBag());
    }

    void Restart()
    {
        StopAllCoroutines();
        core = new TetrisCore();
        bag.Clear();
        nextQueue.Clear();
        holdType = null;
        canHold = true;
        score = 0; level = 1; totalLines = 0; combo = 0; dropInterval = 0.5f;
        gameOver = false; paused = false; inLockDelay = false; inputLocked = false; timer = 0;
        repLeft = repRight = 0; prevLeft = prevRight = false;
        shakeT = 0;
        if (mainCam != null) mainCam.transform.position = camBasePos;
        foreach (var c in activeCubes) Destroy(c);
        activeCubes.Clear();
        foreach (var p in popups) if (p != null) Destroy(p);
        popups.Clear();
        ClearMinis(nextMinis);
        ClearMinis(holdMinis);
        foreach (var g in ghostCubes) Destroy(g);
        ghostCubes.Clear();
        HidePreview();
        foreach (var p in parts) { p.go.SetActive(false); }
        for (int x = 0; x < TetrisCore.Width; x++)
            for (int y = 0; y < TetrisCore.Height; y++)
                if (fixedCubes[x, y] != null) { Destroy(fixedCubes[x, y]); fixedCubes[x, y] = null; }
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (bgm != null) { bgm.pitch = 1f; if (!bgm.isPlaying) bgm.Play(); }
        UpdateScoreUI();
        RefreshHoldPreview();
        SpawnNext();
    }

    void SpawnNext()
    {
        RefillQueue();
        var t = nextQueue[0];
        nextQueue.RemoveAt(0);
        SpawnTyped(t);
        RefreshNextPreview();
        RefreshActiveCubes();
    }

    void SpawnTyped(TetrominoType t)
    {
        core.Spawn(t);
        if (!core.IsValid(core.CurrentPos, core.CurrentCells))
        {
            gameOver = true;
            ShowGameOver();
        }
        inLockDelay = false;
    }

    void Update()
    {
        // WebGL은 첫 제스처 전까지 오디오가 잠겨 있어서 입력이 들어오면 BGM 시작.
        // 이 과정을 하는 이유: Awake에서 Play해도 suspended 상태라 무음으로 끝나버리기 때문.
        if (bgmNeedStart && bgm != null && (Input.anyKeyDown || Input.touchCount > 0 || Input.GetMouseButtonDown(0)))
        {
            bgmNeedStart = false;
            bgm.Play();
        }
        if (gameOver)
        {
            if (Input.GetKeyDown(KeyCode.R)) Restart();
            return;
        }
        if (paused)
        {
            if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Escape)) TogglePause();
            return;
        }
        HandleInput();
        HandleGravity();
        RefreshActiveCubes();
        UpdateParticles(Time.deltaTime);
    }

    void LateUpdate()
    {
        if (mainCam == null) return;
        if (shakeT > 0)
        {
            shakeT -= Time.unscaledDeltaTime;
            float k = shakeMag * Mathf.Max(0, shakeT / shakeDur);
            mainCam.transform.position = camBasePos + new Vector3(Random.Range(-k, k), Random.Range(-k, k), 0);
        }
        else if (mainCam.transform.position != camBasePos)
            mainCam.transform.position = camBasePos;
    }

    void HandleInput()
    {
        if (paused || inputLocked) return;
        if (Input.GetKeyDown(KeyCode.C)) DoHold();
        if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Escape)) TogglePause();
        if (Input.GetKeyDown(KeyCode.M)) ToggleMute();

        bool leftHeld = Input.GetKey(KeyCode.LeftArrow) || (btnLeft != null && btnLeft.held);
        bool rightHeld = Input.GetKey(KeyCode.RightArrow) || (btnRight != null && btnRight.held);
        bool downHeld = Input.GetKey(KeyCode.DownArrow) || (btnDown != null && btnDown.held);

        if (Input.GetKeyDown(KeyCode.UpArrow)) { if (core.TryRotate(1)) Play(sRotate); }
        if (Input.GetKeyDown(KeyCode.Z)) { if (core.TryRotate(-1)) Play(sRotate); }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            OnHardDrop();
            return;
        }

        if (downHeld) timer += Time.deltaTime * 15f; // 소프트드롭

        // 홀드 반복 이동 (키보드+터치 공통, DAS/ARR)
        MoveRepeat(ref repLeft, ref prevLeft, leftHeld && !rightHeld, -1);
        MoveRepeat(ref repRight, ref prevRight, rightHeld && !leftHeld, 1);
    }

    void MoveRepeat(ref float t, ref bool prev, bool held, int dx)
    {
        if (held)
        {
            if (!prev)
            {
                if (core.TryMove(dx, 0)) { inLockDelay = false; Play(sMove, 0.5f); }
                t = 0f;
            }
            else
            {
                t += Time.deltaTime;
                if (t >= DAS)
                {
                    if (core.TryMove(dx, 0)) { inLockDelay = false; Play(sMove, 0.5f); }
                    t = DAS - ARR;
                }
            }
        }
        else t = 0f;
        prev = held;
    }

    // 터치 버튼 연결용 (누르는 순간 1회 실행)
    public void OnRotateCW() { if (!gameOver && !paused && core.TryRotate(1)) Play(sRotate); }
    public void OnRotateCCW() { if (!gameOver && !paused && core.TryRotate(-1)) Play(sRotate); }
    public void OnHardDrop() { if (!gameOver && !paused && !inputLocked) { core.HardDrop(); AfterLock(); } }
    public void OnHold() => DoHold();

    public void TogglePause()
    {
        if (gameOver) return;
        paused = !paused;
        if (pausePanel != null) pausePanel.SetActive(paused);
        if (bgm != null) { if (paused) bgm.Pause(); else bgm.UnPause(); }
        Play(sPause);
    }

    public void TogglePads()
    {
        padsOn = !padsOn;
        PlayerPrefs.SetInt("tetris_pads", padsOn ? 1 : 0);
        PlayerPrefs.Save();
        ApplyPadVisibility();
        Play(sPause);
    }

    void ApplyPadVisibility()
    {
        foreach (var b in padButtons) if (b != null) b.held = false; // 토글 중 눌림 stuck 방지
        if (leftPadGO != null) leftPadGO.SetActive(padsOn);
        if (rightPadGO != null) rightPadGO.SetActive(padsOn);
        if (padImg != null) padImg.color = new Color(1, 1, 1, padsOn ? 0.18f : 0.07f);
    }

    public void ToggleMute()
    {
        muted = !muted;
        if (sfx != null) sfx.mute = muted;
        if (bgm != null) bgm.mute = muted;
        PlayerPrefs.SetInt("tetris_mute", muted ? 1 : 0);
        PlayerPrefs.Save();
        ApplyMuteVisual();
        Play(sPause);
    }

    void ApplyMuteVisual()
    {
        if (sndImg != null) sndImg.color = new Color(1, 1, 1, muted ? 0.07f : 0.18f);
    }

    void DoHold()
    {
        if (gameOver || paused || inputLocked || !canHold) return;
        Play(sHold);
        if (holdType == null)
        {
            holdType = core.CurrentType;
            SpawnNext();
        }
        else
        {
            var t = holdType.Value;
            holdType = core.CurrentType;
            SpawnTyped(t);
            RefreshActiveCubes();
        }
        canHold = false;
        RefreshHoldPreview();
    }

    void HandleGravity()
    {
        if (paused || inputLocked) return;
        timer += Time.deltaTime;
        float interval = dropInterval;
        if (timer < interval && !inLockDelay) return;
        timer = 0;

        if (core.StepDown())
        {
            inLockDelay = false;
            lockTimer = 0;
        }
        else
        {
            // 바닥: Lock Delay 0.5초
            if (!inLockDelay) { inLockDelay = true; lockTimer = 0; }
            lockTimer += Time.deltaTime;
            if (lockTimer >= 0.5f || Input.GetKey(KeyCode.DownArrow))
            {
                core.LockPiece();
                AfterLock();
            }
            else timer = interval - 0.05f; // 다음 프레임에 재시도
        }
    }

    void AfterLock()
    {
        if (inputLocked || gameOver) return;
        Play(sLock);
        StartCoroutine(AfterLockCo());
    }

    IEnumerator AfterLockCo()
    {
        inputLocked = true;
        var rows = core.FindFullLines();
        if (rows.Count > 0)
        {
            combo++;
            int gained = (rows.Count switch { 1 => 100, 2 => 300, 3 => 500, _ => 800 }) * level;
            if (combo >= 2) gained += 50 * combo * level;
            score += gained;
            FlashRows(rows);
            BurstRows(rows);
            PopupScore(new Vector3(TetrisCore.Width / 2f - 0.5f, rows[rows.Count - 1] + 0.6f, -1f), $"+{gained}", Color.yellow);
            if (rows.Count >= 4) Shake(0.28f, 0.3f); else Shake(0.1f, 0.15f);
            Play(rows.Count switch { 1 => sClear1, 2 => sClear2, 3 => sClear3, _ => sTetris });
            UpdateScoreUI();
            yield return new WaitForSeconds(0.15f);
            core.ClearLines();
            RebuildFixedCubes();
            totalLines += rows.Count;
            if (totalLines / 10 + 1 > level)
            {
                level = totalLines / 10 + 1;
                dropInterval = Mathf.Max(0.05f, 0.5f - (level - 1) * 0.04f);
                if (bgm != null) bgm.pitch = 1f + Mathf.Min(0.12f, (level - 1) * 0.015f); // 레벨업마다 살짝 빨라짐
                PopupScore(new Vector3(TetrisCore.Width / 2f - 0.5f, TetrisCore.Height / 2f, -1f), "LEVEL UP", Color.cyan);
                Play(sLevel);
            }
        }
        else
        {
            combo = 0;
            AddFixedCubesForCurrent();
        }
        canHold = true;
        UpdateScoreUI();
        SpawnNext();
        inputLocked = false;
    }

    void FlashRows(List<int> rows)
    {
        foreach (int y in rows)
            for (int x = 0; x < TetrisCore.Width; x++)
                if (fixedCubes[x, y] != null)
                    fixedCubes[x, y].GetComponent<MeshRenderer>().material = flashMat;
    }

    void BurstRows(List<int> rows)
    {
        foreach (int y in rows)
        {
            int matId = 0;
            for (int x = 0; x < TetrisCore.Width; x++)
                if (core.Grid[x, y] != 0) { matId = core.Grid[x, y] - 1; break; }
            for (int i = 0; i < 6; i++)
                Burst(new Vector3(Random.Range(0, TetrisCore.Width), y + Random.Range(0f, 0.6f), 0), matId);
        }
    }

    void Burst(Vector3 pos, int matId)
    {
        int idx = partIdx;
        partIdx = (partIdx + 1) % parts.Count;
        var p = parts[idx];
        p.go.GetComponent<MeshRenderer>().material = blockMats[Mathf.Clamp(matId, 0, 6)];
        p.go.transform.position = pos;
        p.go.transform.localScale = Vector3.one * PartSize;
        p.vel = new Vector3(Random.Range(-3f, 3f), Random.Range(2f, 6f), 0);
        p.life = p.maxLife = Random.Range(0.45f, 0.7f);
        p.go.SetActive(true);
        parts[idx] = p;
    }

    void UpdateParticles(float dt)
    {
        for (int i = 0; i < parts.Count; i++)
        {
            var p = parts[i];
            if (p.life <= 0) continue;
            p.life -= dt;
            if (p.life <= 0) { p.go.SetActive(false); parts[i] = p; continue; }
            p.vel += new Vector3(0, -12f, 0) * dt;
            p.go.transform.position += p.vel * dt;
            p.go.transform.localScale = Vector3.one * (PartSize * (p.life / p.maxLife));
            parts[i] = p;
        }
    }

    void Shake(float mag, float dur)
    {
        shakeMag = mag;
        shakeDur = Mathf.Max(0.01f, dur);
        shakeT = shakeDur;
    }

    void PopupScore(Vector3 pos, string text, Color color)
    {
        var tmp = MakeWorldLabel(text, pos, 64, 0.2f, color);
        popups.Add(tmp.gameObject);
        StartCoroutine(PopupRise(tmp));
    }

    IEnumerator PopupRise(TextMeshPro tmp)
    {
        float t = 0;
        while (t < 0.9f && tmp != null)
        {
            t += Time.deltaTime;
            tmp.transform.position += Vector3.up * Time.deltaTime * 2f;
            tmp.alpha = 1f - t / 0.9f;
            yield return null;
        }
        if (tmp != null)
        {
            popups.Remove(tmp.gameObject);
            Destroy(tmp.gameObject);
        }
    }

    // ---------- 고스트 + 삭제 예고 ----------

    Vector2Int GhostPos()
    {
        var p = core.CurrentPos;
        while (core.IsValid(p + Vector2Int.down, core.CurrentCells)) p += Vector2Int.down;
        return p;
    }

    void RefreshGhost()
    {
        if (gameOver) { HideGhostAndPreview(); return; }
        var gp = GhostPos();
        EnsurePooledCubes(ghostCubes, core.CurrentCells.Count);
        for (int i = 0; i < core.CurrentCells.Count; i++)
        {
            var cell = core.CurrentCells[i];
            var go = ghostCubes[i];
            go.transform.position = new Vector3(gp.x + cell.x, gp.y + cell.y, 0.05f);
            go.transform.localScale = Vector3.one * 0.92f;
            go.GetComponent<MeshRenderer>().sharedMaterial = ghostGrayMat;
            go.SetActive(true);
        }
        RefreshPreviewLines(gp);
    }

    void RefreshPreviewLines(Vector2Int gp)
    {
        var rows = new List<int>();
        for (int y = 0; y < TetrisCore.Height; y++)
        {
            bool full = true;
            for (int x = 0; x < TetrisCore.Width; x++)
            {
                if (core.Grid[x, y] != 0) continue;
                bool covered = false;
                foreach (var cell in core.CurrentCells)
                    if (gp.x + cell.x == x && gp.y + cell.y == y) { covered = true; break; }
                if (!covered) { full = false; break; }
            }
            if (full) rows.Add(y);
        }
        for (int i = 0; i < previewQuads.Count; i++)
        {
            if (i < rows.Count)
            {
                previewQuads[i].transform.position = new Vector3(TetrisCore.Width / 2f - 0.5f, rows[i], 0.5f);
                previewQuads[i].SetActive(true);
            }
            else previewQuads[i].SetActive(false);
        }
    }

    void HidePreview()
    {
        foreach (var q in previewQuads) q.SetActive(false);
    }

    // 고정 4셀 풀: 부족하면 생성, 초과분은 숨김 (매 프레임 Destroy 방지)
    void EnsurePooledCubes(List<GameObject> list, int count)
    {
        while (list.Count < count)
        {
            var go = MakeCube(0, Vector3.zero);
            go.transform.parent = boardRoot.transform;
            go.SetActive(false);
            list.Add(go);
        }
        for (int i = count; i < list.Count; i++)
            if (list[i] != null) list[i].SetActive(false);
    }

    void HideActiveAndGhost()
    {
        foreach (var c in activeCubes) if (c != null) c.SetActive(false);
        HideGhostAndPreview();
    }

    void HideGhostAndPreview()
    {
        foreach (var g in ghostCubes) if (g != null) g.SetActive(false);
        HidePreview();
    }

    GameObject MakeCube(int matId, Vector3 pos, float s = 0.92f)
    {
        GameObject go;
        if (blockPrefab != null)
        {
            go = Instantiate(blockPrefab, pos, Quaternion.identity);
            go.name = "Block";
            go.transform.localScale = Vector3.one * s;
        }
        else // 예비: 프리팹 로드 실패 시 구버전 경로
        {
            var mesh = CubeMesh();
            if (mesh == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(go.GetComponent<Collider>());
                go.transform.position = pos;
                go.transform.localScale = Vector3.one * s;
            }
            else go = NewBlockObject("Block", mesh, blockMats[matId], pos, Vector3.one * s);
        }
        go.GetComponent<MeshRenderer>().sharedMaterial = blockMats[Mathf.Clamp(matId, 0, 6)];
        return go;
    }

    // CreatePrimitive 대신 내장 메시 사용 (물리 모듈/콜라이더 불필요)
    static Mesh s_cubeMesh, s_quadMesh;
    static Mesh CubeMesh() => s_cubeMesh != null ? s_cubeMesh : (s_cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx"));
    static Mesh QuadMesh() => s_quadMesh != null ? s_quadMesh : (s_quadMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx"));

    static GameObject NewBlockObject(string name, Mesh mesh, Material mat, Vector3 pos, Vector3 scale)
    {
        var go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().material = mat;
        go.transform.position = pos;
        go.transform.localScale = scale;
        return go;
    }

    void RefreshActiveCubes()
    {
        if (gameOver) { HideActiveAndGhost(); return; }
        int matId = (int)core.CurrentType;
        EnsurePooledCubes(activeCubes, core.CurrentCells.Count);
        for (int i = 0; i < core.CurrentCells.Count; i++)
        {
            var cell = core.CurrentCells[i];
            var go = activeCubes[i];
            go.transform.position = new Vector3(core.CurrentPos.x + cell.x, core.CurrentPos.y + cell.y, 0);
            go.transform.localScale = Vector3.one * 0.92f;
            go.GetComponent<MeshRenderer>().sharedMaterial = blockMats[matId];
            go.SetActive(true);
        }
        RefreshGhost();
    }

    void AddFixedCubesForCurrent()
    {
        int matId = (int)core.CurrentType;
        foreach (var cell in core.CurrentCells)
        {
            int x = core.CurrentPos.x + cell.x, y = core.CurrentPos.y + cell.y;
            if (x < 0 || x >= TetrisCore.Width || y < 0 || y >= TetrisCore.Height) continue;
            var go = MakeCube(matId, new Vector3(x, y, 0));
            go.transform.parent = boardRoot.transform;
            fixedCubes[x, y] = go;
        }
    }

    void RebuildFixedCubes()
    {
        for (int x = 0; x < TetrisCore.Width; x++)
            for (int y = 0; y < TetrisCore.Height; y++)
                if (fixedCubes[x, y] != null) { Destroy(fixedCubes[x, y]); fixedCubes[x, y] = null; }
        for (int x = 0; x < TetrisCore.Width; x++)
            for (int y = 0; y < TetrisCore.Height; y++)
            {
                int v = core.Grid[x, y];
                if (v == 0) continue;
                var go = MakeCube(v - 1, new Vector3(x, y, 0));
                go.transform.parent = boardRoot.transform;
                fixedCubes[x, y] = go;
            }
    }
}
