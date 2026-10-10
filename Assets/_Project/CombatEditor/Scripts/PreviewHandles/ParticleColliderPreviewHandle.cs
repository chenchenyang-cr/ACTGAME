#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CombatEditor
{
    public sealed class ParticleColliderPreviewHandle : PreviewerOnObject
    {
        public AbilityEventPreview_Particles ParticlePreview;
        private readonly BoxBoundsHandle box = new BoxBoundsHandle { handleColor = Color.green, wireframeColor = Color.green };
        private readonly SphereBoundsHandle sphere = new SphereBoundsHandle { handleColor = Color.green, wireframeColor = Color.green };
        private readonly CapsuleBoundsHandle capsule = new CapsuleBoundsHandle { handleColor = Color.green, wireframeColor = Color.green };

        public override void PaintHandle()
        {
            if (ParticlePreview == null || !ParticlePreview.CollisionVisible ||
                !ParticlePreview.Obj.EnableCollision || ParticlePreview.Obj.Collision == null)
                return;

            var config = ParticlePreview.Obj;
            var settings = config.Collision;
            var previousZTest = Handles.zTest;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            try
            {
                // Use the particle root matrix: changing the handle changes only collider data.
                using (new Handles.DrawingScope(Color.green, transform.parent.localToWorldMatrix *
                       Matrix4x4.Rotate(Quaternion.Euler(settings.Rotation))))
                {
                    Vector3 center = Quaternion.Inverse(Quaternion.Euler(settings.Rotation)) * settings.Offset;
                    PrimitiveBoundsHandle bounds;
                    if (settings.Shape == ParticleColliderShape.Box)
                    {
                        box.center = center;
                        box.size = settings.BoxSize;
                        bounds = box;
                    }
                    else if (settings.Shape == ParticleColliderShape.Sphere)
                    {
                        sphere.center = center;
                        sphere.radius = settings.Radius;
                        bounds = sphere;
                    }
                    else
                    {
                        capsule.center = center;
                        capsule.radius = settings.Radius;
                        capsule.height = settings.Height;
                        capsule.heightAxis = (CapsuleBoundsHandle.HeightAxis)Mathf.Clamp(settings.CapsuleDirection, 0, 2);
                        bounds = capsule;
                    }

                    EditorGUI.BeginChangeCheck();
                    bounds.DrawHandle();
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(config, "Adjust Particle Collider");
                        settings.Offset = Quaternion.Euler(settings.Rotation) * bounds.center;
                        if (bounds == box) settings.BoxSize = box.size;
                        else if (bounds == sphere) settings.Radius = sphere.radius;
                        else
                        {
                            settings.Radius = capsule.radius;
                            settings.Height = capsule.height;
                        }
                        EditorUtility.SetDirty(config);
                        SceneView.RepaintAll();
                    }
                }
            }
            finally
            {
                Handles.zTest = previousZTest;
            }
        }

        private void OnDestroy() => SelfDestroy();
    }
}
#endif
