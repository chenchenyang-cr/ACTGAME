using UnityEngine;

namespace CombatEditor
{
    public abstract class AbilityEventObj_HitSource : AbilityEventObj_CreateObjWithHandle
    {
        [InspectorName("受击配置")]
        public CombatHitProfile HitProfile;
        public virtual bool HasHitBox => true;
    }
}