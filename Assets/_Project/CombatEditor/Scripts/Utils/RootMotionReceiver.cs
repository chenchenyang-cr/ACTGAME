using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

namespace CombatEditor
{
    public class RootMotionReceiver : MonoBehaviour
    {
        private Animator _animator;
        public Vector3 CurrentRootMotion { get; private set; }
        public Quaternion CurrentRootRotation { get; private set; } = Quaternion.identity;
        public int LastRootMotionFrame { get; private set; }
        private Func<Quaternion, Quaternion> rootRotationProcessor;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        private void OnAnimatorMove()
        {
            CurrentRootMotion += _animator.deltaPosition;
            Quaternion deltaRotation = _animator.deltaRotation;
            if (rootRotationProcessor != null)
            {
                deltaRotation = rootRotationProcessor(deltaRotation);
            }

            CurrentRootRotation *= deltaRotation;
            LastRootMotionFrame = Time.frameCount;
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
