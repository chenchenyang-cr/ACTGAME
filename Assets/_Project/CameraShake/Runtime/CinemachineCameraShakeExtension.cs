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
            Vector3 position = Vector3.ClampMagnitude(sample.Position,
                Mathf.Max(0f, MaximumPositionOffset));
            Vector3 rotation = Vector3.ClampMagnitude(sample.Rotation,
                Mathf.Max(0f, MaximumRotationOffset));
            float fov = Mathf.Clamp(sample.Fov, -MaximumFovOffset, MaximumFovOffset);

            state.PositionCorrection += state.CorrectedOrientation * position;
            state.OrientationCorrection *= Quaternion.Euler(rotation);

            LensSettings lens = state.Lens;
            if (!lens.Orthographic)
                lens.FieldOfView = Mathf.Clamp(lens.FieldOfView + fov, 1f, 179f);
            state.Lens = lens;
        }
    }
}
