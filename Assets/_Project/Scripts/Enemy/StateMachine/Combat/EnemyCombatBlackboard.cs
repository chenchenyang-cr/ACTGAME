namespace UnityLearning.EnemySystem
{
    public sealed class EnemyCombatBlackboard
    {
        public EnemyAttackConfig SelectedAttack { get; set; }

        public void Reset()
        {
            SelectedAttack = null;
        }
    }
}
