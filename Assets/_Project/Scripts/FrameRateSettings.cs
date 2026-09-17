using UnityEngine;

public static class FrameRateSettings
{
    private const int TargetFrameRate = 120;

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void ApplyAfterScriptReload()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (Application.isPlaying) Apply();
        };
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        // VSync overrides Application.targetFrameRate on desktop platforms.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFrameRate;
        Time.fixedDeltaTime = 1f / TargetFrameRate;
    }
}
