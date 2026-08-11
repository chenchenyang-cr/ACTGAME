
using System.Collections.Generic;
using UnityEngine;
	
 namespace CombatEditor
{	
	public class HitBox : MonoBehaviour
	{
        public CombatController Owner;

        [Header("Filtering")]
        public LayerMask hitTargetLayers = ~0;

        [Header("Multi Hit")]
        public bool allowMultiHit;
        [Min(0.01f)] public float multiHitInterval = 0.1f;
        [Min(0f)] public float repeatedHitCameraShakeMultiplier = 1f;
        [Min(0f)] public float repeatedHitStopMultiplier = 1f;

        private readonly HashSet<int> hitTargets = new HashSet<int>();
        private readonly Dictionary<int, float> nextHitTimes = new Dictionary<int, float>();
        private readonly Dictionary<int, int> hitCounts = new Dictionary<int, int>();
        private IHitBoxHitSource hitSource;
        private Vector3 lastSampledPosition;
        private Vector3 lastMotionDirection = Vector3.forward;
        private bool hasLastSampledPosition;
  
        public  void Init(CombatController _controller)
        {
            Owner = _controller;
            hitTargets.Clear();
            nextHitTimes.Clear();
            hitCounts.Clear();
            hitSource = null;
            if (Owner == null)
            {
                return;
            }

            hitSource = ResolveHitSource(Owner);
            lastSampledPosition = transform.position;
            lastMotionDirection = ResolveFallbackDirection();
            hasLastSampledPosition = true;
        }

        public Vector3 CurrentMotionDirection
        {
            get
            {
                RefreshMotionDirection();
                if (lastMotionDirection.sqrMagnitude <= 0.0001f)
                {
                    lastMotionDirection = ResolveFallbackDirection();
                }

                return lastMotionDirection;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            TryProcessHit(other, ResolveHitPoint(other));
        }

        private void OnTriggerStay(Collider other)
        {
            TryProcessHit(other, ResolveHitPoint(other));
        }

        private void OnTriggerExit(Collider other)
        {
            ClearMultiHitTracking(other);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Vector3 hitPoint = other != null ? other.ClosestPoint(transform.position) : transform.position;
            TryProcessHit(other, hitPoint);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            Vector3 hitPoint = other != null ? other.ClosestPoint(transform.position) : transform.position;
            TryProcessHit(other, hitPoint);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            ClearMultiHitTracking(other);
        }

        private void TryProcessHit(Component other, Vector3 hitPoint)
        {
            RefreshMotionDirection();

            if (Owner == null || other == null || hitSource == null)
            {
                return;
            }

            if (!IsInHitTargetLayer(other.gameObject.layer))
            {
                return;
            }

            Transform otherRoot = other.transform.root;
            if (otherRoot == null || otherRoot == Owner.transform.root)
            {
                return;
            }

            int targetId = otherRoot.gameObject.GetInstanceID();
            if (!allowMultiHit && hitTargets.Contains(targetId))
            {
                return;
            }

            if (allowMultiHit &&
                nextHitTimes.TryGetValue(targetId, out float nextHitTime) &&
                Time.time < nextHitTime)
            {
                return;
            }

            int previousHitCount = 0;
            hitCounts.TryGetValue(targetId, out previousHitCount);
            int hitSequenceIndex = previousHitCount + 1;
            HitBoxHitContext hitContext = new HitBoxHitContext(
                hitSequenceIndex,
                EvaluateScale(repeatedHitCameraShakeMultiplier, previousHitCount),
                EvaluateScale(repeatedHitStopMultiplier, previousHitCount));

            if (hitSource.TryHandleHit(this, other, hitPoint, hitContext))
            {
                hitCounts[targetId] = hitSequenceIndex;
                if (allowMultiHit)
                {
                    nextHitTimes[targetId] = Time.time + Mathf.Max(0.01f, multiHitInterval);
                }
                else
                {
                    hitTargets.Add(targetId);
                }
            }
        }

        private Vector3 ResolveHitPoint(Collider other)
        {
            if (other == null)
            {
                return transform.position;
            }

            if (CanUseClosestPoint(other))
            {
                return other.ClosestPoint(transform.position);
            }

            return other.bounds.ClosestPoint(transform.position);
        }

        private static bool CanUseClosestPoint(Collider other)
        {
            if (other == null)
            {
                return false;
            }

            if (other is BoxCollider || other is SphereCollider || other is CapsuleCollider)
            {
                return true;
            }

            MeshCollider meshCollider = other as MeshCollider;
            return meshCollider != null && meshCollider.convex;
        }

        private bool IsInHitTargetLayer(int layer)
        {
            return (hitTargetLayers.value & (1 << layer)) != 0;
        }

        private static IHitBoxHitSource ResolveHitSource(CombatController owner)
        {
            if (owner == null)
            {
                return null;
            }

            MonoBehaviour[] selfBehaviours = owner.GetComponents<MonoBehaviour>();
            for (int i = 0; i < selfBehaviours.Length; i++)
            {
                if (selfBehaviours[i] is IHitBoxHitSource selfSource)
                {
                    return selfSource;
                }
            }

            MonoBehaviour[] parentBehaviours = owner.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < parentBehaviours.Length; i++)
            {
                if (parentBehaviours[i] is IHitBoxHitSource parentSource)
                {
                    return parentSource;
                }
            }

            MonoBehaviour[] childBehaviours = owner.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < childBehaviours.Length; i++)
            {
                if (childBehaviours[i] is IHitBoxHitSource childSource)
                {
                    return childSource;
                }
            }

            return null;
        }

        private void ClearMultiHitTracking(Component other)
        {
            if (!allowMultiHit || other == null)
            {
                return;
            }

            Transform otherRoot = other.transform.root;
            if (otherRoot == null || otherRoot == Owner.transform.root)
            {
                return;
            }

            int targetId = otherRoot.gameObject.GetInstanceID();
            nextHitTimes.Remove(targetId);
            hitCounts.Remove(targetId);
        }

        private static float EvaluateScale(float multiplier, int repeatedHitCount)
        {
            if (repeatedHitCount <= 0)
            {
                return 1f;
            }

            return Mathf.Pow(multiplier, repeatedHitCount);
        }

        private void LateUpdate()
        {
            RefreshMotionDirection();
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
            if (delta.sqrMagnitude > 0.000001f)
            {
                lastMotionDirection = delta.normalized;
            }

            lastSampledPosition = currentPosition;
        }

        private Vector3 ResolveFallbackDirection()
        {
            Vector3 fallbackDirection = Owner != null ? Owner.transform.forward : transform.forward;
            if (fallbackDirection.sqrMagnitude <= 0.0001f)
            {
                fallbackDirection = Vector3.forward;
            }

            return fallbackDirection.normalized;
        }
    }
}
