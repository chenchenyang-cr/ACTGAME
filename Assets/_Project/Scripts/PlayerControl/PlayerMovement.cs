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
    [SerializeField, Min(0.1f)]
    private float jumpHeight = 1.5f;
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
        turn180RootMotionHandler?.SetTurn180RootMotionActive(false);
        rootMotionApplier?.SetRootRotationProcessor(null);
        rootMotionApplier?.SetRootMotionTranslationScale(1f);
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
        bool isUsingTurn180RootMotion = UpdateTurn180();
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
        turn180RootMotionHandler?.SetTurn180RootMotionActive(false);
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

        transform.rotation = Quaternion.LookRotation(worldDirection.normalized, Vector3.up);
    }

    private bool UpdateTurn180()
    {
        if (animator == null || !isFastMovementActive)
        {
            return false;
        }

        if (IsTurn180AnimationActive())
        {
            return true;
        }

        if (worldMoveDirection.sqrMagnitude < MoveInputThreshold * MoveInputThreshold)
        {
            return false;
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
        PlayerAnimatorTransition.TryCrossFade(animator, 0, Turn180StateHash, 0.08f);
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

        return CalculateMovementRotation();
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
        if (worldMoveDirection.sqrMagnitude < 0.001f)
        {
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
