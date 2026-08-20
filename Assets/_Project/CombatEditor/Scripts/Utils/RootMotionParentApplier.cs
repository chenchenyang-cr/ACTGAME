using UnityEngine;

namespace CombatEditor
{
    [DisallowMultipleComponent]
    public class RootMotionParentApplier : MonoBehaviour, ITurn180RootMotionHandler
    {
        [SerializeField] private RootMotionReceiver receiver;
        [SerializeField] private Animator sourceAnimator;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private bool autoFindOnChildren = true;

        [Header("Locomotion Velocity Matching")]
        [SerializeField] private bool smoothStartToLoopVelocity = true;
        [SerializeField, Min(0.01f)] private float velocitySmoothTime = 0.12f;
        [SerializeField, Min(0.01f)] private float velocityMatchTolerance = 0.05f;
        [SerializeField, Min(0.05f)] private float maximumMatchTime = 0.35f;

        [Header("Turn 180 Root Motion")]
        [SerializeField, Min(0.01f)] private float turn180BlendSmoothTime = 0.08f;

        private int lastAppliedFrame = -1;
        private Vector3 appliedPlanarVelocity;
        private Vector3 planarVelocitySmoothDamp;
        private bool matchingLocomotionVelocity;
        private float velocityMatchElapsed;
        private float rootMotionTranslationScale = 1f;
        private float lastStablePlanarSpeed;
        private float turn180PlanarSpeed;
        private float turn180BlendWeight;
        private float turn180BlendVelocity;
        private bool applyingTurn180Motion;

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
            TryResolveReceiver();
        }

        private void OnDisable()
        {
            receiver?.SetTurn180RootMotionActive(false);
            ResetTurn180Motion();
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
                delta = MatchStartToLoopVelocity(delta);
            }

            delta = ProcessTurn180Translation(
                delta,
                deltaRotation,
                receiver.CurrentTurn180Weight);

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

        private Vector3 ProcessTurn180Translation(
            Vector3 animationDeltaPosition,
            Quaternion processedDeltaRotation,
            float animatorTurnWeight)
        {
            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 rawPlanarDelta = new Vector3(
                animationDeltaPosition.x,
                0f,
                animationDeltaPosition.z);
            float rawPlanarSpeed = rawPlanarDelta.magnitude / deltaTime;
            float targetTurnWeight = Mathf.Clamp01(animatorTurnWeight);
            bool animatorUsesTurn180 = targetTurnWeight > 0.001f;

            if (animatorUsesTurn180 && !applyingTurn180Motion)
            {
                turn180PlanarSpeed = lastStablePlanarSpeed > 0.01f
                    ? lastStablePlanarSpeed
                    : rawPlanarSpeed;
                applyingTurn180Motion = true;
            }

            turn180BlendWeight = Mathf.SmoothDamp(
                turn180BlendWeight,
                targetTurnWeight,
                ref turn180BlendVelocity,
                turn180BlendSmoothTime,
                Mathf.Infinity,
                deltaTime);

            if (!applyingTurn180Motion)
            {
                if (rawPlanarSpeed > 0.01f)
                {
                    lastStablePlanarSpeed = rawPlanarSpeed;
                }

                return animationDeltaPosition;
            }

            if (!animatorUsesTurn180 && turn180BlendWeight <= 0.001f)
            {
                ResetTurn180Motion();
                if (rawPlanarSpeed > 0.01f)
                {
                    lastStablePlanarSpeed = rawPlanarSpeed;
                }

                return animationDeltaPosition;
            }

            if (turn180PlanarSpeed <= 0.01f)
            {
                return animationDeltaPosition;
            }

            float easedTurnWeight = SmoothStep01(turn180BlendWeight);
            Quaternion nextWorldRotation = transform.rotation * processedDeltaRotation;
            Vector3 turnDirection = nextWorldRotation * Vector3.forward;
            turnDirection.y = 0f;
            if (turnDirection.sqrMagnitude <= 0.0001f)
            {
                turnDirection = transform.forward;
                turnDirection.y = 0f;
            }
            turnDirection.Normalize();

            Vector3 rawDirection = rawPlanarDelta.sqrMagnitude > 0.0001f
                ? rawPlanarDelta.normalized
                : turnDirection;
            Vector3 smoothedDirection = Vector3.Slerp(
                rawDirection,
                turnDirection,
                easedTurnWeight);
            smoothedDirection.y = 0f;
            smoothedDirection.Normalize();

            Vector3 maintainedPlanarDelta =
                smoothedDirection * (turn180PlanarSpeed * deltaTime);
            return new Vector3(
                maintainedPlanarDelta.x,
                animationDeltaPosition.y,
                maintainedPlanarDelta.z);
        }

        private void ResetTurn180Motion()
        {
            turn180PlanarSpeed = 0f;
            turn180BlendWeight = 0f;
            turn180BlendVelocity = 0f;
            applyingTurn180Motion = false;
        }

        private static float SmoothStep01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
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
        }

        private void ResolveCharacterController()
        {
            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }
        }

        public void SetSourceAnimator(Animator animator)
        {
            sourceAnimator = animator;
            receiver = sourceAnimator != null ? sourceAnimator.GetComponent<RootMotionReceiver>() : null;
            ResetTurn180Motion();
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
