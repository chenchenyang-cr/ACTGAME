using UnityEngine;

public sealed class AirborneState : PlayerState
{
    private const float GroundedGraceDuration = 0.1f;
    private float elapsedTime;
    private bool hasLeftGround;

    public AirborneState(PlayerStateMachine machine) : base(machine) { }

    public override void Enter()
    {
        elapsedTime = 0f;
        hasLeftGround = false;
        Machine.Movement.SetRotationMode(PlayerRotationMode.MovementDirection);
    }

    public override void Tick(Vector2 moveInput, bool hasMoveInput)
    {
        elapsedTime += Time.deltaTime;
        Machine.Movement.Tick(moveInput, hasMoveInput);

        if (!Machine.IsGrounded)
        {
            hasLeftGround = true;
            return;
        }

        if (hasLeftGround || elapsedTime >= GroundedGraceDuration)
        {
            Machine.ReturnToControllableState();
        }
    }
}
