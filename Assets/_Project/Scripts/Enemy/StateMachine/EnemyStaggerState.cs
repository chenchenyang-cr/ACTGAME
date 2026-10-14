using CombatEditor;
using UnityEngine;

namespace UnityLearning.EnemySystem
{
    public sealed class EnemyStaggerState : EnemyState
    {
        private float remaining;
        private bool waitingForHitStop;
        private bool hitStopStarted;
        private int hitStopSequence;
        private float waitDeadline;

        public EnemyStaggerState(EnemyController controller) : base(controller) { }
        public override EnemyBehaviourState Id => EnemyBehaviourState.Stagger;

        public void WaitForParryHitStop()
        {
            waitingForHitStop = true;
            hitStopStarted = false;
            hitStopSequence = GlobalHitStopRuntime.StartSequence;
            // If the action is cancelled before its authored event fires, do not trap the enemy.
            waitDeadline = Time.realtimeSinceStartup + 3f;
        }

        public void ClearParryWait()
        {
            waitingForHitStop = false;
            hitStopStarted = false;
        }

        public void OnGlobalHitStopStarted()
        {
            if (!waitingForHitStop || hitStopStarted ||
                Controller.StateMachine.CurrentState != this)
                return;

            hitStopStarted = true;
            Controller.PlayStagger();
        }

        public override void Enter()
        {
            remaining = Controller.PendingStaggerDuration;
            Controller.Motor?.Stop();
            Controller.Combat?.InterruptAttack();
            if (!waitingForHitStop)
                Controller.PlayStagger();
        }

        public override void Tick(float deltaTime)
        {
            if (waitingForHitStop)
            {
                if (GlobalHitStopRuntime.StartSequence != hitStopSequence)
                    OnGlobalHitStopStarted();
                if (hitStopStarted && GlobalHitStopRuntime.IsActive)
                    return;
                if (!hitStopStarted && Time.realtimeSinceStartup < waitDeadline)
                    return;
                bool needsStagger = !hitStopStarted;
                ClearParryWait();
                if (needsStagger)
                    Controller.PlayStagger();
                return;
            }

            remaining -= deltaTime;
            if (remaining > 0f)
                return;

            EnemyState nextState = Controller.IsTargetWithin(
                Controller.Config.CombatEnterDistance)
                ? Controller.StateMachine.CombatState
                : Controller.StateMachine.ChaseState;
            Controller.StateMachine.ChangeState(nextState);
            // The next state's entry may request movement immediately; recover
            // from the hit pose through Idle before locomotion starts.
            Controller.PlayHitRecovery();
        }
    }
}
