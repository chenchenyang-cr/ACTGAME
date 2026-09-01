namespace UnityLearning.EnemySystem
{
    public sealed class EnemyExecuteAttackTactic : EnemyCombatTacticalState
    {
        private bool completed;

        public EnemyExecuteAttackTactic(EnemyCombatTacticalStateMachine machine) : base(machine) { }
        public override EnemyCombatTactic Id => EnemyCombatTactic.ExecuteAttack;

        public override void Enter()
        {
            completed = false;
            Controller.Motor?.Stop();
            if (Blackboard.SelectedAttack == null || Controller.Combat == null ||
                !Controller.Combat.BeginAttack(Blackboard.SelectedAttack))
                Machine.ChangeState(Machine.SelectAttackState);
        }

        public override void Tick(float deltaTime)
        {
            if (Controller.Target != null)
                Controller.Motor?.FaceTarget(Controller.Target.position, deltaTime);

            if (Controller.Combat == null || Controller.Combat.IsAttackComplete())
                CompleteAttack();
        }

        public override void Exit()
        {
            if (!completed) Controller.Combat?.InterruptAttack();
        }

        private void CompleteAttack()
        {
            if (completed) return;
            completed = true;
            Controller.Brain?.MarkAttackUsed(Blackboard.SelectedAttack);
            Controller.Combat?.EndAttack();
            Machine.ChangeState(Machine.RecoverState);
        }
    }
}
