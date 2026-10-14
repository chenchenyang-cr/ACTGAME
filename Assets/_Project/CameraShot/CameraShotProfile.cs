using System;
using System.Collections.Generic;
using UnityEngine;

namespace CombatCamera
{
    [Serializable]
    public struct CameraShotTiming
    {
        [Min(0f)] public float EnterSeconds;
        public AnimationCurve EnterCurve;
        [Min(0f)] public float ExitSeconds;
        public AnimationCurve ExitCurve;

        public static CameraShotTiming Default => new CameraShotTiming {
            EnterSeconds = .2f, ExitSeconds = .25f,
            EnterCurve = AnimationCurve.EaseInOut(0, 0, 1, 1),
            ExitCurve = AnimationCurve.EaseInOut(0, 0, 1, 1)
        };

        public void GetDurations(out float enter, out float exit)
        {
            enter = Mathf.Max(0, EnterSeconds); exit = Mathf.Max(0, ExitSeconds);
        }

        public static float EvaluateCurve(AnimationCurve curve, float progress)
        {
            // Exact endpoints prevent a jump when handing back an edited curve.
            if (progress <= 0) return 0;
            if (progress >= 1) return 1;
            return curve != null && curve.length > 0 ? Mathf.Clamp01(curve.Evaluate(progress)) : progress;
        }
    }

    [Serializable]
    public sealed class CameraShotCandidate
    {
        public string Name = "机位";
        [Tooltip("机位相对玩家的水平方向，编辑器以 0-360 度角度显示。距离由现有 FreeLook 轨道决定。")]
        public Vector2 RelativePosition = new Vector2(1, -3);
        [Tooltip("镜头相对玩家根节点的高度（米），自动限制到现有轨道允许的范围。")]
        public float Height = 1.55f;

        public static float AngleFromDirection(Vector2 direction) =>
            Mathf.Repeat(Mathf.Atan2(direction.x, -direction.y) * Mathf.Rad2Deg, 360f);

        public static Vector2 DirectionFromAngle(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(radians), -Mathf.Cos(radians));
        }
    }

    [CreateAssetMenu(menuName = "Combat Camera/Camera Shot Profile", fileName = "CameraShotProfile")]
    public sealed class CameraShotProfile : ScriptableObject
    {
        public List<CameraShotCandidate> Candidates = new List<CameraShotCandidate> {
            new CameraShotCandidate { Name = "后方右侧", RelativePosition = new Vector2(1, -3) },
            new CameraShotCandidate { Name = "后方左侧", RelativePosition = new Vector2(-1, -3) }
        };
        public int Priority = 10;
        public bool LockCameraInput = true;
    }
}
