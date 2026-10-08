// UI 프리팹 생성기 (에이전트 소유 자동화).
// 현행 TetrisGame.SetupUI가 코드로 찍어내는 UI를 프리팹 2종으로 옮김:
//   Prefabs/UI/HudCanvas.prefab  (Canvas + 점수/콤보 + SysPad 1 + 게임오버/일시정지 패널)
//   Prefabs/UI/PadButton.prefab  (88x88 버튼 단위: Image+Button+TMP+TouchHoldButton)
// 설계: SysPad의 일시정지 버튼 1종만 V2가 Instantiate. Hint/이동패드/SND 미사용.
// onClick/onTap 리스너는 프리팹에 직렬화하지 않고 V2 런타임에 연결.
// 이 과정을 하는 이유: UnityEvent 리스너는 코드 메서드를 가리켜 프리팹에 저장할 수 없고,
// 저장해도 구 스크립트(TetrisGame)를 참조해 V2 분리가 깨지기 때문.
#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

public static class TetrisUiPrefabBuilder
{
    const string UiDir = "Assets/UI/Resources/Prefabs/UI";
    const string FontPath = "Assets/UI/Resources/Fonts/NotoSansKR SDF.asset";

    [MenuItem("Tetris/Build UI Prefabs")]
    public static void BuildUiPrefabs()
    {
        if (!AssetDatabase.IsValidFolder("Assets/UI/Resources/Prefabs"))
            AssetDatabase.CreateFolder("Assets/UI/Resources", "Prefabs");
        if (!AssetDatabase.IsValidFolder(UiDir))
            AssetDatabase.CreateFolder("Assets/UI/Resources/Prefabs", "UI");

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
            throw new System.Exception("[Tetris] UI font 없음: " + FontPath);

        BuildPadButton(font);
        BuildHudCanvas(font);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Verify();
        Debug.Log("[Tetris] UI prefabs OK: " + UiDir);
    }

    // ---------- 버튼 단위 ----------
    static void BuildPadButton(TMP_FontAsset font)
    {
        var go = new GameObject("PadButton");
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
        var label = new GameObject("Label");
        label.transform.SetParent(go.transform, false);
        var lrt = label.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var tmp = label.AddComponent<TextMeshProUGUI>();
        tmp.text = "BTN"; tmp.fontSize = 44;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.font = font; tmp.raycastTarget = false; // 클릭은 Image가 받음 (버튼 Image 필수 함정)
        Thicken(tmp);
        go.AddComponent<TouchHoldButton>(); // held/onTap은 V2가 사용
        PrefabUtility.SaveAsPrefabAsset(go, UiDir + "/PadButton.prefab");
        Object.DestroyImmediate(go);
    }

    // ---------- HUD 캔버스 ----------
    static void BuildHudCanvas(TMP_FontAsset font)
    {
        var cgo = new GameObject("HudCanvas");
        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 600);
        scaler.matchWidthOrHeight = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();

        MakeLabel(cgo.transform, font, "ScoreText", new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(16, -12), 360, "SCORE 0   LV 1   LINES 0", 28, TextAlignmentOptions.TopLeft);
        var combo = MakeLabel(cgo.transform, font, "ComboText", new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(16, -52), 360, "", 24, TextAlignmentOptions.TopLeft);
        combo.gameObject.SetActive(false);

        // SysPad 1종 (일시정지 버튼용). 이동/액션 패드와 Hint는 미사용.
        MakeRow(cgo.transform, "SysPad", new Vector2(1, 1), new Vector2(-16, -12), new Vector2(1, 1));

        BuildPanel(cgo.transform, font, "GameOverPanel", new Vector2(340, 240),
            "GAME OVER", 36, "SCORE 0", 28, "RestartBtn", "RESTART (R)");
        BuildPanel(cgo.transform, font, "PausePanel", new Vector2(300, 200),
            "PAUSED", 34, null, 0, "ResumeBtn", "RESUME");

        PrefabUtility.SaveAsPrefabAsset(cgo, UiDir + "/HudCanvas.prefab");
        Object.DestroyImmediate(cgo);
    }

    static TextMeshProUGUI MakeLabel(Transform parent, TMP_FontAsset font, string name,
        Vector2 anchor, Vector2 pivot, Vector2 offset, float width, string text, int size,
        TextAlignmentOptions align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(width, 70);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.alignment = align;
        tmp.font = font; tmp.raycastTarget = false;
        Thicken(tmp);
        return tmp;
    }

    static void MakeRow(Transform parent, string name, Vector2 anchor, Vector2 offset, Vector2 pivot)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = offset;
        var lay = go.AddComponent<HorizontalLayoutGroup>();
        lay.spacing = 12;
        lay.childControlWidth = lay.childControlHeight = false;
        lay.childForceExpandWidth = lay.childForceExpandHeight = false;
        var fit = go.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    static void BuildPanel(Transform parent, TMP_FontAsset font, string name, Vector2 size,
        string title, int titleSize, string sub, int subSize, string btnName, string btnLabel)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.05f, 0.06f, 0.09f, 0.92f);
        var v = go.AddComponent<VerticalLayoutGroup>();
        v.spacing = 10;
        v.padding = new RectOffset(20, 20, 20, 20);
        v.childAlignment = TextAnchor.MiddleCenter;
        v.childControlWidth = true;
        v.childControlHeight = false;

        var t = new GameObject("PanelLabel");
        t.transform.SetParent(go.transform, false);
        var le = t.AddComponent<LayoutElement>();
        le.preferredHeight = titleSize + 14;
        var ttmp = t.AddComponent<TextMeshProUGUI>();
        ttmp.text = title; ttmp.fontSize = titleSize;
        ttmp.alignment = TextAlignmentOptions.Center;
        ttmp.font = font; ttmp.raycastTarget = false;
        Thicken(ttmp);

        if (sub != null)
        {
            var s = new GameObject("PanelSubLabel");
            s.transform.SetParent(go.transform, false);
            var sle = s.AddComponent<LayoutElement>();
            sle.preferredHeight = subSize + 14;
            var stmp = s.AddComponent<TextMeshProUGUI>();
            stmp.text = sub; stmp.fontSize = subSize;
            stmp.alignment = TextAlignmentOptions.Center;
            stmp.font = font; stmp.raycastTarget = false;
            Thicken(stmp);
        }

        var bgo = new GameObject(btnName);
        bgo.transform.SetParent(go.transform, false);
        var ble = bgo.AddComponent<LayoutElement>();
        ble.preferredHeight = 56;
        var bimg = bgo.AddComponent<Image>();
        bimg.color = new Color(0.25f, 0.55f, 0.9f, 0.9f);
        var bbtn = bgo.AddComponent<Button>();
        bbtn.targetGraphic = bimg; // onClick은 V2가 연결
        var bl = new GameObject("Label");
        bl.transform.SetParent(bgo.transform, false);
        var blrt = bl.AddComponent<RectTransform>();
        blrt.anchorMin = Vector2.zero; blrt.anchorMax = Vector2.one;
        blrt.offsetMin = blrt.offsetMax = Vector2.zero;
        var bltmp = bl.AddComponent<TextMeshProUGUI>();
        bltmp.text = btnLabel; bltmp.fontSize = 28;
        bltmp.alignment = TextAlignmentOptions.Center;
        bltmp.font = font; bltmp.color = Color.white; bltmp.raycastTarget = false;
        Thicken(bltmp);
        go.SetActive(false); // 패널은 꺼진 상태로 저장 (V2가 켬)
    }

    static void Thicken(TextMeshProUGUI tmp)
    {
        tmp.fontStyle = FontStyles.Bold;
        if (tmp.fontSharedMaterial == null && tmp.font != null)
            tmp.fontSharedMaterial = tmp.font.material;
        if (tmp.fontSharedMaterial == null) return;
        tmp.outlineWidth = 0.15f;
        tmp.outlineColor = tmp.color;
    }

    static void Verify()
    {
        var hud = AssetDatabase.LoadAssetAtPath<GameObject>(UiDir + "/HudCanvas.prefab");
        var pad = AssetDatabase.LoadAssetAtPath<GameObject>(UiDir + "/PadButton.prefab");
        if (hud == null || pad == null)
            throw new System.Exception("[Tetris] UI 프리팹 저장 실패.");
        if (hud.GetComponent<Canvas>() == null || hud.GetComponent<CanvasScaler>() == null)
            throw new System.Exception("[Tetris] HudCanvas 컴포넌트 누락.");
        foreach (var n in new[] { "ScoreText", "ComboText", "SysPad", "GameOverPanel", "PausePanel" })
            if (hud.transform.Find(n) == null)
                throw new System.Exception("[Tetris] HudCanvas 자식 누락: " + n);
        var pbtn = pad.GetComponent<Button>();
        if (pbtn == null || pbtn.targetGraphic == null || pad.GetComponent<TouchHoldButton>() == null)
            throw new System.Exception("[Tetris] PadButton 컴포넌트 누락.");
        if (pad.GetComponentInChildren<TextMeshProUGUI>()?.font == null)
            throw new System.Exception("[Tetris] PadButton 폰트 미지정.");
    }
}
#endif
