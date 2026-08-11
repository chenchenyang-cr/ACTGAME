using System.Collections;
using System.Collections.Generic;
using UnityEngine;

 namespace CombatEditor
	{
	    public class MoveExecutor
	    {
	    public CombatController _combatController;
        RootMotionReceiver receiver;
        CharacterController characterController;
        public MoveExecutor(CombatController _controller)
        {
            _combatController = _controller;
            receiver = _combatController._animator.GetComponent<RootMotionReceiver>();
            if (receiver == null)
            {
                Debug.LogError("RootMotionReceiver is missing. Add it to the Animator object in Edit Mode.", _combatController._animator);
            }
            characterController = _combatController.GetComponent<CharacterController>();
        }
	    public void Execute()
	    {
	        
	    }
        /// <summary>
        /// Remember to change this to the physics you desire.
        /// </summary>
        /// <param name="DeltaMove"></param>
	    public void Move(Vector3 DeltaMove)
	    {
            if (characterController != null && characterController.enabled)
            {
                characterController.Move(DeltaMove);
                return;
            }

            _combatController.transform.Translate(DeltaMove, Space.World);
        }
	    public Vector3 GetCurrentRootMotion()
	    {
	        return receiver != null ? receiver.CurrentRootMotion : Vector3.zero;
	    }
	
	}
}
