namespace UnityLearning.EnemySystem
{
    public sealed class EnemyDeadState : EnemyState
    {
        public EnemyDeadState(EnemyController controller) : base(controller) { }
        public override EnemyBehaviourState Id => EnemyBehaviourState.Dead;

        public override void Enter()
        {
            Controller.Motor?.Stop();
            Controller.Motor?.SetNavigationEnabled(false);
            Controller.Combat?.InterruptAttack();
            Controller.PlayDeath();
        }
    }
}
