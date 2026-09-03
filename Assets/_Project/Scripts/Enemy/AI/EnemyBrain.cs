using System.Collections.Generic;
using UnityEngine;

namespace UnityLearning.EnemySystem
{
    public sealed class EnemyBrain : MonoBehaviour
    {
        private readonly Dictionary<EnemyAttackConfig, float> nextReadyTimes = new();
        private EnemyConfig config;
        private float nextDecisionTime;

        public void Initialize(EnemyConfig enemyConfig)
        {
            config = enemyConfig;
            nextReadyTimes.Clear();
            nextDecisionTime = 0f;
        }

        public bool TrySelectAttack(
            float distanceToTarget,
            out EnemyAttackConfig selected,
            bool allowLongApproach = false)
        {
            selected = null;
            if (config == null || config.Attacks == null || Time.time < nextDecisionTime)
                return false;

            nextDecisionTime = Time.time + config.DecisionInterval;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < config.Attacks.Length; i++)
            {
                EnemyAttackConfig attack = config.Attacks[i];
                if (attack == null || attack.Ability == null || !IsReady(attack))
                    continue;
                if (!allowLongApproach &&
                    distanceToTarget > attack.MaximumRange + config.AttackApproachAllowance)
                    continue;

                float rangeError = Mathf.Abs(distanceToTarget - attack.PreferredRange);
                float score = attack.Priority - rangeError;
                if (score > bestScore)
                {
                    bestScore = score;
                    selected = attack;
                }
            }

            return true;
        }

        public void MarkAttackUsed(EnemyAttackConfig attack)
        {
            if (attack != null) nextReadyTimes[attack] = Time.time + attack.Cooldown;
        }

        private bool IsReady(EnemyAttackConfig attack)
        {
            return !nextReadyTimes.TryGetValue(attack, out float readyTime) ||
                   Time.time >= readyTime;
        }
    }
}
