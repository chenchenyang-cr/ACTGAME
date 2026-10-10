using System.Collections.Generic;
using UnityEngine;

/// <summary>Keeps pose snapshots fading after their ability event has ended.</summary>
public sealed class CharacterEchoRuntimeHost : MonoBehaviour
{
    private readonly List<CharacterEchoVisualSession> sessions = new();

    public void Add(CharacterEchoVisualSession session)
    {
        if (session != null) sessions.Add(session);
    }

    private void LateUpdate()
    {
        for (int i = sessions.Count - 1; i >= 0; i--)
        {
            CharacterEchoVisualSession session = sessions[i];
            session.Advance(Time.deltaTime);
            if (!session.IsStopped || session.HasGhosts) continue;
            session.Dispose();
            sessions.RemoveAt(i);
        }
    }

    private void OnDisable()
    {
        foreach (CharacterEchoVisualSession session in sessions) session.Dispose();
        sessions.Clear();
    }
}
