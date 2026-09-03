using UnityEngine;

namespace UnityLearning.EnemySystem
{
    public sealed class EnemyOrbitTactic : EnemySpatialTacticalState
    {
        private readonly System.Random random;
        private float phaseRemaining;
        private float targetAngleOffset;
        private float targetRadius;
        private Vector3 currentTargetPosition;
        private bool walking;
        private bool retryingTargetSelection;
        private bool waitingForStopAnimation;

        public EnemyOrbitTactic(EnemyCombatTacticalStateMachine machine)
            : base(machine)
        {
            random = new System.Random(machine.Controller.GetInstanceID() * 397);
        }

        public override EnemyCombatTactic Id => EnemyCombatTactic.Orbit;

        public override void Enter()
        {
            // 无论来自 Approach、Pressure 还是 Retreat，都先完整停稳再开始首个目标。
            walking = false;
            retryingTargetSelection = true;
            waitingForStopAnimation = true;
            phaseRemaining = GetIdleDuration();
            Controller.Motor?.Stop();
            Controller.PlayIdle();
        }

        public override void Tick(float deltaTime)
        {
            if (TryBeginAttack() || TryYield())
                return;

            EncounterCombatDirector director = Controller.CombatDirector;
            if (director == null ||
                !director.TryGetCombatAssignment(Controller, out EnemyCombatAssignment assignment))
            {
                Machine.ChangeState(Machine.ApproachSlotState);
                return;
            }

            if (!director.IsInsideConfrontationRegion(
                    Controller.transform.position,
                    assignment,
                    director.CloseGapThreshold))
            {
                Machine.ChangeState(Machine.ApproachSlotState);
                return;
            }

            if (walking)
            {
                if (!director.TryGetConfrontationPosition(
                        Controller,
                        assignment,
                        targetAngleOffset,
                        targetRadius,
                        out currentTargetPosition))
                {
                    BeginTargetRetry(director, deltaTime);
                    return;
                }

                MoveTo(currentTargetPosition, deltaTime);
                phaseRemaining -= deltaTime;
                bool reached = Controller.Motor != null &&
                               Controller.Motor.HasReached(
                                   currentTargetPosition,
                                   director.SlotArrivalTolerance * 1.25f);
                if (reached || phaseRemaining <= 0f)
                {
                    if (random.NextDouble() < director.OrbitWaitChance)
                    {
                        BeginStoppedPause(
                            director,
                            retryTargetSelection: false,
                            pauseDuration: GetIdleDuration());
                    }
                    else
                    {
                        ContinueToNextTarget(director, deltaTime);
                    }
                }
                return;
            }

            Controller.Motor?.Stop();
            Controller.PlayIdle();
            FaceTarget(deltaTime);
            if (waitingForStopAnimation)
            {
                if (!Controller.IsLocomotionSettledInIdle())
                    return;

                // 从完全停稳后的下一帧才开始计算额外等待时间。
                waitingForStopAnimation = false;
                return;
            }

            phaseRemaining -= deltaTime;
            if (phaseRemaining > 0f)
                return;

            if (retryingTargetSelection)
            {
                walking = TrySelectRoamTarget(false);
                retryingTargetSelection = !walking;
                waitingForStopAnimation = false;
                phaseRemaining = walking
                    ? GetWalkDuration()
                    : GetTargetRetryPauseDuration();
                return;
            }
            SelectNextPhase(director);
        }

        private void SelectNextPhase(EncounterCombatDirector director)
        {
            double sample = random.NextDouble();
            if (sample < director.PressureChance)
            {
                Machine.ChangeState(Machine.PressureState);
                return;
            }

            bool preferOppositeSide = sample <
                                      director.PressureChance +
                                      director.OrbitReverseChance;
            walking = TrySelectRoamTarget(preferOppositeSide);
            retryingTargetSelection = !walking;
            waitingForStopAnimation = false;
            phaseRemaining = walking
                ? GetWalkDuration()
                : GetTargetRetryPauseDuration();
        }

        private bool TrySelectRoamTarget(bool preferOppositeSide)
        {
            EncounterCombatDirector director = Controller.CombatDirector;
            if (director == null ||
                !director.TryGetCombatAssignment(
                    Controller,
                    out EnemyCombatAssignment assignment))
                return false;

            Vector3 fromTarget = Controller.transform.position - director.Target.position;
            fromTarget.y = 0f;
            float currentRadius = Mathf.Clamp(
                fromTarget.magnitude,
                assignment.ConfrontationMinRadius,
                assignment.ConfrontationMaxRadius);
            float currentAngle = fromTarget.sqrMagnitude > 0.001f
                ? Vector3.SignedAngle(
                    assignment.RadialDirection,
                    fromTarget.normalized,
                    Vector3.up)
                : 0f;

            const int maximumAttempts = 6;
            float angleInset = Mathf.Min(2f, assignment.HalfAngle * 0.3f);
            float radialInset = Mathf.Min(
                0.12f,
                (assignment.ConfrontationMaxRadius -
                 assignment.ConfrontationMinRadius) * 0.25f);
            for (int attempt = 0; attempt < maximumAttempts; attempt++)
            {
                float angleSample = RandomRange(
                    -assignment.HalfAngle + angleInset,
                    assignment.HalfAngle - angleInset);
                if (preferOppositeSide)
                {
                    float side = currentAngle >= 0f ? -1f : 1f;
                    angleSample = side * RandomRange(
                        assignment.HalfAngle * 0.35f,
                        assignment.HalfAngle - angleInset);
                }

                float randomRadius = RandomRange(
                    assignment.ConfrontationMinRadius + radialInset,
                    assignment.ConfrontationMaxRadius - radialInset);
                float radiusSample = Mathf.Lerp(
                    currentRadius,
                    randomRadius,
                    director.OrbitRadialFreedom);
                if (!director.TryGetConfrontationPosition(
                        Controller,
                        assignment,
                        angleSample,
                        radiusSample,
                        out Vector3 projected))
                    continue;

                Vector3 delta = projected - Controller.transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude < director.OrbitMinimumTargetDistance *
                    director.OrbitMinimumTargetDistance)
                    continue;

                targetAngleOffset = angleSample;
                targetRadius = radiusSample;
                currentTargetPosition = projected;
                return true;
            }

            director.ReleaseConfrontationReservation(Controller);
            return false;
        }

        private void BeginTargetRetry(
            EncounterCombatDirector director,
            float deltaTime)
        {
            BeginStoppedPause(
                director,
                retryTargetSelection: true,
                pauseDuration: GetTargetRetryPauseDuration());
            FaceTarget(deltaTime);
        }

        private void ContinueToNextTarget(
            EncounterCombatDirector director,
            float deltaTime)
        {
            bool preferOppositeSide = random.NextDouble() < director.OrbitReverseChance;
            if (TrySelectRoamTarget(preferOppositeSide))
            {
                walking = true;
                retryingTargetSelection = false;
                waitingForStopAnimation = false;
                phaseRemaining = GetWalkDuration();
                MoveTo(currentTargetPosition, deltaTime);
                return;
            }

            // 没有找到有效目标时必须停下，不能保留已经失效的旧路径。
            BeginTargetRetry(director, deltaTime);
        }

        private void BeginStoppedPause(
            EncounterCombatDirector director,
            bool retryTargetSelection,
            float pauseDuration)
        {
            walking = false;
            retryingTargetSelection = retryTargetSelection;
            waitingForStopAnimation = true;
            phaseRemaining = pauseDuration;
            director.ReleaseConfrontationReservation(Controller);
            Controller.Motor?.Stop();
            Controller.PlayIdle();
        }

        private float GetWalkDuration()
        {
            EncounterCombatDirector director = Controller.CombatDirector;
            return director != null
                ? RandomRange(director.OrbitWalkDurationMin, director.OrbitWalkDurationMax)
                : RandomRange(0.8f, 2f);
        }

        private float GetIdleDuration()
        {
            EncounterCombatDirector director = Controller.CombatDirector;
            return director != null
                ? RandomRange(director.OrbitIdleDurationMin, director.OrbitIdleDurationMax)
                : RandomRange(0.3f, 0.8f);
        }

        private float GetTargetRetryPauseDuration()
        {
            EncounterCombatDirector director = Controller.CombatDirector;
            return director != null
                ? RandomRange(
                    director.OrbitTargetRetryPauseMin,
                    director.OrbitTargetRetryPauseMax)
                : RandomRange(0.18f, 0.32f);
        }

        private float RandomRange(float minimum, float maximum)
        {
            return Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
        }
    }
}
