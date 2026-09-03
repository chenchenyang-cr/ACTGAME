using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityLearning.EnemySystem;

public static class MainSceneCombatDirectorConfigurator
{
    private const string ScenePath = "Assets/_Project/Scenes/Main_Nodachi.unity";
    private const string AutoConfigureRequest =
        "Temp/MainSceneCombatDirectorAutoConfigure.request";
    private const string SourceModelPath =
        "Assets/ThirdParty/SwordAnimationPack/Model/9CG_Sword.FBX";
    private const string EnemyControllerPath =
        "Assets/_Project/Enemies/SwordEnemy/Animators/SwordEnemy.controller";

    [MenuItem("Tools/Enemy/Configure Main Scene Combat Director")]
    public static void Configure()
    {
        Scene scene = FindLoadedScene();
        if (!scene.IsValid() || !scene.isLoaded)
            throw new System.InvalidOperationException(
                $"请先打开场景：{ScenePath}");

        EncounterCombatDirector director = FindInScene<EncounterCombatDirector>(scene);
        bool createdDirector = director == null;
        if (director == null)
        {
            GameObject directorObject = new GameObject("Combat Director");
            SceneManager.MoveGameObjectToScene(directorObject, scene);
            director = directorObject.AddComponent<EncounterCombatDirector>();
        }

        Transform player = FindPlayer(scene);
        if (player == null)
            throw new MissingReferenceException("Main_Nodachi 场景中没有 Player 标签对象。");

        SerializedObject serialized = new SerializedObject(director);
        serialized.FindProperty("target").objectReferenceValue = player;
        serialized.FindProperty("findPlayerTargetOnAwake").boolValue = true;
        // 已存在的导演保留手动调过的令牌数；首次创建时默认关闭攻击，方便测试基础移动与受击。
        if (createdDirector)
            serialized.FindProperty("maximumConcurrentAttackers").intValue = 0;
        serialized.FindProperty("minimumSlotCount").intValue = 4;
        serialized.FindProperty("innerRingRadius").floatValue = 2.7f;
        serialized.FindProperty("outerRingRadius").floatValue = 6.5f;
        serialized.FindProperty("confrontationRegionDepth").floatValue = 2.8f;
        serialized.FindProperty("sectorBoundaryPadding").floatValue = 5f;
        serialized.FindProperty("slotArrivalTolerance").floatValue = 0.3f;
        serialized.FindProperty("navMeshSampleDistance").floatValue = 1.5f;
        serialized.FindProperty("minimumEnemySpacing").floatValue = 0.8f;
        serialized.FindProperty("angularOffset").floatValue = 30f;
        serialized.FindProperty("closeGapThreshold").floatValue = 0.12f;
        serialized.FindProperty("orbitMinimumTargetDistance").floatValue = 0.8f;
        serialized.FindProperty("orbitRadialFreedom").floatValue = 0.35f;
        serialized.FindProperty("orbitWalkDurationMin").floatValue = 1f;
        serialized.FindProperty("orbitWalkDurationMax").floatValue = 2f;
        serialized.FindProperty("orbitWaitChance").floatValue = 0.65f;
        serialized.FindProperty("orbitIdleDurationMin").floatValue = 0.3f;
        serialized.FindProperty("orbitIdleDurationMax").floatValue = 0.5f;
        serialized.FindProperty("orbitTargetRetryPauseMin").floatValue = 0.18f;
        serialized.FindProperty("orbitTargetRetryPauseMax").floatValue = 0.32f;
        serialized.FindProperty("pressureChance").floatValue = 0.3f;
        serialized.FindProperty("orbitReverseChance").floatValue = 0.2f;
        serialized.FindProperty("pressureStepDistance").floatValue = 0.8f;
        serialized.FindProperty("pressureDuration").floatValue = 0.9f;
        serialized.FindProperty("attackCorridorWidth").floatValue = 0.9f;
        serialized.FindProperty("yieldDistance").floatValue = 0.9f;
        serialized.FindProperty("yieldDuration").floatValue = 0.7f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        int repairedEnemies = RepairSceneEnemies(scene);

        EditorUtility.SetDirty(director);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new System.InvalidOperationException("保存 Main_Nodachi 场景失败。");

        Selection.activeGameObject = director.gameObject;
        Debug.Log(
            $"Main_Nodachi 战斗导演配置完成：已修复 {repairedEnemies} 个敌人的 Animator/Root Motion 配置；" +
            $"{director.MaximumConcurrentAttackers} 个进攻令牌，4 个扇形区域，内环 2.7 米、外环 6.5 米，并启用自由移动、施压、让位和撤退。",
            director);
    }

    [InitializeOnLoadMethod]
    private static void ConfigureWhenRequested()
    {
        string requestPath = Path.GetFullPath(AutoConfigureRequest);
        if (!File.Exists(requestPath))
            return;

        File.Delete(requestPath);
        EditorApplication.delayCall += () =>
        {
            try
            {
                Configure();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        };
    }

    private static Scene FindLoadedScene()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene candidate = SceneManager.GetSceneAt(i);
            if (candidate.path == ScenePath)
                return candidate;
        }

        return default;
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T result = roots[i].GetComponentInChildren<T>(true);
            if (result != null)
                return result;
        }

        return null;
    }

    private static Transform FindPlayer(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < transforms.Length; j++)
            {
                if (transforms[j].CompareTag("Player"))
                    return transforms[j];
            }
        }

        return null;
    }

    private static int RepairSceneEnemies(Scene scene)
    {
        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(SourceModelPath)
            .OfType<Avatar>()
            .FirstOrDefault();
        RuntimeAnimatorController controller =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(EnemyControllerPath);
        if (avatar == null || controller == null)
            throw new MissingReferenceException("SwordEnemy 的 Avatar 或 AnimatorController 缺失。");

        int repaired = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            EnemyController[] enemies = root.GetComponentsInChildren<EnemyController>(true);
            foreach (EnemyController enemy in enemies)
            {
                Animator animator = enemy.GetComponentInChildren<Animator>(true);
                if (animator == null)
                    continue;

                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = true;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                EditorUtility.SetDirty(animator);

                NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    agent.updatePosition = false;
                    agent.updateRotation = false;
                    EditorUtility.SetDirty(agent);
                }

                repaired++;
            }
        }

        return repaired;
    }
}
