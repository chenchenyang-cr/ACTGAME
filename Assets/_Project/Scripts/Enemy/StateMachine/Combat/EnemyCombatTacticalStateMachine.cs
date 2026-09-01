using UnityEngine;

namespace UnityLearning.EnemySystem
{
    public sealed class EnemyCombatTacticalStateMachine
    {
        public EnemyCombatTacticalStateMachine(EnemyController controller)
        {
            Controller = controller;
            Blackboard = new EnemyCombatBlackboard();
            SelectAttackState = new EnemySelectAttackTactic(this);
            MoveToAttackRangeState = new EnemyMoveToAttackRangeTactic(this);
            ExecuteAttackState = new EnemyExecuteAttackTactic(this);
            RecoverState = new EnemyRecoverTactic(this);
        }

        public EnemyController Controller { get; }
        public EnemyCombatBlackboard Blackboard { get; }
        public EnemyCombatTacticalState CurrentState { get; private set; }
        public EnemyCombatTactic CurrentTactic => CurrentState != null
            ? CurrentState.Id
            : EnemyCombatTactic.None;
        public bool IsRunning { get; private set; }

        public EnemySelectAttackTactic SelectAttackState { get; }
        public EnemyMoveToAttackRangeTactic MoveToAttackRangeState { get; }
        public EnemyExecuteAttackTactic ExecuteAttackState { get; }
        public EnemyRecoverTactic RecoverState { get; }

        public void Start()
        {
            if (IsRunning) return;
            IsRunning = true;
            Blackboard.Reset();
            ChangeState(SelectAttackState);
        }

        public void Tick(float deltaTime)
        {
            if (IsRunning) CurrentState?.Tick(deltaTime);
        }

        public void Stop()
        {
            if (!IsRunning) return;
            IsRunning = false;
            CurrentState?.Exit();
            CurrentState = null;
            Blackboard.Reset();
        }

        public void ChangeState(EnemyCombatTacticalState nextState)
        {
            if (!IsRunning || nextState == null || nextState == CurrentState)
                return;

            CurrentState?.Exit();
            CurrentState = nextState;
            CurrentState.Enter();
        }
    }
}
