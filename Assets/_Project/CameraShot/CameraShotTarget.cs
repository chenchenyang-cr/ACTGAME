using UnityEngine;

namespace CombatCamera
{
    [DisallowMultipleComponent]
    public sealed class CameraShotTarget : MonoBehaviour
    {
        [Tooltip("Opponent or subject used by Target/Midpoint shots. Missing targets fall back to the actor.")]
        public Transform Target;

        public static void Set(GameObject actor, Transform target)
        {
            if (actor == null) return;
            var context = actor.GetComponent<CameraShotTarget>();
            if (context == null) context = actor.AddComponent<CameraShotTarget>();
            context.Target = target;
        }
    }
}
