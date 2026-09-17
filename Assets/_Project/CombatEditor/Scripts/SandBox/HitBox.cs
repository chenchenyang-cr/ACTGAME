using System.Collections.Generic;
using UnityEngine;

namespace CombatEditor
{
    [DefaultExecutionOrder(200)] // After NodeFollower and animation pose evaluation.
    public class HitBox : MonoBehaviour
    {
        private sealed class TargetHitState
        {
            public int HitCount;
            public int NextEligibleFrame;
        }

        public CombatController Owner;
        public AbilityScriptableObject SourceAbility { get; private set; }
        public AbilityEventObj_CreateHitBox SourceEvent { get; private set; }

        // Fallback for legacy hit-boxes created without an ability event.
        [HideInInspector] public LayerMask hitTargetLayers = ~0;

        private readonly Dictionary<int, TargetHitState> targetStates = new();
        private IHitBoxHitSource hitSource;
        private CombatTeam sourceTeam;
        private int currentAnimationFrame;
        private Vector3 lastSampledPosition;
        private Vector3 lastMotionDirection = Vector3.forward;
        private bool hasLastSampledPosition;
        private bool hitsCancelled;
        private Collider hitCollider;
        private NodeFollower nodeFollower;
        private Collider[] overlaps = new Collider[32];
        private readonly HashSet<Collider> sampledColliders = new();
        private Vector3 previousPosition;
        private Quaternion previousRotation;
        private Vector3 previousNodePosition;
        private Quaternion previousNodeRotation;
        private bool hasPreviousPose;
        private bool hasSampledPose;

        private bool UsesPoseQueries => hitCollider is BoxCollider ||
                                       hitCollider is SphereCollider ||
                                       hitCollider is CapsuleCollider;

        public void CancelHits() => hitsCancelled = true;

        public void Init(CombatController controller, AbilityScriptableObject sourceAbility = null,
            AbilityEventObj_CreateHitBox sourceEvent = null)
        {
            Owner = controller;
            hitsCancelled = false;
            SourceAbility = sourceAbility;
            SourceEvent = sourceEvent;
            targetStates.Clear();
            hitSource = ResolveInterface<IHitBoxHitSource>(Owner);
            ICombatTeamProvider teamProvider = ResolveInterface<ICombatTeamProvider>(Owner);
            sourceTeam = teamProvider != null ? teamProvider.Team : CombatTeam.Neutral;
            currentAnimationFrame = 0;
            lastSampledPosition = transform.position;
            lastMotionDirection = ResolveFallbackDirection();
            hasLastSampledPosition = true;
            hitCollider = GetComponent<Collider>();
            nodeFollower = GetComponent<NodeFollower>();
            // Creation runs before animation / NodeFollower. Preserve that pose so
            // the very first LateUpdate also sweeps the blade's movement this frame.
            previousPosition = transform.position;
            previousRotation = transform.rotation;
            bool followsNode = nodeFollower != null && nodeFollower.NodeTrans != null && nodeFollower.FollowPos;
            previousNodePosition = followsNode ? nodeFollower.NodeTrans.position : previousPosition;
            previousNodeRotation = followsNode ? nodeFollower.NodeTrans.rotation : previousRotation;
            hasPreviousPose = true;
            hasSampledPose = false;
        }

        public void UpdateAnimationTime(float normalizedTime)
        {
            currentAnimationFrame = CombatTimeline.ToFrame(normalizedTime,
                SourceAbility != null ? SourceAbility.Clip : null);
        }

        public Vector3 CurrentMotionDirection
        {
            get
            {
                RefreshMotionDirection();
                if (lastMotionDirection.sqrMagnitude <= 0.0001f)
                    lastMotionDirection = ResolveFallbackDirection();
                return lastMotionDirection;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!UsesPoseQueries) TryProcessHit(other, ResolveHitPoint(other));
        }

        private void OnTriggerStay(Collider other)
        {
            if (!UsesPoseQueries) TryProcessHit(other, ResolveHitPoint(other));
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Vector3 point = other != null
                ? other.ClosestPoint(transform.position)
                : transform.position;
            TryProcessHit(other, point);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            Vector3 point = other != null
                ? other.ClosestPoint(transform.position)
                : transform.position;
            TryProcessHit(other, point);
        }

        private void TryProcessHit(Component other, Vector3 hitPoint)
        {
            RefreshMotionDirection();
            if (hitsCancelled || Owner == null || other == null || !IsInHitTargetLayer(other.gameObject.layer))
                return;

            if (!TryResolveDamageReceiver(other, out ICombatDamageReceiver receiver,
                    out MonoBehaviour receiverBehaviour))
                return;
            if (receiverBehaviour.transform.root == Owner.transform.root)
                return;
            if (SourceEvent != null && !SourceEvent.AllowFriendlyFire &&
                sourceTeam != CombatTeam.Neutral && receiver.Team == sourceTeam)
                return;

            int targetId = receiverBehaviour.GetInstanceID();
            if (!targetStates.TryGetValue(targetId, out TargetHitState state))
            {
                state = new TargetHitState();
                targetStates.Add(targetId, state);
            }

            CombatHitMode hitMode = SourceEvent != null
                ? SourceEvent.HitMode
                : CombatHitMode.Single;
            if (state.HitCount > 0 && hitMode == CombatHitMode.Single)
                return;
            if (SourceEvent != null && SourceEvent.MaximumHitsPerTarget > 0 &&
                state.HitCount >= SourceEvent.MaximumHitsPerTarget)
                return;
            if (hitMode == CombatHitMode.Repeated && state.HitCount > 0 &&
                currentAnimationFrame < state.NextEligibleFrame)
                return;

            int hitSequenceIndex = state.HitCount + 1;
            HitBoxHitContext hitContext = new HitBoxHitContext(hitSequenceIndex, 1f, 1f);
            Vector3 attackDirection = CurrentMotionDirection;
            CombatHitRequest request = BuildRequest(other, hitPoint, attackDirection,
                hitSequenceIndex);

            CombatHitResolution resolution;
            bool handled = hitSource != null
                ? hitSource.TryHandleHit(this, other, hitPoint, hitContext, out resolution)
                : receiver.TryReceiveHit(in request, out resolution);
            if (!handled || !resolution.IsAccepted)
                return;

            state.HitCount = hitSequenceIndex;
            if (hitMode == CombatHitMode.Repeated)
            {
                int interval = SourceEvent != null
                    ? Mathf.Max(1, SourceEvent.RepeatIntervalFrames)
                    : 1;
                state.NextEligibleFrame = currentAnimationFrame + interval;
            }

            if (resolution.ResultType != CombatHitResultType.Parried && SourceEvent != null && SourceEvent.EnableHitCameraShake)
            {
                CombatCamera.CameraShakeSettings shakeSettings =
                    SourceEvent.ResolveHitCameraShakeSettings();
                CombatCamera.CameraShakeRuntime.Pulse(
                    shakeSettings,
                    SourceEvent.ResolveHitCameraShakeDuration(),
                    Mathf.Max(0f, resolution.CameraShakeScale),
                    SourceEvent.ResolveHitCameraShakeUseUnscaledTime(),
                    attackDirection);
            }
            if (resolution.ResultType != CombatHitResultType.Parried && SourceEvent != null && SourceEvent.EnableHitAnimationSpeed)
            {
                PlayHitStop(Owner);
                CombatController targetController =
                    receiverBehaviour.GetComponentInParent<CombatController>();
                if (targetController == null)
                    targetController = receiverBehaviour.transform.root
                        .GetComponentInChildren<CombatController>(true);
                if (targetController != Owner)
                    PlayHitStop(targetController, delayOneFrame: true);
            }
            CombatHitEventBus.Publish(new CombatHitConfirmedEvent(Owner, SourceAbility,
                SourceEvent, this, other, receiverBehaviour.gameObject, hitPoint,
                attackDirection, hitContext, resolution));
        }

        private void PlayHitStop(CombatController controller, bool delayOneFrame = false)
        {
            if (controller == null || controller._animSpeedExecutor == null)
                return;

            controller._animSpeedExecutor.PlayHitStop(SourceEvent.HitStopFrames, delayOneFrame);
        }

        private CombatHitRequest BuildRequest(Component other, Vector3 hitPoint,
            Vector3 attackDirection, int hitSequenceIndex)
        {
            float damage = SourceEvent != null ? SourceEvent.Damage : 0f;
            float poiseDamage = SourceEvent != null ? SourceEvent.PoiseDamage : 0f;
            float staggerDuration = SourceEvent != null ? SourceEvent.StaggerDuration : 0f;
            CombatHitReactionPolicy reaction = SourceEvent != null
                ? SourceEvent.HitReaction
                : CombatHitReactionPolicy.None;

            return new CombatHitRequest(Owner, SourceAbility, SourceEvent, this, other,
                hitPoint, attackDirection, hitSequenceIndex, damage, poiseDamage,
                reaction, staggerDuration);
        }

        private bool IsInHitTargetLayer(int layer)
        {
            LayerMask mask = SourceEvent != null ? SourceEvent.TargetLayers : hitTargetLayers;
            return (mask.value & (1 << layer)) != 0;
        }

        private static bool TryResolveDamageReceiver(Component other,
            out ICombatDamageReceiver receiver, out MonoBehaviour receiverBehaviour)
        {
            MonoBehaviour[] behaviours = other.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ICombatDamageReceiver candidate)
                {
                    receiver = candidate;
                    receiverBehaviour = behaviours[i];
                    return true;
                }
            }

            receiver = null;
            receiverBehaviour = null;
            return false;
        }

        private static T ResolveInterface<T>(CombatController owner) where T : class
        {
            if (owner == null) return null;
            MonoBehaviour[] behaviours = owner.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is T candidate) return candidate;

            behaviours = owner.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is T candidate) return candidate;
            return null;
        }

        private Vector3 ResolveHitPoint(Collider other)
        {
            return ResolveHitPoint(other, transform.position);
        }

        private static Vector3 ResolveHitPoint(Collider other, Vector3 samplePosition)
        {
            if (other == null) return samplePosition;
            if (other is BoxCollider || other is SphereCollider || other is CapsuleCollider)
                return other.ClosestPoint(samplePosition);
            if (other is MeshCollider meshCollider && meshCollider.convex)
                return other.ClosestPoint(samplePosition);
            return other.bounds.ClosestPoint(samplePosition);
        }

        private void LateUpdate()
        {
            RefreshMotionDirection();
            SampleHits();
        }

        // Trigger callbacks see the previous physics pose. Sample the displayed pose
        // instead, including intermediate orientations so a rotating blade cannot
        // jump from one side of a target to the other between physics ticks.
        internal void SampleHits()
        {
            if (hitsCancelled || Owner == null || !UsesPoseQueries || !hitCollider.enabled ||
                !gameObject.activeInHierarchy)
            {
                hasPreviousPose = false;
                hasSampledPose = false;
                return;
            }

            Physics.SyncTransforms();
            sampledColliders.Clear();
            Vector3 position = transform.position;
            Quaternion rotation = transform.rotation;
            bool followsNode = nodeFollower != null && nodeFollower.NodeTrans != null &&
                               nodeFollower.FollowPos;
            Vector3 nodePosition = followsNode ? nodeFollower.NodeTrans.position : position;
            Quaternion nodeRotation = followsNode ? nodeFollower.NodeTrans.rotation : rotation;
            if (!hasPreviousPose)
            {
                previousPosition = position;
                previousRotation = rotation;
                previousNodePosition = nodePosition;
                previousNodeRotation = nodeRotation;
            }

            Vector3 scale = transform.lossyScale;
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            GetShape(scale, out Vector3 center, out Vector3 halfExtents,
                out float radius, out Vector3 capsuleAxis, out float halfSegment);
            float thickness = hitCollider is BoxCollider
                ? Mathf.Min(halfExtents.x, Mathf.Min(halfExtents.y, halfExtents.z))
                : radius;
            float reach = hitCollider is BoxCollider ? halfExtents.magnitude : radius + halfSegment;
            float travel = Vector3.Distance(previousPosition, position) +
                           Quaternion.Angle(previousRotation, rotation) * Mathf.Deg2Rad * (reach + center.magnitude);
            if (followsNode)
                travel += Quaternion.Angle(previousNodeRotation, nodeRotation) * Mathf.Deg2Rad *
                          nodeFollower.PosOffset.magnitude;
            // At most half the narrowest half-extent per sample; no inflated hit volume.
            int steps = Mathf.Clamp(Mathf.CeilToInt(travel / Mathf.Max(0.0025f, thickness * 0.5f)), 1, 512);
            int layerMask = SourceEvent != null ? SourceEvent.TargetLayers.value : hitTargetLayers.value;
            for (int layer = 0; layer < 32; layer++)
                if (Physics.GetIgnoreLayerCollision(gameObject.layer, layer)) layerMask &= ~(1 << layer);

            for (int step = hasSampledPose ? 1 : 0; step <= steps && !hitsCancelled; step++)
            {
                float t = step / (float)steps;
                Quaternion sampleRotation = Quaternion.Slerp(previousRotation, rotation, t);
                Vector3 samplePosition = followsNode
                    ? Vector3.Lerp(previousNodePosition, nodePosition, t) +
                      Quaternion.Slerp(previousNodeRotation, nodeRotation, t) * nodeFollower.PosOffset
                    : Vector3.Lerp(previousPosition, position, t);
                Vector3 sampleCenter = samplePosition + sampleRotation * center;
                int count;
                do
                {
                    if (hitCollider is BoxCollider)
                        count = Physics.OverlapBoxNonAlloc(sampleCenter, halfExtents, overlaps,
                            sampleRotation, layerMask, QueryTriggerInteraction.Collide);
                    else if (hitCollider is SphereCollider)
                        count = Physics.OverlapSphereNonAlloc(sampleCenter, radius, overlaps,
                            layerMask, QueryTriggerInteraction.Collide);
                    else
                    {
                        Vector3 axis = sampleRotation * capsuleAxis * halfSegment;
                        count = Physics.OverlapCapsuleNonAlloc(sampleCenter - axis, sampleCenter + axis,
                            radius, overlaps, layerMask, QueryTriggerInteraction.Collide);
                    }
                    if (count < overlaps.Length) break;
                    System.Array.Resize(ref overlaps, overlaps.Length * 2);
                } while (true);

                for (int i = 0; i < count && !hitsCancelled; i++)
                {
                    Collider other = overlaps[i];
                    if (other == null || other == hitCollider || !sampledColliders.Add(other) ||
                        Physics.GetIgnoreCollision(hitCollider, other)) continue;
                    TryProcessHit(other, ResolveHitPoint(other, sampleCenter));
                }
            }
            previousPosition = position;
            previousRotation = rotation;
            previousNodePosition = nodePosition;
            previousNodeRotation = nodeRotation;
            hasPreviousPose = true;
            hasSampledPose = true;
        }

        private void GetShape(Vector3 scale, out Vector3 center, out Vector3 halfExtents,
            out float radius, out Vector3 capsuleAxis, out float halfSegment)
        {
            halfExtents = Vector3.zero;
            radius = halfSegment = 0f;
            capsuleAxis = Vector3.up;
            Vector3 localCenter;
            if (hitCollider is BoxCollider box)
            {
                localCenter = box.center;
                halfExtents = Vector3.Scale(box.size, scale) * 0.5f;
            }
            else if (hitCollider is SphereCollider sphere)
            {
                localCenter = sphere.center;
                radius = sphere.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
            }
            else
            {
                var capsule = (CapsuleCollider)hitCollider;
                localCenter = capsule.center;
                int direction = capsule.direction;
                capsuleAxis = direction == 0 ? Vector3.right : direction == 1 ? Vector3.up : Vector3.forward;
                float axisScale = scale[direction];
                radius = capsule.radius * Mathf.Max(scale[(direction + 1) % 3], scale[(direction + 2) % 3]);
                halfSegment = Mathf.Max(0f, capsule.height * axisScale * 0.5f - radius);
            }
            // Preserve signed center offsets under mirrored transforms.
            center = Vector3.Scale(localCenter, transform.lossyScale);
        }

        private void RefreshMotionDirection()
        {
            Vector3 currentPosition = transform.position;
            if (!hasLastSampledPosition)
            {
                lastSampledPosition = currentPosition;
                lastMotionDirection = ResolveFallbackDirection();
                hasLastSampledPosition = true;
                return;
            }

            Vector3 delta = currentPosition - lastSampledPosition;
            if (delta.sqrMagnitude > 0.000001f) lastMotionDirection = delta.normalized;
            lastSampledPosition = currentPosition;
        }

        private Vector3 ResolveFallbackDirection()
        {
            Vector3 direction = Owner != null ? Owner.transform.forward : transform.forward;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        }
    }
}
