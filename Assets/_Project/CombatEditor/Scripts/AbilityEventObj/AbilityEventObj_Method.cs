using UnityEngine;

namespace CombatEditor
{
    [AbilityEvent]
    [CreateAssetMenu(menuName = "AbilityEvents / Method")]
    public class AbilityEventObj_Method : AbilityEventObj
    {
        public string ScriptTypeName = "";
        public string MethodName = "";
        public bool LogMissingMethod = true;

#if UNITY_EDITOR
        private void OnValidate()
        {
            SyncNameWithMethod();
        }
#endif

        public override EventTimeType GetEventTimeType()
        {
            return EventTimeType.EventTime;
        }

        public override AbilityEventEffect Initialize()
        {
            return new AbilityEventEffect_Method(this);
        }

        private void SyncNameWithMethod()
        {
            if (string.IsNullOrWhiteSpace(MethodName))
            {
                return;
            }

            if (name == MethodName)
            {
                return;
            }

            name = MethodName;
        }
    }
}
