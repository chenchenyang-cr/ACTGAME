using UnityEditor;
using UnityEngine;

public static class Slash270RotationDiagnostic
{
    public static void Run()
    {
        const string path = "Assets/_Project/VFX/Effects/Prefabs/Slash270/Slash270.prefab";
        var prefab = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var systems = prefab.GetComponentsInChildren<ParticleSystem>(true);
            var controller = prefab.GetComponent<Slash270Controller>();
            Debug.Log($"Slash270 diagnostic: systems={systems.Length}, curve={controller.rotationSpeedCurve.Evaluate(0.5f):F3}");
            for (int i = 0; i < systems.Length; i++)
            {
                var rotation = systems[i].rotationOverLifetime;
                Debug.Log($"Slash270 diagnostic before play: {i}, enabled={rotation.enabled}, mode={rotation.y.mode}, speed={rotation.y.Evaluate(0):F3}");
            }
            controller.Play();
            var root = prefab.GetComponent<ParticleSystem>();
            foreach (float time in new[] { 0.05f, 0.25f })
            {
                root.Simulate(time, true, true, false);
                for (int i = 0; i < systems.Length; i++)
                {
                    var particles = new ParticleSystem.Particle[4];
                    int count = systems[i].GetParticles(particles);
                    Debug.Log($"Slash270 diagnostic at {time:F2}s: {i}, particles={count}, rotationY={(count > 0 ? particles[0].rotation3D.y : 0):F3}");
                }
            }
            controller.Stop();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefab);
        }
    }
}
