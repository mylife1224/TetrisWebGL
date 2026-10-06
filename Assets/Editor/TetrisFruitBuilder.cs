// 과일 블럭 텍스처 생성기 (에이전트 소유 자동화).
// 7종 테트로미노 색상 대신 과일 그림을 입힌 64x64 PNG를 코드로 그림.
// 인터넷 이미지를 쓰지 않는 이유: 저작권 불명 + 빌드 시 머티리얼이 재생성되므로
// 파이프라인에 포함되지 않은 외부 파일은 빌드 때마다 날아감.
// 배경은 기존 블럭색 유지(게임 가독성) + 가운데 과일 픽셀아트.
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public static class TetrisFruitBuilder
{
    public enum Fruit { Apple, Strawberry, Banana, Orange, Pear, Peach, Watermelon }

    public struct BlockSpec
    {
        public string matName;
        public Color bg;
        public Fruit fruit;
        public BlockSpec(string n, Color b, Fruit f) { matName = n; bg = b; fruit = f; }
    }

    // 단일 진실 공급원: 머티리얼명/배경색/과일 매핑.
    // EnsureRuntimeMaterials도 이 테이블을 사용 (색상 이중 정의 방지).
    public static readonly BlockSpec[] Blocks = {
        new BlockSpec("Block_I", new Color(0.2f,0.8f,0.9f), Fruit.Apple),
        new BlockSpec("Block_O", new Color(0.95f,0.8f,0.2f), Fruit.Strawberry),
        new BlockSpec("Block_T", new Color(0.7f,0.4f,0.9f), Fruit.Banana),
        new BlockSpec("Block_S", new Color(0.3f,0.85f,0.4f), Fruit.Orange),
        new BlockSpec("Block_Z", new Color(0.95f,0.3f,0.3f), Fruit.Pear),
        new BlockSpec("Block_J", new Color(0.3f,0.5f,0.95f), Fruit.Peach),
        new BlockSpec("Block_L", new Color(0.95f,0.55f,0.2f), Fruit.Watermelon),
    };

    const int S = 64;
    const string TexDir = "Assets/UI/Resources/Textures";

    static string FruitFile(Fruit f) => TexDir + "/Fruit_" + f + ".png";

    [MenuItem("Tetris/Redraw Fruit Textures")]
    public static void RedrawMenu()
    {
        EnsureFruitTextures(force: true);
        ApplyToMaterials("Assets/UI/Resources/Materials");
    }

    // PNG 파일 준비. force=false면 이미 있는 파일은 그대로 둠 (미리 준비된 7개 우선).
    // 이 과정을 하는 이유: 과일 그림은 리포지토리에 실물 파일로 두고 파이프라인은
    // '부착'만 담당하게 분리하면, 나중에 PNG를 덧그려도 파이프라인 수정 없이 반영됨.
    public static void EnsureFruitTextures(bool force = false)
    {
        Directory.CreateDirectory(TexDir);
        bool wrote = false;
        foreach (var b in Blocks)
        {
            string path = FruitFile(b.fruit);
            if (!force && File.Exists(path)) continue;
            var px = Paint(b.fruit, b.bg);
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            wrote = true;
        }
        if (!wrote) return;
        AssetDatabase.Refresh();
        foreach (var b in Blocks)
        {
            var importer = AssetImporter.GetAtPath(FruitFile(b.fruit)) as TextureImporter;
            if (importer != null)
            {
                importer.filterMode = FilterMode.Point; // 도트 그래픽 선명
                importer.SaveAndReimport();
            }
        }
        AssetDatabase.Refresh();
    }

    // 기존 PNG를 머티리얼에 부착. SaveMat 재생성 직후 호출해야 빌드 때 살아남음.
    public static void ApplyToMaterials(string matDir)
    {
        foreach (var b in Blocks)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/{b.matName}.mat");
            var fruitTex = AssetDatabase.LoadAssetAtPath<Texture2D>(FruitFile(b.fruit));
            if (mat == null || fruitTex == null)
                throw new System.Exception("[Tetris] 과일 부착 실패: " + b.matName);
            mat.mainTexture = fruitTex;
            mat.color = Color.white; // 배경색은 텍스처에 구워져 있으므로 틴트 제거
            EditorUtility.SetDirty(mat);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Tetris] fruit textures OK: " + Blocks.Length + " textures -> " + matDir);
    }

    static Color[] Paint(Fruit f, Color bg)
    {
        var px = new Color[S * S];
        var edge = bg * 0.55f; edge.a = 1f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                Color c = bg;
                if (x < 2 || y < 2 || x >= S - 2 || y >= S - 2) c = edge; // 테두리
                else c = FruitPixel(f, x, y, bg);
                c.a = 1f;
                px[y * S + x] = c;
            }
        return px;
    }

    static bool Disc(float x, float y, float cx, float cy, float r)
    {
        float dx = x - cx, dy = y - cy;
        return dx * dx + dy * dy <= r * r;
    }
    static bool Ell(float x, float y, float cx, float cy, float rx, float ry)
    {
        float dx = (x - cx) / rx, dy = (y - cy) / ry;
        return dx * dx + dy * dy <= 1f;
    }
    static bool Bar(float x, float y, float x0, float x1, float y0, float y1)
        => x >= x0 && x <= x1 && y >= y0 && y <= y1;

    static Color FruitPixel(Fruit f, float x, float y, Color bg)
    {
        var red = new Color(0.9f, 0.15f, 0.2f);
        var darkRed = new Color(0.65f, 0.08f, 0.12f);
        var green = new Color(0.25f, 0.7f, 0.25f);
        var darkGreen = new Color(0.1f, 0.45f, 0.15f);
        var brown = new Color(0.45f, 0.28f, 0.12f);
        var yellow = new Color(1f, 0.85f, 0.15f);
        var orange = new Color(1f, 0.55f, 0.1f);
        var pink = new Color(1f, 0.65f, 0.7f);
        var darkPink = new Color(0.9f, 0.4f, 0.5f);
        var paleGreen = new Color(0.65f, 0.9f, 0.45f);
        var white = Color.white;

        switch (f)
        {
            case Fruit.Apple:
                if (Bar(x, y, 30, 34, 8, 18)) return brown;                 // 꼭지
                if (Ell(x, y, 40, 13, 8, 4)) return green;                  // 잎
                if (Disc(x, y, 32, 35, 17))                                  // 몸
                {
                    if (Disc(x, y, 26, 41, 4)) return white;                // 광택
                    return red;
                }
                return bg;
            case Fruit.Strawberry:
                if (Bar(x, y, 20, 44, 12, 17)) return green;                // 잎대
                if (Ell(x, y, 26, 15, 5, 3) || Ell(x, y, 38, 15, 5, 3)) return green;
                bool body = Disc(x, y, 32, 32, 15) ||
                    (y >= 30 && y <= 54 && Mathf.Abs(x - 32) < (54 - y) * 0.55f);
                if (body)
                {
                    if ((int)(x + y * 7) % 11 == 0 && Disc(x, y, 32, 34, 13)) return white; // 씨
                    return red;
                }
                return bg;
            case Fruit.Banana:
                bool crescent = Disc(x, y, 30, 32, 20) && !Disc(x, y, 40, 24, 18);
                if (crescent) return yellow;
                if (Disc(x, y, 14, 44, 3) || Disc(x, y, 47, 38, 3)) return brown; // 양끝
                return bg;
            case Fruit.Orange:
                if (Ell(x, y, 39, 14, 7, 3.5f)) return green;               // 잎
                if (Disc(x, y, 32, 35, 16))
                {
                    if (Disc(x, y, 26, 41, 4)) return white;
                    if (Disc(x, y, 32, 35, 16) && !Disc(x, y, 32, 35, 13)) return darkRed; // 테두리 음영
                    return orange;
                }
                return bg;
            case Fruit.Pear:
                if (Bar(x, y, 30, 34, 6, 14)) return brown;
                if (Ell(x, y, 40, 11, 7, 3.5f)) return green;
                if (Disc(x, y, 32, 40, 14) || Disc(x, y, 32, 25, 9)) return paleGreen; // 몸+위
                return bg;
            case Fruit.Peach:
                if (Ell(x, y, 39, 14, 7, 3.5f)) return green;
                if (Disc(x, y, 32, 35, 15))
                {
                    if (Mathf.Abs(x - (32 + (y - 35) * 0.18f)) < 1.6f) return darkPink; // 골
                    if (Disc(x, y, 26, 41, 4)) return white;
                    return pink;
                }
                return bg;
            case Fruit.Watermelon:
                if (Bar(x, y, 30, 34, 8, 16)) return darkGreen;             // 꼭지
                if (Disc(x, y, 32, 35, 16))
                {
                    if ((int)((x - 14) / 6) % 2 == 0) return darkGreen;     // 줄무늬
                    return green;
                }
                return bg;
            default: return bg;
        }
    }
}
#endif
