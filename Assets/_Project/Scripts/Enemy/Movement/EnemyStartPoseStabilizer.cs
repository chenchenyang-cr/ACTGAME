using UnityEngine;

namespace UnityLearning.EnemySystem
{
    [DefaultExecutionOrder(12000)]
    [RequireComponent(typeof(Animator))]
    public sealed class EnemyStartPoseStabilizer : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Transform pelvisRoot;
        [SerializeField] private Transform leftFoot;
        [SerializeField] private Transform rightFoot;
        [SerializeField, Min(0f)] private float footContactHeight = 0.03f;

        private bool hasIdlePose;
        private float idlePelvisHeight;
        private float idleLeftFootHeight;
        private float idleRightFootHeight;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();
        }

        private void LateUpdate()
        {
            CorrectPose();
        }

        public void CorrectPose()
        {
            if (animator == null || pelvisRoot == null || leftFoot == null || rightFoot == null)
                return;

            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            bool transitioning = animator.IsInTransition(0);
            bool starting = current.IsTag("LocomotionStart") ||
                            (transitioning && animator.GetNextAnimatorStateInfo(0).IsTag("LocomotionStart"));

            if (!starting)
            {
                if (!transitioning && current.IsName("Idle"))
                {
                    idlePelvisHeight = RelativeHeight(pelvisRoot);
                    idleLeftFootHeight = RelativeHeight(leftFoot);
                    idleRightFootHeight = RelativeHeight(rightFoot);
                    hasIdlePose = true;
                }
                return;
            }

            if (!hasIdlePose)
                return;

            float leftHeight = RelativeHeight(leftFoot);
            float rightHeight = RelativeHeight(rightFoot);
            float lift = Mathf.Max(0f, idlePelvisHeight - RelativeHeight(pelvisRoot));
            if (lift > 0f)
                pelvisRoot.position += Vector3.up * lift;
            KeepPlantedFoot(leftFoot, leftHeight, idleLeftFootHeight, lift);
            KeepPlantedFoot(rightFoot, rightHeight, idleRightFootHeight, lift);
        }

        private float RelativeHeight(Transform bone)
        {
            return bone.position.y - transform.position.y;
        }

        private void KeepPlantedFoot(Transform foot, float originalHeight, float idleHeight, float lift)
        {
            float contactWeight = 1f - Mathf.Clamp01(
                (originalHeight - idleHeight) / Mathf.Max(footContactHeight, 0.0001f));
            if (contactWeight <= 0f)
                return;

            float targetHeight = Mathf.Max(idleHeight, originalHeight);
            float correction = (originalHeight + lift - targetHeight) * contactWeight;
            foot.position -= Vector3.up * correction;
        }
    }
}
