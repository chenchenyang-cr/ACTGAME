using UnityEngine;

namespace UnityLearning.EnemySystem
{
    public sealed class EnemyApproachSlotTactic : EnemySpatialTacticalState
    {
        public EnemyApproachSlotTactic(EnemyCombatTacticalStateMachine machine)
            : base(machine) { }

        public override EnemyCombatTactic Id => EnemyCombatTactic.ApproachSlot;

        public override void Tick(float deltaTime)
        {
            if (TryBeginAttack() || TryYield())
                return;

            EncounterCombatDirector director = Controller.CombatDirector;
            if (director == null ||
                !director.TryGetCombatAssignment(Controller, out EnemyCombatAssignment assignment))
            {
                Controller.Motor?.Stop();
                Controller.PlayIdle();
                FaceTarget(deltaTime);
                return;
            }

            if (director.IsInsideConfrontationRegion(
                    Controller.transform.position,
                    assignment,
                    director.SlotArrivalTolerance))
            {
                Machine.ChangeState(Machine.OrbitState);
                return;
            }

            // 只寻找当前位置进入扇区的最近边界，不再追逐固定的槽位中心点。
            if (director.TryGetNearestConfrontationPosition(
                    Controller,
                    assignment,
                    out Vector3 entryPosition))
                MoveTo(entryPosition, deltaTime);
            else
            {
                Controller.Motor?.Stop();
                Controller.PlayIdle();
                FaceTarget(deltaTime);
            }
        }
    }
}
