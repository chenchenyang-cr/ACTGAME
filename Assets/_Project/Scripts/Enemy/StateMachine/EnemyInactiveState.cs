namespace UnityLearning.EnemySystem
{
    public sealed class EnemyInactiveState : EnemyState
    {
        public EnemyInactiveState(EnemyController controller) : base(controller) { }
        public override EnemyBehaviourState Id => EnemyBehaviourState.Inactive;

        public override void Enter()
        {
            Controller.Motor?.Stop();
            Controller.Combat?.InterruptAttack();
            Controller.PlayIdle();
        }

        public override void Tick(float deltaTime)
        {
            if (Controller.Config != null &&
                Controller.IsTargetWithin(Controller.Config.DetectionDistance))
                Controller.StateMachine.ChangeState(Controller.StateMachine.AlertState);
        }
    }
}
