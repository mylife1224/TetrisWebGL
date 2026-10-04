using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// 빈 씬에 이 스크립트 하나만 넣으면 동작. WebGL 대응 (쓰레드/외부파일 없음)
// UI: Canvas + TextMeshPro. 조작: 키보드 + 터치 패드 (좌 하단 이동 / 우 하단 회전)
// 연출: 고스트+라인예고, Next/Hold, 플래시+파티클+쉐이크, 콤보, 일시정지, 절차적 효과음
public class TetrisGame : MonoBehaviour
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
    AudioClip sMove, sRotate, sLock, sHold, sPause, sLevel, sOver;
    AudioClip sClear1, sClear2, sClear3, sTetris;
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
        SetupUI();
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
    }

    void SetupMaterials()
    {
        for (int i = 0; i < 7; i++)
            blockMats[i] = Resources.Load<Material>($"Materials/Block_{(TetrominoType)i}") ?? CreateLitMaterial(BlockColors[i]);
        bgMat = Resources.Load<Material>("Materials/BoardBg") ?? CreateLitMaterial(new Color(0.13f, 0.15f, 0.2f));
        // 빌드에서 Shader.Find가 null일 수 있어 Resources 머티리얼을 복제 (셰이더 참조 유지)
        ghostGrayMat = CloneWithAlpha(blockMats[0], new Color(0.78f, 0.82f, 0.9f), 0.14f);
        previewMat = CloneWithAlpha(blockMats[0], Color.white, 0.1f);
        flashMat = new Material(blockMats[0]);
        flashMat.color = Color.white;
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
            GameObject go = cube != null
                ? NewBlockObject("P", cube, blockMats[0], Vector3.zero, Vector3.one * PartSize)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);
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

    void SetupUI()
    {
        uiFont = Resources.Load<TMP_FontAsset>("Fonts/NotoSansKR SDF");
        if (uiFont == null) uiFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (uiFont == null)
            Debug.LogError("[Tetris] Korean font missing. Run: Tetris > Create Korean Font Asset (needs TMP Essential Resources first)");

        var es =
#if UNITY_6000_0_OR_NEWER
            FindFirstObjectByType<EventSystem>();
#else
            FindObjectOfType<EventSystem>();
#endif
        if (es == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var cgo = new GameObject("UI Canvas");
        uiCanvas = cgo.AddComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 600);
        scaler.matchWidthOrHeight = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();

        scoreText = MakeLabel("ScoreText", new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -12), 360,
            "점수 0   레벨 1   줄 0", 28, TextAlignmentOptions.TopLeft);
        comboText = MakeLabel("ComboText", new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -52), 360,
            "", 24, TextAlignmentOptions.TopLeft);
        comboText.gameObject.SetActive(false);
        MakeLabel("HintText", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -104), 340,
            "방향키/터치 이동 UP/Z 회전 SPACE 하드드롭", 18, TextAlignmentOptions.TopRight);

        // 좌 하단: 이동 패드 (◄ ▼ ►)
        leftPadGO = MakeRow("MovePad", new Vector2(0, 0), new Vector2(24, 24)).gameObject;
        btnLeft = MakePadButton(leftPadGO.GetComponent<RectTransform>(), Pick("◄", "<"));
        btnDown = MakePadButton(leftPadGO.GetComponent<RectTransform>(), Pick("▼", "v"));
        btnRight = MakePadButton(leftPadGO.GetComponent<RectTransform>(), Pick("►", ">"));

        // 우 하단: 홀드 + 회전 + 하드드롭
        rightPadGO = MakeRow("ActionPad", new Vector2(1, 0), new Vector2(-24, 24)).gameObject;
        var rightRT = rightPadGO.GetComponent<RectTransform>();
        MakeTapButton(rightRT, "HOLD", OnHold);
        MakeTapButton(rightRT, Pick("↺", "CCW"), OnRotateCCW);
        MakeTapButton(rightRT, Pick("↻", "CW"), OnRotateCW);
        MakeTapButton(rightRT, "DROP", OnHardDrop);

        // 우상단 모서리: 일시정지 + 패드 표시 + 소리
        var centerPad = MakeCenterRow("SysPad", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -12));
        var pauseTap = MakeTapButton(centerPad, "II", TogglePause);
        var padTap = MakeTapButton(centerPad, "PAD", TogglePads);
        var sndTap = MakeTapButton(centerPad, "SND", ToggleMute);
        padImg = padTap.GetComponent<Image>();
        sndImg = sndTap.GetComponent<Image>();

        // 게임오버 패널
        gameOverPanel = new GameObject("GameOverPanel");
        gameOverPanel.transform.SetParent(uiCanvas.transform, false);
        var prt = gameOverPanel.AddComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(340, 240);
        var pimg = gameOverPanel.AddComponent<Image>();
        pimg.color = new Color(0.05f, 0.06f, 0.09f, 0.92f);
        var v = gameOverPanel.AddComponent<VerticalLayoutGroup>();
        v.spacing = 10;
        v.padding = new RectOffset(20, 20, 20, 20);
        v.childAlignment = TextAnchor.MiddleCenter;
        v.childControlWidth = true;
        v.childControlHeight = false;
        MakePanelLabel(gameOverPanel.transform, "게임 오버", 36);
        finalScoreText = MakePanelLabel(gameOverPanel.transform, "점수 0", 28);
        var rgo = new GameObject("RestartBtn");
        rgo.transform.SetParent(gameOverPanel.transform, false);
        var rrt = rgo.AddComponent<RectTransform>();
        rrt.sizeDelta = new Vector2(0, 56);
        var rle = rgo.AddComponent<LayoutElement>();
        rle.preferredHeight = 56;
        var rimg = rgo.AddComponent<Image>();
        rimg.color = new Color(0.25f, 0.55f, 0.9f, 0.9f);
        var rbtn = rgo.AddComponent<Button>();
        rbtn.onClick.AddListener(Restart);
        var rlabel = MakeChildLabel(rgo.transform, "다시 시작 (R)", 28);
        rlabel.color = Color.white;
        gameOverPanel.SetActive(false);

        // 일시정지 패널
        pausePanel = new GameObject("PausePanel");
        pausePanel.transform.SetParent(uiCanvas.transform, false);
        var ppt = pausePanel.AddComponent<RectTransform>();
        ppt.anchorMin = ppt.anchorMax = new Vector2(0.5f, 0.5f);
        ppt.sizeDelta = new Vector2(300, 200);
        var ppimg = pausePanel.AddComponent<Image>();
        ppimg.color = new Color(0.05f, 0.06f, 0.09f, 0.92f);
        var pv = pausePanel.AddComponent<VerticalLayoutGroup>();
        pv.spacing = 10;
        pv.padding = new RectOffset(20, 20, 20, 20);
        pv.childAlignment = TextAnchor.MiddleCenter;
        pv.childControlWidth = true;
        pv.childControlHeight = false;
        MakePanelLabel(pausePanel.transform, "일시정지", 34);
        var pgo = new GameObject("ResumeBtn");
        pgo.transform.SetParent(pausePanel.transform, false);
        var ple = pgo.AddComponent<LayoutElement>();
        ple.preferredHeight = 56;
        var ppbimg = pgo.AddComponent<Image>();
        ppbimg.color = new Color(0.25f, 0.55f, 0.9f, 0.9f);
        var pbtn = pgo.AddComponent<Button>();
        pbtn.onClick.AddListener(TogglePause);
        var plabel = MakeChildLabel(pgo.transform, "계속", 28);
        plabel.color = Color.white;
        pausePanel.SetActive(false);

        // 저장된 설정 적용
        padsOn = PlayerPrefs.GetInt("tetris_pads", 1) == 1;
        ApplyPadVisibility();
        ApplyMuteVisual();
    }

    // 기본 폰트+폴백에 없는 글리프면 ASCII 대체 (네모박스 방지)
    string Pick(string main, string fallback)
    {
        if (uiFont != null && uiFont.HasCharacters(main)) return main;
        var lib = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (lib != null && lib != uiFont && lib.HasCharacters(main)) return main;
        return fallback;
    }

    TextMeshProUGUI MakeLabel(string name, Vector2 anchor, Vector2 pivot, Vector2 offset, float width, string text, int size, TextAlignmentOptions align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(uiCanvas.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(width, 70);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.font = uiFont;
        tmp.raycastTarget = false;
        return tmp;
    }

    TextMeshProUGUI MakePanelLabel(Transform parent, string text, int size)
    {
        var go = new GameObject("PanelLabel");
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = size + 14;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.font = uiFont;
        tmp.raycastTarget = false;
        return tmp;
    }

    TextMeshProUGUI MakeChildLabel(Transform parent, string text, int size)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.font = uiFont;
        tmp.raycastTarget = false;
        return tmp;
    }

    RectTransform MakeRow(string name, Vector2 anchor, Vector2 offset)
    {
        var go = new GameObject(name);
        go.transform.SetParent(uiCanvas.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(anchor.x > 0.5f ? 1 : 0, 0);
        rt.anchoredPosition = offset;
        var lay = go.AddComponent<HorizontalLayoutGroup>();
        lay.spacing = 12;
        lay.childControlWidth = false;
        lay.childControlHeight = false;
        lay.childForceExpandWidth = false;
        lay.childForceExpandHeight = false;
        var fit = go.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return rt;
    }

    RectTransform MakeCenterRow(string name, Vector2 anchor, Vector2 pivot, Vector2 offset)
    {
        var go = new GameObject(name);
        go.transform.SetParent(uiCanvas.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = offset;
        var lay = go.AddComponent<HorizontalLayoutGroup>();
        lay.spacing = 12;
        lay.childControlWidth = false;
        lay.childControlHeight = false;
        lay.childForceExpandWidth = false;
        lay.childForceExpandHeight = false;
        var fit = go.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return rt;
    }

    TouchHoldButton MakePadButton(RectTransform parent, string label)
    {
        return MakeButtonVisual(parent, label, 44);
    }

    TouchHoldButton MakeTapButton(RectTransform parent, string label, UnityAction act)
    {
        var hold = MakeButtonVisual(parent, label, label.Length > 2 ? 26 : 44);
        hold.onTap.AddListener(act);
        return hold;
    }

    // 88x88 터치 타겟 (모바일 최소 64px 이상 권장)
    TouchHoldButton MakeButtonVisual(RectTransform parent, string label, int fontSize)
    {
        var go = new GameObject("PadBtn");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(88, 88);
        var img = go.AddComponent<Image>();
        img.color = new Color(1, 1, 1, 0.18f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = new Color(1, 1, 1, 0.18f);
        colors.highlightedColor = new Color(1, 1, 1, 0.3f);
        colors.pressedColor = new Color(0.4f, 0.8f, 1f, 0.5f);
        btn.colors = colors;
        MakeChildLabel(go.transform, label, fontSize);
        var hold = go.AddComponent<TouchHoldButton>();
        padButtons.Add(hold);
        return hold;
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
        var mesh = CubeMesh();
        if (mesh == null) // 예비: 구버전 경로
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(go.GetComponent<Collider>());
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * s;
            go.GetComponent<MeshRenderer>().material = blockMats[matId];
            return go;
        }
        var cube = NewBlockObject("Block", mesh, blockMats[matId], pos, Vector3.one * s);
        return cube;
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
