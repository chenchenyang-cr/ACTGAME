using System.Collections;
using System.Collections.Generic;
using UnityEngine;

 namespace CombatEditor
{	
	[DefaultExecutionOrder(100)]
	public class NodeFollower : MonoBehaviour
	{
	    public Transform NodeTrans;
	    public bool FollowPos;
	    public bool FollowRotation;
	    public Vector3 PosOffset;
	    public Quaternion RotOverNode;
	    public CombatController _controller;
	    public void Init(Transform trans , Vector3 Offset , Quaternion Rot, bool followPos, bool followRot,CombatController controller)
	    {
	        NodeTrans = trans;
	        PosOffset = Offset;
	        RotOverNode = Rot;
	        FollowPos = followPos;
	        FollowRotation = followRot;
	        _controller = controller;
	
	        transform.position = NodeTrans.position + NodeTrans.rotation * PosOffset;
	        if (FollowRotation)
	        {
	            transform.rotation = NodeTrans.rotation * RotOverNode;
	        }
	        else
	        {
	            transform.rotation =  _controller.GetNodeTranform(CharacterNode.NodeType.Animator).rotation * RotOverNode;
	        }
	    }
	    public void SetTransform()
	    {
	        if (FollowPos)
	        {
	            transform.position = NodeTrans.position + NodeTrans.rotation * PosOffset;
	        }
	        if (FollowPos)
	        {
	            transform.rotation = (FollowRotation ? NodeTrans.rotation :
	                _controller.GetNodeTranform(CharacterNode.NodeType.Animator).rotation) * RotOverNode;
	        }
	    }
	
	    // Follow the evaluated bone pose after animation and root-motion application.
	    private void LateUpdate()
	    {
	        SetTransform();
	    }
	}
}
