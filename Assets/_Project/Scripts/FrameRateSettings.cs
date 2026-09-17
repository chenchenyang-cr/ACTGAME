using UnityEngine;

public static class FrameRateSettings
{
    private const int TargetFrameRate = 120;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        // VSync overrides Application.targetFrameRate on desktop platforms.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFrameRate;
    }
}
