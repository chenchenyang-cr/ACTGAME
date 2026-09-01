namespace UnityLearning.EnemySystem
{
    public enum EnemyLifeState
    {
        Alive,
        Dead
    }

    public enum EnemyBehaviourState
    {
        Inactive,
        Alert,
        Chase,
        Combat,
        Stagger,
        Dead
    }

    public enum EnemyCombatTactic
    {
        None,
        SelectAttack,
        MoveToAttackRange,
        ExecuteAttack,
        Recover
    }
}
