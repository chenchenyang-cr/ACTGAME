namespace UnityLearning.EnemySystem
{
    public sealed class EnemySelectAttackTactic : EnemyCombatTacticalState
    {
        public EnemySelectAttackTactic(EnemyCombatTacticalStateMachine machine) : base(machine) { }
        public override EnemyCombatTactic Id => EnemyCombatTactic.SelectAttack;

        public override void Enter()
        {
            Blackboard.SelectedAttack = null;
            Controller.Motor?.Stop();
            Controller.PlayIdle();
        }

        public override void Tick(float deltaTime)
        {
            if (Controller.Target != null)
                Controller.Motor?.FaceTarget(Controller.Target.position, deltaTime);

            if (!Controller.Brain.TrySelectAttack(
                    Controller.DistanceToTarget(),
                    out EnemyAttackConfig attack))
                return;

            if (attack == null)
                return;

            Blackboard.SelectedAttack = attack;
            Machine.ChangeState(Machine.MoveToAttackRangeState);
        }
    }
}
