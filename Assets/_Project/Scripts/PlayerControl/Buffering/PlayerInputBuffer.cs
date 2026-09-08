using System.Collections.Generic;
using UnityEngine;

public class PlayerInputBuffer : MonoBehaviour
{
    [SerializeField]
    [Min(0)]
    private float bufferDuration = 0.35f;
    private readonly Queue<BufferedInput> inputBuffer = new();
    [SerializeField, Min(0f)] private float parryBufferDuration = 0.12f;
    private bool parryPending;
    private BufferedInput parryInput;
    public bool TryPeekParry(out BufferedInput input)
    {
        if (parryPending && Time.time - parryInput.Time > parryBufferDuration) parryPending = false;
        input = parryInput;
        return parryPending;
    }
    public void ConsumeParry() => parryPending = false;
    private void OnDisable() => Clear();
    public int Count => inputBuffer.Count;

    public void AddInput(PlayerActionCommand command)
    {
        if (command == PlayerActionCommand.Parry)
        {
            parryInput = new BufferedInput(command, Time.time);
            parryPending = true;
            return;
        }
        RemoveExpiredInputs();
        inputBuffer.Enqueue(new BufferedInput(command, Time.time));
    }

    private void RemoveExpiredInputs()
    {
        while (Count > 0)
        {
            float timeSinceInput = Time.time - inputBuffer.Peek().Time;
            if (timeSinceInput > bufferDuration)
            {
                inputBuffer.Dequeue();
            }
            else
            {
                break;
            }

        }
    }

    public bool TryPeek(out BufferedInput bufferedInput)
    {
        RemoveExpiredInputs();
        if(inputBuffer.Count==0)
        {
            bufferedInput = default;
            return false;
        }
        bufferedInput = inputBuffer.Peek();
        return true;
    }
    public bool TryConsume(PlayerActionCommand command, out BufferedInput bufferedInput)
    {
        RemoveExpiredInputs();
        if (inputBuffer.Count == 0)
        {
            bufferedInput = default;
            return false;
        }
        if(inputBuffer.Peek().Command!=command)
        {
            bufferedInput = default;
            return false;
        }
        bufferedInput = inputBuffer.Dequeue();
        return true;
    }
    public bool TryConsumeNext(out BufferedInput bufferedInput)
    {
        RemoveExpiredInputs();
        if (inputBuffer.Count == 0)
        {
            bufferedInput = default;
            return false;
        }
        bufferedInput = inputBuffer.Dequeue();
        return true;
    }

    public void Clear()
    {
        parryPending = false;
        inputBuffer.Clear();
    }

}
