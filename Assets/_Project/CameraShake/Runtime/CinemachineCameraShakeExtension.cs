using Cinemachine;
using UnityEngine;

namespace CombatCamera
{
    [ExecuteAlways]
    [SaveDuringPlay]
    [DisallowMultipleComponent]
    [AddComponentMenu("Cinemachine/Combat Camera Shake Extension")]
    public sealed class CinemachineCameraShakeExtension : CinemachineExtension
    {
        [Header("Safety Limits")]
        [Min(0f)] public float MaximumPositionOffset = 1f;
        [Min(0f)] public float MaximumRotationOffset = 15f;
        [Min(0f)] public float MaximumFovOffset = 20f;

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Finalize || vcam != VirtualCamera)
                return;

            CameraShakeSample sample = CameraShakeRuntime.EvaluateCurrent();
            Vector3 worldPosition = state.CorrectedOrientation * sample.Position +
                                    sample.WorldPosition;
            Vector3 position = SoftLimit(worldPosition, MaximumPositionOffset);
            Vector3 rotation = SoftLimit(sample.Rotation, MaximumRotationOffset);
            float fov = SoftLimit(sample.Fov, MaximumFovOffset);

            state.PositionCorrection += position;
            state.OrientationCorrection *= Quaternion.Euler(rotation);

            LensSettings lens = state.Lens;
            if (!lens.Orthographic)
                lens.FieldOfView = Mathf.Clamp(lens.FieldOfView + fov, 1f, 179f);
            state.Lens = lens;
        }

        private static Vector3 SoftLimit(Vector3 value, float limit)
        {
            limit = Mathf.Max(0f, limit);
            float magnitude = value.magnitude;
            if (limit <= 0f || magnitude <= 0.000001f)
                return Vector3.zero;

            float limitedMagnitude = limit *
                                     (float)System.Math.Tanh(magnitude / limit);
            return value * (limitedMagnitude / magnitude);
        }

        private static float SoftLimit(float value, float limit)
        {
            limit = Mathf.Max(0f, limit);
            if (limit <= 0f)
                return 0f;
            return limit * (float)System.Math.Tanh(value / limit);
        }
    }
}
