using UnityEngine;

namespace CombatEditor
{
#if UNITY_EDITOR
    public static class CombatEditorTransformEditState
    {
        public enum EditMode
        {
            None,
            Position,
            Rotation
        }

        public static AbilityEventObj CurrentTarget;
        public static EditMode CurrentMode;

        public static bool IsEditing(AbilityEventObj obj, EditMode mode)
        {
            return CurrentTarget == obj && CurrentMode == mode;
        }

        public static void Set(AbilityEventObj obj, EditMode mode)
        {
            CurrentTarget = obj;
            CurrentMode = mode;
        }

        public static void Clear()
        {
            CurrentTarget = null;
            CurrentMode = EditMode.None;
        }
    }
#endif
}
