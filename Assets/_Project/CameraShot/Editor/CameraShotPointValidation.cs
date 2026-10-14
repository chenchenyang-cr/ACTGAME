using System;
using CombatEditor;
using UnityEditor;
using UnityEngine;

namespace CombatCamera.Editor
{
    internal static class CameraShotPointValidation
    {
        public static int Run()
        {
            var actor = new GameObject("CameraShot point test") { hideFlags = HideFlags.HideAndDontSave };
            var config = ScriptableObject.CreateInstance<AbilityEventObj_CameraShot>();
            var profile = ScriptableObject.CreateInstance<CameraShotProfile>();
            var ability = ScriptableObject.CreateInstance<AbilityScriptableObject>();
            var clip = new AnimationClip();
            int checks = 0;
            void Check(bool ok, string name) { if (!ok) throw new Exception("CameraShot point: " + name); checks++; }
            try {
                var controller = actor.AddComponent<CombatController>();
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "localPosition.x"), AnimationCurve.Linear(0, 0, 1, 0));
                ability.Clip = clip;
                Check(config.ReturnToOriginalCamera, "existing nodes return by default");
                Check(!config.IgnoreHitStop, "existing nodes follow action time by default");
                config.Profile = profile; config.Channel = 741;
                config.Timing = new CameraShotTiming { EnterSeconds = .1f, ExitSeconds = .1f,
                    EnterCurve = AnimationCurve.Linear(0, 0, 1, 1), ExitCurve = AnimationCurve.Linear(0, 0, 1, 1) };
                config.HoldSeconds = .2f;
                var entry = new AbilityEvent { Obj = config, EventTime = .2f, EventRange = new Vector2(.8f, .9f), Previewable = true };
                Check(config.GetEventTimeType() == AbilityEventObj.EventTimeType.EventTime, "uses particle-style point type");
                Check(Mathf.Abs(entry.GetEventStartTime() - .2f) < .0001f, "trigger comes from EventTime");
                Check(Mathf.Abs(config.Duration - .4f) < .0001f, "duration is enter plus hold plus exit");
                config.ReturnToOriginalCamera = false;
                Check(Mathf.Abs(config.Duration - .3f) < .0001f, "no-return duration excludes exit");
                config.ReturnToOriginalCamera = true;
                var preview = new AbilityEventPreview_CameraShot(config) { eve = entry, AnimObj = ability, _combatController = controller };
                preview.FetchCurrentValues();
                Check(preview.PreviewInRange(.2f) && preview.PreviewInRange(.599f) && !preview.PreviewInRange(.601f), "preview lifetime follows node seconds");
                config.ReturnToOriginalCamera = false;
                Check(preview.PreviewInRange(.9f), "no-return preview stays at final orbit after the node");
                config.ReturnToOriginalCamera = true;
                var effect = new AbilityEventEffect_CameraShot(config) { eve = entry, AnimObj = ability, _combatController = controller };
                CameraShotRuntime.Reset();
                effect.StartEffect(); effect.EffectRunning(.25f);
                Check(CameraShotRuntime.ActiveCount == 1, "point starts one shot");
                effect.EffectRunning(.7f);
                Check(effect.IsRunning && CameraShotRuntime.ActiveCount == 0, "completed point stays latched");
                effect.EffectRunning(.8f);
                Check(CameraShotRuntime.ActiveCount == 0, "later frames do not retrigger");
                entry.EventTime = 0; effect.EffectRunning(.02f);
                Check(CameraShotRuntime.ActiveCount == 1, "a new action loop retriggers the point");
                effect.EndEffect();
                Check(!effect.IsRunning && CameraShotRuntime.ActiveCount == 0, "action interruption clears pending shot");
                config.IgnoreHitStop = true;
                effect.StartEffect(); effect.EffectRunning(.8f);
                Check(effect.IsRunning && CameraShotRuntime.ActiveCount == 1, "independent clock does not finish from action progress");
                effect.EndEffect();
                Check(CameraShotRuntime.ActiveCount == 0, "independent clock still respects action interruption");
                preview.DestroyPreview();
                return checks;
            }
            finally {
                CameraShotRuntime.Reset();
                UnityEngine.Object.DestroyImmediate(actor); UnityEngine.Object.DestroyImmediate(config);
                UnityEngine.Object.DestroyImmediate(profile); UnityEngine.Object.DestroyImmediate(ability); UnityEngine.Object.DestroyImmediate(clip);
            }
        }
    }
}
