using UnityEngine;

namespace UnityLearning.EnemySystem
{
    [DisallowMultipleComponent]
    public sealed class EnemyStateMachine : MonoBehaviour
    {
        public EnemyState CurrentState { get; private set; }
        public EnemyInactiveState InactiveState { get; private set; }
        public EnemyAlertState AlertState { get; private set; }
        public EnemyChaseState ChaseState { get; private set; }
        public EnemyCombatState CombatState { get; private set; }
        public EnemyStaggerState StaggerState { get; private set; }
        public EnemyDeadState DeadState { get; private set; }
        public EnemyBehaviourState CurrentStateId => CurrentState != null
            ? CurrentState.Id
            : EnemyBehaviourState.Inactive;
        public EnemyCombatTactic CurrentCombatTactic => CombatState != null &&
                                                        CurrentState == CombatState
            ? CombatState.CurrentTactic
            : EnemyCombatTactic.None;

        public void Initialize(EnemyController controller)
        {
            InactiveState = new EnemyInactiveState(controller);
            AlertState = new EnemyAlertState(controller);
            ChaseState = new EnemyChaseState(controller);
            CombatState = new EnemyCombatState(controller);
            StaggerState = new EnemyStaggerState(controller);
            DeadState = new EnemyDeadState(controller);
            ChangeState(InactiveState);
        }

        public void Tick(float deltaTime)
        {
            CurrentState?.Tick(deltaTime);
        }

        public void ChangeState(EnemyState nextState)
        {
            if (nextState == null || nextState == CurrentState)
                return;

            CurrentState?.Exit();
            CurrentState = nextState;
            CurrentState.Enter();
        }
    }
}
