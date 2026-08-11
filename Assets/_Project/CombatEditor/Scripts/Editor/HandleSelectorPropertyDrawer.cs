using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

 namespace CombatEditor
{	
	public class Node_Sp
	{
	    public CharacterNode.NodeType type;
	    public SerializedProperty sp;
	}
	
	[CustomPropertyDrawer(typeof(InsedObject))]
	public class HandleSelectorProperty : PropertyDrawer
	{
	    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
	    {
	        bool hideScale = property.serializedObject.targetObject is AbilityEventObj_Particles;
	        return hideScale ? 110 : 130;
	    }

	    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	    {
	        EditorGUI.BeginProperty(position, label, property);
	        bool hideScale = property.serializedObject.targetObject is AbilityEventObj_Particles;
	        GUILayout.BeginVertical("TransformData", "window",GUILayout.Height(hideScale ? 110 : 130));
	        float TargetlabelWidth = 100;
	        float TabHeight = 20;
	        
	        //Property
	        var labelWidth = EditorGUIUtility.labelWidth;
	        EditorGUIUtility.labelWidth = TargetlabelWidth;
	        //UpdatePreviewIfChange
	        EditorGUI.BeginChangeCheck();
	        EditorGUILayout.PropertyField(property.FindPropertyRelative("TargetObj"));
	        if (EditorGUI.EndChangeCheck())
	        {
	            property.serializedObject.ApplyModifiedProperties();
	            if (CombatEditorUtility.EditorExist())
	            {
	                var edit = CombatEditorUtility.GetCurrentEditor();
	                edit.RequirePreviewReload();
	                //edit.HardResetPreviewToCurrentFrame();
	            }
	        }
	
	        EditorGUILayout.HelpBox("Pos and Rot can only be edited from the Scene Edit buttons.", MessageType.None);

	        if (!hideScale)
	        {
	            EditorGUILayout.BeginHorizontal(GUILayout.Height(TabHeight));
	            EditorGUILayout.LabelField("Scale", GUILayout.Width(TargetlabelWidth));
	            EditorGUILayout.PropertyField(property.FindPropertyRelative("Scale"), new GUIContent(""));
	            EditorGUILayout.EndHorizontal();
	        }
	
	
	
	        var editor = CombatEditorUtility.GetCurrentEditor();
	        List<string> NodeTypesInController = new List<string>();
	        var nodes = editor.SelectedController.Nodes;
	
	        var DefaultNodeName = System.Enum.GetName(typeof(CharacterNode.NodeType), 0);
	        NodeTypesInController.Add(DefaultNodeName);
	        for (int i = 0; i < nodes.Count; i++)
	        {
	            string EnumName = System.Enum.GetName(typeof(CharacterNode.NodeType), (int)nodes[i].type);
	            NodeTypesInController.Add(System.Enum.GetName(typeof(CharacterNode.NodeType), (int)nodes[i].type));
	        }
	
	        GenericMenu menu = new GenericMenu();
	        string name = property.FindPropertyRelative("TargetNode").enumNames[property.FindPropertyRelative("TargetNode").enumValueIndex];
	
	        EditorGUILayout.BeginHorizontal();
	        EditorGUILayout.LabelField("TargetNode", GUILayout.Width(TargetlabelWidth - 1));
	        if (EditorGUILayout.DropdownButton(new GUIContent(name), FocusType.Passive))
	        {
	            for (int i = 0; i < NodeTypesInController.Count; i++)
	            {
	                Node_Sp node_So = new Node_Sp();
	                node_So.sp = property;
	                if (i == 0)
	                {
	                    node_So.type = CharacterNode.NodeType.Animator;
	                    menu.AddItem(new GUIContent(NodeTypesInController[i]), false, SetNode, node_So);
	                }
	                else if (nodes[i - 1].type != CharacterNode.NodeType.Animator)
	                {
	                    node_So.type = nodes[i - 1].type;
	                    menu.AddItem(new GUIContent(NodeTypesInController[i]), false, SetNode, node_So);
	                }
	            }
	            menu.ShowAsContext();
	        }
	
	
	        //Texture Config = EditorGUIUtility.IconContent("_Popup@2x").image;
	        //Config.filterMode = FilterMode.Bilinear;
	        if(GUILayout.Button("Config", GUILayout.Width(60), GUILayout.Height(18)))
	        {
	            //CombatEditorUltilies.GetCurrentEditor().select
	            CombatInspector.GetInspector().SelectCombatConfig();
	        }
	
	        EditorGUILayout.EndHorizontal();
	        EditorGUILayout.PropertyField(property.FindPropertyRelative("FollowNode"));
	        EditorGUILayout.PropertyField(property.FindPropertyRelative("RotateByNode"));
	
	        //SerializedObject so = new SerializedObject(editor.SelectedController);
	        //so.Update();
	        //EditorGUILayout.PropertyField(so.FindProperty("Nodes"));
	        //so.ApplyModifiedProperties();
	
	        EditorGUIUtility.labelWidth = labelWidth;
	
	        EditorGUI.EndProperty();
	        GUILayout.EndVertical();
	        //base.OnGUI(position, property, label);
	
	    }
	
	    public void SetNode(object nodetype)
	    {
	        Node_Sp nodeSO = nodetype as Node_Sp;
	        SerializedObject so = nodeSO.sp.serializedObject;
	        so.Update();
	
	        nodeSO.sp.FindPropertyRelative("TargetNode").enumValueIndex = (int)nodeSO.type;
	        //SceneView.RepaintAll();
	        so.ApplyModifiedProperties();
	    }
	
	}
}
