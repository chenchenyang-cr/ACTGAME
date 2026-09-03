namespace UnityLearning.EnemySystem
{
    public sealed class EnemyCombatState : EnemyState
    {
        private readonly EnemyCombatTacticalStateMachine tacticalStateMachine;

        public EnemyCombatState(EnemyController controller) : base(controller)
        {
            tacticalStateMachine = new EnemyCombatTacticalStateMachine(controller);
        }

        public override EnemyBehaviourState Id => EnemyBehaviourState.Combat;
        public EnemyCombatTactic CurrentTactic => tacticalStateMachine.CurrentTactic;

        public override void Enter()
        {
            tacticalStateMachine.Start();
        }

        public override void Tick(float deltaTime)
        {
            if (Controller.Target == null ||
                !Controller.IsTargetWithin(Controller.Config.LoseTargetDistance))
            {
                Controller.StateMachine.ChangeState(Controller.StateMachine.InactiveState);
                return;
            }

            if (Controller.DistanceToTarget() > Controller.Config.ChaseResumeDistance &&
                tacticalStateMachine.CurrentTactic != EnemyCombatTactic.ExecuteAttack &&
                tacticalStateMachine.CurrentTactic != EnemyCombatTactic.MoveToAttackRange)
            {
                Controller.StateMachine.ChangeState(Controller.StateMachine.ChaseState);
                return;
            }

            tacticalStateMachine.Tick(deltaTime);
        }

        public override void Exit()
        {
            tacticalStateMachine.Stop();
            Controller.Motor?.Stop();
            Controller.Combat?.InterruptAttack();
        }
    }
}
