namespace UnityLearning.EnemySystem
{
    public sealed class EnemyAlertState : EnemyState
    {
        private float elapsed;

        public EnemyAlertState(EnemyController controller) : base(controller) { }
        public override EnemyBehaviourState Id => EnemyBehaviourState.Alert;

        public override void Enter()
        {
            elapsed = 0f;
            Controller.Motor?.Stop();
            Controller.PlayAlert();
        }

        public override void Tick(float deltaTime)
        {
            if (Controller.Target == null ||
                !Controller.IsTargetWithin(Controller.Config.LoseTargetDistance))
            {
                Controller.StateMachine.ChangeState(Controller.StateMachine.InactiveState);
                return;
            }

            Controller.Motor?.FaceTarget(Controller.Target.position, deltaTime);
            elapsed += deltaTime;
            if (elapsed >= Controller.Config.AlertDuration)
                Controller.StateMachine.ChangeState(Controller.StateMachine.ChaseState);
        }
    }
}
