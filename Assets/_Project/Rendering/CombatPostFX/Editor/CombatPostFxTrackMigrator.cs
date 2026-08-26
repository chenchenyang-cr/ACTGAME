#if UNITY_EDITOR
#pragma warning disable 0618
using System.Collections.Generic;
using System.Linq;
using CombatEditor;
using UnityEditor;
using UnityEngine;

namespace CombatPostFX.Editor
{
    [InitializeOnLoad]
    internal static class CombatPostFxTrackMigrator
    {
        private const string SessionKey = "CombatPostFX.IndependentTracksMigration.v2";
        private static readonly string[] AbilityFolders =
            { "Assets/_Project/CombatEditor/ScriptableObjects/Abilities" };
        private static readonly string[] CollectionFolders =
            { "Assets/_Project/CombatEditor/ScriptableObjects/GameplayCollections/PostFX" };

        static CombatPostFxTrackMigrator()
        {
            EditorApplication.delayCall += MigrateOnce;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += MigrateOnce;
        }

        [MenuItem("Tools/Combat Post FX/Migrate Legacy Collections To Tracks")]
        private static void MigrateFromMenu()
        {
            int migrated = MigrateAll();
            Debug.Log($"Combat Post FX migrated {migrated} legacy event(s) to independent tracks.");
        }

        private static void MigrateOnce()
        {
            if (SessionState.GetBool(SessionKey, false) || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            SessionState.SetBool(SessionKey, true);
            MigrateAll();
        }

        private static int MigrateAll()
        {
            int migrated = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:AbilityScriptableObject", AbilityFolders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ability = AssetDatabase.LoadAssetAtPath<AbilityScriptableObject>(path);
                if (ability == null)
                    continue;

                bool changed = false;
                for (int i = ability.events.Count - 1; i >= 0; i--)
                {
                    AbilityEvent sourceEvent = ability.events[i];
                    if (!(sourceEvent.Obj is AbilityEventObj_CombatPostFx legacy))
                        continue;

                    CombatPostFxCollection collection = legacy.collection != null
                        ? legacy.collection
                        : FindFallbackCollection(ability.name);
                    if (collection == null)
                        continue;

                    List<AbilityEventObj_PostFxTrack> tracks = CreateTracks(collection);
                    if (tracks.Count == 0)
                        continue;

                    Vector2 range = sourceEvent.EventRange;
                    if (ability.name == "Combo_Attack_01_01" && range == new Vector2(0f, 1f))
                        range = new Vector2(0.15454546f, 0.37272727f);

                    ability.events.RemoveAt(i);
                    for (int trackIndex = 0; trackIndex < tracks.Count; trackIndex++)
                    {
                        AbilityEventObj_PostFxTrack track = tracks[trackIndex];
                        track.name = track.GetType().Name.Replace("AbilityEventObj_", string.Empty);
                        AssetDatabase.AddObjectToAsset(track, ability);
                        ability.events.Insert(i + trackIndex, CloneEvent(sourceEvent, range, track));
                    }

                    Object.DestroyImmediate(legacy, true);
                    changed = true;
                    migrated++;
                }

                // The first aggregate prototype could be removed by Unity while its schema was changing.
                // Recover its preserved settings asset into real tracks when no post-FX tracks exist yet.
                if (ability.name == "Combo_Attack_01_01" &&
                    !ability.events.Any(item => item.Obj is AbilityEventObj_PostFxTrack))
                {
                    CombatPostFxCollection recovery = FindFallbackCollection(ability.name);
                    if (recovery != null)
                    {
                        List<AbilityEventObj_PostFxTrack> tracks = CreateTracks(recovery);
                        var source = new AbilityEvent
                        {
                            EventRange = new Vector2(0.15454546f, 0.37272727f),
                            EventMultiRange = new[] { 0.2f, 0.4f, 0.6f, 0.8f }
                        };
                        for (int trackIndex = 0; trackIndex < tracks.Count; trackIndex++)
                        {
                            AbilityEventObj_PostFxTrack track = tracks[trackIndex];
                            track.name = track.GetType().Name.Replace("AbilityEventObj_", string.Empty);
                            AssetDatabase.AddObjectToAsset(track, ability);
                            ability.events.Add(CloneEvent(source, source.EventRange, track));
                        }
                        changed = tracks.Count > 0;
                        if (changed)
                            migrated++;
                    }
                }

                if (changed)
                {
                    EditorUtility.SetDirty(ability);
                    AssetDatabase.ImportAsset(path);
                }
            }

            if (migrated > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            return migrated;
        }

        private static CombatPostFxCollection FindFallbackCollection(string abilityName)
        {
            string[] matches = AssetDatabase.FindAssets($"{abilityName}_PostFx t:CombatPostFxCollection",
                CollectionFolders);
            return matches.Length > 0
                ? AssetDatabase.LoadAssetAtPath<CombatPostFxCollection>(
                    AssetDatabase.GUIDToAssetPath(matches[0]))
                : null;
        }

        private static AbilityEvent CloneEvent(AbilityEvent source, Vector2 range,
            AbilityEventObj_PostFxTrack track)
        {
            return new AbilityEvent
            {
                EventTime = source.EventTime,
                EventRange = range,
                EventMultiRange = source.EventMultiRange != null
                    ? (float[])source.EventMultiRange.Clone()
                    : new float[4],
                Previewable = source.Previewable,
                Obj = track
            };
        }

        private static List<AbilityEventObj_PostFxTrack> CreateTracks(CombatPostFxCollection source)
        {
            var result = new List<AbilityEventObj_PostFxTrack>();
            if (source.radialBlur.enabled)
            {
                var track = Create<AbilityEventObj_PostFxRadialBlur>(source.radialBlur);
                track.SampleDistance = source.radialBlur.sampleDistance;
                result.Add(track);
            }
            if (source.chromaticAberration.enabled)
            {
                var track = Create<AbilityEventObj_PostFxChromaticAberration>(source.chromaticAberration);
                track.Spread = source.chromaticAberration.spread;
                result.Add(track);
            }
            if (source.vignette.enabled)
            {
                var track = Create<AbilityEventObj_PostFxVignette>(source.vignette);
                track.InnerRadius = source.vignette.innerRadius;
                track.OuterRadius = source.vignette.outerRadius;
                track.Color = source.vignette.color;
                result.Add(track);
            }
            if (source.flash.enabled)
            {
                var track = Create<AbilityEventObj_PostFxFlash>(source.flash);
                track.Color = source.flash.color;
                result.Add(track);
            }
            if (source.color.enabled)
            {
                var track = Create<AbilityEventObj_PostFxColor>(source.color);
                track.Desaturation = source.color.desaturation;
                track.TintStrength = source.color.tintStrength;
                track.Tint = source.color.tint;
                result.Add(track);
            }
            if (source.glitch.enabled)
            {
                var track = Create<AbilityEventObj_PostFxGlitch>(source.glitch);
                track.Speed = source.glitch.speed;
                track.RowDensity = source.glitch.rowDensity;
                track.Displacement = source.glitch.displacement;
                track.ChannelSplit = source.glitch.channelSplit;
                result.Add(track);
            }
            if (source.speedLines.enabled)
            {
                var track = Create<AbilityEventObj_PostFxSpeedLines>(source.speedLines);
                track.Density = source.speedLines.density;
                track.Sharpness = source.speedLines.sharpness;
                track.InnerRadius = source.speedLines.innerRadius;
                track.OuterRadius = source.speedLines.outerRadius;
                track.RotationSpeed = source.speedLines.rotationSpeed;
                track.Color = source.speedLines.color;
                result.Add(track);
            }
            if (source.filmGrain.enabled)
            {
                var track = Create<AbilityEventObj_PostFxFilmGrain>(source.filmGrain);
                track.Scale = source.filmGrain.scale;
                track.Speed = source.filmGrain.speed;
                result.Add(track);
            }
            return result;
        }

        private static T Create<T>(CombatPostFxTrack source) where T : AbilityEventObj_PostFxTrack
        {
            var track = ScriptableObject.CreateInstance<T>();
            track.Intensity = source.intensity;
            track.IntensityCurve = CopyCurve(source.curve);
            return track;
        }

        private static AnimationCurve CopyCurve(AnimationCurve source)
        {
            if (source == null)
                return AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
            return new AnimationCurve(source.keys)
            {
                preWrapMode = source.preWrapMode,
                postWrapMode = source.postWrapMode
            };
        }
    }
}
#pragma warning restore 0618
#endif
