using System.Collections.Generic;
using UnityEditor.AnimatedValues;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

namespace UnityEditor.Rendering.Universal
{
    [CanEditMultipleObjects]
    [CustomEditorForRenderPipeline(typeof(Light), typeof(UniversalRenderPipelineAsset))]
    class UniversalRenderPipelineLightEditor : LightEditor
    {
        static class Styles
        {
            public static readonly GUIContent SpotAngle = EditorGUIUtility.TrTextContent("Spot Angle", "Controls the angle in degrees at the base of a Spot light's cone.");

            public static readonly GUIContent BakingWarning = EditorGUIUtility.TrTextContent("Light mode is currently overridden to Realtime mode. Enable Baked Global Illumination to use Mixed or Baked light modes.");
            public static readonly GUIContent DisabledLightWarning = EditorGUIUtility.TrTextContent("Lighting has been disabled in at least one Scene view. Any changes applied to lights in the Scene will not be updated in these views until Lighting has been enabled again.");
            public static readonly GUIContent SunSourceWarning = EditorGUIUtility.TrTextContent("This light is set as the current Sun Source, which requires a directional light. Go to the Lighting Window's Environment settings to edit the Sun Source.");

            public static readonly GUIContent ShadowRealtimeSettings = EditorGUIUtility.TrTextContent("Realtime Shadows", "Settings for realtime direct shadows.");
            public static readonly GUIContent ShadowStrength = EditorGUIUtility.TrTextContent("Strength", "Controls how dark the shadows cast by the light will be.");
            public static readonly GUIContent ShadowNearPlane = EditorGUIUtility.TrTextContent("Near Plane", "Controls the value for the near clip plane when rendering shadows. Currently clamped to 0.1 units or 1% of the lights range property, whichever is lower.");
            public static readonly GUIContent ShadowNormalBias = EditorGUIUtility.TrTextContent("Normal", "Controls the distance shadow caster vertices are offset along their normals when rendering shadow maps. Currently ignored for Point Lights.");
            public static readonly GUIContent ShadowDepthBias = EditorGUIUtility.TrTextContent("Depth");

            // Resolution (default or custom)
            public static readonly GUIContent ShadowResolution = EditorGUIUtility.TrTextContent("Resolution", $"Sets the rendered resolution of the shadow maps. A higher resolution increases the fidelity of shadows at the cost of GPU performance and memory usage. Rounded to the next power of two, and clamped to be at least {UniversalAdditionalLightData.AdditionalLightsShadowMinimumResolution}.");
            public static readonly int[] ShadowResolutionDefaultValues =
            {
                UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierCustom,
                UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow,
                UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium,
                UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierHigh
            };
            public static readonly GUIContent[] ShadowResolutionDefaultOptions =
            {
                new GUIContent("Custom"),
                UniversalRenderPipelineAssetEditor.Styles.additionalLightsShadowResolutionTierNames[0],
                UniversalRenderPipelineAssetEditor.Styles.additionalLightsShadowResolutionTierNames[1],
                UniversalRenderPipelineAssetEditor.Styles.additionalLightsShadowResolutionTierNames[2],
            };

            // Bias (default or custom)
            public static GUIContent shadowBias = EditorGUIUtility.TrTextContent("Bias", "Select if the Bias should use the settings from the Pipeline Asset or Custom settings.");
            public static int[] optionDefaultValues = { 0, 1 };
            public static GUIContent[] displayedDefaultOptions =
            {
                new GUIContent("Custom"),
                new GUIContent("Use Pipeline Settings")
            };
        }

        public bool typeIsSame { get { return !settings.lightType.hasMultipleDifferentValues; } }
        public bool shadowTypeIsSame { get { return !settings.shadowsType.hasMultipleDifferentValues; } }
        public bool lightmappingTypeIsSame { get { return !settings.lightmapping.hasMultipleDifferentValues; } }
        public Light lightProperty { get { return target as Light; } }

        public bool spotOptionsValue { get { return typeIsSame && lightProperty.type == LightType.Spot; } }
        public bool pointOptionsValue { get { return typeIsSame && lightProperty.type == LightType.Point; } }
        public bool dirOptionsValue { get { return typeIsSame && lightProperty.type == LightType.Directional; } }
        public bool areaOptionsValue { get { return typeIsSame && (lightProperty.type == LightType.Rectangle || lightProperty.type == LightType.Disc); } }
        public bool shadowResolutionOptionsValue  { get { return spotOptionsValue || pointOptionsValue; } } // Currently only additional punctual lights can specify per-light shadow resolution

        //  Area light shadows not supported
        public bool runtimeOptionsValue { get { return typeIsSame && (lightProperty.type != LightType.Rectangle && !settings.isCompletelyBaked); } }
        public bool bakedShadowRadius { get { return typeIsSame && (lightProperty.type == LightType.Point || lightProperty.type == LightType.Spot) && settings.isBakedOrMixed; } }
        public bool bakedShadowAngle { get { return typeIsSame && lightProperty.type == LightType.Directional && settings.isBakedOrMixed; } }
        public bool shadowOptionsValue { get { return shadowTypeIsSame && lightProperty.shadows != LightShadows.None; } }
#pragma warning disable 618
        public bool bakingWarningValue { get { return !UnityEditor.Lightmapping.bakedGI && lightmappingTypeIsSame && settings.isBakedOrMixed; } }
#pragma warning restore 618
        public bool showLightBounceIntensity { get { return true; } }

        public bool isShadowEnabled { get { return settings.shadowsType.intValue != 0; } }

        UniversalRenderPipelineSerializedLight serializedLight { get; set; }

        protected override void OnEnable()
        {
            serializedLight = new UniversalRenderPipelineSerializedLight(serializedObject, settings);
        }

        public override void OnInspectorGUI()
        {
            serializedLight.Update();

            serializedLight.settings.DrawLightType();

            Light light = target as Light;
            if (LightType.Directional != light.type && light == RenderSettings.sun)
            {
                EditorGUILayout.HelpBox(Styles.SunSourceWarning.text, MessageType.Warning);
            }

            EditorGUILayout.Space();

            // When we are switching between two light types that don't show the range (directional and area lights)
            // we want the fade group to stay hidden.
            if (dirOptionsValue)
#if UNITY_2020_1_OR_NEWER
                serializedLight.settings.DrawRange();
#else
                serializedLight.settings.DrawRange(m_AnimAreaOptions.target);
#endif

            // Spot angle
            if (spotOptionsValue)
                DrawSpotAngle();

            // Area width & height
            if (areaOptionsValue)
                serializedLight.settings.DrawArea();

            serializedLight.settings.DrawColor();

            EditorGUILayout.Space();

            CheckLightmappingConsistency();
            if (areaOptionsValue && light.type != LightType.Disc)
            {
                serializedLight.settings.DrawLightmapping();
            }

            serializedLight.settings.DrawIntensity();

            if (showLightBounceIntensity)
                serializedLight.settings.DrawBounceIntensity();

            ShadowsGUI();

            serializedLight.settings.DrawRenderMode();
            serializedLight.settings.DrawCullingMask();

            EditorGUILayout.Space();

            if (SceneView.lastActiveSceneView != null)
            {
#if UNITY_2019_1_OR_NEWER
                var sceneLighting = SceneView.lastActiveSceneView.sceneLighting;
#else
                var sceneLighting = SceneView.lastActiveSceneView.m_SceneLighting;
#endif
                if (!sceneLighting)
                    EditorGUILayout.HelpBox(Styles.DisabledLightWarning.text, MessageType.Warning);
            }

            serializedLight.Apply();
        }

        void CheckLightmappingConsistency()
        {
            //Universal render-pipeline only supports baked area light, enforce it as this inspector is the universal one.
            if (settings.isAreaLightType && settings.lightmapping.intValue != (int)LightmapBakeType.Baked)
            {
                settings.lightmapping.intValue = (int)LightmapBakeType.Baked;
                serializedObject.ApplyModifiedProperties();
            }
        }

        void SetOptions(AnimBool animBool, bool initialize, bool targetValue)
        {
            if (initialize)
            {
                animBool.value = targetValue;
                animBool.valueChanged.AddListener(Repaint);
            }
            else
            {
                animBool.target = targetValue;
            }
        }

        void DrawSpotAngle()
        {
            serializedLight.settings.DrawInnerAndOuterSpotAngle();
        }

        void DrawAdditionalShadowData()
        {
            // 0: Custom bias - 1: Bias values defined in Pipeline settings
            int selectedUseAdditionalData = serializedLight.additionalLightData.usePipelineSettings ? 1 : 0;
            Rect r = EditorGUILayout.GetControlRect(true);
            EditorGUI.BeginProperty(r, Styles.shadowBias, serializedLight.useAdditionalDataProp);
            {
                using (var checkScope = new EditorGUI.ChangeCheckScope())
                {
                    selectedUseAdditionalData = EditorGUI.IntPopup(r, Styles.shadowBias, selectedUseAdditionalData, Styles.displayedDefaultOptions, Styles.optionDefaultValues);
                    if (checkScope.changed)
                    {
                        foreach (var additionData in serializedLight.lightsAdditionalData)
                            additionData.usePipelineSettings = selectedUseAdditionalData != 0;

                        serializedLight.Apply();
                    }
                }
            }
            EditorGUI.EndProperty();

            if (!serializedLight.useAdditionalDataProp.hasMultipleDifferentValues)
            {
                if (selectedUseAdditionalData != 1) // Custom Bias
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        using (var checkScope = new EditorGUI.ChangeCheckScope())
                        {
                            EditorGUILayout.Slider(settings.shadowsBias, 0f, 10f, Styles.ShadowDepthBias);
                            EditorGUILayout.Slider(settings.shadowsNormalBias, 0f, 10f, Styles.ShadowNormalBias);
                            if (checkScope.changed)
                                serializedLight.Apply();
                        }
                    }
                }
            }
        }

        void DrawShadowsResolutionGUI()
        {
            int shadowResolutionTier = serializedLight.additionalLightData.additionalLightsShadowResolutionTier;

            using (new EditorGUILayout.HorizontalScope())
            {
                using (var checkScope = new EditorGUI.ChangeCheckScope())
                {
                    Rect r = EditorGUILayout.GetControlRect(true);
                    r.width += 30;

                    shadowResolutionTier = EditorGUI.IntPopup(r, Styles.ShadowResolution, shadowResolutionTier, Styles.ShadowResolutionDefaultOptions, Styles.ShadowResolutionDefaultValues);
                    if (shadowResolutionTier == UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierCustom)
                    {
                        // show the custom value field GUI.
                        var newResolution = EditorGUILayout.IntField(settings.shadowsResolution.intValue, GUILayout.ExpandWidth(false));
                        settings.shadowsResolution.intValue = Mathf.Max(UniversalAdditionalLightData.AdditionalLightsShadowMinimumResolution, Mathf.NextPowerOfTwo(newResolution));
                    }
                    else
                    {
                        if (GraphicsSettings.renderPipelineAsset is UniversalRenderPipelineAsset urpAsset)
                            EditorGUILayout.LabelField($"{urpAsset.GetAdditionalLightsShadowResolution(shadowResolutionTier)} ({urpAsset.name})", GUILayout.ExpandWidth(false));
                    }
                    if (checkScope.changed)
                    {
                        serializedLight.additionalLightsShadowResolutionTierProp.intValue = shadowResolutionTier;
                        serializedLight.Apply();
                    }
                }
            }
        }

        void ShadowsGUI()
        {
            settings.DrawShadowsType();

            if (!shadowOptionsValue)
                return;

            using (new EditorGUI.IndentLevelScope())
            {
                // Baked Shadow radius
                if (bakedShadowRadius)
                    settings.DrawBakedShadowRadius();

                if (bakedShadowAngle)
                    settings.DrawBakedShadowAngle();

                if (runtimeOptionsValue)
                {
                    EditorGUILayout.LabelField(Styles.ShadowRealtimeSettings, EditorStyles.boldLabel);
                    using (new EditorGUI.IndentLevelScope())
                    {
                        // Resolution
                        if (shadowResolutionOptionsValue)
                            DrawShadowsResolutionGUI();

                        EditorGUILayout.Slider(settings.shadowsStrength, 0f, 1f, Styles.ShadowStrength);

                        // Bias
                        DrawAdditionalShadowData();

                        // this min bound should match the calculation in SharedLightData::GetNearPlaneMinBound()
                        float nearPlaneMinBound = Mathf.Min(0.01f * settings.range.floatValue, 0.1f);
                        EditorGUILayout.Slider(settings.shadowsNearPlane, nearPlaneMinBound, 10.0f, Styles.ShadowNearPlane);
                    }
                }
            }

            if (bakingWarningValue)
                EditorGUILayout.HelpBox(Styles.BakingWarning.text, MessageType.Warning);
        }

        protected override void OnSceneGUI()
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))
                return;

            Light light = target as Light;

            switch (light.type)
            {
                case LightType.Spot:
                    using (new Handles.DrawingScope(Matrix4x4.TRS(light.transform.position, light.transform.rotation, Vector3.one)))
                    {
                        CoreLightEditorUtilities.DrawSpotLightGizmo(light);
                    }
                    break;

                case LightType.Point:
                    using (new Handles.DrawingScope(Matrix4x4.TRS(light.transform.position, Quaternion.identity, Vector3.one)))
                    {
                        CoreLightEditorUtilities.DrawPointLightGizmo(light);
                    }
                    break;

                case LightType.Rectangle:
                    using (new Handles.DrawingScope(Matrix4x4.TRS(light.transform.position, light.transform.rotation, Vector3.one)))
                    {
                        CoreLightEditorUtilities.DrawRectangleLightGizmo(light);
                    }
                    break;

                case LightType.Disc:
                    using (new Handles.DrawingScope(Matrix4x4.TRS(light.transform.position, light.transform.rotation, Vector3.one)))
                    {
                        CoreLightEditorUtilities.DrawDiscLightGizmo(light);
                    }
                    break;

                case LightType.Directional:
                    using (new Handles.DrawingScope(Matrix4x4.TRS(light.transform.position, light.transform.rotation, Vector3.one)))
                    {
                        CoreLightEditorUtilities.DrawDirectionalLightGizmo(light);
                    }
                    break;

                default:
                    base.OnSceneGUI();
                    break;
            }
        }
    }
}
