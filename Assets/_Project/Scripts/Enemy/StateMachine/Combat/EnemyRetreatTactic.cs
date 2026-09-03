using UnityEngine;

namespace UnityLearning.EnemySystem
{
    public sealed class EnemyRetreatTactic : EnemySpatialTacticalState
    {
        private float cooldownRemaining;

        public EnemyRetreatTactic(EnemyCombatTacticalStateMachine machine)
            : base(machine) { }

        public override EnemyCombatTactic Id => EnemyCombatTactic.Retreat;

        public override void Enter()
        {
            Controller.ReleaseAttackToken();
            Machine.ClearSelectedAttack();
            cooldownRemaining = Controller.Config.PostAttackRecovery;
        }

        public override void Tick(float deltaTime)
        {
            cooldownRemaining -= deltaTime;
            EncounterCombatDirector director = Controller.CombatDirector;
            if (director == null ||
                !director.TryGetCombatAssignment(Controller, out EnemyCombatAssignment assignment))
            {
                if (cooldownRemaining <= 0f)
                {
                    Machine.ChangeState(Machine.ApproachSlotState);
                }
                return;
            }

            bool reached = director.IsInsideConfrontationRegion(
                Controller.transform.position,
                assignment,
                director.SlotArrivalTolerance);
            if (reached)
            {
                Controller.Motor.Stop();
                Controller.PlayIdle();
                FaceTarget(deltaTime);
            }
            else
            {
                if (director.TryGetNearestConfrontationPosition(
                        Controller,
                        assignment,
                        out Vector3 retreatPosition))
                    MoveTo(retreatPosition, deltaTime);
            }
            if (reached && cooldownRemaining <= 0f)
            {
                Machine.ChangeState(Machine.OrbitState);
            }
        }
    }
}
