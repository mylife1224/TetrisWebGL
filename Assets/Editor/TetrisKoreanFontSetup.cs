using System.Collections.Generic;
using System.IO;
using System.Net;
using TMPro;
using UnityEditor;
using UnityEngine;

// Tetris 한글 폰트 자동 셋업. 메뉴: Tetris > Create Korean Font Asset
// TTF가 없으면 Google Fonts에서 자동 다운로드 후 TMP SDF 에셋 생성 (Dynamic).
public static class TetrisKoreanFontSetup
{
    const string FontUrl = "https://raw.githubusercontent.com/google/fonts/main/ofl/notosanskr/NotoSansKR%5Bwght%5D.ttf";
    const string TtfPath = "Assets/Fonts/NotoSansKR.ttf";
    const string OutPath = "Assets/UI/Resources/Fonts/NotoSansKR SDF.asset";

    // 게임 UI 전체 글리프 (ASCII + 한글 + 기호). 기호가 폰트에 없으면 LiberationSans 폴백이 커버
    const string KoreanChars = "점수레벨줄게임오버다시시작화살표이동회전소프트드롭하방향키터치콤보일시정지계속";
    const string SymbolChars = "◄►▼↺↻";
    static readonly string[] SymbolFontPaths = {
        "Assets/Fonts/NotoSansSymbols.ttf",
        "Assets/Fonts/NotoSansSymbols2.ttf",
    };

    static string BuildCharset()
    {
        var sb = new System.Text.StringBuilder();
        for (int c = 32; c <= 126; c++) sb.Append((char)c);
        sb.Append(KoreanChars);
        sb.Append("◄►▼↺↻");
        return sb.ToString();
    }

    [MenuItem("Tetris/Create Korean Font Asset")]
    public static void Create() => BuildAndSave(false);

    // Static bake: 필요 글리프만 아틀라스에 굽고 원본 TTF 참조를 끊어 빌드 용량 축소
    [MenuItem("Tetris/Create Korean Font Asset (Static)")]
    public static void CreateStatic() => BuildAndSave(true);

    static void BuildAndSave(bool staticBake)
    {
        AssetDatabase.Refresh();
        if (!File.Exists(TtfPath))
        {
            Debug.Log("[Tetris] NotoSansKR downloading...");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(TtfPath));
                using (var wc = new WebClient()) wc.DownloadFile(FontUrl, TtfPath);
                AssetDatabase.Refresh();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Tetris] font download failed: " + e.Message + " - manual: " + FontUrl + " -> " + TtfPath);
                return;
            }
        }

        var font = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
        if (font == null) { Debug.LogError("[Tetris] font not imported: " + TtfPath); return; }

        // TryAddCharacters는 Static에서 거부되므로 Dynamic으로 채운 뒤 전환
        var fontAsset = TMP_FontAsset.CreateFontAsset(font, 64, 5, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        if (fontAsset == null) { Debug.LogError("[Tetris] CreateFontAsset returned null"); return; }

        var liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (liberation != null)
        {
            fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset> { liberation };
            Debug.Log("[Tetris] LiberationSans fallback linked (symbols covered)");
        }
        else Debug.LogWarning("[Tetris] LiberationSans SDF not found. Import TMP Essential Resources for symbol fallback.");

        if (staticBake)
        {
            string missing;
            fontAsset.TryAddCharacters(BuildCharset(), out missing);
            int baked = fontAsset.characterTable.Count;
            if (!string.IsNullOrEmpty(missing))
                Debug.Log("[Tetris] KR missing (" + missing.Length + " chars)");

            // 기호는 커버리지 넓은 폰트에서 추가 (소스 교체 후 복원)
            string symRemaining = SymbolChars;
            foreach (string symPath in SymbolFontPaths)
            {
                var symFont = AssetDatabase.LoadAssetAtPath<Font>(symPath);
                if (symFont == null) { Debug.LogWarning("[Tetris] symbol font not found: " + symPath); continue; }
                SetSourceFont(fontAsset, symFont);
                fontAsset.TryAddCharacters(symRemaining, out string stillMissing);
                symRemaining = stillMissing ?? "";
                Debug.Log("[Tetris] symbols from " + symPath + ": remaining=" + symRemaining.Length);
                if (symRemaining.Length == 0) break;
            }
            if (!string.IsNullOrEmpty(symRemaining))
                Debug.LogWarning("[Tetris] symbol glyphs missing (ASCII fallback used): " + symRemaining);
            Debug.Log("[Tetris] static bake: glyphs=" + fontAsset.characterTable.Count + " (base=" + baked + ")");

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

            // 원본 TTF가 빌드에 딸려가지 않도록 참조 제거 (setter가 internal이라 리플렉션)
            var field = typeof(TMP_FontAsset).GetField("m_SourceFontFile",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) field.SetValue(fontAsset, null);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(OutPath));
        AssetDatabase.DeleteAsset(OutPath);
        AssetDatabase.CreateAsset(fontAsset, OutPath);
        // 아틀라스 텍스처 + 머티리얼을 서브에셋으로 포함 (없으면 글리프 위치만 있고 그림이 비어 깨져 보임)
        if (fontAsset.atlasTextures != null)
        {
            foreach (var tex in fontAsset.atlasTextures)
                if (tex != null) AssetDatabase.AddObjectToAsset(tex, fontAsset);
        }
        if (fontAsset.material != null)
        {
            fontAsset.material.name = fontAsset.name + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Tetris] Korean font asset created: " + OutPath + (staticBake ? " (static)" : " (dynamic)"));
    }

    static void SetSourceFont(TMP_FontAsset asset, Font f)
    {
        var field = typeof(TMP_FontAsset).GetField("m_SourceFontFile",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null) field.SetValue(asset, f);
    }

    // ClearLines 결정적 테스트 (인접/이격/테트리스/없음)
    [MenuItem("Tetris/Test ClearLines")]
    public static void TestClearLines()
    {
        int fails = 0;
        void Check(bool cond, string name)
        {
            if (!cond) { fails++; Debug.LogError("[TEST] FAIL: " + name); }
            else Debug.Log("[TEST] ok: " + name);
        }
        {
            var c = new TetrisCore();
            for (int x = 0; x < TetrisCore.Width; x++) { c.Grid[x, 0] = 1; c.Grid[x, 1] = 2; }
            c.Grid[0, 2] = 3;
            Check(c.ClearLines() == 2, "adjacent count");
            Check(c.Grid[0, 0] == 3, "adjacent shift");
            bool clean = true;
            for (int x = 1; x < TetrisCore.Width; x++) if (c.Grid[x, 0] != 0) clean = false;
            for (int y = 1; y < TetrisCore.Height; y++)
                for (int x = 0; x < TetrisCore.Width; x++) if (c.Grid[x, y] != 0) clean = false;
            Check(clean, "adjacent rest empty");
        }
        {
            var c = new TetrisCore();
            for (int x = 0; x < TetrisCore.Width; x++) { c.Grid[x, 0] = 1; c.Grid[x, 2] = 2; }
            c.Grid[0, 1] = 3; c.Grid[0, 3] = 4;
            Check(c.ClearLines() == 2, "gap count");
            Check(c.Grid[0, 0] == 3 && c.Grid[0, 1] == 4, "gap order");
        }
        {
            var c = new TetrisCore();
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < TetrisCore.Width; x++) c.Grid[x, y] = y + 1;
            c.Grid[5, 4] = 9;
            Check(c.ClearLines() == 4, "tetris count");
            Check(c.Grid[5, 0] == 9, "tetris shift");
        }
        {
            var c = new TetrisCore();
            c.Grid[0, 0] = 1;
            Check(c.ClearLines() == 0 && c.Grid[0, 0] == 1, "none");
        }
        Debug.Log(fails == 0 ? "[TEST] ClearLines ALL PASS" : $"[TEST] ClearLines {fails} FAILURES");
    }

    // itch.io WebGL 권장 세팅 (Gzip + Decompression Fallback)
    [MenuItem("Tetris/Apply itch.io WebGL Settings")]
    public static void ApplyItchio()
    {
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        AssetDatabase.SaveAssets();
        Debug.Log("[Tetris] itch.io settings applied: Gzip + DecompressionFallback");
    }

    // 빌드에서 Shader.Find가 null이 되는 문제 방지: 셰이더를 참조하는 Material 에셋 미리 생성
    [MenuItem("Tetris/Ensure Runtime Materials")]
    public static void EnsureRuntimeMaterials()
    {
        AssetDatabase.Refresh();
        string[] names = { "Block_I", "Block_O", "Block_T", "Block_S", "Block_Z", "Block_J", "Block_L" };
        Color[] colors = {
            new Color(0.2f,0.8f,0.9f), new Color(0.95f,0.8f,0.2f), new Color(0.7f,0.4f,0.9f),
            new Color(0.3f,0.85f,0.4f), new Color(0.95f,0.3f,0.3f), new Color(0.3f,0.5f,0.95f),
            new Color(0.95f,0.55f,0.2f),
        };
        const string dir = "Assets/UI/Resources/Materials";
        Directory.CreateDirectory(dir);
        var lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        for (int i = 0; i < 7; i++) SaveMat($"{dir}/{names[i]}.mat", lit, colors[i], 0.35f);
        SaveMat($"{dir}/BoardBg.mat", lit, new Color(0.13f, 0.15f, 0.2f), 0f);
        var unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        SaveMat($"{dir}/BoardLine.mat", unlit, new Color(0.3f, 0.35f, 0.45f), 0f);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Tetris] runtime materials ready: " + dir);
    }

    static void SaveMat(string path, Shader shader, Color color, float gloss)
    {
        if (shader == null) { Debug.LogError("[Tetris] shader missing for " + path); return; }
        var m = new Material(shader);
        m.color = color;
        if (gloss > 0 && m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(m, path);
    }
    [MenuItem("Tetris/Build WebGL (itch.io + Desktop)")]
    public static void BuildItchioPlayer()
    {
        ApplyItchio();
        EnsureRuntimeMaterials();
        CreateStatic();
        const string scenePath = "Assets/Scenes/Main.unity";
        if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
            UnityEditor.SceneManagement.NewSceneMode.Single);
        var go = new GameObject("Game");
        go.AddComponent<TetrisGame>();
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.Refresh();
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };

        string outDir = System.Environment.GetEnvironmentVariable("TETRIS_BUILD_OUT");
        if (string.IsNullOrEmpty(outDir)) outDir = "Build/WebGL";
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { scenePath },
            locationPathName = outDir,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(opts);
        Debug.Log("[Tetris] build result: " + report.summary.result + " totalBytes=" + report.summary.totalSize);
    }
}
