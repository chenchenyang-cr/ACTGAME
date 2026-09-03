using UnityEngine;

namespace UnityLearning.EnemySystem
{
    public sealed class EnemyYieldTactic : EnemySpatialTacticalState
    {
        private Vector3 yieldDirection;
        private float remaining;

        public EnemyYieldTactic(EnemyCombatTacticalStateMachine machine)
            : base(machine) { }

        public override EnemyCombatTactic Id => EnemyCombatTactic.Yield;

        public void SetYieldDirection(Vector3 direction)
        {
            direction.y = 0f;
            yieldDirection = direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : Controller.transform.right;
        }

        public override void Enter()
        {
            remaining = Controller.CombatDirector != null
                ? Controller.CombatDirector.YieldDuration
                : 0.7f;
        }

        public override void Tick(float deltaTime)
        {
            EncounterCombatDirector director = Controller.CombatDirector;
            if (director == null)
            {
                Machine.ChangeState(Machine.ApproachSlotState);
                return;
            }

            remaining -= deltaTime;
            Vector3 desired = Controller.transform.position +
                              yieldDirection * director.YieldDistance;
            if (director.TryProjectSpatialPosition(Controller, desired, out Vector3 projected))
                MoveTo(projected, deltaTime);

            if (remaining <= 0f || !director.ShouldYield(Controller, out _))
                Machine.ChangeState(Machine.ApproachSlotState);
        }
    }
}
