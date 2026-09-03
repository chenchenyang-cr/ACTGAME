using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace UnityLearning.EnemySystem
{
    public readonly struct EnemyCombatAssignment
    {
        public EnemyCombatAssignment(
            int slotIndex,
            float desiredAngle,
            Vector3 radialDirection,
            Vector3 tangentDirection,
            float attackRadius,
            float confrontationMinRadius,
            float confrontationMaxRadius,
            float halfAngle)
        {
            SlotIndex = slotIndex;
            DesiredAngle = desiredAngle;
            RadialDirection = radialDirection;
            TangentDirection = tangentDirection;
            AttackRadius = attackRadius;
            ConfrontationMinRadius = confrontationMinRadius;
            ConfrontationMaxRadius = confrontationMaxRadius;
            HalfAngle = halfAngle;
        }

        public int SlotIndex { get; }
        public float DesiredAngle { get; }
        public Vector3 RadialDirection { get; }
        public Vector3 TangentDirection { get; }
        public float AttackRadius { get; }
        public float ConfrontationMinRadius { get; }
        public float ConfrontationMaxRadius { get; }
        public float HalfAngle { get; }
    }

    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class EncounterCombatDirector : MonoBehaviour
    {
        [Header("目标")]
        [Tooltip("所有敌人围绕和进攻的目标，通常指定玩家根节点。")]
        [SerializeField] private Transform target;
        [Tooltip("Target 为空时，Awake 是否自动查找带 Player 标签的对象。")]
        [SerializeField] private bool findPlayerTargetOnAwake = true;

        [Header("进攻令牌")]
        [Tooltip("同时允许进入真实攻击链的敌人数。设为 0 可关闭新攻击，便于测试受击和对峙。")]
        [SerializeField, Min(0)] private int maximumConcurrentAttackers = 0;

        [Header("对峙区域")]
        [Tooltip("玩家周围最少划分多少个稳定站位扇区。敌人数更多时会自动增加。")]
        [SerializeField, Min(1)] private int minimumSlotCount = 4;
        [Tooltip("靠近玩家的施压/进攻参考半径，必须小于外环半径。")]
        [SerializeField, Min(0.5f)] private float innerRingRadius = 2.7f;
        [Tooltip("等待、环绕和攻击后撤退的主要站位半径。")]
        [SerializeField, Min(0.5f)] private float outerRingRadius = 6.5f;
        [Tooltip("每个扇区从外环向内延伸的活动深度。敌人在这段环带内自由移动，不再追逐槽位中心点。")]
        [SerializeField, Min(0.2f)] private float confrontationRegionDepth = 2.8f;
        [Tooltip("扇区两侧预留的角度，防止相邻敌人在边界贴得太近。")]
        [SerializeField, Range(0f, 30f)] private float sectorBoundaryPadding = 5f;
        [Tooltip("距离自己的扇区边界小于该值时，视为已经进入对峙区域。")]
        [SerializeField, Min(0.05f)] private float slotArrivalTolerance = 0.3f;
        [Tooltip("计算出的站位不在 NavMesh 上时，向周围搜索可行走点的半径。")]
        [SerializeField, Min(0.1f)] private float navMeshSampleDistance = 1.5f;
        [Tooltip("生成空间目标时要求敌人与其他敌人/预留点保持的最小距离。")]
        [SerializeField, Min(0.1f)] private float minimumEnemySpacing = 0.8f;
        [Tooltip("整组扇区的角度偏移。首次分配时会锁定目标朝向，之后玩家转身不会重排槽位。")]
        [SerializeField] private float angularOffset = 30f;

        [Header("空间行为")]
        [Tooltip("敌人超出自己扇形活动区域后允许的容差；超过后重新寻找最近的区域边界。")]
        [SerializeField, Min(0.1f)] private float closeGapThreshold = 0.12f;
        [Tooltip("随机目标点与敌人当前位置至少保持的距离，避免选到脚下导致频繁启停。")]
        [SerializeField, Min(0.1f)] private float orbitMinimumTargetDistance = 0.8f;
        [Tooltip("随机目标点允许改变多少径向距离。0 表示保持当前半径、主要横移，1 表示可覆盖整个环带深度。")]
        [SerializeField, Range(0f, 1f)] private float orbitRadialFreedom = 0.35f;
        // 以下三组范围只控制对峙节奏；角色实际移动距离由当前动画的 Root Motion 决定。
        [Tooltip("敌人朝一个随机目标点移动的最短时间，单位为秒。")]
        [SerializeField, Min(0.05f)] private float orbitWalkDurationMin = 1f;
        [Tooltip("敌人朝一个随机目标点移动的最长时间，单位为秒。")]
        [SerializeField, Min(0.05f)] private float orbitWalkDurationMax = 2f;
        [Tooltip("抵达或放弃当前目标点后，进入 Stop→Idle 等待流程的概率。0 表示总是连续换点，1 表示每次都停顿。")]
        [SerializeField, Range(0f, 1f)] private float orbitWaitChance = 0.65f;
        [Tooltip("Stop 动画完整结束并稳定进入 Idle 后，额外原地观察的最短时间。")]
        [SerializeField, Min(0f)] private float orbitIdleDurationMin = 0.3f;
        [Tooltip("Stop 动画完整结束并稳定进入 Idle 后，额外原地观察的最长时间。")]
        [SerializeField, Min(0f)] private float orbitIdleDurationMax = 0.5f;
        [Tooltip("目标点不可用时，完整停稳后再次选点前等待的最短时间。")]
        [SerializeField, Min(0f)] private float orbitTargetRetryPauseMin = 0.18f;
        [Tooltip("目标点不可用时，完整停稳后再次选点前等待的最长时间。")]
        [SerializeField, Min(0f)] private float orbitTargetRetryPauseMax = 0.32f;
        [Tooltip("每次环绕停顿结束后，转为向内试探施压的概率。")]
        [SerializeField, Range(0f, 1f)] private float pressureChance = 0.3f;
        [Tooltip("每次停顿结束后，把下一个随机目标选在当前角度另一侧的概率。")]
        [SerializeField, Range(0f, 1f)] private float orbitReverseChance = 0.2f;
        [Tooltip("Pressure 状态从外环向内试探的距离。")]
        [SerializeField, Min(0.1f)] private float pressureStepDistance = 0.8f;
        [Tooltip("一次向内施压最多持续多久，单位为秒。")]
        [SerializeField, Min(0.1f)] private float pressureDuration = 0.9f;
        [Tooltip("持令牌敌人到玩家连线两侧的避让半宽；进入通道的其他敌人会让位。")]
        [SerializeField, Min(0.1f)] private float attackCorridorWidth = 0.9f;
        [Tooltip("Yield 状态一次侧向让位的目标距离。")]
        [SerializeField, Min(0.1f)] private float yieldDistance = 0.9f;
        [Tooltip("一次让位最多持续多久，单位为秒。")]
        [SerializeField, Min(0.1f)] private float yieldDuration = 0.7f;

        private readonly List<EnemyController> participants = new();
        private readonly List<EnemyController> tokenQueue = new();
        private readonly HashSet<EnemyController> tokenHolders = new();
        private readonly Dictionary<EnemyController, Vector3> reservedPositions = new();
        private readonly Dictionary<EnemyController, int> assignedSlots = new();
        private int cachedSlotCount = -1;
        private Vector3 slotReferenceDirection;
        private bool hasSlotReferenceDirection;

        public Transform Target => target;
        public int ActiveAttackers => tokenHolders.Count;
        public int MaximumConcurrentAttackers => maximumConcurrentAttackers;
        public int RegisteredEnemies => participants.Count;
        public float SlotArrivalTolerance => slotArrivalTolerance;
        public float CloseGapThreshold => closeGapThreshold;
        public float OrbitMinimumTargetDistance => orbitMinimumTargetDistance;
        public float OrbitRadialFreedom => orbitRadialFreedom;
        public float OrbitWalkDurationMin => Mathf.Min(
            orbitWalkDurationMin,
            orbitWalkDurationMax);
        public float OrbitWalkDurationMax => Mathf.Max(
            orbitWalkDurationMin,
            orbitWalkDurationMax);
        public float OrbitWaitChance => orbitWaitChance;
        public float OrbitIdleDurationMin => Mathf.Min(
            orbitIdleDurationMin,
            orbitIdleDurationMax);
        public float OrbitIdleDurationMax => Mathf.Max(
            orbitIdleDurationMin,
            orbitIdleDurationMax);
        public float OrbitTargetRetryPauseMin => Mathf.Min(
            orbitTargetRetryPauseMin,
            orbitTargetRetryPauseMax);
        public float OrbitTargetRetryPauseMax => Mathf.Max(
            orbitTargetRetryPauseMin,
            orbitTargetRetryPauseMax);
        public float PressureChance => pressureChance;
        public float OrbitReverseChance => orbitReverseChance;
        public float PressureStepDistance => pressureStepDistance;
        public float PressureDuration => pressureDuration;
        public float YieldDistance => yieldDistance;
        public float YieldDuration => yieldDuration;

        public void SetTarget(Transform newTarget)
        {
            if (target == newTarget)
                return;
            target = newTarget;
            ResetSlotAssignments();
        }

        private void Awake()
        {
            ResolveTarget();
        }

        private void LateUpdate()
        {
            CleanupInvalidEntries();
        }

        private void OnDisable()
        {
            tokenQueue.Clear();
            tokenHolders.Clear();
            reservedPositions.Clear();
            ResetSlotAssignments();
        }

        public void Register(EnemyController enemy)
        {
            if (enemy == null || participants.Contains(enemy))
                return;
            participants.Add(enemy);
        }

        public void Unregister(EnemyController enemy)
        {
            if (enemy == null)
                return;
            participants.Remove(enemy);
            tokenQueue.Remove(enemy);
            tokenHolders.Remove(enemy);
            reservedPositions.Remove(enemy);
            assignedSlots.Remove(enemy);
            if (GetActiveParticipantCount() == 0)
                ResetSlotAssignments();
        }

        public bool TryAcquireAttackToken(EnemyController enemy)
        {
            if (maximumConcurrentAttackers <= 0 || !IsEligible(enemy))
                return false;

            Register(enemy);
            if (tokenHolders.Contains(enemy))
                return true;

            CleanupInvalidEntries();
            if (!tokenQueue.Contains(enemy))
                tokenQueue.Add(enemy);

            if (tokenHolders.Count >= maximumConcurrentAttackers ||
                tokenQueue.Count == 0 || tokenQueue[0] != enemy)
                return false;

            tokenQueue.RemoveAt(0);
            tokenHolders.Add(enemy);
            reservedPositions.Remove(enemy);
            return true;
        }

        public void ReleaseAttackToken(EnemyController enemy)
        {
            if (enemy == null)
                return;
            tokenQueue.Remove(enemy);
            tokenHolders.Remove(enemy);
        }

        public bool HasAttackToken(EnemyController enemy)
        {
            return enemy != null && tokenHolders.Contains(enemy);
        }

        public bool TryGetCombatAssignment(
            EnemyController enemy,
            out EnemyCombatAssignment assignment)
        {
            assignment = default;
            if (!IsEligible(enemy) || !ResolveTarget())
                return false;

            Register(enemy);
            int activeCount = GetActiveParticipantCount();
            int slotCount = Mathf.Max(minimumSlotCount, activeCount);
            EnsureSlotAssignments(slotCount);
            int slotIndex = GetOrAssignNearestSlot(enemy, slotCount);
            if (slotIndex < 0)
                return false;
            float angle = angularOffset + 360f * slotIndex / slotCount;
            Vector3 referenceDirection = GetSlotReferenceDirection();
            Vector3 radial = Quaternion.AngleAxis(angle, Vector3.up) *
                             referenceDirection.normalized;
            Vector3 tangent = Vector3.Cross(Vector3.up, radial).normalized;
            float attackRadius = Mathf.Min(innerRingRadius, outerRingRadius - 0.1f);
            float regionMaxRadius = Mathf.Max(attackRadius + 0.2f, outerRingRadius);
            float regionMinRadius = Mathf.Max(
                attackRadius + 0.1f,
                regionMaxRadius - confrontationRegionDepth);
            float halfAngle = Mathf.Max(
                5f,
                180f / slotCount - sectorBoundaryPadding);

            assignment = new EnemyCombatAssignment(
                slotIndex,
                angle,
                radial,
                tangent,
                attackRadius,
                regionMinRadius,
                regionMaxRadius,
                halfAngle);
            return true;
        }

        public bool IsInsideConfrontationRegion(
            Vector3 position,
            in EnemyCombatAssignment assignment,
            float distanceTolerance = 0f)
        {
            if (!ResolveTarget())
                return false;

            Vector3 fromTarget = Flatten(position - target.position);
            float radius = fromTarget.magnitude;
            if (radius <= 0.001f ||
                radius < assignment.ConfrontationMinRadius - distanceTolerance ||
                radius > assignment.ConfrontationMaxRadius + distanceTolerance)
                return false;

            float angularTolerance = Mathf.Rad2Deg *
                                     Mathf.Max(0f, distanceTolerance) /
                                     Mathf.Max(0.1f, radius);
            float signedAngle = Vector3.SignedAngle(
                assignment.RadialDirection,
                fromTarget / radius,
                Vector3.up);
            return Mathf.Abs(signedAngle) <= assignment.HalfAngle + angularTolerance;
        }

        public bool TryGetNearestConfrontationPosition(
            EnemyController enemy,
            in EnemyCombatAssignment assignment,
            out Vector3 position)
        {
            position = enemy != null ? enemy.transform.position : target.position;
            if (!IsEligible(enemy) || !ResolveTarget())
                return false;

            Vector3 desired = ClampToConfrontationRegion(
                enemy.transform.position,
                assignment,
                0.12f,
                1.5f);
            return TryProjectSpatialPosition(enemy, desired, out position);
        }

        public bool TryGetConfrontationPosition(
            EnemyController enemy,
            in EnemyCombatAssignment assignment,
            float angleOffset,
            float radius,
            out Vector3 position)
        {
            position = enemy != null ? enemy.transform.position : target.position;
            if (!IsEligible(enemy) || !ResolveTarget())
                return false;

            float angleInset = Mathf.Min(1.5f, assignment.HalfAngle * 0.25f);
            float radialInset = Mathf.Min(
                0.12f,
                (assignment.ConfrontationMaxRadius -
                 assignment.ConfrontationMinRadius) * 0.25f);
            float clampedAngle = Mathf.Clamp(
                angleOffset,
                -assignment.HalfAngle + angleInset,
                assignment.HalfAngle - angleInset);
            float clampedRadius = Mathf.Clamp(
                radius,
                assignment.ConfrontationMinRadius + radialInset,
                assignment.ConfrontationMaxRadius - radialInset);
            Vector3 direction = Quaternion.AngleAxis(clampedAngle, Vector3.up) *
                                assignment.RadialDirection;
            Vector3 desired = target.position + direction * clampedRadius;
            if (!TryProjectSpatialPosition(enemy, desired, out position) ||
                !IsInsideConfrontationRegion(position, assignment, 0.05f))
            {
                ReleaseConfrontationReservation(enemy);
                position = enemy.transform.position;
                return false;
            }
            return true;
        }

        private Vector3 ClampToConfrontationRegion(
            Vector3 position,
            in EnemyCombatAssignment assignment,
            float radialInset,
            float angularInset)
        {
            Vector3 fromTarget = Flatten(position - target.position);
            float radius = fromTarget.magnitude;
            Vector3 direction = radius > 0.001f
                ? fromTarget / radius
                : assignment.RadialDirection;
            float signedAngle = Vector3.SignedAngle(
                assignment.RadialDirection,
                direction,
                Vector3.up);

            float minimumRadius = Mathf.Min(
                assignment.ConfrontationMaxRadius,
                assignment.ConfrontationMinRadius + radialInset);
            float maximumRadius = Mathf.Max(
                minimumRadius,
                assignment.ConfrontationMaxRadius - radialInset);
            float maximumAngle = Mathf.Max(0f, assignment.HalfAngle - angularInset);
            radius = Mathf.Clamp(radius, minimumRadius, maximumRadius);
            signedAngle = Mathf.Clamp(signedAngle, -maximumAngle, maximumAngle);
            Vector3 clampedDirection = Quaternion.AngleAxis(signedAngle, Vector3.up) *
                                       assignment.RadialDirection;
            Vector3 result = target.position + clampedDirection * radius;
            result.y = position.y;
            return result;
        }

        public bool TryProjectSpatialPosition(
            EnemyController enemy,
            Vector3 desired,
            out Vector3 position)
        {
            position = enemy != null ? enemy.transform.position : desired;
            if (!IsEligible(enemy) ||
                !NavMesh.SamplePosition(
                    desired,
                    out NavMeshHit hit,
                    navMeshSampleDistance,
                    NavMesh.AllAreas) ||
                !HasCompletePath(enemy.transform.position, hit.position) ||
                !HasEnoughSpacing(enemy, hit.position))
                return false;

            reservedPositions[enemy] = hit.position;
            position = hit.position;
            return true;
        }

        public bool ShouldYield(EnemyController enemy, out Vector3 yieldDirection)
        {
            yieldDirection = Vector3.zero;
            if (!IsEligible(enemy) || !ResolveTarget())
                return false;

            foreach (EnemyController attacker in tokenHolders)
            {
                if (!IsEligible(attacker) || attacker == enemy)
                    continue;

                Vector3 corridorStart = Flatten(attacker.transform.position);
                Vector3 corridorEnd = Flatten(target.position);
                Vector3 point = Flatten(enemy.transform.position);
                Vector3 corridor = corridorEnd - corridorStart;
                float corridorLengthSqr = corridor.sqrMagnitude;
                if (corridorLengthSqr <= 0.001f)
                    continue;

                float along = Mathf.Clamp01(
                    Vector3.Dot(point - corridorStart, corridor) / corridorLengthSqr);
                Vector3 closest = corridorStart + corridor * along;
                Vector3 away = point - closest;
                if (away.sqrMagnitude > attackCorridorWidth * attackCorridorWidth)
                    continue;

                Vector3 tangent = Vector3.Cross(Vector3.up, corridor.normalized);
                yieldDirection = Vector3.Dot(away, tangent) >= 0f ? tangent : -tangent;
                return true;
            }
            return false;
        }

        public void ReleaseConfrontationReservation(EnemyController enemy)
        {
            if (enemy != null)
                reservedPositions.Remove(enemy);
        }

        private bool ResolveTarget()
        {
            if (target != null)
                return true;
            if (!findPlayerTargetOnAwake)
                return false;
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            target = player != null ? player.transform : null;
            if (target != null)
                ResetSlotAssignments();
            return target != null;
        }

        private int GetActiveParticipantCount()
        {
            int activeCount = 0;
            for (int i = 0; i < participants.Count; i++)
            {
                if (IsEligible(participants[i]))
                    activeCount++;
            }
            return activeCount;
        }

        private void EnsureSlotAssignments(int slotCount)
        {
            GetSlotReferenceDirection();
            if (cachedSlotCount == slotCount)
                return;

            // 槽位总数变化时按当前位置重新做一次“最近空槽”分配；平时不重排。
            assignedSlots.Clear();
            cachedSlotCount = slotCount;
            for (int i = 0; i < participants.Count; i++)
            {
                EnemyController participant = participants[i];
                if (IsEligible(participant))
                    AssignNearestFreeSlot(participant, slotCount);
            }
        }

        private int GetOrAssignNearestSlot(EnemyController enemy, int slotCount)
        {
            if (assignedSlots.TryGetValue(enemy, out int existingSlot) &&
                existingSlot >= 0 && existingSlot < slotCount)
                return existingSlot;
            return AssignNearestFreeSlot(enemy, slotCount);
        }

        private int AssignNearestFreeSlot(EnemyController enemy, int slotCount)
        {
            if (!IsEligible(enemy) || slotCount <= 0 || !ResolveTarget())
                return -1;

            Vector3 reference = GetSlotReferenceDirection();
            float comparisonRadius = Mathf.Max(innerRingRadius + 0.2f, outerRingRadius);
            int bestSlot = -1;
            float bestSqrDistance = float.PositiveInfinity;
            for (int slot = 0; slot < slotCount; slot++)
            {
                if (IsSlotOccupied(slot, enemy))
                    continue;

                float angle = angularOffset + 360f * slot / slotCount;
                Vector3 radial = Quaternion.AngleAxis(angle, Vector3.up) * reference;
                Vector3 comparisonPosition = target.position + radial * comparisonRadius;
                float sqrDistance = Flatten(
                    enemy.transform.position - comparisonPosition).sqrMagnitude;
                if (sqrDistance >= bestSqrDistance)
                    continue;
                bestSqrDistance = sqrDistance;
                bestSlot = slot;
            }

            if (bestSlot >= 0)
                assignedSlots[enemy] = bestSlot;
            return bestSlot;
        }

        private bool IsSlotOccupied(int slotIndex, EnemyController except)
        {
            foreach (KeyValuePair<EnemyController, int> pair in assignedSlots)
            {
                if (pair.Key != except && IsEligible(pair.Key) && pair.Value == slotIndex)
                    return true;
            }
            return false;
        }

        private Vector3 GetSlotReferenceDirection()
        {
            if (hasSlotReferenceDirection)
                return slotReferenceDirection;

            Vector3 reference = target != null ? Flatten(target.forward) : Vector3.forward;
            if (reference.sqrMagnitude <= 0.0001f)
                reference = Vector3.forward;
            slotReferenceDirection = reference.normalized;
            hasSlotReferenceDirection = true;
            return slotReferenceDirection;
        }

        private void ResetSlotAssignments()
        {
            assignedSlots.Clear();
            cachedSlotCount = -1;
            slotReferenceDirection = Vector3.zero;
            hasSlotReferenceDirection = false;
        }

        private bool HasEnoughSpacing(EnemyController enemy, Vector3 position)
        {
            float minimumSqrDistance = minimumEnemySpacing * minimumEnemySpacing;
            for (int i = 0; i < participants.Count; i++)
            {
                EnemyController participant = participants[i];
                if (participant == enemy || !IsEligible(participant))
                    continue;
                if (Flatten(participant.transform.position - position).sqrMagnitude <
                    minimumSqrDistance)
                    return false;
            }

            foreach (KeyValuePair<EnemyController, Vector3> pair in reservedPositions)
            {
                if (pair.Key == enemy || !IsEligible(pair.Key))
                    continue;
                if (Flatten(pair.Value - position).sqrMagnitude < minimumSqrDistance)
                    return false;
            }
            return true;
        }

        private static bool HasCompletePath(Vector3 from, Vector3 to)
        {
            NavMeshPath path = new NavMeshPath();
            return NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) &&
                   path.status == NavMeshPathStatus.PathComplete;
        }

        private void CleanupInvalidEntries()
        {
            participants.RemoveAll(enemy => enemy == null);
            tokenQueue.RemoveAll(enemy => !IsEligible(enemy));
            tokenHolders.RemoveWhere(enemy => !IsEligible(enemy));

            List<EnemyController> staleSlotAssignments = null;
            foreach (EnemyController enemy in assignedSlots.Keys)
            {
                if (IsEligible(enemy))
                    continue;
                staleSlotAssignments ??= new List<EnemyController>();
                staleSlotAssignments.Add(enemy);
            }
            if (staleSlotAssignments != null)
            {
                for (int i = 0; i < staleSlotAssignments.Count; i++)
                    assignedSlots.Remove(staleSlotAssignments[i]);
            }

            List<EnemyController> staleReservations = null;
            foreach (EnemyController enemy in reservedPositions.Keys)
            {
                if (IsEligible(enemy))
                    continue;
                staleReservations ??= new List<EnemyController>();
                staleReservations.Add(enemy);
            }
            if (staleReservations == null)
                return;
            for (int i = 0; i < staleReservations.Count; i++)
                reservedPositions.Remove(staleReservations[i]);
        }

        private static bool IsEligible(EnemyController enemy)
        {
            return enemy != null && enemy.isActiveAndEnabled &&
                   enemy.LifeState == EnemyLifeState.Alive;
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (target == null)
                return;

            DrawRing(target.position, innerRingRadius, new Color(1f, 0.25f, 0.15f, 0.7f));
            float regionMinRadius = Mathf.Max(
                innerRingRadius + 0.1f,
                outerRingRadius - confrontationRegionDepth);
            DrawRing(target.position, regionMinRadius, new Color(0.2f, 1f, 0.4f, 0.65f));
            DrawRing(target.position, outerRingRadius, new Color(0.1f, 0.75f, 1f, 0.7f));
            int slotCount = Mathf.Max(1, minimumSlotCount);
            Vector3 reference = hasSlotReferenceDirection
                ? slotReferenceDirection
                : Flatten(target.forward).normalized;
            if (reference.sqrMagnitude <= 0.0001f)
                reference = Vector3.forward;
            for (int i = 0; i < slotCount; i++)
            {
                float centerAngle = angularOffset + 360f * i / slotCount;
                float boundaryAngle = centerAngle + 180f / slotCount;
                Vector3 radial = Quaternion.AngleAxis(centerAngle, Vector3.up) * reference;
                Vector3 boundary = Quaternion.AngleAxis(boundaryAngle, Vector3.up) * reference;
                Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.35f);
                Gizmos.DrawLine(
                    target.position + radial * regionMinRadius,
                    target.position + radial * outerRingRadius);
                Gizmos.color = new Color(1f, 0.65f, 0.1f, 0.8f);
                Gizmos.DrawLine(
                    target.position + boundary * regionMinRadius,
                    target.position + boundary * outerRingRadius);
            }

            Gizmos.color = Color.magenta;
            foreach (Vector3 reservation in reservedPositions.Values)
                Gizmos.DrawSphere(reservation, 0.12f);

            Gizmos.color = new Color(1f, 0f, 0f, 0.8f);
            foreach (EnemyController attacker in tokenHolders)
            {
                if (attacker != null)
                    Gizmos.DrawLine(attacker.transform.position, target.position);
            }
        }

        private static void DrawRing(Vector3 center, float radius, Color color)
        {
            Gizmos.color = color;
            const int segments = 64;
            Vector3 previous = center + Vector3.forward * radius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 next = center +
                               new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
#endif
    }
}
