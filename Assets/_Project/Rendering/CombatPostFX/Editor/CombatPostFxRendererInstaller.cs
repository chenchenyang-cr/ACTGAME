#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CombatPostFX.Editor
{
    [InitializeOnLoad]
    internal static class CombatPostFxRendererInstaller
    {
        private const string SessionKey = "CombatPostFX.RenderersChecked.v2";

        static CombatPostFxRendererInstaller()
        {
            EditorApplication.delayCall += EnsureInstalledOnce;
        }

        [MenuItem("Tools/Combat Post FX/Install Renderer Feature")]
        private static void InstallFromMenu()
        {
            int count = EnsureInstalled();
            Debug.Log(count > 0
                ? $"Combat Post FX installed in {count} URP renderer asset(s)."
                : "Combat Post FX is already installed in all project URP renderer assets.");
        }

        private static void EnsureInstalledOnce()
        {
            if (SessionState.GetBool(SessionKey, false) || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            SessionState.SetBool(SessionKey, true);
            EnsureInstalled();
        }

        private static int EnsureInstalled()
        {
            int installed = 0;
            string[] guids = AssetDatabase.FindAssets("t:UniversalRendererData",
                new[] { "Assets/_Project/Settings/Rendering" });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ScriptableRendererData rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
                if (rendererData == null || ContainsFeature(rendererData))
                    continue;

                var feature = ScriptableObject.CreateInstance<CombatPostFxRendererFeature>();
                feature.name = "Combat Post FX";
                feature.hideFlags = HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(feature, rendererData);
                var serializedData = new SerializedObject(rendererData);
                SerializedProperty features = serializedData.FindProperty("m_RendererFeatures");
                int index = features.arraySize;
                features.InsertArrayElementAtIndex(index);
                features.GetArrayElementAtIndex(index).objectReferenceValue = feature;

                SerializedProperty featureMap = serializedData.FindProperty("m_RendererFeatureMap");
                if (featureMap != null)
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                    featureMap.InsertArrayElementAtIndex(featureMap.arraySize);
                    featureMap.GetArrayElementAtIndex(featureMap.arraySize - 1).longValue = localId;
                }

                serializedData.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(rendererData);
                EditorUtility.SetDirty(feature);
                installed++;
            }

            if (installed > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            return installed;
        }

        private static bool ContainsFeature(ScriptableRendererData rendererData)
        {
            var serializedData = new SerializedObject(rendererData);
            SerializedProperty features = serializedData.FindProperty("m_RendererFeatures");
            for (int i = 0; i < features.arraySize; i++)
            {
                Object value = features.GetArrayElementAtIndex(i).objectReferenceValue;
                if (value is CombatPostFxRendererFeature)
                    return true;
            }

            // Do not silently remove unrelated missing features; Unity's renderer inspector can repair those.
            return false;
        }
    }
}
#endif
