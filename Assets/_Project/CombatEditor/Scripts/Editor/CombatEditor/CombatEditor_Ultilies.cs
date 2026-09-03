using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

 namespace CombatEditor
{	
	public partial class CombatEditor : EditorWindow
	{
	    private Texture2D MakeTex(int width, int height, Color col)
	    {
	        Color[] pix = new Color[width * height];
	        for (int i = 0; i < pix.Length; ++i)
	        {
	            pix[i] = col;
	        }
	        Texture2D result = new Texture2D(width, height);
	        result.SetPixels(pix);
	        result.Apply();
	        return result;
	    }
	
	    public void InitGUIStyle()
	    {
	        InitDeleteButtonStyle();
	        InitBoxGUIStyle();
	        InitHeaderStyle();
	    }
	
	    //public GUIStyle OnInspectedButtonStyle;
	    //public void InitOnInspectedButtonStyle()
	    //{
	    //    if(OnInspectedButtonStyle == null)
	    //    {
	    //        OnInspectedButtonStyle = new GUIStyle(GUI.skin.button);
	    //    }
	    //}
	    //public void InitOnSelectedButtonStyle()
	    //{
	
	    //}
	
	    public void HighlightBGIfInspectType(InspectedType type)
	    {
	        if (CurrentInspectedType == type)
	        {
	            GUI.backgroundColor = OnInspectedColor;
	        }
	        else
	        {
	            GUI.backgroundColor = SelectedColor;
	        }
	    }
	
	    public void InitDeleteButtonStyle()
	    {
	        if (MyDeleteButtonStyle == null)
	        {
	            MyDeleteButtonStyle = new GUIStyle(GUI.skin.button);
	            MyDeleteButtonStyle.margin = new RectOffset(0, 0, 0, 0);
	            MyDeleteButtonStyle.fontSize = 20;
	            MyDeleteButtonStyle.padding = new RectOffset(0,0,0,0);
	            MyDeleteButtonStyle.alignment = TextAnchor.MiddleCenter;
	            MyDeleteButtonStyle.fontStyle = FontStyle.Bold;
	            MyDeleteButtonStyle.contentOffset = new Vector2(0, 0);
	        }
	    }
	
	
	    public void InitBoxGUIStyle()
	    {
	        if (MyBoxGUIStyle == null)
	        {
	            MyBoxGUIStyle = new GUIStyle(GUI.skin.box);
	            MyBoxGUIStyle.normal.background = MakeTex(2, 2, Color.white);
	            MyBoxGUIStyle.border = new RectOffset(2, 2, 2, 2);
	        }
	    }
	
	
	    public void InitHeaderStyle()
	    {
	        if (HeaderStyle == null)
	        {
	            HeaderStyle = new GUIStyle(EditorStyles.helpBox);
	            HeaderStyle.alignment = GUI.skin.button.alignment;
	            HeaderStyle.fontSize = HeaderFontSize;
	            HeaderStyle.fontStyle = FontStyle.Bold;
	        }
	    }
	
	
	    bool IsPaintingRenameField;
	    Rect RenameFieldRect;
	    Rect RenameTargetRect;
	    string NameOfRename = "";
	
	
	    public void StartPaintRenameField(Rect TargetRect, string DefaultName, System.Action finishRenameAction)
	    {
	        FinishRenameAction = finishRenameAction;
	        NameOfRename = DefaultName;
	        RenameTargetRect = TargetRect;
	        Event e = Event.current;
	        //RenameFieldRect = new Rect(e.mousePosition.x, e.mousePosition.y, 200, 100);
	        RenameFieldRect = TargetRect;
	        //Vector2 StartPos = new Vector2(e.mousePosition.x, e.mousePosition.y);
	        IsPaintingRenameField = true;
	        PaintRenameField();
	
	        GUI.FocusControl("RenameField");
	        //Debug.Log(GUI.GetNameOfFocusedControl());
	    }
	    System.Action FinishRenameAction;
	
	    public void PaintRenameField()
	    {
	        //EditorGUI.DrawRect(RenameTargetRect,Color.green);
	        if (!IsPaintingRenameField)
	        {
	            return;
	        }
	
	        Event e = Event.current;
	        if (e.isKey && e.keyCode == KeyCode.Return)
	        {
	            StopRename();
	        }
	        if (e.isMouse)
	        {
	            if (!RenameFieldRect.Contains(e.mousePosition))
	            {
	                StopRename();
	            }
	        }
	        GUI.SetNextControlName("RenameField");
	
	        Rect InputRect = new Rect(RenameFieldRect.x, RenameFieldRect.y, RenameFieldRect.width, RenameFieldRect.height);
	        GUI.FocusControl("RenameField");
	
	        GUIStyle RenameStyle = EditorStyles.textField;
	        RenameStyle.alignment = TextAnchor.MiddleLeft;
	        NameOfRename = EditorGUI.TextField(InputRect, NameOfRename, RenameStyle);
	
	
	        //GUI.depth = 1;
	
	        //Repaint();
	    }
	
	   
	
	
	    public void StopRename()
	    {
	        IsPaintingRenameField = false;
	        if (FinishRenameAction != null)
	        {
	            FinishRenameAction.Invoke();
	        }
	    }
	    public static T[] GetAtPath<T>(string path)
	    {
	
	        ArrayList al = new ArrayList();
	
	        path = path.Remove(0, 6);
	        string[] fileEntries = Directory.GetFiles(Application.dataPath + "/" + path);
	        foreach (string fileName in fileEntries)
	        {
	            int index = fileName.LastIndexOf("/");
	            string localPath = "Assets/" + path;
	
	            if (index > 0)
	                localPath += fileName.Substring(index);
	            //Debug.Log(path);
	            Object t = AssetDatabase.LoadAssetAtPath(localPath, typeof(T));
	
	            if (t != null)
	                al.Add(t);
	        }
	        T[] result = new T[al.Count];
	        for (int i = 0; i < al.Count; i++)
	            result[i] = (T)al[i];
	
	        return result;
	    }
	
	    public void DrawHorizontalLine(Vector3 p1, Vector3 p2, Color color, float Width)
	    {
	        EditorGUI.DrawRect(new Rect(p1.x, p1.y - Width / 2, (p2 - p1).x, Width), color);
	    }
	    public void DrawVerticalLine(Vector3 p1, Vector3 p2, Color color, float Width)
	    {
	        EditorGUI.DrawRect(new Rect(p1.x - Width / 2, p1.y, Width, (p2 - p1).y), color);
	    }
	    public void SaveAbilityAsset(AbilityScriptableObject ability)
	    {
	        if (ability == null)
	        {
	            return;
	        }

	        EditorUtility.SetDirty(ability);
	        AssetDatabase.SaveAssets();
	    }

	    public void SaveEventAsset(AbilityScriptableObject ability, AbilityEventObj eventObj)
	    {
	        if (eventObj != null)
	        {
	            EditorUtility.SetDirty(eventObj);
	        }

	        SaveAbilityAsset(ability);
	    }

	    public void UpdateAsset(Object obj)
	    {
	        if (obj != null)
	        {
	            EditorUtility.SetDirty(obj);
	        }

	        if (SelectedAbilityObj != null && SelectedAbilityObj != obj)
	        {
	            EditorUtility.SetDirty(SelectedAbilityObj);
	        }

	        AssetDatabase.SaveAssets();
	    }
	    
	
	    public void LoadL3()
	    {
	        AnimEventTracks = new List<AnimEventTrack>();
	        if (SelectedAbilityObj != null)
	        {
                bool removedInvalidEvent = false;
                for (int i = 0; i < SelectedAbilityObj.events.Count; i++)
	            {
                    var eve = SelectedAbilityObj.events[i];
                    if (eve == null || eve.Obj == null)
                    {
                        SelectedAbilityObj.events.RemoveAt(i);
                        i--;
                        removedInvalidEvent = true;
                        continue;
                    }

	                AnimEventTracks.Add(new AnimEventTrack(eve, this));
	            }
                if (removedInvalidEvent)
                {
                    SaveAbilityAsset(SelectedAbilityObj);
                }
	            if (SelectedAbilityObj.Clip != null)
	            {
                AnimFrameCount = (int)(SelectedAbilityObj.Clip.length * CombatTimeline.FramesPerSecond);
	            }
	            else
	            {
	                AnimFrameCount = 0;
	            }
	        }
	        InitRect();
	    }
	   
	}
	
	public static class CombatEditorUtility
	{
	    public static void ReloadAnimEvents()
	    {
	        
	        GetCurrentEditor().LoadL3();
	    }
	    public static CombatEditor GetCurrentEditor()
	    {
	        return EditorWindow.GetWindow<CombatEditor>(false,"",false);
	    }
	    public static bool EditorExist()
	    {
	        return EditorWindow.HasOpenInstances<CombatEditor>();
	    }
	
	    public static Rect ScaleRect(Rect rect,float Scale)
	    {
	        Rect RectAfterScale = new Rect
	            (rect.x + 0.5f* (rect.width - rect.width * Scale),
	            rect.y + 0.5f * (rect.height - rect.height * Scale),
	            rect.width * Scale,rect.height*Scale);
	        return RectAfterScale;
	    }
	    public static void DrawEditorTextureOnRect(Rect rect,float Scale, string name)
	    {
	        rect = CombatEditorUtility.ScaleRect(rect, Scale);
	        var texture = EditorGUIUtility.IconContent(name).image;
	        if(texture == null)
	        {
	            return;
	        }
	
	        texture.filterMode = FilterMode.Bilinear;
	        GUI.DrawTexture(rect, EditorGUIUtility.IconContent(name).image);
	    }
	
	
	}


    public class TimeLineHelper
    {
        private static readonly Dictionary<string, Texture2D> TintedTextureCache = new Dictionary<string, Texture2D>();

        private enum DragRangeMode
        {
            None,
            LeftHandle,
            Body,
            RightHandle
        }

        private DragRangeMode _rangeDragMode = DragRangeMode.None;
        private float _rangeDragStartMouseX;
        private int _rangeDragStartLeftValue;
        private int _rangeDragStartRightValue;

        public EditorWindow TargetWindow;
        public TimeLineHelper(EditorWindow window)
        {
            TargetWindow = window;
        }
        public int DrawHorizontalDraggablePoint(int Value,
            int MaxValue,
            Rect rect,
            Color color,
            GUIStyle style,
            float Width = 5,
            bool LeftMouse = true,
            bool DrawPoint = true,
            bool DragStartOnMouseIn = false,
            System.Action<float> DragAction = null,
            System.Action FinishAction = null)
        {
            Event e = Event.current;
            float Percentage = (float)Value / (float)MaxValue;

            Rect PointRect = new Rect(rect.x + Percentage * rect.width - Width / 2, rect.y , Width, rect.height );
            int controlID = GUIUtility.GetControlID(FocusType.Passive);

            //Draw white background when selected.
            if (GUIUtility.hotControl == controlID)
            {
                //if (rect.Contains(e.mousePosition))
                //{
                    EditorGUI.DrawRect(rect, 0.5f * Color.white);
                //}
            }

            //GUI.depth = 1;
            if (DrawPoint)
            {
                if (ShouldDrawSolidColor(color))
                {
                    DrawTintedStyleRect(PointRect, style, color);
                }
                else
                {
                    GUI.Box(PointRect, "", style);
                }
            }
            //GUI.depth = 0;

            int TargetMouseButton = LeftMouse ? 0 : 1;

            //Paint On Focus?
        
            switch (e.GetTypeForControl(controlID))
            {
                case (EventType.MouseDown):
                    if (e.button == TargetMouseButton)
                    {
                        if ((DragStartOnMouseIn && PointRect.Contains(e.mousePosition)) || (!DragStartOnMouseIn && rect.Contains(e.mousePosition)))
                        {
                            GUIUtility.hotControl = controlID;
                            e.Use();
                        }

                    }
                    break;
                case (EventType.MouseDrag):
                    {
                        if (GUIUtility.hotControl == controlID && e.button == TargetMouseButton)
                        {
                            Percentage = ((e.mousePosition.x - rect.x) / rect.width);
                            Percentage = Mathf.Clamp(Percentage, 0, 1);
                            Value = Mathf.RoundToInt(Percentage * MaxValue);
                            Percentage = (float)Value / (float)MaxValue;
                            PointRect = new Rect(rect.x + Percentage * rect.width - Width / 2, rect.y, Width, rect.height);
                            if (DrawPoint)
                            {
                                DrawTintedStyleRect(PointRect, style, color);
                            }
                            if (DragAction != null)
                            {
                                DragAction(Percentage);
                            }
                            TargetWindow.Repaint();
                        }
                    }
                    break;
                case (EventType.MouseUp):
                    {
                        if (e.button == TargetMouseButton)
                        {
                            if (GUIUtility.hotControl == controlID)
                            //if (IsDraggingthis)
                            {
                                GUIUtility.hotControl = 0;
                                if (FinishAction != null)
                                {
                                    FinishAction.Invoke();
                                }
                            }
                            //IsDraggingthis = false;
                            
                        }
                    }
                    break;
            }


            EditorGUIUtility.AddCursorRect(PointRect, MouseCursor.SlideArrow, controlID);
            return Value;
        }

        public int[] DrawHorizontalDraggableRange(int Value1, int Value2, int MaxValue, Rect rect, Color color, GUIStyle boxStyle, float Width = 5, System.Action FinishDragAction = null)
        {
            if (MaxValue <= 0)
            {
                return new int[] { Value1, Value2 };
            }

            int leftValue = Mathf.Clamp(Mathf.Min(Value1, Value2), 0, MaxValue);
            int rightValue = Mathf.Clamp(Mathf.Max(Value1, Value2), 0, MaxValue);
            int minRange = 0;

            float leftPercentage = (float)leftValue / (float)MaxValue;
            float rightPercentage = (float)rightValue / (float)MaxValue;
            float leftX = rect.x + leftPercentage * rect.width;
            float rightX = rect.x + rightPercentage * rect.width;

            Rect leftHandleRect = new Rect(leftX - Width * 0.5f, rect.y, Width, rect.height);
            Rect rightHandleRect = new Rect(rightX - Width * 0.5f, rect.y, Width, rect.height);

            float bodyWidth = Mathf.Max(0f, rightX - leftX);
            Rect bodyRect = new Rect(leftX, rect.y, bodyWidth, rect.height);
            if (bodyRect.width < Width)
            {
                bodyRect.xMin -= (Width - bodyRect.width) * 0.5f;
                bodyRect.xMax += (Width - bodyRect.width) * 0.5f;
            }

            int controlID = GUIUtility.GetControlID(FocusType.Passive);
            Event e = Event.current;
            switch (e.GetTypeForControl(controlID))
            {
                case EventType.MouseDown:
                    if (e.button != 0)
                    {
                        break;
                    }

                    if (leftHandleRect.Contains(e.mousePosition))
                    {
                        GUIUtility.hotControl = controlID;
                        _rangeDragMode = DragRangeMode.LeftHandle;
                    }
                    else if (rightHandleRect.Contains(e.mousePosition))
                    {
                        GUIUtility.hotControl = controlID;
                        _rangeDragMode = DragRangeMode.RightHandle;
                    }
                    else if (bodyRect.Contains(e.mousePosition))
                    {
                        GUIUtility.hotControl = controlID;
                        _rangeDragMode = DragRangeMode.Body;
                    }
                    else
                    {
                        break;
                    }

                    _rangeDragStartMouseX = e.mousePosition.x;
                    _rangeDragStartLeftValue = leftValue;
                    _rangeDragStartRightValue = rightValue;
                    e.Use();
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != controlID || e.button != 0)
                    {
                        break;
                    }

                    int deltaValue = Mathf.RoundToInt((e.mousePosition.x - _rangeDragStartMouseX) / rect.width * MaxValue);
                    if (_rangeDragMode == DragRangeMode.LeftHandle)
                    {
                        leftValue = Mathf.Clamp(_rangeDragStartLeftValue + deltaValue, 0, rightValue - minRange);
                    }
                    else if (_rangeDragMode == DragRangeMode.RightHandle)
                    {
                        rightValue = Mathf.Clamp(_rangeDragStartRightValue + deltaValue, leftValue + minRange, MaxValue);
                    }
                    else if (_rangeDragMode == DragRangeMode.Body)
                    {
                        int rangeLength = _rangeDragStartRightValue - _rangeDragStartLeftValue;
                        int newLeft = Mathf.Clamp(_rangeDragStartLeftValue + deltaValue, 0, MaxValue - rangeLength);
                        leftValue = newLeft;
                        rightValue = newLeft + rangeLength;
                    }

                    TargetWindow.Repaint();
                    e.Use();
                    break;

                case EventType.MouseUp:
                    if (e.button != 0 || GUIUtility.hotControl != controlID)
                    {
                        break;
                    }

                    GUIUtility.hotControl = 0;
                    _rangeDragMode = DragRangeMode.None;
                    FinishDragAction?.Invoke();
                    e.Use();
                    break;
            }

            leftPercentage = (float)leftValue / (float)MaxValue;
            rightPercentage = (float)rightValue / (float)MaxValue;
            Rect targetRect = new Rect(rect.x + leftPercentage * rect.width, rect.y, (rightPercentage - leftPercentage) * rect.width, rect.height);
            if (ShouldDrawSolidColor(color))
            {
                DrawTintedStyleRect(targetRect, boxStyle, color);
            }
            else
            {
                Color defaultColor = GUI.color;
                GUI.color = color;
                GUI.Box(targetRect, "", boxStyle);
                GUI.color = defaultColor;
            }

            Rect paintLeftHandle = new Rect(targetRect.xMin - Width * 0.5f, rect.y, Width, rect.height);
            Rect paintRightHandle = new Rect(targetRect.xMax - Width * 0.5f, rect.y, Width, rect.height);
            EditorGUI.DrawRect(paintLeftHandle, new Color(1f, 1f, 1f, 0.25f));
            EditorGUI.DrawRect(paintRightHandle, new Color(1f, 1f, 1f, 0.25f));

            EditorGUIUtility.AddCursorRect(paintLeftHandle, MouseCursor.ResizeHorizontal, controlID);
            EditorGUIUtility.AddCursorRect(paintRightHandle, MouseCursor.ResizeHorizontal, controlID);
            EditorGUIUtility.AddCursorRect(targetRect, MouseCursor.SlideArrow, controlID);

            return new int[] { leftValue, rightValue };
        }

        private static bool ShouldDrawSolidColor(Color color)
        {
            return color != Color.white;
        }

        private static void DrawTintedStyleRect(Rect rect, GUIStyle style, Color tintColor)
        {
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            if (style == null || style.normal.background == null)
            {
                EditorGUI.DrawRect(rect, tintColor);
                return;
            }

            Texture2D tintedTexture = GetTintedTexture(style.normal.background, tintColor);
            if (tintedTexture == null)
            {
                EditorGUI.DrawRect(rect, tintColor);
                return;
            }

            GUI.Box(rect, GUIContent.none, style);
            GUI.DrawTexture(rect, tintedTexture, ScaleMode.StretchToFill, true);
        }

        private static Texture2D GetTintedTexture(Texture2D source, Color tintColor)
        {
            if (source == null)
            {
                return null;
            }

            string cacheKey = source.GetInstanceID() + "_" + ColorUtility.ToHtmlStringRGBA(tintColor);
            if (TintedTextureCache.TryGetValue(cacheKey, out Texture2D cachedTexture) && cachedTexture != null)
            {
                return cachedTexture;
            }

            Color[] sourcePixels;
            try
            {
                sourcePixels = source.GetPixels();
            }
            catch
            {
                return null;
            }

            Color[] tintedPixels = new Color[sourcePixels.Length];
            Color dark = Color.Lerp(tintColor, Color.black, 0.35f);
            Color mid = tintColor;
            Color light = Color.Lerp(tintColor, Color.white, 0.35f);

            for (int i = 0; i < sourcePixels.Length; i++)
            {
                Color src = sourcePixels[i];
                float grayscale = src.grayscale;
                Color shaded = grayscale < 0.5f
                    ? Color.Lerp(dark, mid, grayscale / 0.5f)
                    : Color.Lerp(mid, light, (grayscale - 0.5f) / 0.5f);

                shaded.a = src.a;
                tintedPixels[i] = shaded;
            }

            Texture2D tintedTexture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = source.filterMode,
                wrapMode = source.wrapMode
            };
            tintedTexture.SetPixels(tintedPixels);
            tintedTexture.Apply();

            TintedTextureCache[cacheKey] = tintedTexture;
            return tintedTexture;
        }


        //int BoxEdgeWidth = 1;
        //static Color[] MultiColors= new Color{Color.blue,Color.cyan};
        
        public int[] DrawHorizontalMultiDraggable(int[] Values,string[] Names, int MaxValue, Rect rect, Color color, string styleName, float Width = 5, System.Action FinishDragAction = null)
        {
            int[] ModifiedValue = Values;
          
            Color defaultColor = GUI.color;
            GUI.color = color;

            float[] VisiableValue = new float[Values.Length + 2];
            VisiableValue[0] = 0;
            VisiableValue[Values.Length + 1] = 1;
            for(int i =0;i<Values.Length;i++)
            {
                VisiableValue[i + 1] = (float)Values[i] / (float)MaxValue;
            }
            for (int i = 0; i < Values.Length; i++)
            {
                ModifiedValue[i] = DrawHorizontalDraggablePoint(Values[i], MaxValue, rect, color, styleName, Width, true, false, true, null, FinishDragAction);
            }
            for (int i = 0; i < VisiableValue.Length - 1; i++)
            {
                Rect TargetRect = new Rect(rect.x + VisiableValue[i] * rect.width + 1, rect.y, (VisiableValue[i + 1] - VisiableValue[i]) * rect.width - 2 , rect.height);
                //Rect InnerRect = new Rect(TargetRect.x + BoxEdgeWidth, TargetRect.y + BoxEdgeWidth, TargetRect.width - 2 * BoxEdgeWidth, TargetRect.height - 2 * BoxEdgeWidth);

                if (Names[i] != "" && Names[i]!=null)
                {
                    GUI.Box(TargetRect, Names[i], "flow node " + 5);
                }
                else
                {
                    GUI.Box(TargetRect, Names[i], "flow node " + 0);
                }
               
            }
          


            return ModifiedValue;
        }



	
	    public float DrawSplitLine(float X, float width, float MinX, float MaxX)
	    {
	        //DrawVerticalLine();
	        float Percentage = X / TargetWindow.position.width;
	
	        //TriggerField
	        Rect DraggableStartField = new Rect(X - 8, 0, 16, TargetWindow.position.height);
	        //GUI.Box(DraggableStartField,"LineTrigger");
	        Event e = Event.current;
	        Rect rect = new Rect(0, 0, TargetWindow.position.width, TargetWindow.position.height);
	        Rect TargetRect = new Rect(rect.x + Percentage * rect.width, rect.y, width, rect.height);
	        EditorGUI.DrawRect(TargetRect, Color.grey);
	        int controlID = GUIUtility.GetControlID(FocusType.Passive);
            EditorGUIUtility.AddCursorRect(DraggableStartField, MouseCursor.SlideArrow, controlID);


            if (e.GetTypeForControl(controlID) == EventType.MouseDown)
	        {
	            if (e.button == 0)
	            {
	                if (DraggableStartField.Contains(e.mousePosition))
	                {
                        GUIUtility.hotControl = controlID;
                        e.Use();
                    }
	            }
	        }
	        if (e.GetTypeForControl(controlID) == EventType.MouseDrag)
	        {
                if (GUIUtility.hotControl == controlID)
                {
                    Percentage = ((e.mousePosition.x - rect.x) / rect.width);
                    Percentage = Mathf.Clamp(Percentage, MinX / rect.width, MaxX / rect.width);
                    TargetRect = new Rect(rect.x + Percentage * rect.width, rect.y, 10, rect.height);
                    EditorGUI.DrawRect(TargetRect, Color.grey);
                    TargetWindow.Repaint();
                }
            }
            
            return Percentage * TargetWindow.position.width;
        }
	
	 
	}
}
