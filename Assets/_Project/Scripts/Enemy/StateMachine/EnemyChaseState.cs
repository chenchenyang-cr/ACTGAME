namespace UnityLearning.EnemySystem
{
    public sealed class EnemyChaseState : EnemyState
    {
        public EnemyChaseState(EnemyController controller) : base(controller) { }
        public override EnemyBehaviourState Id => EnemyBehaviourState.Chase;

        public override void Enter()
        {
            Controller.Motor?.SetSpeed(Controller.Config.ChaseSpeed);
            Controller.PlayLocomotion();
        }

        public override void Tick(float deltaTime)
        {
            if (Controller.Target == null ||
                !Controller.IsTargetWithin(Controller.Config.LoseTargetDistance))
            {
                Controller.StateMachine.ChangeState(Controller.StateMachine.InactiveState);
                return;
            }

            if (Controller.IsTargetWithin(Controller.Config.CombatEnterDistance))
            {
                Controller.Motor?.Stop();
                Controller.StateMachine.ChangeState(Controller.StateMachine.CombatState);
                return;
            }

            Controller.Motor?.MoveTo(Controller.Target.position);
            Controller.Motor?.FaceMovement(deltaTime);
        }
    }
}
