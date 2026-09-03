using UnityEngine;

namespace UnityLearning.EnemySystem
{
    public sealed class EnemyPressureTactic : EnemySpatialTacticalState
    {
        private float remaining;

        public EnemyPressureTactic(EnemyCombatTacticalStateMachine machine)
            : base(machine) { }

        public override EnemyCombatTactic Id => EnemyCombatTactic.Pressure;

        public override void Enter()
        {
            remaining = Controller.CombatDirector != null
                ? Controller.CombatDirector.PressureDuration
                : 0.8f;
        }

        public override void Tick(float deltaTime)
        {
            EncounterCombatDirector director = Controller.CombatDirector;
            if (director == null ||
                !director.TryGetCombatAssignment(Controller, out EnemyCombatAssignment assignment))
            {
                Machine.ChangeState(Machine.OrbitState);
                return;
            }

            remaining -= deltaTime;
            Vector3 fromTarget = Controller.transform.position - director.Target.position;
            fromTarget.y = 0f;
            Vector3 currentRadial = fromTarget.sqrMagnitude > 0.001f
                ? fromTarget.normalized
                : assignment.RadialDirection;
            float radius = Mathf.Max(
                assignment.AttackRadius + 0.3f,
                fromTarget.magnitude - director.PressureStepDistance);
            Vector3 desired = director.Target.position +
                              currentRadial * radius;
            if (director.TryProjectSpatialPosition(Controller, desired, out Vector3 projected))
                MoveTo(projected, deltaTime);
            else
                remaining = 0f;

            if (remaining <= 0f ||
                (Controller.Motor != null &&
                 Controller.Motor.HasReached(desired, director.SlotArrivalTolerance)))
                Machine.ChangeState(Machine.OrbitState);
        }
    }
}
