using UnityEngine;
using UnityEngine.AI;

namespace UnityLearning.EnemySystem
{
    [DefaultExecutionOrder(11000)]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class EnemyMotor : MonoBehaviour
    {
        [SerializeField] private NavMeshAgent navMeshAgent;
        [SerializeField] private Animator animator;
        [SerializeField, Min(0f)] private float rotationSpeed = 540f;
        [SerializeField, Min(0.01f)] private float arrivalTolerance = 0.35f;
        [Tooltip("Animator 参数自身的数值阻尼。方向转向主要由下方的最大方向变化速度控制。")]
        [SerializeField, Min(0f)] private float animatorDampTime = 0.12f;
        [Tooltip("移动过程中每秒允许改变的最大方向角度，防止 NavMesh 拐点让八方向动画瞬间跳变。")]
        [SerializeField, Min(1f)] private float maximumDirectionChangeSpeed = 360f;
        [Tooltip("小于该角度的寻路方向波动会被忽略，避免 MoveX/MoveY 在融合树中抖动。")]
        [SerializeField, Range(0f, 30f)] private float directionDeadZone = 4f;
        [SerializeField, Min(0.1f)] private float destinationProjectionDistance = 2f;

        private int speedHash;
        private int moveXHash;
        private int moveYHash;
        private int isMovingHash;
        private int startXHash;
        private int startYHash;
        private int stopXHash;
        private int stopYHash;
        private bool hasSpeedParameter;
        private bool hasDirectionalParameters;
        private bool hasIsMovingParameter;
        private bool hasStartParameters;
        private bool hasStopParameters;
        private bool wasMoving;
        private bool warnedOffNavMesh;
        private bool hasMovementRequest;
        private Vector3 requestedDestination;
        private Vector3 currentMovementVelocity;
        private Vector3 stabilizedMovementDirection;
        private bool hasStabilizedMovementDirection;
        private Vector2 lastNonZeroDirection = Vector2.up;
        private Vector3 hitRecoilDirection;
        private AnimationCurve hitRecoilDecayCurve;
        private float hitRecoilDuration;
        private float hitRecoilSpeed;
        private float hitRecoilElapsed;
        private bool hitRecoilActive;

        public bool HasValidPath => navMeshAgent != null && navMeshAgent.isOnNavMesh &&
                                    navMeshAgent.pathStatus != NavMeshPathStatus.PathInvalid;
        public bool IsMoving => currentMovementVelocity.sqrMagnitude > 0.01f;
        public Vector3 Velocity => currentMovementVelocity;
        public bool HasMovementRequest => hasMovementRequest;
        public bool IsOnNavMesh => navMeshAgent != null && navMeshAgent.isOnNavMesh;
        public bool IsNavigationStopped => navMeshAgent == null || navMeshAgent.isStopped;
        public NavMeshPathStatus PathStatus => navMeshAgent != null
            ? navMeshAgent.pathStatus
            : NavMeshPathStatus.PathInvalid;

        private void Awake()
        {
            ResolveDependencies();
            if (navMeshAgent != null)
            {
                navMeshAgent.updatePosition = false;
                navMeshAgent.updateRotation = false;
            }
        }

        private void Start()
        {
            TryPlaceOnNavMesh(2f);
        }

        public void Configure(EnemyConfig config)
        {
            if (config == null)
                return;

            ResolveDependencies();

            rotationSpeed = config.RotationSpeed;
            arrivalTolerance = config.ArrivalTolerance;

            speedHash = Animator.StringToHash(config.MoveSpeedParameter);
            hasSpeedParameter = HasAnimatorParameter(
                config.MoveSpeedParameter,
                AnimatorControllerParameterType.Float);
            moveXHash = Animator.StringToHash(config.MoveXParameter);
            moveYHash = Animator.StringToHash(config.MoveYParameter);
            hasDirectionalParameters = HasAnimatorParameter(
                                           config.MoveXParameter,
                                           AnimatorControllerParameterType.Float) &&
                                       HasAnimatorParameter(
                                           config.MoveYParameter,
                                           AnimatorControllerParameterType.Float);
            isMovingHash = Animator.StringToHash(config.IsMovingParameter);
            hasIsMovingParameter = HasAnimatorParameter(
                config.IsMovingParameter,
                AnimatorControllerParameterType.Bool);
            startXHash = Animator.StringToHash(config.StartXParameter);
            startYHash = Animator.StringToHash(config.StartYParameter);
            hasStartParameters = HasAnimatorParameter(
                                     config.StartXParameter,
                                     AnimatorControllerParameterType.Float) &&
                                 HasAnimatorParameter(
                                     config.StartYParameter,
                                     AnimatorControllerParameterType.Float);
            stopXHash = Animator.StringToHash(config.StopXParameter);
            stopYHash = Animator.StringToHash(config.StopYParameter);
            hasStopParameters = HasAnimatorParameter(
                                    config.StopXParameter,
                                    AnimatorControllerParameterType.Float) &&
                                HasAnimatorParameter(
                                    config.StopYParameter,
                                    AnimatorControllerParameterType.Float);
        }

        private void Update()
        {
            if (animator == null || navMeshAgent == null)
                return;

            Vector3 desiredVelocity = StabilizeMovementIntent(
                CalculateMovementIntent(),
                Time.deltaTime);
            currentMovementVelocity = desiredVelocity;
            desiredVelocity.y = 0f;
            float normalizedSpeed = Mathf.Clamp01(desiredVelocity.magnitude);
            bool moving = normalizedSpeed > 0.02f;
            Vector3 localDirection3D = desiredVelocity.sqrMagnitude > 0.0001f
                ? transform.InverseTransformDirection(desiredVelocity.normalized)
                : Vector3.zero;
            Vector2 localDirection = new Vector2(localDirection3D.x, localDirection3D.z);
            if (moving && localDirection.sqrMagnitude > 0.0001f)
            {
                localDirection.Normalize();
                lastNonZeroDirection = localDirection;
                if (!wasMoving && hasStartParameters)
                    SetDiscreteDirection(startXHash, startYHash, localDirection);
            }
            else if (!moving && wasMoving && hasStopParameters)
            {
                SetDiscreteDirection(stopXHash, stopYHash, lastNonZeroDirection);
            }

            if (hasSpeedParameter)
                animator.SetFloat(speedHash, normalizedSpeed, animatorDampTime, Time.deltaTime);

            if (hasDirectionalParameters)
            {
                animator.SetFloat(
                    moveXHash,
                    localDirection.x * normalizedSpeed,
                    animatorDampTime,
                    Time.deltaTime);
                animator.SetFloat(
                    moveYHash,
                    localDirection.y * normalizedSpeed,
                    animatorDampTime,
                    Time.deltaTime);
            }

            if (hasIsMovingParameter)
                animator.SetBool(isMovingHash, moving);
            wasMoving = moving;
        }

        private void LateUpdate()
        {
            if (!hitRecoilActive)
                return;

            float normalizedTime = Mathf.Clamp01(hitRecoilElapsed / hitRecoilDuration);
            float decay = hitRecoilDecayCurve != null &&
                          hitRecoilDecayCurve.length > 0
                ? Mathf.Max(0f, hitRecoilDecayCurve.Evaluate(normalizedTime))
                : 1f - normalizedTime;
            ApplyExternalDisplacement(hitRecoilDirection *
                                      (hitRecoilSpeed * decay * Time.deltaTime));

            hitRecoilElapsed += Time.deltaTime;
            if (hitRecoilElapsed >= hitRecoilDuration)
                hitRecoilActive = false;
        }

        public void PlayHitRecoil(Vector3 direction, float duration, float speed,
            AnimationCurve decayCurve)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f || duration <= 0f || speed <= 0f)
            {
                hitRecoilActive = false;
                return;
            }

            hitRecoilDirection = direction.normalized;
            hitRecoilDuration = duration;
            hitRecoilSpeed = speed;
            hitRecoilDecayCurve = decayCurve;
            hitRecoilElapsed = 0f;
            hitRecoilActive = true;
        }

        private void ApplyExternalDisplacement(Vector3 displacement)
        {
            if (displacement.sqrMagnitude <= 0f)
                return;

            transform.position += displacement;
            if (navMeshAgent != null && navMeshAgent.enabled &&
                navMeshAgent.isOnNavMesh)
                navMeshAgent.nextPosition = transform.position;
        }

        public bool MoveTo(Vector3 destination)
        {
            if (navMeshAgent == null || !navMeshAgent.enabled)
            {
                hasMovementRequest = false;
                return false;
            }

            if (!navMeshAgent.isOnNavMesh && !TryPlaceOnNavMesh(2f))
            {
                if (!warnedOffNavMesh)
                {
                    Debug.LogWarning(
                        $"{name} 不在 NavMesh 上，无法移动。请检查敌人出生点和场景 NavMesh 烘焙范围。",
                        this);
                    warnedOffNavMesh = true;
                }
                hasMovementRequest = false;
                return false;
            }

            warnedOffNavMesh = false;
            if (!NavMesh.SamplePosition(
                    destination,
                    out NavMeshHit destinationHit,
                    destinationProjectionDistance,
                    navMeshAgent.areaMask))
            {
                hasMovementRequest = false;
                return false;
            }
            destination = destinationHit.position;

            navMeshAgent.isStopped = false;
            hasMovementRequest = navMeshAgent.SetDestination(destination);
            if (hasMovementRequest)
                requestedDestination = destination;
            return hasMovementRequest;
        }

        public bool TryPlaceOnNavMesh(float sampleDistance)
        {
            if (navMeshAgent == null || !navMeshAgent.enabled)
                return false;
            if (navMeshAgent.isOnNavMesh)
                return true;
            if (!NavMesh.SamplePosition(
                    transform.position,
                    out NavMeshHit hit,
                    Mathf.Max(0.1f, sampleDistance),
                    navMeshAgent.areaMask))
                return false;

            bool placed = navMeshAgent.Warp(hit.position);
            if (placed)
                transform.position = hit.position;
            return placed;
        }

        public void Stop()
        {
            hasMovementRequest = false;
            currentMovementVelocity = Vector3.zero;
            stabilizedMovementDirection = Vector3.zero;
            hasStabilizedMovementDirection = false;
            if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
                return;

            navMeshAgent.isStopped = true;
            navMeshAgent.ResetPath();
        }

        public bool HasReached(Vector3 destination, float extraTolerance = 0f)
        {
            float tolerance = Mathf.Max(arrivalTolerance, extraTolerance);
            Vector3 flatDelta = destination - transform.position;
            flatDelta.y = 0f;
            return flatDelta.sqrMagnitude <= tolerance * tolerance;
        }

        public void FaceTarget(Vector3 targetPosition, float deltaTime)
        {
            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f;
            FaceDirection(direction, deltaTime);
        }

        public void FaceMovement(float deltaTime)
        {
            Vector3 direction = Velocity;
            direction.y = 0f;
            FaceDirection(direction, deltaTime);
        }

        public bool IsFacing(Vector3 targetPosition, float tolerance)
        {
            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f;
            return direction.sqrMagnitude <= 0.0001f ||
                   Vector3.Angle(transform.forward, direction) <= tolerance;
        }

        public void SetNavigationEnabled(bool value)
        {
            if (navMeshAgent == null || navMeshAgent.enabled == value)
                return;

            navMeshAgent.enabled = value;
            if (!value)
            {
                hasMovementRequest = false;
                currentMovementVelocity = Vector3.zero;
                stabilizedMovementDirection = Vector3.zero;
                hasStabilizedMovementDirection = false;
            }
            if (value)
            {
                navMeshAgent.updatePosition = false;
                navMeshAgent.updateRotation = false;
            }
        }

        private void FaceDirection(Vector3 direction, float deltaTime)
        {
            if (direction.sqrMagnitude <= 0.0001f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed * deltaTime);
        }

        private Vector3 CalculateMovementIntent()
        {
            if (!hasMovementRequest || navMeshAgent == null ||
                !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
                return Vector3.zero;

            Vector3 steeringPoint = navMeshAgent.hasPath && !navMeshAgent.pathPending
                ? navMeshAgent.steeringTarget
                : requestedDestination;
            Vector3 toSteering = steeringPoint - transform.position;
            toSteering.y = 0f;

            if (toSteering.sqrMagnitude <= arrivalTolerance * arrivalTolerance)
            {
                toSteering = requestedDestination - transform.position;
                toSteering.y = 0f;
            }

            return toSteering.sqrMagnitude > arrivalTolerance * arrivalTolerance
                ? toSteering.normalized
                : Vector3.zero;
        }

        private Vector3 StabilizeMovementIntent(Vector3 desiredVelocity, float deltaTime)
        {
            desiredVelocity.y = 0f;
            float speed = desiredVelocity.magnitude;
            if (speed <= 0.0001f)
                return Vector3.zero;

            Vector3 targetDirection = desiredVelocity / speed;
            if (!wasMoving || !hasStabilizedMovementDirection)
            {
                stabilizedMovementDirection = targetDirection;
                hasStabilizedMovementDirection = true;
                return stabilizedMovementDirection * speed;
            }

            float angle = Vector3.Angle(stabilizedMovementDirection, targetDirection);
            if (angle > directionDeadZone)
            {
                float maximumRadians = maximumDirectionChangeSpeed *
                                       Mathf.Deg2Rad * Mathf.Max(0f, deltaTime);
                stabilizedMovementDirection = Vector3.RotateTowards(
                    stabilizedMovementDirection,
                    targetDirection,
                    maximumRadians,
                    0f).normalized;
            }

            return stabilizedMovementDirection * speed;
        }

        private bool HasAnimatorParameter(
            string parameterName,
            AnimatorControllerParameterType parameterType)
        {
            if (animator == null || string.IsNullOrWhiteSpace(parameterName))
                return false;

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].type == parameterType &&
                    parameters[i].name == parameterName)
                    return true;
            }
            return false;
        }

        private void ResolveDependencies()
        {
            if (navMeshAgent == null)
                navMeshAgent = GetComponent<NavMeshAgent>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);
        }

        private void SetDiscreteDirection(int xHash, int yHash, Vector2 direction)
        {
            float angle = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            float snappedAngle = Mathf.Round(angle / 45f) * 45f * Mathf.Deg2Rad;
            animator.SetFloat(xHash, Mathf.Sin(snappedAngle));
            animator.SetFloat(yHash, Mathf.Cos(snappedAngle));
        }
    }
}
