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
    [SerializeField, Min(0.1f)]
    private float jumpHeight = 1.5f;
    [SerializeField, Min(1f)]
    private float maximumFallSpeed = 35f;
    [Header("Rotation")]
    [SerializeField]
    private float rotationSpeed = 720;
    [SerializeField, Range(90f, 180f)]
    private float turn180Threshold = 135f;
    [SerializeField, Range(0f, 1f)]
    private float turn180CorrectionStartNormalizedTime = 0.45f;
    [SerializeField, Range(0f, 1f)]
    private float turn180CorrectionEndNormalizedTime = 0.75f;
    [SerializeField, Range(0.1f, 45f)]
    private float turn180RetargetThreshold = 5f;
    [SerializeField, Min(1f)]
    private float turn180SettlementRotationSpeed = 360f;
    [SerializeField]
    private AnimationCurve turn180RotationBlend = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
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
    private static readonly int CombatWeightHash = Animator.StringToHash("CombatWeight");
    private static readonly int TurnDirectionHash = Animator.StringToHash("TurnDirection");
    private static readonly int Turn180StateHash =
        Animator.StringToHash("Base Layer.NormalLocomotion.Turn180");
    private const float MoveInputThreshold = 0.01f;
    private const float CombatModeThreshold = 0.5f;
    private const float WalkGaitSample = 0.35f;
    private const float RunGaitSample = 1f;
    private const float DirectionStepDegrees = 45f;

    private Vector2 moveInput;
    private Vector2 smoothedAnimatorMoveInput;
    private float smoothedAnimatorMoveAmount;
    private Vector3 worldMoveDirection;
    private Vector2 lastNonZeroLocalDirection = Vector2.up;
    private bool wasMoving;
    private CombatEditor.RootMotionParentApplier rootMotionApplier;
    private bool isBlendingTurn180Rotation;
    private bool hasEnteredTurn180State;
    private Vector3 turn180TargetDirection;
    private float lastTurn180CorrectionWeight;
    private bool hasCompletedTurn180Correction;
    private bool isSettlingTurn180Rotation;
    private PlayerRotationMode rotationMode = PlayerRotationMode.MovementDirection;
    private bool isCombatMovement;
    private float verticalVelocity;

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
        rootMotionApplier?.SetRootRotationProcessor(null);

        isBlendingTurn180Rotation = false;
        isSettlingTurn180Rotation = false;
    }

    private void BindRootRotationProcessor()
    {
        rootMotionApplier?.SetRootRotationProcessor(ProcessRootRotation);
    }

    public void Tick(Vector2 input, bool hasMoveInput)
    {
        UpdateVerticalMotion();
        moveInput = Vector2.ClampMagnitude(input, 1f);
        UpdateMoveDirection();
        isCombatMovement = animator != null && animator.GetFloat(CombatWeightHash) >= CombatModeThreshold;
        UpdateTurn180(isCombatMovement);
        UpdateAnimatorParameters(isCombatMovement, hasMoveInput);
    }

    public bool TryJump()
    {
        if (!IsGrounded || gravity >= 0f)
        {
            return false;
        }

        verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        IsGrounded = false;
        return true;
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
        rotationMode = mode;
    }

    private bool UpdateTurn180(bool isCombatMovement)
    {
        if (animator == null)
        {
            return false;
        }

        if (isBlendingTurn180Rotation)
        {
            TryRetargetOrRestartTurn180();
            return true;
        }

        if (isSettlingTurn180Rotation)
        {
            UpdateTurn180SettlementTarget();
            return true;
        }

        if (isCombatMovement ||
            worldMoveDirection.sqrMagnitude < MoveInputThreshold * MoveInputThreshold)
        {
            return false;
        }

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        bool isInTurnState = currentState.IsTag("LocomotionTurn180") ||
                             (animator.IsInTransition(0) &&
                              animator.GetNextAnimatorStateInfo(0).IsTag("LocomotionTurn180"));
        if (isInTurnState)
        {
            return true;
        }

        float angle = Vector3.Angle(transform.forward, worldMoveDirection);
        if (angle < turn180Threshold)
        {
            return false;
        }

        float signedAngle = Vector3.SignedAngle(
            transform.forward,
            worldMoveDirection,
            Vector3.up);
        float directionSign = signedAngle < 0f ? -1f : 1f;
        animator.SetFloat(TurnDirectionHash, directionSign);
        BeginTurn180RotationBlend(worldMoveDirection.normalized);
        animator.CrossFadeInFixedTime(Turn180StateHash, 0.08f, 0, 0f);
        return true;
    }

    private void TryRetargetOrRestartTurn180()
    {
        if (worldMoveDirection.sqrMagnitude < MoveInputThreshold * MoveInputThreshold)
        {
            return;
        }

        Vector3 latestTargetDirection = worldMoveDirection.normalized;
        float targetChangeAngle = Vector3.Angle(
            turn180TargetDirection,
            latestTargetDirection);
        float angleFromCurrentFacing = Vector3.Angle(
            transform.forward,
            latestTargetDirection);

        if (targetChangeAngle >= turn180RetargetThreshold &&
            angleFromCurrentFacing >= turn180Threshold)
        {
            float signedAngle = Vector3.SignedAngle(
                transform.forward,
                latestTargetDirection,
                Vector3.up);
            animator.SetFloat(TurnDirectionHash, signedAngle < 0f ? -1f : 1f);
            BeginTurn180RotationBlend(latestTargetDirection);
            animator.CrossFadeInFixedTime(Turn180StateHash, 0.08f, 0, 0f);
            return;
        }

        if (targetChangeAngle >= turn180RetargetThreshold)
        {
            turn180TargetDirection = latestTargetDirection;
            if (hasCompletedTurn180Correction)
            {
                isSettlingTurn180Rotation = true;
            }
        }
    }

    private void UpdateTurn180SettlementTarget()
    {
        if (worldMoveDirection.sqrMagnitude < MoveInputThreshold * MoveInputThreshold)
        {
            return;
        }

        Vector3 latestTargetDirection = worldMoveDirection.normalized;
        float angleFromCurrentFacing = Vector3.Angle(
            transform.forward,
            latestTargetDirection);

        if (angleFromCurrentFacing >= turn180Threshold)
        {
            float signedAngle = Vector3.SignedAngle(
                transform.forward,
                latestTargetDirection,
                Vector3.up);
            animator.SetFloat(TurnDirectionHash, signedAngle < 0f ? -1f : 1f);
            BeginTurn180RotationBlend(latestTargetDirection);
            animator.CrossFadeInFixedTime(Turn180StateHash, 0.08f, 0, 0f);
            return;
        }

        turn180TargetDirection = latestTargetDirection;
    }

    private void BeginTurn180RotationBlend(Vector3 targetDirection)
    {
        isBlendingTurn180Rotation = true;
        hasEnteredTurn180State = false;
        turn180TargetDirection = targetDirection;
        lastTurn180CorrectionWeight = 0f;
        hasCompletedTurn180Correction = false;
        isSettlingTurn180Rotation = false;
    }

    private Quaternion ProcessRootRotation(Quaternion animationDeltaRotation)
    {
        if (isBlendingTurn180Rotation)
        {
            bool isStillInTurnState = TryGetTurn180NormalizedTime(out float normalizedTime);
            if (!hasEnteredTurn180State && !isStillInTurnState)
            {
                return Quaternion.identity;
            }

            if (!isStillInTurnState)
            {
                isBlendingTurn180Rotation = false;
                if (isSettlingTurn180Rotation)
                {
                    return ProcessTurn180Settlement();
                }
                return ResolveRotation(animationDeltaRotation);
            }

            hasEnteredTurn180State = true;
            if (!hasCompletedTurn180Correction)
            {
                return ProcessTurn180Rotation(animationDeltaRotation, normalizedTime);
            }

            if (isSettlingTurn180Rotation)
            {
                return ProcessTurn180Settlement();
            }
        }

        if (isSettlingTurn180Rotation)
        {
            return ProcessTurn180Settlement();
        }

        return ResolveRotation(animationDeltaRotation);
    }

    private Quaternion ProcessTurn180Settlement()
    {
        if (turn180TargetDirection.sqrMagnitude < MoveInputThreshold * MoveInputThreshold)
        {
            isSettlingTurn180Rotation = false;
            return Quaternion.identity;
        }

        Quaternion currentRotation = transform.rotation;
        Quaternion targetRotation = Quaternion.LookRotation(
            turn180TargetDirection,
            Vector3.up);
        Quaternion nextRotation = Quaternion.RotateTowards(
            currentRotation,
            targetRotation,
            turn180SettlementRotationSpeed * Time.deltaTime);

        if (Quaternion.Angle(nextRotation, targetRotation) <= 0.1f)
        {
            nextRotation = targetRotation;
            isSettlingTurn180Rotation = false;
        }

        return Quaternion.Inverse(currentRotation) * nextRotation;
    }

    private Quaternion ResolveRotation(Quaternion animationDeltaRotation)
    {
        if (rotationMode == PlayerRotationMode.Animation)
        {
            return animationDeltaRotation;
        }

        if (rotationMode == PlayerRotationMode.Preserve || isCombatMovement)
        {
            return Quaternion.identity;
        }

        return CalculateMovementRotation();
    }

    private Quaternion ProcessTurn180Rotation(
        Quaternion animationDeltaRotation,
        float normalizedTime)
    {
        if (normalizedTime < turn180CorrectionStartNormalizedTime)
        {
            return animationDeltaRotation;
        }

        float correctionEndTime = Mathf.Max(
            turn180CorrectionStartNormalizedTime + 0.01f,
            turn180CorrectionEndNormalizedTime);
        float correctionProgress = Mathf.InverseLerp(
            turn180CorrectionStartNormalizedTime,
            correctionEndTime,
            normalizedTime);
        float correctionWeight = turn180RotationBlend != null
            ? Mathf.Clamp01(turn180RotationBlend.Evaluate(correctionProgress))
            : correctionProgress;

        float remainingWeight = Mathf.Max(0.0001f, 1f - lastTurn180CorrectionWeight);
        float frameCorrectionWeight = Mathf.Clamp01(
            (correctionWeight - lastTurn180CorrectionWeight) / remainingWeight);
        lastTurn180CorrectionWeight = Mathf.Max(
            lastTurn180CorrectionWeight,
            correctionWeight);

        Quaternion predictedRotation = transform.rotation * animationDeltaRotation;
        Quaternion targetRotation = Quaternion.LookRotation(
            turn180TargetDirection,
            Vector3.up);
        Quaternion correctedRotation = Quaternion.Slerp(
            predictedRotation,
            targetRotation,
            frameCorrectionWeight);

        if (correctionProgress >= 0.999f)
        {
            correctedRotation = targetRotation;
            hasCompletedTurn180Correction = true;
        }

        return Quaternion.Inverse(transform.rotation) * correctedRotation;
    }

    private bool TryGetTurn180NormalizedTime(out float normalizedTime)
    {
        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(0);
            if (nextState.IsTag("LocomotionTurn180"))
            {
                normalizedTime = nextState.normalizedTime;
                return true;
            }
        }

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        if (currentState.IsTag("LocomotionTurn180"))
        {
            normalizedTime = currentState.normalizedTime;
            return true;
        }

        normalizedTime = 0f;
        return false;
    }

    private void UpdateMoveDirection()
    {
        if (cameraTransform == null)
        {
            worldMoveDirection = new Vector3(moveInput.x, 0f, moveInput.y);
            return;
        }
        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;
        cameraForward.y = 0f;
        cameraRight.y = 0f;
        cameraForward.Normalize();
       
       worldMoveDirection=Vector3.ClampMagnitude((cameraForward * moveInput.y + cameraRight * moveInput.x), 1f);
    }
    private Quaternion CalculateMovementRotation()
    {
        if (worldMoveDirection.sqrMagnitude < 0.001f)
        {
            return Quaternion.identity;
        }

        Quaternion currentRotation = transform.rotation;
        Quaternion targetRotation = Quaternion.LookRotation(worldMoveDirection);
        Quaternion nextRotation = Quaternion.RotateTowards(
            currentRotation,
            targetRotation,
            rotationSpeed * Time.deltaTime);
        return Quaternion.Inverse(currentRotation) * nextRotation;
    }

    private Vector2 GetTargetAnimatorDirection(bool isCombatMovement)
    {
        // Once normal locomotion rotates the character towards its movement,
        // the matching animation is always forward. Only combat locomotion
        // should select the authored side/back directional clips.
        if (!isCombatMovement)
        {
            return Vector2.up;
        }

        return GetNormalizedLocalMoveDirection();
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
            ? Mathf.Lerp(WalkGaitSample, RunGaitSample, inputMagnitude)
            : 0f;
        Vector2 targetDirection = GetTargetAnimatorDirection(isCombatMovement);
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
                Vector2 startDirection = QuantizePhaseDirection(targetDirection);
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
                Vector2 stopDirection = QuantizePhaseDirection(lastNonZeroLocalDirection);
                animator.SetFloat(StopXHash, stopDirection.x);
                animator.SetFloat(StopYHash, stopDirection.y);
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

    private static Vector2 QuantizePhaseDirection(Vector2 direction)
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
