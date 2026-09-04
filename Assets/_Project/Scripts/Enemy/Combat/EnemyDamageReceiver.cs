using CombatEditor;
using UnityEngine;

namespace UnityLearning.EnemySystem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyController))]
    public sealed class EnemyDamageReceiver : MonoBehaviour, ICombatDamageReceiver
    {
        [SerializeField] private EnemyController controller;

        private float currentHealth;
        private float currentPoise;
        private float lastPoiseDamageTime = float.NegativeInfinity;
        private bool initialized;

        public CombatTeam Team => CombatTeam.Enemy;
        public float CurrentHealth => currentHealth;
        public float CurrentPoise => currentPoise;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<EnemyController>();
            InitializeVitals();
        }

        private void Update()
        {
            if (!initialized || controller == null || controller.Config == null ||
                controller.LifeState == EnemyLifeState.Dead)
                return;

            if (Time.time < lastPoiseDamageTime + controller.Config.PoiseRecoveryDelay)
                return;

            currentPoise = Mathf.MoveTowards(currentPoise, controller.Config.MaximumPoise,
                controller.Config.PoiseRecoveryPerSecond * Time.deltaTime);
        }

        public bool TryReceiveHit(in CombatHitRequest request,
            out CombatHitResolution resolution)
        {
            EnsureInitialized();
            if (!initialized || controller.LifeState == EnemyLifeState.Dead)
            {
                resolution = CombatHitResolution.Rejected;
                return false;
            }

            float appliedDamage = Mathf.Min(currentHealth, request.Damage);
            currentHealth = Mathf.Max(0f, currentHealth - request.Damage);

            bool poiseBroken = false;
            if (request.PoiseDamage > 0f && controller.Config.MaximumPoise > 0f)
            {
                currentPoise -= request.PoiseDamage;
                lastPoiseDamageTime = Time.time;
                if (currentPoise <= 0f)
                {
                    poiseBroken = true;
                    currentPoise = controller.Config.MaximumPoise;
                }
            }

            bool killed = currentHealth <= 0f;
            controller.PlayHitVisualShake();
            controller.PlayHitRecoil(
                request.Attacker != null ? request.Attacker.transform : null,
                request.AttackDirection);
            if (killed)
            {
                controller.NotifyDied();
            }
            else if (ShouldReact(request, poiseBroken))
            {
                float duration = request.StaggerDuration > 0f
                    ? request.StaggerDuration
                    : controller.Config.DefaultStaggerDuration;
                controller.NotifyStagger(duration);
            }

            resolution = new CombatHitResolution(true, CombatHitResultType.Normal,
                appliedDamage, request.PoiseDamage, killed);
            return true;
        }

        private static bool ShouldReact(in CombatHitRequest request, bool poiseBroken)
        {
            switch (request.HitReaction)
            {
                case CombatHitReactionPolicy.FirstHitOnly:
                    return request.HitSequenceIndex == 1;
                case CombatHitReactionPolicy.EveryHit:
                    return true;
                case CombatHitReactionPolicy.PoiseBreakOnly:
                    return poiseBroken;
                default:
                    return false;
            }
        }

        private void EnsureInitialized()
        {
            if (!initialized) InitializeVitals();
        }

        private void InitializeVitals()
        {
            if (controller == null || controller.Config == null)
                return;

            currentHealth = controller.Config.MaximumHealth;
            currentPoise = controller.Config.MaximumPoise;
            initialized = true;
        }
    }
}
