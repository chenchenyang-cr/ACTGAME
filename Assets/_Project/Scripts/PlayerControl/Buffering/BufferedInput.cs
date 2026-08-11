public readonly struct BufferedInput
{
   public PlayerActionCommand Command { get; }
   public float Time { get; }

   public BufferedInput(PlayerActionCommand command, float time)
   {
       Command = command;
       Time = time;
   }
}
