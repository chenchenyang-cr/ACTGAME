using UnityEngine;

[RequireComponent(typeof(PlayerInputReader), typeof(PlayerInputBuffer))]
public sealed class PlayerCommandInputAdapter : MonoBehaviour
{
    [SerializeField]
    private PlayerInputReader inputReader;
    [SerializeField]
    private PlayerInputBuffer inputBuffer;

    private void Awake()
    {
        if (inputReader == null)
        {
            inputReader = GetComponent<PlayerInputReader>();
        }
        if (inputBuffer == null)
        {
            inputBuffer = GetComponent<PlayerInputBuffer>();
        }
    }

    private void OnEnable()
    {
        inputReader.JumpPressed += BufferJump;
        inputReader.DodgePressed += BufferDodge;
        inputReader.LightAttackPressed += BufferLightAttack;
    }

    private void OnDisable()
    {
        inputReader.JumpPressed -= BufferJump;
        inputReader.DodgePressed -= BufferDodge;
        inputReader.LightAttackPressed -= BufferLightAttack;
    }

    private void BufferJump()
    {
        inputBuffer.AddInput(PlayerActionCommand.Jump);
    }

    private void BufferDodge()
    {
        inputBuffer.AddInput(PlayerActionCommand.Dodge);
    }

    private void BufferLightAttack()
    {
        inputBuffer.AddInput(PlayerActionCommand.LightAttack);
    }
}
