// V2 플레이모드 스모크 테스트 (에이전트 소유 자동화).
// V2.unity를 열어 90프레임 돌린 뒤 HUD/활성블럭/점수텍스트를 확인.
// batchmode: -executeMethod TetrisV2SmokeTest.RunSmoke (종료는 스크립트가 직접).
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

public static class TetrisV2SmokeTest
{
    static int frame;
    static bool running;
    static bool exiting;
    static double startTime;

    [MenuItem("Tetris/Smoke Test V2")]
    public static void RunSmoke()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/V2.unity");
        // 이 과정을 하는 이유: 플레이모드 진입 시 domain reload가 일어나면
        // static 카운터/구독이 초기화돼 테스트가 멈추기 때문. 갓 띄운 프로세스라 안전.
        EditorSettings.enterPlayModeOptions =
            EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
        frame = 0; running = true; exiting = false;
        startTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    static void Tick()
    {
        if (!running) return;
        if (!EditorApplication.isPlaying && !exiting) return;
        if (exiting)
        {
            if (!EditorApplication.isPlaying)
            {
                running = false;
                EditorApplication.update -= Tick;
                EditorApplication.Exit(exitCode);
            }
            return;
        }
        frame++;
        if (frame >= 90 || EditorApplication.timeSinceStartup - startTime > 180)
        {
            Evaluate();
            exiting = true;
            EditorApplication.isPlaying = false;
        }
    }

    static int exitCode;

    static void Evaluate()
    {
        var hud = GameObject.Find("HudCanvas");
        string score = hud?.transform.Find("ScoreText")?.GetComponent<TextMeshProUGUI>()?.text;
        int blocks = 0;
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (!go.scene.IsValid()) continue; // 프리팹 에셋 제외
            if (go.name == "Block" && go.activeInHierarchy) blocks++; // MakeCube가 "Block"으로 명명
        }
        bool pass = hud != null && score != null && score.StartsWith("점수") && blocks >= 4;
        Debug.Log($"[TetrisV2] SMOKE frames={frame} hud={hud != null} score='{score}' blocks={blocks} => {(pass ? "PASS" : "FAIL")}");
        exitCode = pass ? 0 : 1;
    }
}
#endif
