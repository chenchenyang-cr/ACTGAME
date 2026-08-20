using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

namespace CombatEditor
{
    public interface ITurn180RootMotionHandler
    {
        void SetTurn180RootMotionActive(bool active);
    }

    public class RootMotionReceiver : MonoBehaviour, ITurn180RootMotionHandler
    {
        private Animator _animator;
        public Vector3 CurrentRootMotion { get; private set; }
        public Quaternion CurrentRootRotation { get; private set; } = Quaternion.identity;
        public float CurrentTurn180Weight { get; private set; }
        public int LastRootMotionFrame { get; private set; }
        private Func<Quaternion, Quaternion> rootRotationProcessor;
        private bool turn180RootMotionActive;
        private const string Turn180Tag = "LocomotionTurn180";

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        private void OnAnimatorMove()
        {
            CurrentRootMotion += _animator.deltaPosition;
            Quaternion animationDeltaRotation = _animator.deltaRotation;
            Quaternion deltaRotation = animationDeltaRotation;
            CurrentTurn180Weight = turn180RootMotionActive
                ? EvaluateTurn180Weight()
                : 0f;
            if (rootRotationProcessor != null)
            {
                Quaternion regularDeltaRotation =
                    rootRotationProcessor(animationDeltaRotation);
                float easedTurnWeight = SmoothStep01(CurrentTurn180Weight);
                deltaRotation = Quaternion.Slerp(
                    regularDeltaRotation,
                    animationDeltaRotation,
                    easedTurnWeight);
            }

            CurrentRootRotation *= deltaRotation;
            LastRootMotionFrame = Time.frameCount;
        }

        public void SetTurn180RootMotionActive(bool active)
        {
            turn180RootMotionActive = active;
            if (!active)
            {
                CurrentTurn180Weight = 0f;
            }
        }

        private float EvaluateTurn180Weight()
        {
            AnimatorStateInfo currentState = _animator.GetCurrentAnimatorStateInfo(0);
            bool currentIsTurn180 = currentState.IsTag(Turn180Tag);
            if (!_animator.IsInTransition(0))
            {
                return currentIsTurn180 ? 1f : 0f;
            }

            AnimatorStateInfo nextState = _animator.GetNextAnimatorStateInfo(0);
            bool nextIsTurn180 = nextState.IsTag(Turn180Tag);
            if (currentIsTurn180 == nextIsTurn180)
            {
                return currentIsTurn180 ? 1f : 0f;
            }

            float transitionProgress = Mathf.Clamp01(
                _animator.GetAnimatorTransitionInfo(0).normalizedTime);
            return nextIsTurn180
                ? transitionProgress
                : 1f - transitionProgress;
        }

        private static float SmoothStep01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        public void SetRootRotationProcessor(Func<Quaternion, Quaternion> processor)
        {
            rootRotationProcessor = processor;
        }

        public Vector3 ConsumeRootMotion()
        {
            Vector3 delta = CurrentRootMotion;
            CurrentRootMotion = Vector3.zero;
            return delta;
        }

        public Quaternion ConsumeRootRotation()
        {
            Quaternion delta = CurrentRootRotation;
            CurrentRootRotation = Quaternion.identity;
            return delta;
        }
    }
}
