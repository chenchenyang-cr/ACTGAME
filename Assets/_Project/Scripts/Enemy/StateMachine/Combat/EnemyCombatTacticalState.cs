namespace UnityLearning.EnemySystem
{
    public abstract class EnemyCombatTacticalState
    {
        protected EnemyCombatTacticalState(EnemyCombatTacticalStateMachine machine)
        {
            Machine = machine;
        }

        protected EnemyCombatTacticalStateMachine Machine { get; }
        protected EnemyController Controller => Machine.Controller;
        protected EnemyCombatBlackboard Blackboard => Machine.Blackboard;
        public abstract EnemyCombatTactic Id { get; }

        public virtual void Enter() { }
        public virtual void Tick(float deltaTime) { }
        public virtual void Exit() { }
    }
}
