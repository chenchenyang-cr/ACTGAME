using UnityEngine;

namespace CombatEditor
{
    // One owner per spawned effect, so a later attack cannot overwrite an older instance.
    [DefaultExecutionOrder(50)]
    public sealed class ParticleEffectLifetime : MonoBehaviour
    {
        private CombatController owner;
        private AbilityScriptableObject ability;
        private AbilityEventObj_Particles source;
        private ParticleSystem[] particles;
        private float[] initialSpeeds;
        private float eventStartTime;
        private float currentTime;
        private bool collisionPending;
        private bool detached;
        private bool finished;
        private bool initialized;
        public HitBox CurrentHitBox { get; private set; }

        public void Initialize(CombatController controller, AbilityScriptableObject anim,
            AbilityEventObj_Particles config, float startTime)
        {
            owner = controller;
            ability = anim;
            source = config;
            eventStartTime = currentTime = startTime;
            particles = GetComponentsInChildren<ParticleSystem>(true);
            initialSpeeds = new float[particles.Length];
            for (int i = 0; i < particles.Length; i++)
                initialSpeeds[i] = particles[i].main.simulationSpeed;
            collisionPending = source.EnableCollision && source.Collision != null;
            initialized = true;
            if (source.CollisionDelayFrames == 0) TryCreateHitBox();
            ApplyHitStopSpeed();
        }

        public void UpdateAnimationTime(float time)
        {
            if (finished || detached) return;
            currentTime = time;
            TryCreateHitBox();
            CurrentHitBox?.UpdateAnimationTime(time);
        }

        public void ReleaseFromAnimation()
        {
            if (finished || detached) return;
            detached = true;
            // A looping effect has no natural end. Drain its live particles on release.
            for (int i = 0; i < particles.Length; i++)
                if (particles[i] != null && particles[i].main.loop)
                    particles[i].Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        private void LateUpdate()
        {
            if (!initialized || finished) return;
            if (owner == null || !owner.isActiveAndEnabled)
            {
                Finish();
                return;
            }
            ApplyHitStopSpeed();
            bool alive = false;
            for (int i = 0; i < particles.Length; i++)
                if (particles[i] != null && particles[i].IsAlive(false)) { alive = true; break; }
            if (!alive && (particles.Length > 0 || detached))
            {
                Finish();
                return;
            }
            if (detached)
            {
                float length = ability != null && ability.Clip != null ? ability.Clip.length : 0f;
                if (length > 0f)
                    currentTime += Time.deltaTime * HitStopSpeed / length;
                TryCreateHitBox();
                CurrentHitBox?.UpdateAnimationTime(currentTime);
            }
        }

        private float HitStopSpeed => source != null && source.AffectedByHitStop &&
            owner != null && owner._animSpeedExecutor != null
                ? owner._animSpeedExecutor.CurrentHitStopSpeedScale : 1f;

        public void ApplyHitStopSpeed()
        {
            if (finished || source == null || !source.AffectedByHitStop) return;
            float speed = HitStopSpeed;
            for (int i = 0; i < particles.Length; i++)
            {
                if (particles[i] == null) continue;
                var main = particles[i].main;
                main.simulationSpeed = initialSpeeds[i] * speed;
            }
        }

        private void TryCreateHitBox()
        {
            if (!collisionPending || finished || !gameObject.activeInHierarchy ||
                source == null || !source.EnableCollision || source.Collision == null ||
                !source.IsCollisionDelayElapsed(currentTime, eventStartTime,
                    ability != null ? ability.Clip : null))
                return;
            collisionPending = false;
            Collider collider = source.Collision.CreateCollider(gameObject);
            CurrentHitBox = collider.gameObject.AddComponent<HitBox>();
            CurrentHitBox.Init(owner, ability, source);
            CurrentHitBox.TrackActionSingleHits = false;
            CurrentHitBox.UpdateAnimationTime(currentTime);
        }

        public void Finish()
        {
            if (finished) return;
            finished = true;
            collisionPending = false;
            CurrentHitBox?.CancelHits();
            var follower = GetComponent<NodeFollower>();
            if (follower != null) follower.enabled = false;
            Destroy(gameObject);
        }

        private void OnDisable()
        {
            if (initialized) Finish();
        }

        private void OnDestroy() => CurrentHitBox?.CancelHits();
    }
}
