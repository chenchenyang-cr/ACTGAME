using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CombatEditor
{
    [CustomEditor(typeof(CombatController))]
    public class CombatControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            CombatController controller = (CombatController)target;

            serializedObject.Update();
            SerializedProperty animatorProperty = serializedObject.FindProperty("_animator");
            EditorGUILayout.PropertyField(animatorProperty);
            serializedObject.ApplyModifiedProperties();

            TryAutoSetupRootMotionBridge(controller);

            if (GUILayout.Button("Open CombatEditor", GUILayout.Height(35)))
            {
                CombatEditor.Init();
            }

            if (controller.transform.parent == null)
            {
                EditorGUILayout.HelpBox("CombatController 没有父节点。若要把 RootMotion 转给父物体，请把角色放在父节点下。", MessageType.Info);
            }
        }

        private static void TryAutoSetupRootMotionBridge(CombatController controller)
        {
            if (controller == null)
            {
                return;
            }

            Transform target = ResolveApplierTarget(controller);
            if (target == null)
            {
                return;
            }

            RootMotionParentApplier applier = target.GetComponent<RootMotionParentApplier>();
            if (applier == null)
            {
                applier = Undo.AddComponent<RootMotionParentApplier>(target.gameObject);
            }

            if (controller._animator != null)
            {
                RootMotionReceiver receiver = controller._animator.GetComponent<RootMotionReceiver>();
                if (receiver == null)
                {
                    receiver = Undo.AddComponent<RootMotionReceiver>(controller._animator.gameObject);
                }

                applier.SetSourceAnimator(controller._animator);
                EditorUtility.SetDirty(controller._animator.gameObject);
            }

            EditorUtility.SetDirty(target.gameObject);
        }

        private static Transform ResolveApplierTarget(CombatController controller)
        {
            if (controller == null)
            {
                return null;
            }

            if (controller._animator == null)
            {
                return controller.transform;
            }

            Transform controllerTransform = controller.transform;
            Transform animatorTransform = controller._animator.transform;

            if (animatorTransform == controllerTransform || animatorTransform.IsChildOf(controllerTransform))
            {
                return controllerTransform;
            }

            if (controllerTransform.parent != null && animatorTransform.IsChildOf(controllerTransform.parent))
            {
                return controllerTransform.parent;
            }

            return controllerTransform;
        }
    }
}
