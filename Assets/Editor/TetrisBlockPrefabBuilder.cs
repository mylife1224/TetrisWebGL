// 블럭 프리팹 생성기 (에이전트 소유 자동화).
// 기존 TetrisGame.NewBlockObject("Block", CubeMesh, blockMats[i], pos, 0.92) 호출과
// 동등한 내용의 프리팹(Assets/UI/Resources/Prefabs/Block.prefab)을 코드로 생성.
// 손으로 .prefab YAML을 쓰지 않는 이유: Mesh/Material GUID 참조를 틀리면
// 깨진 프리팹이 되므로, 에디터 API로 생성하는 것이 안전.
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public static class TetrisBlockPrefabBuilder
{
    const string PrefabDir = "Assets/UI/Resources/Prefabs";
    const string PrefabPath = PrefabDir + "/Block.prefab";

    [MenuItem("Tetris/Build Block Prefab")]
    public static void BuildBlockPrefab()
    {
        // 머티리얼 7종이 없으면 여기서 보장 (멱등).
        // 이 과정을 하는 이유: 프리팹의 기본 머티리얼로 Block_I를 참조하는데,
        // 없는 상태에서 프리팹을 만들면 missing 참조가 되기 때문.
        TetrisKoreanFontSetup.EnsureRuntimeMaterials();

        if (!AssetDatabase.IsValidFolder(PrefabDir))
        {
            string parent = "Assets/UI/Resources";
            AssetDatabase.CreateFolder(parent, "Prefabs");
        }

        var blockMat = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/UI/Resources/Materials/Fruit_I.mat");
        if (blockMat == null) // Fruit 생성 전이면 구 머티리얼로 대체
            blockMat = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/UI/Resources/Materials/Block_I.mat");
        if (blockMat == null)
            throw new System.Exception("[Tetris] Block_I.mat 없음. EnsureRuntimeMaterials 실패?");

        // 내장 큐브 메시 사용. CreatePrimitive를 쓰지 않는 이유:
        // CreatePrimitive는 Collider를 달고 오는데, 현행 코드는 물리가 없어
        // 콜라이더를 떼거나(NewBlockObject 방식) 애초에 달지 않기 때문.
        var cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        if (cubeMesh == null)
            throw new System.Exception("[Tetris] 내장 Cube.fbx를 찾지 못함.");

        var go = new GameObject("Block");
        go.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = blockMat;
        // 스케일은 런타임 코드가 지정(현행 0.92). 프리팹은 1 유지.
        go.transform.localScale = Vector3.one;

        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);

        // 검증: 저장된 프리팹을 다시 읽어 구성 확인.
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
            throw new System.Exception("[Tetris] 프리팹 저장 실패: " + PrefabPath);
        var mf = prefab.GetComponent<MeshFilter>();
        var mr = prefab.GetComponent<MeshRenderer>();
        if (mf == null || mf.sharedMesh == null)
            throw new System.Exception("[Tetris] 프리팹 MeshFilter/Mesh 누락.");
        if (mr == null || mr.sharedMaterial == null)
            throw new System.Exception("[Tetris] 프리팹 MeshRenderer/Material 누락.");
        if (prefab.GetComponent<Collider>() != null)
            throw new System.Exception("[Tetris] 프리팹에 Collider가 있음(물리 불필요).");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Tetris] Block prefab OK: " + PrefabPath
            + " mesh=" + mf.sharedMesh.name
            + " mat=" + mr.sharedMaterial.name);
    }
}
#endif
