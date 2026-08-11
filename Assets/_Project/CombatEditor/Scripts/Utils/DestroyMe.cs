using UnityEngine;

namespace CombatEditor
{
    public class DestroyMe : MonoBehaviour
    {
        [Min(0f)]
        public float DelaySeconds = 1f;

        private void Start()
        {
            Destroy(gameObject, DelaySeconds);
        }
    }
}
