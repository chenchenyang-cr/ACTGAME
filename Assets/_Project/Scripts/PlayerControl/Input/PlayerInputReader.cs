using System;
using UnityEngine;
using UnityEngine.InputSystem;


public class PlayerInputReader : MonoBehaviour//输入系统与Unity Input System之间唯一的入口
{
    private const float MoveControlActuationThreshold = 0.125f;
    private PlayerAction _gameInput;
    public PlayerInputState State { get; private set; }

    
    public event Action DodgePressed;
    public event Action LightAttackPressed;


    void Awake()
    {
        _gameInput = new PlayerAction();
        State = new PlayerInputState();

    }
    void OnEnable()
    {
        RegisterCallback();
        _gameInput.GamePlay.Enable();
    }
    void OnDisable()
    {
        UnregisterCallback();
        _gameInput.GamePlay.Disable();
        State.Reset();
    }
    void OnDestroy()
    {
        UnregisterCallback();
        _gameInput.Dispose();
    }

    void RegisterCallback() 
    {
        _gameInput.GamePlay.Move.performed += OnMovePreformed;
        _gameInput.GamePlay.Move.canceled += OnMoveCanceled;
        _gameInput.GamePlay.Look.performed += OnLookPreformed;
        _gameInput.GamePlay.Look.canceled += OnLookCanceled;
        _gameInput.GamePlay.Dodge.performed += OnDodgePreformed;
        _gameInput.GamePlay.LightAttack.performed += OnLightAttackPreformed;

    }
    void UnregisterCallback()
    {
        _gameInput.GamePlay.Move.performed -= OnMovePreformed;
        _gameInput.GamePlay.Move.canceled -= OnMoveCanceled;
        _gameInput.GamePlay.Look.performed -= OnLookPreformed;
        _gameInput.GamePlay.Look.canceled -= OnLookCanceled;
        _gameInput.GamePlay.Dodge.performed -= OnDodgePreformed;
        _gameInput.GamePlay.LightAttack.performed -= OnLightAttackPreformed;

    }
    void OnMovePreformed(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        State.SetMoveInput(context.ReadValue<Vector2>());
    }
    void OnMoveCanceled(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        State.SetMoveInput(Vector2.zero);
    }
    void OnLookCanceled(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        State.SetLookInput(Vector2.zero);
    }
    void OnLookPreformed(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        State.SetLookInput(context.ReadValue<Vector2>());
    }
    void OnDodgePreformed(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        DodgePressed?.Invoke();
    }
    void OnLightAttackPreformed(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        LightAttackPressed?.Invoke();
    }

    public bool HasActiveMoveControl()
    {
        foreach (InputControl control in _gameInput.GamePlay.Move.controls)
        {
            if (control.IsActuated(MoveControlActuationThreshold))
            {
                return true;
            }
        }

        return false;
    }
    
}
