using System.Collections.Generic;
using UnityEngine;

namespace CombatEditor
{
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

        public void Init(CombatController controller, AbilityScriptableObject sourceAbility = null,
            AbilityEventObj_CreateHitBox sourceEvent = null)
        {
            Owner = controller;
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

        private void OnTriggerEnter(Collider other) =>
            TryProcessHit(other, ResolveHitPoint(other));

        private void OnTriggerStay(Collider other) =>
            TryProcessHit(other, ResolveHitPoint(other));

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
            if (Owner == null || other == null || !IsInHitTargetLayer(other.gameObject.layer))
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

            if (SourceEvent != null && SourceEvent.EnableHitCameraShake)
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
            CombatHitEventBus.Publish(new CombatHitConfirmedEvent(Owner, SourceAbility,
                SourceEvent, this, other, receiverBehaviour.gameObject, hitPoint,
                attackDirection, hitContext, resolution));
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
            if (other == null) return transform.position;
            if (other is BoxCollider || other is SphereCollider || other is CapsuleCollider)
                return other.ClosestPoint(transform.position);
            if (other is MeshCollider meshCollider && meshCollider.convex)
                return other.ClosestPoint(transform.position);
            return other.bounds.ClosestPoint(transform.position);
        }

        private void LateUpdate() => RefreshMotionDirection();

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
