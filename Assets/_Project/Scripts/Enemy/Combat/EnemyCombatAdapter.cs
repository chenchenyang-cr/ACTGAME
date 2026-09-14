using CombatEditor;
using UnityEngine;

namespace UnityLearning.EnemySystem
{
    public sealed class EnemyCombatAdapter : MonoBehaviour, ICombatGameplayWindowListener,
        ICombatTeamProvider
    {
        [SerializeField] private Animator animator;
        [SerializeField] private CombatController combatController;
        [SerializeField, Min(0f)] private float attackBlendDuration = 0.12f;

        private int activeStateShortHash;
        private bool requestedAnimation;
        private bool enteredAnimation;
        private bool exitRequested;
        private bool followUpAttempted;
        private AbilityScriptableObject activeAbility;

        public EnemyAttackConfig CurrentAttack { get; private set; }
        public bool IsAttacking => CurrentAttack != null;
        public CombatTeam Team => CombatTeam.Enemy;

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
            followUpAttempted = false;
            PlayAttackAbility(attack.Ability);
            return true;
        }

        public bool TryBeginFollowUp()
        {
            if (CurrentAttack == null || followUpAttempted || animator == null) return false;
            followUpAttempted = true;
            AbilityScriptableObject followUp = CurrentAttack.FollowUpAbility;
            if (followUp == null || followUp.Clip == null ||
                CurrentAttack.FollowUpChance <= 0f ||
                (CurrentAttack.FollowUpChance < 1f && Random.value >= CurrentAttack.FollowUpChance))
                return false;

            PlayAttackAbility(followUp);
            return true;
        }

        private void PlayAttackAbility(AbilityScriptableObject ability)
        {
            activeAbility = ability;
            activeStateShortHash = Animator.StringToHash(ability.Clip.name);
            requestedAnimation = true;
            enteredAnimation = false;
            exitRequested = false;
            animator.CrossFadeInFixedTime(
                ability.Clip.name,
                attackBlendDuration,
                0,
                0f);
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
            activeAbility = null;
            followUpAttempted = false;
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
            if (CurrentAttack == null || context.Ability != activeAbility)
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
