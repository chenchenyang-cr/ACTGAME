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
        inputReader.DodgePressed += BufferDodge;
        inputReader.LightAttackPressed += BufferLightAttack;
    }

    private void OnDisable()
    {
        inputReader.DodgePressed -= BufferDodge;
        inputReader.LightAttackPressed -= BufferLightAttack;
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
