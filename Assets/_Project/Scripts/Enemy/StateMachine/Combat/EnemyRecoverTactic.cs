namespace UnityLearning.EnemySystem
{
    public sealed class EnemyRecoverTactic : EnemyCombatTacticalState
    {
        private float remaining;

        public EnemyRecoverTactic(EnemyCombatTacticalStateMachine machine) : base(machine) { }
        public override EnemyCombatTactic Id => EnemyCombatTactic.AttackRecovery;

        public override void Enter()
        {
            Controller.ReleaseAttackToken();
            remaining = UnityEngine.Mathf.Min(
                0.2f,
                Controller.Config.PostAttackRecovery * 0.25f);
            Controller.Motor?.Stop();
            Controller.PlayIdle();
        }

        public override void Tick(float deltaTime)
        {
            if (Controller.Target != null)
                Controller.Motor?.FaceTarget(Controller.Target.position, deltaTime);

            remaining -= deltaTime;
            if (remaining <= 0f)
                Machine.ChangeState(Machine.RetreatState);
        }
    }
}
