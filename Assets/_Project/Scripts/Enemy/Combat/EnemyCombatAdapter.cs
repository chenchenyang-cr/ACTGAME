using CombatEditor;
using UnityEngine;

namespace UnityLearning.EnemySystem
{
    public sealed class EnemyCombatAdapter : MonoBehaviour, ICombatGameplayWindowListener
    {
        [SerializeField] private Animator animator;
        [SerializeField] private CombatController combatController;
        [SerializeField, Min(0f)] private float attackBlendDuration = 0.12f;

        private int activeStateShortHash;
        private bool requestedAnimation;
        private bool enteredAnimation;
        private bool exitRequested;

        public EnemyAttackConfig CurrentAttack { get; private set; }
        public bool IsAttacking => CurrentAttack != null;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (combatController == null) combatController = GetComponent<CombatController>();
            if (combatController == null) combatController = GetComponentInChildren<CombatController>();
        }

        public bool BeginAttack(EnemyAttackConfig attack)
        {
            if (attack == null || attack.Ability == null || attack.Ability.Clip == null ||
                animator == null)
                return false;

            CurrentAttack = attack;
            activeStateShortHash = Animator.StringToHash(attack.Ability.Clip.name);
            requestedAnimation = true;
            enteredAnimation = false;
            exitRequested = false;
            animator.CrossFadeInFixedTime(
                attack.Ability.Clip.name,
                attackBlendDuration,
                0,
                0f);
            return true;
        }

        public bool IsAttackComplete()
        {
            if (!requestedAnimation || CurrentAttack == null || animator == null)
                return true;
            if (exitRequested)
                return true;

            if (animator.IsInTransition(0))
            {
                AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
                if (next.shortNameHash == activeStateShortHash)
                {
                    enteredAnimation = true;
                    return next.normalizedTime >= 1f;
                }
            }

            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            if (current.shortNameHash == activeStateShortHash)
            {
                enteredAnimation = true;
                return current.normalizedTime >= 1f;
            }

            return enteredAnimation;
        }

        public void EndAttack()
        {
            CurrentAttack = null;
            requestedAnimation = false;
            enteredAnimation = false;
            exitRequested = false;
        }

        public void InterruptAttack()
        {
            EndAttack();
        }

        public void OnCombatWindowEntered(in CombatGameplayWindowContext context)
        {
            if (CurrentAttack == null || context.Ability != CurrentAttack.Ability)
                return;

            if (context.Window is AbilityEventObj_ExitWindow exit &&
                exit.AllowControllerExit && exit.ExitOnWindowEnter)
                exitRequested = true;
        }

        public void OnCombatWindowUpdated(in CombatGameplayWindowContext context) { }

        public void OnCombatWindowExited(
            in CombatGameplayWindowContext context,
            CombatWindowExitReason reason) { }
    }
}
