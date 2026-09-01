using UnityEngine;

namespace UnityLearning.EnemySystem
{
    public sealed class EnemyMoveToAttackRangeTactic : EnemyCombatTacticalState
    {
        public EnemyMoveToAttackRangeTactic(EnemyCombatTacticalStateMachine machine) : base(machine) { }
        public override EnemyCombatTactic Id => EnemyCombatTactic.MoveToAttackRange;

        public override void Enter()
        {
            Controller.Motor?.SetSpeed(Controller.Config.CombatSpeed);
            Controller.PlayLocomotion();
        }

        public override void Tick(float deltaTime)
        {
            EnemyAttackConfig attack = Blackboard.SelectedAttack;
            Transform target = Controller.Target;
            EnemyMotor motor = Controller.Motor;
            if (attack == null || target == null || motor == null)
            {
                Machine.ChangeState(Machine.SelectAttackState);
                return;
            }

            float distance = Controller.DistanceToTarget();
            bool inRange = distance >= attack.MinimumRange &&
                           distance <= attack.MaximumRange;
            bool facing = motor.IsFacing(target.position, attack.FacingTolerance);
            if (inRange && facing)
            {
                motor.Stop();
                Machine.ChangeState(Machine.ExecuteAttackState);
                return;
            }

            Vector3 outward = Controller.transform.position - target.position;
            outward.y = 0f;
            if (outward.sqrMagnitude <= 0.0001f) outward = -target.forward;
            Vector3 entryPosition = target.position +
                                    outward.normalized * attack.PreferredRange;
            motor.MoveTo(entryPosition);
            if (motor.HasReached(entryPosition, attack.EntryTolerance))
                motor.FaceTarget(target.position, deltaTime);
            else
                motor.FaceMovement(deltaTime);
        }
    }
}
