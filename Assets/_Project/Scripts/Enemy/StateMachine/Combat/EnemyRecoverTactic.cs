namespace UnityLearning.EnemySystem
{
    public sealed class EnemyRecoverTactic : EnemyCombatTacticalState
    {
        private float remaining;

        public EnemyRecoverTactic(EnemyCombatTacticalStateMachine machine) : base(machine) { }
        public override EnemyCombatTactic Id => EnemyCombatTactic.Recover;

        public override void Enter()
        {
            remaining = Controller.Config.PostAttackRecovery;
            Blackboard.SelectedAttack = null;
            Controller.Motor?.Stop();
            Controller.PlayIdle();
        }

        public override void Tick(float deltaTime)
        {
            if (Controller.Target != null)
                Controller.Motor?.FaceTarget(Controller.Target.position, deltaTime);

            remaining -= deltaTime;
            if (remaining <= 0f)
                Machine.ChangeState(Machine.SelectAttackState);
        }
    }
}
