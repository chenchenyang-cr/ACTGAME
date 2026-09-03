using UnityEngine;

namespace UnityLearning.EnemySystem
{
    public abstract class EnemySpatialTacticalState : EnemyCombatTacticalState
    {
        protected EnemySpatialTacticalState(EnemyCombatTacticalStateMachine machine)
            : base(machine) { }

        protected bool TryBeginAttack()
        {
            return Machine.TryBeginAttack();
        }

        protected bool TryYield()
        {
            EncounterCombatDirector director = Controller.CombatDirector;
            if (director == null ||
                !director.ShouldYield(Controller, out Vector3 yieldDirection))
                return false;

            Machine.YieldState.SetYieldDirection(yieldDirection);
            Machine.ChangeState(Machine.YieldState);
            return true;
        }

        protected bool MoveTo(Vector3 destination, float deltaTime)
        {
            EnemyMotor motor = Controller.Motor;
            if (motor == null)
                return false;
            Controller.PlayLocomotion();
            bool accepted = motor.MoveTo(destination);
            FaceTarget(deltaTime);
            return accepted;
        }
    }
}
