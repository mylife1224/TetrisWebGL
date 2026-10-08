// V2 씬/에셋 빌더 (에이전트 소유 자동화).
// V2.unity는 BuildItchioPlayer가 건드리는 Main.unity와 다른 경로라 증발 걱정 없음.
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

public static class TetrisV2Builder
{
    const string ScenePath = "Assets/Scenes/V2.unity";

    [MenuItem("Tetris/Build V2 Scene")]
    public static void BuildV2Scene()
    {
        if (!System.IO.Directory.Exists("Assets/Scenes"))
            System.IO.Directory.CreateDirectory("Assets/Scenes");
        if (!System.IO.Directory.Exists("Assets/ScriptsV2"))
            System.IO.Directory.CreateDirectory("Assets/ScriptsV2");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.EventSystems.StandaloneInputModule));
        var go = new GameObject("GameV2");
        go.AddComponent<TetrisGameV2>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
        Debug.Log("[Tetris] V2 scene OK: " + ScenePath);
    }

    // 머티리얼(Block plain 복원 + Fruit) + Block 프리팹 + V2 씬을 한 번에.
    // 이 과정을 하는 이유: 세 작업이 같은 에디터 세션에서 순서대로 돌 때
    // 임포트 1회로 끝나서 batchmode 1회 호출로 충분하기 때문.
    public static void BuildAllV2Assets()
    {
        TetrisKoreanFontSetup.EnsureRuntimeMaterials();
        TetrisBlockPrefabBuilder.BuildBlockPrefab();
        BuildV2Scene();
    }

    // V2 WebGL 빌드 (v1 BuildItchioPlayer의 V2판).
    // 이 과정을 하는 이유: v1은 참고용 동결이라 V2에 독립 파이프라인이 필요하고,
    // 출력 경로를 분리(Build/WebGL_V2)해야 v1 빌드 산출물을 덮어쓰지 않기 때문.
    // V2.unity는 매번 재생성 (에이전트 소유 자동생성 파일이라 직접 편집 금지).
    // SSD 폰트 머티리얼 보장 (빌드용).
    // 이 과정을 하는 이유: CreateStatic이 폰트를 매번 재생성해서 stock DF로 돌아가므로,
    // 빌드 직후 SSD로 교체해야 실행본에 반영되기 때문. batchmode 에디터라 Shader.Find 사용 가능.
    // (빌드된 플레이어에서 Shader.Find가 null인 함정과 무관 — 에디터 단계 처리)
    static void EnsureSsdFontMaterial()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Resources/Fonts/NotoSansKR SDF.asset");
        if (font == null || font.material == null) { Debug.LogError("[TetrisV2] font/material missing for SSD"); return; }
        var ssd = Shader.Find("TextMeshPro/Mobile/Distance Field SSD");
        if (ssd == null) { Debug.LogError("[TetrisV2] SSD shader not found"); return; }
        font.material.shader = ssd;
        EditorUtility.SetDirty(font.material);
        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();
        Debug.Log("[TetrisV2] font material -> SSD");
    }
    public static void BuildV2Player()
    {
        TetrisKoreanFontSetup.ApplyItchio();
        TetrisKoreanFontSetup.EnsureRuntimeMaterials();
        TetrisKoreanFontSetup.CreateStatic();
        EnsureSsdFontMaterial();
        BuildV2Scene();
        AssetDatabase.Refresh();
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        string outDir = System.Environment.GetEnvironmentVariable("TETRIS_V2_BUILD_OUT");
        if (string.IsNullOrEmpty(outDir)) outDir = "Build/WebGL_V2";
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = outDir,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(opts);
        Debug.Log("[TetrisV2] build result: " + report.summary.result + " totalBytes=" + report.summary.totalSize);
    }
}
#endif
