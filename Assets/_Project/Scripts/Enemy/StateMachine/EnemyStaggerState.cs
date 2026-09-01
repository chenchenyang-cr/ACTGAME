namespace UnityLearning.EnemySystem
{
    public sealed class EnemyStaggerState : EnemyState
    {
        private float remaining;

        public EnemyStaggerState(EnemyController controller) : base(controller) { }
        public override EnemyBehaviourState Id => EnemyBehaviourState.Stagger;

        public override void Enter()
        {
            remaining = Controller.PendingStaggerDuration;
            Controller.Motor?.Stop();
            Controller.Combat?.InterruptAttack();
            Controller.PlayStagger();
        }

        public override void Tick(float deltaTime)
        {
            remaining -= deltaTime;
            if (remaining > 0f)
                return;

            EnemyState nextState = Controller.IsTargetWithin(
                Controller.Config.CombatEnterDistance)
                ? Controller.StateMachine.CombatState
                : Controller.StateMachine.ChaseState;
            Controller.StateMachine.ChangeState(nextState);
        }
    }
}
