namespace UnityLearning.EnemySystem
{
    public abstract class EnemyState
    {
        protected EnemyState(EnemyController controller)
        {
            Controller = controller;
        }

        protected EnemyController Controller { get; }
        public abstract EnemyBehaviourState Id { get; }

        public virtual void Enter() { }
        public virtual void Tick(float deltaTime) { }
        public virtual void Exit() { }
    }
}
