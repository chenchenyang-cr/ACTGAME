#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CombatEditor
{
    // Editor-only fixtures; no scene or ability assets are changed by validation.
    public sealed class ParticleHitBoxValidationReceiver : MonoBehaviour, ICombatDamageReceiver
    {
        public CombatTeam Team { get; set; } = CombatTeam.Enemy;
        public bool Accept = true;
        public int Hits;
        public CombatHitRequest LastRequest;
        public bool TryReceiveHit(in CombatHitRequest request, out CombatHitResolution resolution)
        {
            LastRequest = request;
            if (!Accept)
            {
                resolution = CombatHitResolution.Rejected;
                return false;
            }
            Hits++;
            resolution = CombatHitResolution.Normal(request.Damage, request.PoiseDamage);
            return true;
        }
    }

    public static class ParticleHitBoxValidation
    {
        private const string PendingKey = "CombatEditor.ParticleHitBoxValidation.Pending";
        private static int checks;

        [InitializeOnLoadMethod]
        private static void Register()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static void RunBatch()
        {
            try
            {
                RunCollisionChecks();
                SessionState.SetInt(PendingKey, checks);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            int pending = SessionState.GetInt(PendingKey, 0);
            if (pending == 0 || state != PlayModeStateChange.EnteredPlayMode) return;
            SessionState.EraseInt(PendingKey);
            checks = pending;
            try
            {
                RunLifetimeChecks();
                Debug.Log($"PASS: Particle HitBox {checks} checks (native collision and Play mode lifecycle)");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("Particle HitBox: " + message);
            checks++;
        }

        private static void RunCollisionChecks()
        {
            var objects = new List<Object>();
            T Keep<T>(T obj) where T : Object { objects.Add(obj); return obj; }
            var ownerObject = Keep(new GameObject("Validation owner"));
            ownerObject.transform.position = new Vector3(4000, 4000, 4000);
            var owner = ownerObject.AddComponent<CombatController>();
            ownerObject.AddComponent<ParticleHitBoxValidationReceiver>().Team = CombatTeam.Player;
            owner.BeginHitExecution();
            var target = Keep(new GameObject("Validation target"));
            target.transform.position = ownerObject.transform.position;
            var receiver = target.AddComponent<ParticleHitBoxValidationReceiver>();
            target.AddComponent<SphereCollider>().radius = .1f;
            var secondCollider = new GameObject("Second target collider");
            secondCollider.transform.SetParent(target.transform, false);
            secondCollider.AddComponent<SphereCollider>().radius = .1f;
            var ability = Keep(ScriptableObject.CreateInstance<AbilityScriptableObject>());
            ability.Clip = Keep(new AnimationClip { legacy = true });
            ability.Clip.SetCurve("", typeof(Transform), "localPosition.x",
                AnimationCurve.Linear(0, 0, 1, 0));

            AbilityEventObj_Particles NewParticle()
            {
                var source = Keep(ScriptableObject.CreateInstance<AbilityEventObj_Particles>());
                source.EnableCollision = true;
                source.HitProfile = Keep(ScriptableObject.CreateInstance<CombatHitProfile>());
                return source;
            }
            HitBox CreateBox(AbilityEventObj_HitSource source)
            {
                var root = Keep(new GameObject("Validation particle"));
                root.transform.position = target.transform.position;
                var settings = source is AbilityEventObj_Particles p ? p.Collision : new ParticleHitBoxSettings();
                var collider = settings.CreateCollider(root);
                var hitBox = collider.gameObject.AddComponent<HitBox>();
                hitBox.Init(owner, ability, source);
                return hitBox;
            }
            int events = 0;
            CombatHitConfirmedEvent lastEvent = default;
            void OnHit(CombatHitConfirmedEvent e) { events++; lastEvent = e; }
            CombatHitEventBus.HitConfirmed += OnHit;
            try
            {
                var original = Keep(ScriptableObject.CreateInstance<AbilityEventObj_Particles>());
                Check(!original.EnableCollision && !original.HasHitBox, "old particles default to no collision");
                foreach (ParticleColliderShape shape in Enum.GetValues(typeof(ParticleColliderShape)))
                {
                    var root = Keep(new GameObject("Shape validation"));
                    root.transform.position = target.transform.position + Vector3.right * 10f;
                    root.transform.rotation = Quaternion.Euler(0, 30, 0);
                    root.transform.localScale = new Vector3(2, 3, 4);
                    var settings = new ParticleHitBoxSettings
                    {
                        Shape = shape, Offset = new Vector3(1, 2, 3),
                        Rotation = new Vector3(0, 45, 0),
                        BoxSize = new Vector3(1, 2, 3), Radius = .3f, Height = 1.4f,
                        CapsuleDirection = 2
                    };
                    var collider = settings.CreateCollider(root);
                    Check(collider.isTrigger && collider.transform.parent == root.transform, "trigger belongs to whole effect");
                    Check(Vector3.Distance(collider.transform.position, root.transform.TransformPoint(settings.Offset)) < .001f,
                        "offset follows scaled and rotated root");
                    Check(Quaternion.Angle(collider.transform.localRotation, Quaternion.Euler(settings.Rotation)) < .01f,
                        "local collider rotation");
                    Check(shape == ParticleColliderShape.Box ? collider is BoxCollider :
                        shape == ParticleColliderShape.Sphere ? collider is SphereCollider : collider is CapsuleCollider,
                        "requested collider shape");
                }

                foreach (ParticleColliderShape shape in Enum.GetValues(typeof(ParticleColliderShape)))
                {
                    var shapeEvent = NewParticle();
                    shapeEvent.Collision.Shape = shape;
                    int before = receiver.Hits;
                    CreateBox(shapeEvent).SampleHits();
                    Check(receiver.Hits == before + 1, "native damage overlap for " + shape);
                }
                receiver.Hits = 0;
                events = 0;
                var a = NewParticle();
                a.HitProfile.Damage = 17; a.HitProfile.PoiseDamage = 9; a.HitProfile.StaggerDuration = .2f;
                a.HitProfile.HitReaction = CombatHitReactionPolicy.EveryHit; a.HitProfile.CanBeParried = false;
                var box = CreateBox(a);
                box.SampleHits(); box.SampleHits();
                Check(receiver.Hits == 1 && events == 1, "Single and multiple target colliders dedupe");
                Check(receiver.LastRequest.Damage == 17 && receiver.LastRequest.PoiseDamage == 9 &&
                    receiver.LastRequest.HitReaction == CombatHitReactionPolicy.EveryHit &&
                    Mathf.Abs(receiver.LastRequest.StaggerDuration - .2f) < .001f,
                    "damage and reaction config reach receiver");
                Check(lastEvent.SourceHitBoxEvent == a && lastEvent.HitBox == box && lastEvent.Damage == 17,
                    "particle publishes existing confirmed-hit feedback event");
                CreateBox(a).SampleHits();
                Check(receiver.Hits == 1, "same source recreation preserves action Single dedupe");
                var b = NewParticle();
                b.HitProfile = a.HitProfile;
                CreateBox(b).SampleHits();
                Check(receiver.Hits == 2, "separate particle events count separately");
                var weapon = Keep(ScriptableObject.CreateInstance<AbilityEventObj_CreateHitBox>());
                weapon.HitProfile = a.HitProfile;
                CreateBox(weapon).SampleHits();
                Check(receiver.Hits == 3, "weapon and particle count independently");
                owner.EndHitExecution(); owner.BeginHitExecution();
                CreateBox(a).SampleHits();
                Check(receiver.Hits == 4, "next action resets Single count");

                var repeated = NewParticle();
                repeated.HitProfile.HitMode = CombatHitMode.Repeated;
                repeated.HitProfile.RepeatIntervalFrames = 6; repeated.HitProfile.MaximumHitsPerTarget = 3;
                var repeatedBox = CreateBox(repeated);
                repeatedBox.UpdateAnimationTime(0); repeatedBox.SampleHits();
                repeatedBox.UpdateAnimationTime(5f / 60); repeatedBox.SampleHits();
                Check(receiver.Hits == 5, "repeated interval blocks early overlap");
                repeatedBox.UpdateAnimationTime(6f / 60); repeatedBox.SampleHits();
                Check(receiver.Hits == 6 && receiver.LastRequest.HitSequenceIndex == 2, "repeat at frame interval");
                repeatedBox.UpdateAnimationTime(12f / 60); repeatedBox.SampleHits();
                repeatedBox.UpdateAnimationTime(18f / 60); repeatedBox.SampleHits();
                Check(receiver.Hits == 7 && receiver.LastRequest.HitSequenceIndex == 3, "maximum hits per target");

                var rejected = NewParticle();
                var rejectedBox = CreateBox(rejected);
                receiver.Accept = false; rejectedBox.SampleHits();
                Check(receiver.Hits == 7, "rejected damage does not confirm hit");
                receiver.Accept = true; rejectedBox.SampleHits();
                Check(receiver.Hits == 8 && receiver.LastRequest.HitSequenceIndex == 1, "rejection consumes no count");

                var filtered = NewParticle();
                filtered.HitProfile.TargetLayers = 0;
                var filteredBox = CreateBox(filtered);
                filteredBox.SampleHits();
                Check(receiver.Hits == 8, "target layer filtering");
                filtered.HitProfile.TargetLayers = ~0;
                receiver.Team = CombatTeam.Player; filteredBox.SampleHits();
                Check(receiver.Hits == 8, "friendly fire blocked");
                filtered.HitProfile.AllowFriendlyFire = true; filteredBox.SampleHits();
                Check(receiver.Hits == 9, "friendly fire opt-in");

                var lifetime = NewParticle();
                var lifetimeBox = CreateBox(lifetime);
                lifetimeBox.transform.parent.gameObject.SetActive(false);
                lifetimeBox.SampleHits();
                Check(receiver.Hits == 9, "inactive effect causes no hits");
                receiver.Team = CombatTeam.Enemy;
                lifetimeBox.transform.parent.gameObject.SetActive(true);
                lifetimeBox.CancelHits(); lifetimeBox.SampleHits();
                Check(receiver.Hits == 9, "cancel ends collision immediately");

                // Serialized field paths survive the shared-base extraction.
                var serialized = new SerializedObject(weapon.HitProfile);
                foreach (string field in new[] { "Damage", "PoiseDamage", "HitMode", "MaximumHitsPerTarget",
                    "EnableHitAnimationSpeed", "HitStopFrames", "HitVfxPrefab", "HitCameraShakeProfile" })
                    Check(serialized.FindProperty(field) != null, "SO serialized path " + field);
                var trackData = new SerializedObject(weapon);
                Check(trackData.FindProperty("HitProfile") != null &&
                    trackData.FindProperty("Damage") == null && trackData.FindProperty("ObjData") != null,
                    "track stores SO reference and spatial data only");
                var missing = NewParticle();
                missing.HitProfile = null;
                CreateBox(missing).SampleHits();
                Check(receiver.Hits == 9, "unconfigured SO reference cannot deal damage");
                var existingAbility = AssetDatabase.LoadAssetAtPath<AbilityScriptableObject>(
                    "Assets/_Project/CombatEditor/ScriptableObjects/Abilities/Swordman/Unagi_Attack_01.asset");
                Check(existingAbility != null && existingAbility.events.Exists(e => e.Obj is AbilityEventObj_CreateHitBox h && h.HitProfile != null),
                    "existing weapon ability still loads");
            }
            finally
            {
                CombatHitEventBus.HitConfirmed -= OnHit;
                for (int i = objects.Count - 1; i >= 0; i--)
                    if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            }
        }

        private static void RunLifetimeChecks()
        {
            var ownerObject = new GameObject("Lifecycle owner");
            var owner = ownerObject.AddComponent<CombatController>();
            owner._animator = ownerObject.AddComponent<Animator>();
            var prefab = new GameObject("Lifecycle particle prefab");
            prefab.AddComponent<ParticleSystem>();
            var source = ScriptableObject.CreateInstance<AbilityEventObj_Particles>();
            var ability = ScriptableObject.CreateInstance<AbilityScriptableObject>();
            var clip = new AnimationClip { legacy = true };
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 0));
            ability.Clip = clip;
            var effect = (AbilityEventEffect_Particles)source.Initialize();
            effect._combatController = owner;
            effect.AnimObj = ability;
            effect.eve = new AbilityEvent { Obj = source, EventTime = .1f };
            source.ObjData.TargetObj = prefab;
            GameObject instance = null;
            var target = new GameObject("Lifecycle target");
            target.AddComponent<SphereCollider>().radius = .1f;
            var receiver = target.AddComponent<ParticleHitBoxValidationReceiver>();
            try
            {
                source.EnableCollision = true;
                source.HitProfile = ScriptableObject.CreateInstance<CombatHitProfile>();
                source.HitProfile.HitMode = CombatHitMode.Repeated;
                source.HitProfile.RepeatIntervalFrames = 6;
                effect.StartEffect();
                var hitBox = effect.CurrentHitBox;
                instance = hitBox.transform.parent.gameObject;
                Check(hitBox.Owner == owner && hitBox.SourceEvent == source, "StartEffect initializes particle hit source");
                var clipId = clip.GetInstanceID();
                owner.ClipID_To_EventEffects = new Dictionary<int, List<AbilityEventWithEffects>>
                {
                    [clipId] = new List<AbilityEventWithEffects>
                    {
                        new AbilityEventWithEffects { eve = effect.eve, effect = effect }
                    }
                };
                owner.RunningEventsOnClip(clipId, .1f, 0); hitBox.SampleHits();
                owner.RunningEventsOnClip(clipId, .2f, 0); hitBox.SampleHits();
                Check(receiver.Hits == 2, "point-event dispatch advances repeated-hit frames");
                effect.EndEffect(); hitBox.SampleHits();
                Check(receiver.Hits == 2 && effect.CurrentHitBox == null && !effect.IsRunning,
                    "EndEffect cancels hits before deferred destruction");
                Object.DestroyImmediate(instance); instance = null;
                source.EnableCollision = false;
                effect.StartEffect();
                Check(effect.CurrentHitBox == null, "disabled collision spawns no hit source");
                // Get the spawned object only for fixture cleanup.
                instance = GameObject.Find(prefab.name + "(Clone)");
                effect.EndEffect();
            }
            finally
            {
                if (instance != null) Object.DestroyImmediate(instance);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(ownerObject);
                Object.DestroyImmediate(prefab);
                if (source.HitProfile != null) Object.DestroyImmediate(source.HitProfile);
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(ability);
                Object.DestroyImmediate(clip);
            }
        }
    }
}
#endif
