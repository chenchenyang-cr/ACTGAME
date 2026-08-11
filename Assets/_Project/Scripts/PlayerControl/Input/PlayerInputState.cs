using UnityEngine;

public class PlayerInputState //数据容器
{
    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }

    public void SetMoveInput(Vector2 moveInput)
    {
        MoveInput = moveInput;
    }

    public void SetLookInput(Vector2 lookInput)
    {
        LookInput = lookInput;
    }

    public void Reset()
    {
        MoveInput = Vector2.zero;
        LookInput = Vector2.zero;
    }
}
