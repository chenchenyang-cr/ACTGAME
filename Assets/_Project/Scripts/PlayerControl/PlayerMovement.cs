using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum PlayerRotationMode
{
    MovementDirection,
    Animation,
    Preserve
}

public readonly struct PlayerMovementDirectionSnapshot
{
    public Vector2 Input { get; }
    public Vector3 WorldDirection { get; }
    public Vector2 LocalDirection { get; }
    public bool HasDirection { get; }

    public PlayerMovementDirectionSnapshot(
        Vector2 input,
        Vector3 worldDirection,
        Vector2 localDirection,
        bool hasDirection)
    {
        Input = input;
        WorldDirection = worldDirection;
        LocalDirection = localDirection;
        HasDirection = hasDirection;
    }
}

public class PlayerMovement : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private PlayerInputReader inputReader;
    [SerializeField]
    private CharacterController characterController;
    [SerializeField]
    private Transform cameraTransform;
    [Header("Vertical Motion")]
    [SerializeField]
    private float gravity = -25f;
    [SerializeField]
    private float groundedVerticalSpeed = -2f;
    [SerializeField, Min(1f)]
    private float maximumFallSpeed = 35f;
    [Header("Rotation")]
    [SerializeField, Min(0f)]
    [Tooltip("Maximum movement-facing angular speed in degrees per second.")]
    private float rotationSpeed = 720;
    [SerializeField]
    [Tooltip("X is the current-to-target angle normalized from 0 to 180 degrees; Y is the rotation-speed multiplier.")]
    private AnimationCurve rotationSpeedByAngle =
        AnimationCurve.Linear(0f, 0.15f, 1f, 1f);
    [SerializeField, Range(90f, 180f)]
    private float turn180Threshold = 135f;
    [SerializeField] private bool turn180RequiresFastMovement = true;
    [SerializeField, Min(0f)] private float rotationSmoothTime;
    [Header("Target Matched Turn")]
    [SerializeField] private bool matchTurn180ToInput;
    [Tooltip("Signed horizontal rotation of the source turn clip.")]
    [SerializeField] private float authoredTurn180Angle = 180f;
    [Tooltip("Normalized yaw progress sampled from the source clip, not a linear turn.")]
    [SerializeField] private AnimationCurve turn180YawProgress = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField, Min(0.01f)] private float turn180RecoveryTime = 0.18f;
    [Header("Animation")]
    [SerializeField]
    private Animator animator;
    [SerializeField]
    private float animatorParameterSmoothSpeed = 10f;
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int StartXHash = Animator.StringToHash("StartX");
    private static readonly int StartYHash = Animator.StringToHash("StartY");
    private static readonly int StopXHash = Animator.StringToHash("StopX");
    private static readonly int StopYHash = Animator.StringToHash("StopY");
    private static readonly int StopSpeedHash = Animator.StringToHash("StopSpeed");
    private static readonly int CombatWeightHash = Animator.StringToHash("CombatWeight");
    private static readonly int TurnDirectionHash = Animator.StringToHash("TurnDirection");
    private static readonly int Turn180StateHash =
        Animator.StringToHash("Base Layer.NormalLocomotion.Turn180");
    private static readonly int CombatTurn180StateHash =
        Animator.StringToHash("Base Layer.CombatLocomotion.Turn180");
    private const float MoveInputThreshold = 0.01f;
    private const float CombatModeThreshold = 0.5f;
    private const float WalkGaitSample = 0.35f;
    private const float RunGaitSample = 1f;
    private const float FastRunGaitSample = 2f;
    private const float DirectionStepDegrees = 45f;

    private Vector2 moveInput;
    private Vector2 smoothedAnimatorMoveInput;
    private float smoothedAnimatorMoveAmount;
    private Vector3 worldMoveDirection;
    private Vector2 lastNonZeroLocalDirection = Vector2.up;
    private bool wasMoving;
    private CombatEditor.RootMotionParentApplier rootMotionApplier;
    private CombatEditor.ITurn180RootMotionHandler turn180RootMotionHandler;
    private PlayerRotationMode rotationMode = PlayerRotationMode.MovementDirection;
    private bool isCombatMovement;
    private bool isFastMovementActive;
    private float verticalVelocity;
    private bool matchedTurnActive;
    private bool matchedTurnStateSeen;
    private int matchedTurnRequestFrame;
    private Quaternion matchedTurnStartRotation;
    private float matchedTurnAngle;
    private float turnRecoveryRemaining;
    private float angularVelocity;

    public bool IsGrounded { get; private set; }

    private void Awake()
    {
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            animator.applyRootMotion = true;
        }
        if (animator != null)
        {
            rootMotionApplier = GetComponent<CombatEditor.RootMotionParentApplier>();
            if (rootMotionApplier == null)
            {
                rootMotionApplier = gameObject.AddComponent<CombatEditor.RootMotionParentApplier>();
            }
            rootMotionApplier.SetSourceAnimator(animator);
            turn180RootMotionHandler = rootMotionApplier;
        }
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    private void OnEnable()
    {
        BindRootRotationProcessor();
    }

    private void OnDisable()
    {
        ResetMatchedTurn();
        turn180RootMotionHandler?.SetTurn180RootMotionActive(false);
        rootMotionApplier?.SetRootRotationProcessor(null);
        rootMotionApplier?.SetRootMotionTranslationScale(1f);
    }

    private void BindRootRotationProcessor()
    {
        rootMotionApplier?.SetRootRotationProcessor(ProcessRootRotation, matchTurn180ToInput);
    }

    public void Tick(Vector2 input, bool hasMoveInput, bool allowTurn180 = true)
    {
        if (!allowTurn180 || rotationMode != PlayerRotationMode.MovementDirection)
            ResetMatchedTurn();
        UpdateVerticalMotion();
        moveInput = Vector2.ClampMagnitude(input, 1f);
        UpdateMoveDirection();
        isCombatMovement = animator != null && animator.GetFloat(CombatWeightHash) >= CombatModeThreshold;
        bool isUsingTurn180RootMotion = allowTurn180 && UpdateTurn180();
        turn180RootMotionHandler?.SetTurn180RootMotionActive(
            isUsingTurn180RootMotion);
        UpdateAnimatorParameters(isCombatMovement, hasMoveInput);
    }

    public void PrepareLocomotionAnimation(Vector2 input)
    {
        moveInput = Vector2.ClampMagnitude(input, 1f);
        UpdateMoveDirection();
        isCombatMovement = animator != null &&
                           animator.GetFloat(CombatWeightHash) >= CombatModeThreshold;
        if (isFastMovementActive)
        {
            smoothedAnimatorMoveAmount = FastRunGaitSample;
            animator?.SetFloat(MoveSpeedHash, FastRunGaitSample);
        }
        UpdateAnimatorParameters(isCombatMovement, true);
    }

    public PlayerMovementDirectionSnapshot CaptureDirectionSnapshot(Vector2 input)
    {
        Vector2 clampedInput = Vector2.ClampMagnitude(input, 1f);
        Vector3 worldDirection = ResolveWorldMoveDirection(clampedInput);
        bool hasDirection = worldDirection.sqrMagnitude > MoveInputThreshold * MoveInputThreshold;
        Vector2 localDirection = Vector2.zero;

        if (hasDirection)
        {
            Vector3 local = transform.InverseTransformDirection(worldDirection.normalized);
            localDirection = new Vector2(local.x, local.z).normalized;
        }

        return new PlayerMovementDirectionSnapshot(
            clampedInput,
            worldDirection,
            localDirection,
            hasDirection);
    }

    private void UpdateVerticalMotion()
    {
        if (characterController == null || !characterController.enabled)
        {
            IsGrounded = false;
            return;
        }

        if (IsGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedVerticalSpeed;
        }
        else
        {
            verticalVelocity = Mathf.Max(
                verticalVelocity + gravity * Time.deltaTime,
                -maximumFallSpeed);
        }

        CollisionFlags collisionFlags = characterController.Move(
            Vector3.up * (verticalVelocity * Time.deltaTime));
        bool hitGround = (collisionFlags & CollisionFlags.Below) != 0 ||
                         characterController.isGrounded;

        if (hitGround && verticalVelocity <= 0f)
        {
            IsGrounded = true;
            verticalVelocity = groundedVerticalSpeed;
        }
        else
        {
            IsGrounded = false;
        }

        if ((collisionFlags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
        {
            verticalVelocity = 0f;
        }
    }

    public void SetRotationMode(PlayerRotationMode mode)
    {
        if (mode != rotationMode) ResetMatchedTurn();
        rotationMode = mode;
    }

    public void SetRootMotionTranslationScale(float scale)
    {
        rootMotionApplier?.SetRootMotionTranslationScale(scale);
    }

    public void BeginFastMovement()
    {
        isFastMovementActive = true;
        smoothedAnimatorMoveAmount = FastRunGaitSample;
        animator?.SetFloat(MoveSpeedHash, FastRunGaitSample);
    }

    public void EndFastMovement()
    {
        isFastMovementActive = false;
        if (!matchedTurnActive) turn180RootMotionHandler?.SetTurn180RootMotionActive(false);
        smoothedAnimatorMoveAmount = Mathf.Min(
            smoothedAnimatorMoveAmount,
            RunGaitSample);
    }

    public void FaceWorldDirectionImmediately(Vector3 worldDirection)
    {
        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude <= MoveInputThreshold * MoveInputThreshold)
        {
            return;
        }

        ResetMatchedTurn();
        transform.rotation = Quaternion.LookRotation(worldDirection.normalized, Vector3.up);
    }

    private bool UpdateTurn180()
    {
        if (rotationMode != PlayerRotationMode.MovementDirection) return false;
        if (matchedTurnActive) return true;
        if (matchTurn180ToInput && turnRecoveryRemaining > 0f) return false;
        if (animator == null || (turn180RequiresFastMovement && !isFastMovementActive))
        {
            return false;
        }

        if (IsTurn180AnimationActive())
        {
            // The recovery pose may keep playing after steering is released.
            // Do not restart another turn while this state is still active.
            return !matchTurn180ToInput;
        }

        if (worldMoveDirection.sqrMagnitude < MoveInputThreshold * MoveInputThreshold)
        {
            return false;
        }

        float angle = Vector3.Angle(transform.forward, worldMoveDirection);
        // Signed directions reconstructed from input can round just below the
        // threshold on one side (for example -150 becomes 149.99998).
        if (angle + 0.01f < turn180Threshold)
        {
            return false;
        }

        float signedAngle = Vector3.SignedAngle(
            transform.forward,
            worldMoveDirection,
            Vector3.up);
        float directionSign = signedAngle < 0f ? -1f : 1f;
        animator.SetFloat(TurnDirectionHash, directionSign);
        int turnStateHash = isCombatMovement
            ? CombatTurn180StateHash
            : Turn180StateHash;
        if (!PlayerAnimatorTransition.TryCrossFade(animator, 0, turnStateHash, 0.08f))
            return false;
        if (matchTurn180ToInput)
        {
            matchedTurnStartRotation = transform.rotation;
            // A single Generic clip cannot safely mirror its skeleton. Keep its
            // authored side and adjust only the amount (150..210 degrees).
            float authoredSign = authoredTurn180Angle < 0f ? -1f : 1f;
            matchedTurnAngle = signedAngle;
            if (matchedTurnAngle * authoredSign < 0f) matchedTurnAngle += authoredSign * 360f;
            matchedTurnActive = true;
            matchedTurnStateSeen = false;
            matchedTurnRequestFrame = Time.frameCount;
            angularVelocity = 0f;
        }
        return true;
    }

    private Quaternion ProcessRootRotation(Quaternion animationDeltaRotation)
    {
        return ResolveRotation(animationDeltaRotation);
    }

    private Quaternion ResolveRotation(Quaternion animationDeltaRotation)
    {
        if (rotationMode == PlayerRotationMode.Animation)
        {
            return animationDeltaRotation;
        }

        if (rotationMode == PlayerRotationMode.Preserve)
        {
            return Quaternion.identity;
        }

        if (matchTurn180ToInput && matchedTurnActive)
            return CalculateMatchedTurnRotation();
        return CalculateMovementRotation();
    }

    private Quaternion CalculateMatchedTurnRotation()
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (animator.IsInTransition(0))
        {
            var next = animator.GetNextAnimatorStateInfo(0);
            if (next.IsTag("LocomotionTurn180")) state = next;
        }
        if (state.IsTag("LocomotionTurn180"))
        {
            matchedTurnStateSeen = true;
            float progress = Mathf.Clamp01(turn180YawProgress.Evaluate(Mathf.Clamp01(state.normalizedTime)));
            bool pivotComplete = progress >= 0.999f;
            if (pivotComplete) progress = 1f;
            Quaternion desired = Quaternion.AngleAxis(matchedTurnAngle * progress, Vector3.up) *
                                 matchedTurnStartRotation;
            if (pivotComplete) ReleaseMatchedTurn();
            return Quaternion.Inverse(transform.rotation) * desired;
        }
        if (!matchedTurnStateSeen && Time.frameCount <= matchedTurnRequestFrame + 2)
            return Quaternion.identity;

        // Preserve the pose reached by the animation. New input takes ownership
        // gradually; there is no final LookRotation/instant correction.
        ReleaseMatchedTurn();
        return Quaternion.identity;
    }

    private void ReleaseMatchedTurn()
    {
        matchedTurnActive = false;
        angularVelocity = 0f;
        turnRecoveryRemaining = turn180RecoveryTime;
    }

    private void ResetMatchedTurn()
    {
        matchedTurnActive = matchedTurnStateSeen = false;
        turnRecoveryRemaining = 0f;
        angularVelocity = 0f;
    }

    private bool IsTurn180AnimationActive()
    {
        if (animator == null)
        {
            return false;
        }

        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(0);
            if (nextState.IsTag("LocomotionTurn180"))
            {
                return true;
            }
        }

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        return currentState.IsTag("LocomotionTurn180");
    }

    private void UpdateMoveDirection()
    {
        worldMoveDirection = ResolveWorldMoveDirection(moveInput);
    }

    private Vector3 ResolveWorldMoveDirection(Vector2 input)
    {
        if (cameraTransform == null)
        {
            return new Vector3(input.x, 0f, input.y);
        }

        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;
        cameraForward.y = 0f;
        cameraRight.y = 0f;
        cameraForward.Normalize();

        return Vector3.ClampMagnitude(cameraForward * input.y + cameraRight * input.x, 1f);
    }
    private Quaternion CalculateMovementRotation()
    {
        float recoveryWeight = 1f;
        if (turnRecoveryRemaining > 0f)
        {
            turnRecoveryRemaining = Mathf.Max(0f, turnRecoveryRemaining - Time.deltaTime);
            recoveryWeight = Mathf.SmoothStep(0f, 1f, 1f - turnRecoveryRemaining / turn180RecoveryTime);
        }
        if (worldMoveDirection.sqrMagnitude < 0.001f)
        {
            angularVelocity = 0f;
            return Quaternion.identity;
        }

        Quaternion currentRotation = transform.rotation;
        Quaternion targetRotation = Quaternion.LookRotation(worldMoveDirection);
        float angleToTarget = Quaternion.Angle(currentRotation, targetRotation);
        float normalizedAngle = Mathf.Clamp01(angleToTarget / 180f);
        float speedMultiplier = rotationSpeedByAngle != null
            ? Mathf.Max(0f, rotationSpeedByAngle.Evaluate(normalizedAngle))
            : 1f;
        float angularSpeed = IsTurn180AnimationActive()
            ? rotationSpeed
            : rotationSpeed * speedMultiplier;
        if (rotationSmoothTime > 0f)
        {
            float signedAngle = Vector3.SignedAngle(transform.forward, worldMoveDirection, Vector3.up);
            float maximumSpeed = rotationSpeed * Mathf.Clamp01(speedMultiplier) * recoveryWeight;
            float step = Mathf.SmoothDampAngle(0f, signedAngle, ref angularVelocity,
                rotationSmoothTime, maximumSpeed, Time.deltaTime);
            step = Mathf.Clamp(step, -maximumSpeed * Time.deltaTime, maximumSpeed * Time.deltaTime);
            angularVelocity = Mathf.Clamp(angularVelocity, -maximumSpeed, maximumSpeed);
            return Quaternion.AngleAxis(step, Vector3.up);
        }

        Quaternion nextRotation = Quaternion.RotateTowards(
            currentRotation,
            targetRotation,
            angularSpeed * Time.deltaTime);
        return Quaternion.Inverse(currentRotation) * nextRotation;
    }

    private static Vector2 GetTargetAnimatorDirection()
    {
        // Until target lock-on is implemented, both locomotion modes face the
        // movement direction and only sample their authored forward clips.
        return Vector2.up;
    }

    private void UpdateAnimatorParameters(bool isCombatMovement, bool hasMoveInput)
    {
        if (animator == null)
        {
            return;
        }

        bool isMoving = hasMoveInput;
        float inputMagnitude = moveInput.magnitude;
        float targetMoveSpeed = isMoving
            ? isFastMovementActive
                ? FastRunGaitSample
                : Mathf.Lerp(WalkGaitSample, RunGaitSample, inputMagnitude)
            : 0f;
        Vector2 targetDirection = GetTargetAnimatorDirection();
        if (targetDirection.sqrMagnitude <= MoveInputThreshold * MoveInputThreshold)
        {
            targetDirection = lastNonZeroLocalDirection;
        }

        targetDirection.Normalize();

        float lerpAmount = 1f - Mathf.Exp(-animatorParameterSmoothSpeed * Time.deltaTime);
        if (isMoving)
        {
            smoothedAnimatorMoveAmount = Mathf.Lerp(
                smoothedAnimatorMoveAmount,
                targetMoveSpeed,
                lerpAmount);

            if (!wasMoving)
            {
                Vector2 startDirection = QuantizeEightWayDirection(targetDirection);
                animator.SetFloat(StartXHash, startDirection.x);
                animator.SetFloat(StartYHash, startDirection.y);
            }

            smoothedAnimatorMoveInput = Vector2.Lerp(
                smoothedAnimatorMoveInput,
                targetDirection * inputMagnitude,
                lerpAmount);

            lastNonZeroLocalDirection = smoothedAnimatorMoveInput.sqrMagnitude > MoveInputThreshold * MoveInputThreshold
                ? smoothedAnimatorMoveInput.normalized
                : targetDirection;
        }
        else
        {
            if (wasMoving)
            {
                Vector2 stopDirection = QuantizeEightWayDirection(lastNonZeroLocalDirection);
                animator.SetFloat(StopXHash, stopDirection.x);
                animator.SetFloat(StopYHash, stopDirection.y);
                // Keep the stopping gait fixed while MoveSpeed decays toward idle.
                // Older controllers do not expose this optional parameter.
                foreach (AnimatorControllerParameter parameter in animator.parameters)
                {
                    if (parameter.nameHash == StopSpeedHash &&
                        parameter.type == AnimatorControllerParameterType.Float)
                    {
                        animator.SetFloat(StopSpeedHash, smoothedAnimatorMoveAmount >= 1.5f
                            ? FastRunGaitSample
                            : Mathf.Clamp(smoothedAnimatorMoveAmount, WalkGaitSample, RunGaitSample));
                        break;
                    }
                }
            }

            smoothedAnimatorMoveAmount = Mathf.Lerp(
                smoothedAnimatorMoveAmount,
                0f,
                lerpAmount);
            if (smoothedAnimatorMoveAmount <= MoveInputThreshold)
            {
                smoothedAnimatorMoveAmount = 0f;
            }
        }

        animator.SetBool(IsMovingHash, isMoving);
        animator.SetFloat(MoveXHash, smoothedAnimatorMoveInput.x);
        animator.SetFloat(MoveYHash, smoothedAnimatorMoveInput.y);
        animator.SetFloat(MoveSpeedHash, smoothedAnimatorMoveAmount);
        wasMoving = isMoving;
    }

    public static Vector2 QuantizeEightWayDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= MoveInputThreshold * MoveInputThreshold)
        {
            return Vector2.up;
        }

        direction.Normalize();
        float angle = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
        int sector = Mathf.RoundToInt(angle / DirectionStepDegrees);
        sector = (sector % 8 + 8) % 8;

        switch (sector)
        {
            case 0: return new Vector2(0f, 1f);
            case 1: return new Vector2(0.707107f, 0.707107f);
            case 2: return new Vector2(1f, 0f);
            case 3: return new Vector2(0.707107f, -0.707107f);
            case 4: return new Vector2(0f, -1f);
            case 5: return new Vector2(-0.707107f, -0.707107f);
            case 6: return new Vector2(-1f, 0f);
            default: return new Vector2(-0.707107f, 0.707107f);
        }
    }

    private Vector2 GetNormalizedLocalMoveDirection()
    {
        if (worldMoveDirection.sqrMagnitude <= MoveInputThreshold * MoveInputThreshold)
        {
            return lastNonZeroLocalDirection;
        }

        Vector3 localDirection = transform.InverseTransformDirection(worldMoveDirection.normalized);
        return new Vector2(localDirection.x, localDirection.z).normalized;
    }

}
