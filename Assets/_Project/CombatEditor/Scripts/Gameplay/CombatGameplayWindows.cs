using System;
using UnityEngine;

namespace CombatEditor
{
    public enum CombatGameplayWindowType
    {
        Rotation,
        TargetAssist,
        Combo,
        Interrupt,
        Exit
    }

    public enum CombatRotationPolicy
    {
        Default,
        LockCurrentFacing,
        Animation,
        FaceTarget,
        InputDirection
    }

    public enum CombatWindowExitReason
    {
        ReachedEnd,
        AbilityChanged,
        ControllerDisabled
    }

    [Serializable]
    public struct CombatWindowHandle : IEquatable<CombatWindowHandle>
    {
        [SerializeField] private int value;

        internal CombatWindowHandle(int value)
        {
            this.value = value;
        }

        public bool Equals(CombatWindowHandle other) => value == other.value;
        public override bool Equals(object obj) => obj is CombatWindowHandle other && Equals(other);
        public override int GetHashCode() => value;
        public static bool operator ==(CombatWindowHandle left, CombatWindowHandle right) => left.Equals(right);
        public static bool operator !=(CombatWindowHandle left, CombatWindowHandle right) => !left.Equals(right);
    }

    public readonly struct CombatGameplayWindowContext
    {
        public CombatWindowHandle Handle { get; }
        public CombatGameplayWindowType Type { get; }
        public AbilityScriptableObject Ability { get; }
        public AbilityEventObj_GameplayWindow Window { get; }
        public float NormalizedTime { get; }

        public CombatGameplayWindowContext(
            CombatWindowHandle handle,
            CombatGameplayWindowType type,
            AbilityScriptableObject ability,
            AbilityEventObj_GameplayWindow window,
            float normalizedTime)
        {
            Handle = handle;
            Type = type;
            Ability = ability;
            Window = window;
            NormalizedTime = normalizedTime;
        }
    }

    /// <summary>
    /// Implement this on a game-specific adapter. CombatEditor never needs to know
    /// which state machine, input system or movement component the game uses.
    /// </summary>
    public interface ICombatGameplayWindowListener
    {
        void OnCombatWindowEntered(in CombatGameplayWindowContext context);
        void OnCombatWindowUpdated(in CombatGameplayWindowContext context);
        void OnCombatWindowExited(in CombatGameplayWindowContext context, CombatWindowExitReason reason);
    }

    public abstract class AbilityEventObj_GameplayWindow : AbilityEventObj
    {
        [Min(0)] public int Priority;

        public abstract CombatGameplayWindowType WindowType { get; }

        public override EventTimeType GetEventTimeType() => EventTimeType.EventRange;

        public override AbilityEventEffect Initialize()
        {
            return new AbilityEventEffect_GameplayWindow(this);
        }
    }

    [AbilityEvent]
    [CreateAssetMenu(menuName = "AbilityEvents/Gameplay/Rotation Window")]
    public sealed class AbilityEventObj_RotationWindow : AbilityEventObj_GameplayWindow
    {
        public CombatRotationPolicy Policy = CombatRotationPolicy.Animation;
        public override CombatGameplayWindowType WindowType => CombatGameplayWindowType.Rotation;
    }

    [AbilityEvent]
    [CreateAssetMenu(menuName = "AbilityEvents/Gameplay/Target Assist Window")]
    public sealed class AbilityEventObj_TargetAssistWindow : AbilityEventObj_GameplayWindow
    {
        [Min(0)] public float MaxAcquireDistance = 4f;
        [Min(0)] public float StopDistance = 1.2f;
        [Min(0)] public float MoveSpeed = 8f;
        [Min(0)] public float RotationSpeed = 720f;
        public bool FaceTarget = true;
        public bool HorizontalOnly = true;
        public override CombatGameplayWindowType WindowType => CombatGameplayWindowType.TargetAssist;
    }

    [AbilityEvent]
    [CreateAssetMenu(menuName = "AbilityEvents/Gameplay/Combo Window")]
    public sealed class AbilityEventObj_ComboWindow : AbilityEventObj_GameplayWindow
    {
        [Tooltip("Game-defined command ID, for example LightAttack. It deliberately does not reference an input enum.")]
        public string CommandId = "LightAttack";
        public AbilityScriptableObject NextAbility;
        [Tooltip("When enabled, a matching buffered command starts Next Ability and is consumed after success.")]
        public bool ConsumeBufferedInput = true;
        public override CombatGameplayWindowType WindowType => CombatGameplayWindowType.Combo;
    }

    [AbilityEvent]
    [CreateAssetMenu(menuName = "AbilityEvents/Gameplay/Interrupt Window")]
    public sealed class AbilityEventObj_InterruptWindow : AbilityEventObj_GameplayWindow
    {
        [Tooltip("Comma separated game-defined command IDs, for example Dodge,HeavyAttack.")]
        public string AllowedCommandIds = "Dodge";
        [Min(0)] public int MinimumPriority;
        public bool AllowHitReaction = true;
        public override CombatGameplayWindowType WindowType => CombatGameplayWindowType.Interrupt;

        public bool Allows(string commandId)
        {
            if (string.IsNullOrWhiteSpace(commandId) || string.IsNullOrWhiteSpace(AllowedCommandIds))
            {
                return false;
            }

            string[] ids = AllowedCommandIds.Split(',');
            for (int i = 0; i < ids.Length; i++)
            {
                if (string.Equals(ids[i].Trim(), commandId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }

    [AbilityEvent]
    [CreateAssetMenu(menuName = "AbilityEvents/Gameplay/Exit Window")]
    public sealed class AbilityEventObj_ExitWindow : AbilityEventObj_GameplayWindow
    {
        [Tooltip("Allows the game-specific controller to leave its attack state during this range.")]
        public bool AllowControllerExit = true;
        [Tooltip("Immediately asks the game-specific controller to exit when this window starts.")]
        public bool ExitOnWindowEnter;
        public override CombatGameplayWindowType WindowType => CombatGameplayWindowType.Exit;
    }

    internal static class CombatGameplayListenerUtility
    {
        public static ICombatGameplayWindowListener[] FindListeners(CombatController controller)
        {
            if (controller == null) return Array.Empty<ICombatGameplayWindowListener>();
            Transform characterRoot = controller._animator != null
                ? controller._animator.transform.root
                : controller.transform.root;
            MonoBehaviour[] behaviours = characterRoot.GetComponentsInChildren<MonoBehaviour>(true);
            var result = new System.Collections.Generic.List<ICombatGameplayWindowListener>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ICombatGameplayWindowListener listener) result.Add(listener);
            }
            return result.ToArray();
        }
    }

    public sealed class AbilityEventEffect_GameplayWindow : AbilityEventEffect
    {
        private static int nextHandle;
        private CombatWindowHandle handle;
        private ICombatGameplayWindowListener[] listeners = Array.Empty<ICombatGameplayWindowListener>();
        private AbilityEventObj_GameplayWindow Window => (AbilityEventObj_GameplayWindow)_EventObj;

        public AbilityEventEffect_GameplayWindow(AbilityEventObj obj) : base(obj) { }

        public override void StartEffect()
        {
            base.StartEffect();
            handle = new CombatWindowHandle(++nextHandle);
            listeners = FindListeners();
            CombatGameplayWindowContext context = CreateContext(eve.GetEventStartTime());
            for (int i = 0; i < listeners.Length; i++)
            {
                listeners[i].OnCombatWindowEntered(in context);
            }
        }

        public override void EffectRunning(float currentTimePercentage)
        {
            base.EffectRunning(currentTimePercentage);
            CombatGameplayWindowContext context = CreateContext(currentTimePercentage);
            for (int i = 0; i < listeners.Length; i++)
            {
                listeners[i].OnCombatWindowUpdated(in context);
            }
        }

        public override void EndEffect()
        {
            if (!IsRunning)
            {
                return;
            }

            CombatGameplayWindowContext context = CreateContext(eve.GetEventEndTime());
            for (int i = 0; i < listeners.Length; i++)
            {
                listeners[i].OnCombatWindowExited(in context, CombatWindowExitReason.ReachedEnd);
            }

            listeners = Array.Empty<ICombatGameplayWindowListener>();
            base.EndEffect();
        }

        private CombatGameplayWindowContext CreateContext(float normalizedTime)
        {
            return new CombatGameplayWindowContext(handle, Window.WindowType, AnimObj, Window, normalizedTime);
        }

        private ICombatGameplayWindowListener[] FindListeners()
        {
            return CombatGameplayListenerUtility.FindListeners(_combatController);
        }
    }
}
