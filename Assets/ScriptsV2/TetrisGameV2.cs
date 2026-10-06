// V2 테트리스 (프리팹 기반). 구 TetrisGame.cs는 손대지 않음.
// 이번 단계 = 껍데기 + 프리팹 Instantiate:
//   HudCanvas/PadButton 프리팹 배치, 버튼 탭 연결, 과일 데모 큐브 7개.
// 게임 규칙 마이그레이션(낙하/회전/삭제)은 다음 단계. TODO 표시.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

public class TetrisGameV2 : MonoBehaviour
{
    GameObject hud;
    GameObject blockPrefab;
    Material[] fruitMats = new Material[7];
    TMP_FontAsset uiFont;
    readonly List<TouchHoldButton> padButtons = new();
    bool padsOn = true;
    GameObject movePad, actionPad;

    void Start()
    {
        EnsureEventSystem();
        SetupCamera();
        LoadAssets();
        BuildHud();
        BuildDemoRow();
    }

    void EnsureEventSystem()
    {
#if UNITY_6000_0_OR_NEWER
        var es = FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
#else
        var es = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
#endif
        if (es == null)
            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
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
    }

    void LoadAssets()
    {
        uiFont = Resources.Load<TMP_FontAsset>("Fonts/NotoSansKR SDF");
        hud = Resources.Load<GameObject>("Prefabs/UI/HudCanvas");
        blockPrefab = Resources.Load<GameObject>("Prefabs/Block");
        for (int i = 0; i < 7; i++)
            fruitMats[i] = Resources.Load<Material>($"Materials/Fruit_{(TetrominoType)i}");
        if (hud == null || blockPrefab == null)
            Debug.LogError("[TetrisV2] 프리팹 로드 실패. Tetris > Build UI Prefabs / Build Block Prefab 실행 필요.");
    }

    // 네모박스 방지: 폰트에 없는 글리프면 ASCII 대체 (구 Pick과 동일)
    string Pick(string main, string fallback)
    {
        if (uiFont != null && uiFont.HasCharacters(main)) return main;
        var lib = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (lib != null && lib != uiFont && lib.HasCharacters(main)) return main;
        return fallback;
    }

    void BuildHud()
    {
        if (hud == null) return;
        var inst = Instantiate(hud);
        inst.name = "HudCanvas";
        movePad = inst.transform.Find("MovePad")?.gameObject;
        actionPad = inst.transform.Find("ActionPad")?.gameObject;
        var sysPad = inst.transform.Find("SysPad");

        AddPadButton(movePad?.transform, Pick("◄", "<"), 44, null); // TODO: hold 이동 (게임 규칙 이후)
        AddPadButton(movePad?.transform, Pick("▼", "v"), 44, null);
        AddPadButton(movePad?.transform, Pick("►", ">"), 44, null);

        AddPadButton(actionPad?.transform, "HOLD", 26, () => Debug.Log("[TetrisV2] HOLD (stub)"));
        AddPadButton(actionPad?.transform, Pick("↺", "CCW"), 44, () => Debug.Log("[TetrisV2] 회전CCW (stub)"));
        AddPadButton(actionPad?.transform, Pick("↻", "CW"), 44, () => Debug.Log("[TetrisV2] 회전CW (stub)"));
        AddPadButton(actionPad?.transform, "DROP", 26, () => Debug.Log("[TetrisV2] 하드드롭 (stub)"));
        AddPadButton(sysPad, "II", 44, () => TogglePanel(inst.transform, "PausePanel"));
        AddPadButton(sysPad, "PAD", 26, TogglePads);
        AddPadButton(sysPad, "SND", 26, () => Debug.Log("[TetrisV2] 음소거 (stub)"));

        // 게임오버/일시정지 패널 버튼 연결 (리스너는 프리팹에 저장 불가 → 여기서 연결)
        WirePanelButton(inst.transform, "GameOverPanel/RestartBtn", () => Debug.Log("[TetrisV2] 재시작 (stub)"));
        WirePanelButton(inst.transform, "PausePanel/ResumeBtn", () => TogglePanel(inst.transform, "PausePanel"));
    }

    void AddPadButton(Transform parent, string label, int fontSize, UnityAction tap)
    {
        if (parent == null) return;
        var btnPrefab = Resources.Load<GameObject>("Prefabs/UI/PadButton");
        if (btnPrefab == null) return;
        var go = Instantiate(btnPrefab, parent, false);
        go.name = "PadBtn_" + label;
        var tmp = go.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) { tmp.text = label; tmp.fontSize = fontSize; }
        var hold = go.GetComponent<TouchHoldButton>();
        if (hold != null)
        {
            padButtons.Add(hold);
            if (tap != null) hold.onTap.AddListener(tap);
        }
    }

    void WirePanelButton(Transform root, string path, UnityAction act)
    {
        var btn = root.Find(path)?.GetComponent<UnityEngine.UI.Button>();
        if (btn != null) btn.onClick.AddListener(act);
    }

    void TogglePanel(Transform root, string name)
    {
        var p = root.Find(name)?.gameObject;
        if (p != null) p.SetActive(!p.activeSelf);
    }

    void TogglePads()
    {
        padsOn = !padsOn;
        if (movePad != null) movePad.SetActive(padsOn);
        if (actionPad != null) actionPad.SetActive(padsOn);
    }

    // 과일 머티리얼 + Block 프리팹 렌더 증명용 7개
    void BuildDemoRow()
    {
        if (blockPrefab == null) return;
        var names = new[] { "I", "O", "T", "S", "Z", "J", "L" };
        for (int i = 0; i < 7; i++)
        {
            var go = Instantiate(blockPrefab, new Vector3(i + 1.5f, 2f, 0), Quaternion.identity);
            go.name = "Demo_" + names[i];
            if (fruitMats[i] != null)
                go.GetComponent<MeshRenderer>().sharedMaterial = fruitMats[i];
            go.transform.localScale = Vector3.one * 0.92f;
        }
    }
}
