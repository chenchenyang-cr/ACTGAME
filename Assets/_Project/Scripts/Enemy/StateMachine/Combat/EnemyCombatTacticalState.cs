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
        protected EnemyAttackConfig SelectedAttack => Machine.SelectedAttack;
        public abstract EnemyCombatTactic Id { get; }

        public virtual void Enter() { }
        public virtual void Tick(float deltaTime) { }
        public virtual void Exit() { }

        protected void FaceTarget(float deltaTime)
        {
            if (Controller.Target != null)
                Controller.Motor?.FaceTarget(Controller.Target.position, deltaTime);
        }
    }
}
