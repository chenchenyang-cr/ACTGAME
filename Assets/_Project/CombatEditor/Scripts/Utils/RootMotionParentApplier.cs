using UnityEngine;

using UnityEngine.AI;

namespace CombatEditor
{
    [DisallowMultipleComponent]
    public class RootMotionParentApplier : MonoBehaviour, ITurn180RootMotionHandler
    {
        [SerializeField] private RootMotionReceiver receiver;
        [SerializeField] private Animator sourceAnimator;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private NavMeshAgent navMeshAgent;
        [SerializeField] private bool autoFindOnChildren = true;

        [Header("Locomotion Velocity Matching")]
        [SerializeField] private bool smoothStartToLoopVelocity = true;
        [SerializeField, Min(0.01f)] private float velocitySmoothTime = 0.12f;
        [SerializeField, Min(0.01f)] private float velocityMatchTolerance = 0.05f;
        [SerializeField, Min(0.05f)] private float maximumMatchTime = 0.35f;

        private int lastAppliedFrame = -1;
        private Vector3 appliedPlanarVelocity;
        private Vector3 planarVelocitySmoothDamp;
        private bool matchingLocomotionVelocity;
        private float velocityMatchElapsed;
        private float rootMotionTranslationScale = 1f;

        private void Reset()
        {
            ResolveCharacterController();
            if (sourceAnimator == null && autoFindOnChildren)
            {
                sourceAnimator = GetComponentInChildren<Animator>(true);
            }

            if (sourceAnimator == null)
            {
                return;
            }

            receiver = sourceAnimator.GetComponent<RootMotionReceiver>();
#if UNITY_EDITOR
            if (receiver == null)
            {
                receiver = UnityEditor.Undo.AddComponent<RootMotionReceiver>(sourceAnimator.gameObject);
            }
#endif
        }

        private void Awake()
        {
            ResolveCharacterController();
            ResolveNavMeshAgent();
            TryResolveReceiver();
        }

        private void OnEnable()
        {
            ResolveNavMeshAgent();
            if (navMeshAgent != null)
            {
                navMeshAgent.updatePosition = false;
                navMeshAgent.updateRotation = false;
            }
        }

        private void OnDisable()
        {
            receiver?.SetTurn180RootMotionActive(false);
        }

        private void LateUpdate()
        {
            if (!TryResolveReceiver())
            {
                return;
            }

            int sourceFrame = receiver.LastRootMotionFrame;
            if (sourceFrame <= 0 || sourceFrame == lastAppliedFrame)
            {
                return;
            }

            Vector3 delta = receiver.ConsumeRootMotion();
            Quaternion deltaRotation = receiver.ConsumeRootRotation();
            if (delta.sqrMagnitude > 0f)
            {
                if (receiver.CurrentTurn180Weight > 0.001f)
                {
                    float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
                    Vector3 rawPlanarVelocity = new Vector3(delta.x, 0f, delta.z) / deltaTime;
                    ResetVelocityMatching(rawPlanarVelocity);
                }
                else
                {
                    delta = MatchStartToLoopVelocity(delta);
                }
            }

            if (delta.sqrMagnitude > 0f)
            {
                delta = new Vector3(
                    delta.x * rootMotionTranslationScale,
                    delta.y,
                    delta.z * rootMotionTranslationScale);
                ApplyMotion(delta);
            }

            if (Quaternion.Angle(Quaternion.identity, deltaRotation) > 0.001f)
            {
                transform.rotation *= deltaRotation;
            }

            lastAppliedFrame = sourceFrame;
        }

        private Vector3 MatchStartToLoopVelocity(Vector3 delta)//处理start动画到loop中间速度不相同的平滑过渡
        {
            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 rawPlanarVelocity = new Vector3(delta.x, 0f, delta.z) / deltaTime;

            if (!smoothStartToLoopVelocity || sourceAnimator == null)
            {
                ResetVelocityMatching(rawPlanarVelocity);
                return delta;
            }

            bool isStartToLoopTransition = IsStartToLoopTransition();
            bool isLoopState = sourceAnimator.GetCurrentAnimatorStateInfo(0).IsTag("LocomotionLoop");

            if (isStartToLoopTransition && !matchingLocomotionVelocity)
            {
                matchingLocomotionVelocity = true;
                velocityMatchElapsed = 0f;
                planarVelocitySmoothDamp = Vector3.zero;
            }

            if (!matchingLocomotionVelocity)
            {
                appliedPlanarVelocity = rawPlanarVelocity;
                return delta;
            }

            velocityMatchElapsed += deltaTime;
            appliedPlanarVelocity = Vector3.SmoothDamp(
                appliedPlanarVelocity,
                rawPlanarVelocity,
                ref planarVelocitySmoothDamp,
                velocitySmoothTime,
                Mathf.Infinity,
                deltaTime);

            bool velocityMatched = (appliedPlanarVelocity - rawPlanarVelocity).sqrMagnitude <=
                                   velocityMatchTolerance * velocityMatchTolerance;
            bool leftLocomotion = !isStartToLoopTransition && !isLoopState;
            if (leftLocomotion || velocityMatched || velocityMatchElapsed >= maximumMatchTime)
            {
                ResetVelocityMatching(rawPlanarVelocity);
            }

            return new Vector3(
                appliedPlanarVelocity.x * deltaTime,
                delta.y,
                appliedPlanarVelocity.z * deltaTime);
        }

        private bool IsStartToLoopTransition()
        {
            if (!sourceAnimator.IsInTransition(0))
            {
                return false;
            }

            AnimatorStateInfo current = sourceAnimator.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo next = sourceAnimator.GetNextAnimatorStateInfo(0);
            return current.IsTag("LocomotionStart") && next.IsTag("LocomotionLoop");
        }

        private void ResetVelocityMatching(Vector3 rawPlanarVelocity)
        {
            appliedPlanarVelocity = rawPlanarVelocity;
            planarVelocitySmoothDamp = Vector3.zero;
            matchingLocomotionVelocity = false;
            velocityMatchElapsed = 0f;
        }

        private bool TryResolveReceiver()
        {
            if (receiver != null)
            {
                return true;
            }

            if (sourceAnimator == null && autoFindOnChildren)
            {
                sourceAnimator = GetComponentInChildren<Animator>(true);
            }

            if (sourceAnimator != null)
            {
                receiver = sourceAnimator.GetComponent<RootMotionReceiver>();
            }

            return receiver != null;
        }

        private void ApplyMotion(Vector3 delta)
        {
            ResolveCharacterController();
            if (characterController != null && characterController.enabled)
            {
                characterController.Move(delta);
                return;
            }

            transform.position += delta;
            SyncNavMeshAgentToTransform();
        }

        private void ResolveCharacterController()
        {
            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }
        }

        private void ResolveNavMeshAgent()
        {
            if (navMeshAgent == null)
            {
                navMeshAgent = GetComponent<NavMeshAgent>();
            }
        }

        private void SyncNavMeshAgentToTransform()
        {
            ResolveNavMeshAgent();
            if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
            {
                return;
            }

            // Agent 只计算路径与期望速度，动画根运动拥有最终的位置写入权。
            navMeshAgent.nextPosition = transform.position;
        }

        public void SetSourceAnimator(Animator animator)
        {
            sourceAnimator = animator;
            receiver = sourceAnimator != null ? sourceAnimator.GetComponent<RootMotionReceiver>() : null;
        }

        public bool SetRootRotationProcessor(
            System.Func<Quaternion, Quaternion> processor)
        {
            if (!TryResolveReceiver())
            {
                return false;
            }

            receiver.SetRootRotationProcessor(processor);
            return true;
        }

        public void SetTurn180RootMotionActive(bool active)
        {
            if (!TryResolveReceiver())
            {
                return;
            }

            receiver.SetTurn180RootMotionActive(active);
        }

        public void SetRootMotionTranslationScale(float scale)
        {
            rootMotionTranslationScale = Mathf.Max(0f, scale);
        }

    }
}
