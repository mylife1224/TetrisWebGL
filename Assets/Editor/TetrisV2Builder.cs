// V2 씬/에셋 빌더 (에이전트 소유 자동화).
// V2.unity는 BuildItchioPlayer가 건드리는 Main.unity와 다른 경로라 증발 걱정 없음.
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

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
}
#endif
