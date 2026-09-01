using UnityEngine;
using UnityEngine.AI;

namespace UnityLearning.EnemySystem
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class EnemyMotor : MonoBehaviour
    {
        [SerializeField] private NavMeshAgent navMeshAgent;
        [SerializeField] private Animator animator;
        [SerializeField, Min(0f)] private float rotationSpeed = 540f;
        [SerializeField, Min(0.01f)] private float arrivalTolerance = 0.35f;
        [SerializeField, Min(0f)] private float animatorDampTime = 0.12f;

        private int speedHash;
        private bool hasSpeedParameter;

        public bool HasValidPath => navMeshAgent != null && navMeshAgent.isOnNavMesh &&
                                    navMeshAgent.pathStatus != NavMeshPathStatus.PathInvalid;
        public bool IsMoving => navMeshAgent != null && navMeshAgent.enabled &&
                                navMeshAgent.desiredVelocity.sqrMagnitude > 0.01f;
        public Vector3 Velocity => navMeshAgent != null
            ? navMeshAgent.desiredVelocity
            : Vector3.zero;

        private void Awake()
        {
            if (navMeshAgent == null) navMeshAgent = GetComponent<NavMeshAgent>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (navMeshAgent != null)
            {
                navMeshAgent.updatePosition = false;
                navMeshAgent.updateRotation = false;
            }
        }

        public void Configure(EnemyConfig config)
        {
            if (config == null)
                return;

            rotationSpeed = config.RotationSpeed;
            arrivalTolerance = config.ArrivalTolerance;
            if (navMeshAgent != null) navMeshAgent.speed = config.ChaseSpeed;

            if (string.IsNullOrWhiteSpace(config.MoveSpeedParameter))
            {
                hasSpeedParameter = false;
                return;
            }

            speedHash = Animator.StringToHash(config.MoveSpeedParameter);
            hasSpeedParameter = true;
        }

        public void SetSpeed(float speed)
        {
            if (navMeshAgent != null) navMeshAgent.speed = Mathf.Max(0f, speed);
        }

        private void Update()
        {
            if (hasSpeedParameter && animator != null && navMeshAgent != null)
            {
                float normalizedSpeed = navMeshAgent.speed > 0.01f? 
                navMeshAgent.desiredVelocity.magnitude / navMeshAgent.speed : 0f;
                animator.SetFloat(speedHash, normalizedSpeed, animatorDampTime, Time.deltaTime);
            }
        }

        public bool MoveTo(Vector3 destination)
        {
            if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
                return false;

            navMeshAgent.isStopped = false;
            return navMeshAgent.SetDestination(destination);
        }

        public void Stop()
        {
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
    }
}
