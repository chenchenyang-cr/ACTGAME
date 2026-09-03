using UnityEngine;

namespace UnityLearning.EnemySystem
{
    public sealed class EnemyCombatTacticalStateMachine
    {
        public EnemyCombatTacticalStateMachine(EnemyController controller)
        {
            Controller = controller;
            ApproachSlotState = new EnemyApproachSlotTactic(this);
            OrbitState = new EnemyOrbitTactic(this);
            PressureState = new EnemyPressureTactic(this);
            YieldState = new EnemyYieldTactic(this);
            MoveToAttackRangeState = new EnemyMoveToAttackRangeTactic(this);
            ExecuteAttackState = new EnemyExecuteAttackTactic(this);
            RecoverState = new EnemyRecoverTactic(this);
            RetreatState = new EnemyRetreatTactic(this);
        }

        public EnemyController Controller { get; }
        public EnemyAttackConfig SelectedAttack { get; private set; }
        public EnemyCombatTacticalState CurrentState { get; private set; }
        public EnemyCombatTactic CurrentTactic => CurrentState != null
            ? CurrentState.Id
            : EnemyCombatTactic.None;
        public bool IsRunning { get; private set; }

        public EnemyApproachSlotTactic ApproachSlotState { get; }
        public EnemyOrbitTactic OrbitState { get; }
        public EnemyPressureTactic PressureState { get; }
        public EnemyYieldTactic YieldState { get; }
        public EnemyMoveToAttackRangeTactic MoveToAttackRangeState { get; }
        public EnemyExecuteAttackTactic ExecuteAttackState { get; }
        public EnemyRecoverTactic RecoverState { get; }
        public EnemyRetreatTactic RetreatState { get; }

        public void Start()
        {
            if (IsRunning) return;
            IsRunning = true;
            SelectedAttack = null;
            ChangeState(ApproachSlotState);
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
            Controller.ReleaseAttackToken();
            CurrentState = null;
            SelectedAttack = null;
        }

        public bool TryBeginAttack()
        {
            if (!IsRunning || SelectedAttack != null ||
                !Controller.Brain.TrySelectAttack(
                    Controller.DistanceToTarget(),
                    out EnemyAttackConfig attack,
                    allowLongApproach: true) ||
                attack == null || !Controller.TryAcquireAttackToken())
                return false;

            SelectedAttack = attack;
            ChangeState(MoveToAttackRangeState);
            return true;
        }

        public void ClearSelectedAttack()
        {
            SelectedAttack = null;
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
