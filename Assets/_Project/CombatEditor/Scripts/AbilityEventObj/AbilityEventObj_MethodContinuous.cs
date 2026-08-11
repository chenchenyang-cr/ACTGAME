using UnityEngine;

namespace CombatEditor
{
    [AbilityEvent]
    [CreateAssetMenu(menuName = "AbilityEvents / MethodContinuous")]
    public class AbilityEventObj_MethodContinuous : AbilityEventObj
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
            return EventTimeType.EventRange;
        }

        public override AbilityEventEffect Initialize()
        {
            return new AbilityEventEffect_MethodContinuous(this);
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
