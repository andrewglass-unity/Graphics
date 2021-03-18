using System;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;

namespace UnityEditor.Rendering.HighDefinition
{
    using CED = CoreEditorDrawer<SerializedFrameSettings>;

    // Mirrors MaterialQuality enum and adds `FromQualitySettings`
    enum MaterialQualityMode
    {
        Low,
        Medium,
        High,
        FromQualitySettings,
    }

    static class MaterialQualityModeExtensions
    {
        public static MaterialQuality Into(this MaterialQualityMode quality)
        {
            switch (quality)
            {
                case MaterialQualityMode.High: return MaterialQuality.High;
                case MaterialQualityMode.Medium: return MaterialQuality.Medium;
                case MaterialQualityMode.Low: return MaterialQuality.Low;
                case MaterialQualityMode.FromQualitySettings: return (MaterialQuality)0;
                default: throw new ArgumentOutOfRangeException(nameof(quality));
            }
        }

        public static MaterialQualityMode Into(this MaterialQuality quality)
        {
            if (quality == (MaterialQuality)0)
                return MaterialQualityMode.FromQualitySettings;
            switch (quality)
            {
                case MaterialQuality.High: return MaterialQualityMode.High;
                case MaterialQuality.Medium: return MaterialQualityMode.Medium;
                case MaterialQuality.Low: return MaterialQualityMode.Low;
                default: throw new ArgumentOutOfRangeException(nameof(quality));
            }
        }
    }

    interface IDefaultFrameSettingsType
    {
        FrameSettingsRenderType GetFrameSettingsType();
    }

    partial class FrameSettingsUI
    {
        enum Expandable
        {
            RenderingPasses = 1 << 0,
            RenderingSettings = 1 << 1,
            LightingSettings = 1 << 2,
            AsynComputeSettings = 1 << 3,
            LightLoop = 1 << 4,
        }

        readonly static ExpandedState<Expandable, FrameSettings> k_ExpandedState = new ExpandedState<Expandable, FrameSettings>(~(-1), "HDRP");

        static Rect lastBoxRect;
        internal static CED.IDrawer Inspector(bool withOverride = true) => CED.Group(
            CED.Group((serialized, owner) =>
            {
                lastBoxRect = EditorGUILayout.BeginVertical("box");

                // Add dedicated scope here and on each FrameSettings field to have the contextual menu on everything
                Rect rect = GUILayoutUtility.GetRect(1, EditorGUIUtility.singleLineHeight);
                using (new SerializedFrameSettings.TitleDrawingScope(rect, FrameSettingsUI.frameSettingsHeaderContent, serialized))
                {
                    EditorGUI.LabelField(rect, FrameSettingsUI.frameSettingsHeaderContent, EditorStyles.boldLabel);
                }
            }),
            InspectorInnerbox(withOverride),
            CED.Group((serialized, owner) =>
            {
                EditorGUILayout.EndVertical();
                using (new SerializedFrameSettings.TitleDrawingScope(lastBoxRect, FrameSettingsUI.frameSettingsHeaderContent, serialized))
                {
                    //Nothing to draw.
                    //We just want to have a big blue bar at left that match the whole framesetting box.
                    //This is because framesettings will be considered as one bg block from prefab point
                    //of view as there is no way to separate it bit per bit in serialization and Prefab
                    //override API rely on SerializedProperty.
                }
            })
        );

        //separated to add enum popup on default frame settings
        internal static CED.IDrawer InspectorInnerbox(bool withOverride = true, bool isBoxed = true) => CED.Group(
            CED.FoldoutGroup(renderingSettingsHeaderContent, Expandable.RenderingPasses, k_ExpandedState, isBoxed ? FoldoutOption.Indent | FoldoutOption.Boxed : FoldoutOption.Indent,
                CED.Group(206, (serialized, owner) => Drawer_SectionRenderingSettings(serialized, owner, withOverride))
                ),
            CED.FoldoutGroup(lightSettingsHeaderContent, Expandable.LightingSettings, k_ExpandedState, isBoxed ? FoldoutOption.Indent | FoldoutOption.Boxed : FoldoutOption.Indent,
                CED.Group(206, (serialized, owner) => Drawer_SectionLightingSettings(serialized, owner, withOverride))
                ),
            CED.FoldoutGroup(asyncComputeSettingsHeaderContent, Expandable.AsynComputeSettings, k_ExpandedState, isBoxed ? FoldoutOption.Indent | FoldoutOption.Boxed : FoldoutOption.Indent,
                CED.Group(206, (serialized, owner) => Drawer_SectionAsyncComputeSettings(serialized, owner, withOverride))
                ),
            CED.FoldoutGroup(lightLoopSettingsHeaderContent, Expandable.LightLoop, k_ExpandedState, isBoxed ? FoldoutOption.Indent | FoldoutOption.Boxed : FoldoutOption.Indent,
                CED.Group(206, (serialized, owner) => Drawer_SectionLightLoopSettings(serialized, owner, withOverride))
                ),
            CED.Group((serialized, owner) =>
            {
                var hdrpAsset = GetHDRPAssetFor(owner);
                if (hdrpAsset != null)
                {
                    RenderPipelineSettings hdrpSettings = hdrpAsset.currentPlatformRenderPipelineSettings;
                    if (hdrpSettings.supportRayTracing)
                    {
                        bool rtEffectUseAsync = (serialized.IsEnabled(FrameSettingsField.SSRAsync) ?? false) || (serialized.IsEnabled(FrameSettingsField.SSAOAsync) ?? false)
                            //|| (serialized.IsEnabled(FrameSettingsField.ContactShadowsAsync) ?? false) // Contact shadow async is not visible in the UI for now and defaults to true.
                        ;
                        if (rtEffectUseAsync)
                            EditorGUILayout.HelpBox("Asynchronous execution of Raytracing effects is not supported. Asynchronous Execution will be forced to false for them", MessageType.Warning);
                    }
                }
            }));

        static HDRenderPipelineAsset GetHDRPAssetFor(Editor owner)
        {
            HDRenderPipelineAsset hdrpAsset;
            if (owner is HDRenderPipelineEditor)
            {
                // When drawing the inspector of a selected HDRPAsset in Project windows, access HDRP by owner drawing itself
                hdrpAsset = (owner as HDRenderPipelineEditor).target as HDRenderPipelineAsset;
            }
            else
            {
                // Else rely on GraphicsSettings are you should be in hdrp and owner could be probe or camera.
                hdrpAsset = HDRenderPipeline.currentAsset;
            }
            return hdrpAsset;
        }

        static FrameSettings GetDefaultFrameSettingsFor(Editor owner)
        {
            return owner is IDefaultFrameSettingsType getType
                ? HDRenderPipelineGlobalSettings.instance.GetDefaultFrameSettings(getType.GetFrameSettingsType())
                : HDRenderPipelineGlobalSettings.instance.GetDefaultFrameSettings(FrameSettingsRenderType.Camera);
        }

        static internal void Drawer_SectionRenderingSettings(SerializedFrameSettings serialized, Editor owner, bool withOverride)
        {
            bool isGUIenabled = GUI.enabled;
            FrameSettings defaultFrameSettings = GetDefaultFrameSettingsFor(owner);
            var frameSettingType = owner is IDefaultFrameSettingsType getType ? getType.GetFrameSettingsType() : FrameSettingsRenderType.Camera;
            var area = OverridableFrameSettingsArea.GetGroupContent(0, defaultFrameSettings, serialized);

            var hdrpAsset = GetHDRPAssetFor(owner);
            if (hdrpAsset != null)
            {
                RenderPipelineSettings hdrpSettings = hdrpAsset.currentPlatformRenderPipelineSettings;
                LitShaderMode defaultShaderLitMode;
                switch (hdrpSettings.supportedLitShaderMode)
                {
                    case RenderPipelineSettings.SupportedLitShaderMode.ForwardOnly:
                        defaultShaderLitMode = LitShaderMode.Forward;
                        break;
                    case RenderPipelineSettings.SupportedLitShaderMode.DeferredOnly:
                        defaultShaderLitMode = LitShaderMode.Deferred;
                        break;
                    case RenderPipelineSettings.SupportedLitShaderMode.Both:
                        defaultShaderLitMode = defaultFrameSettings.litShaderMode;
                        break;
                    default:
                        throw new System.ArgumentOutOfRangeException("Unknown ShaderLitMode");
                }

                area.AmmendInfo(FrameSettingsField.LitShaderMode,
                    overrideable: () => hdrpSettings.supportedLitShaderMode == RenderPipelineSettings.SupportedLitShaderMode.Both,
                    overridedDefaultValue: defaultShaderLitMode);

                bool hdrpAssetSupportForward = hdrpSettings.supportedLitShaderMode != RenderPipelineSettings.SupportedLitShaderMode.DeferredOnly;
                bool hdrpAssetSupportDeferred = hdrpSettings.supportedLitShaderMode != RenderPipelineSettings.SupportedLitShaderMode.ForwardOnly;
                bool hdrpAssetIsForward = hdrpSettings.supportedLitShaderMode == RenderPipelineSettings.SupportedLitShaderMode.ForwardOnly;
                bool hdrpAssetIsDeferred = hdrpSettings.supportedLitShaderMode == RenderPipelineSettings.SupportedLitShaderMode.DeferredOnly;
                bool frameSettingsOverrideToForward = serialized.GetOverrides(FrameSettingsField.LitShaderMode) && serialized.litShaderMode == LitShaderMode.Forward;
                bool frameSettingsOverrideToDeferred = serialized.GetOverrides(FrameSettingsField.LitShaderMode) && serialized.litShaderMode == LitShaderMode.Deferred;
                bool defaultForwardUsed = !serialized.GetOverrides(FrameSettingsField.LitShaderMode) && defaultShaderLitMode == LitShaderMode.Forward;
                bool defaultDeferredUsed = !serialized.GetOverrides(FrameSettingsField.LitShaderMode) && defaultShaderLitMode == LitShaderMode.Deferred;

                // Due to various reasons, MSAA and ray tracing are not compatible, if ray tracing is enabled on the asset. MSAA can not be enabled on the frame settings.
                bool msaaEnablable = ((hdrpAssetSupportForward && (frameSettingsOverrideToForward || defaultForwardUsed)) || hdrpAssetIsForward) && !hdrpSettings.supportRayTracing;
                area.AmmendInfo(
                    FrameSettingsField.MSAAMode,
                    overrideable: () => msaaEnablable,
                    ignoreDependencies: true,
                    overridedDefaultValue: defaultFrameSettings.msaaMode,
                    customGetter: () => serialized.msaaMode.GetEnumValue<MSAAMode>(),
                    customSetter: v => serialized.msaaMode.SetEnumValue((MSAAMode)v),
                    hasMixedValues: serialized.msaaMode.hasMultipleDifferentValues);

                bool msaaIsOff = (msaaEnablable && serialized.GetOverrides(FrameSettingsField.MSAAMode))
                    ? serialized.msaaMode.GetEnumValue<MSAAMode>() != MSAAMode.None
                    : defaultFrameSettings.msaaMode != MSAAMode.None;
                area.AmmendInfo(FrameSettingsField.AlphaToMask,
                    overrideable: () => msaaEnablable && !msaaIsOff,
                    ignoreDependencies: true,
                    overridedDefaultValue: msaaEnablable && defaultFrameSettings.IsEnabled(FrameSettingsField.AlphaToMask) && !msaaIsOff);

                bool depthPrepassEnablable = (hdrpAssetSupportDeferred && (defaultDeferredUsed || frameSettingsOverrideToDeferred)) || (hdrpAssetIsDeferred);
                area.AmmendInfo(FrameSettingsField.DepthPrepassWithDeferredRendering,
                    overrideable: () => depthPrepassEnablable,
                    ignoreDependencies: true,
                    overridedDefaultValue: depthPrepassEnablable && defaultFrameSettings.IsEnabled(FrameSettingsField.DepthPrepassWithDeferredRendering));

                bool clearGBufferEnablable = (hdrpAssetSupportDeferred && (defaultDeferredUsed || frameSettingsOverrideToDeferred)) || (hdrpAssetIsDeferred);
                area.AmmendInfo(FrameSettingsField.ClearGBuffers,
                    overrideable: () => clearGBufferEnablable,
                    ignoreDependencies: true,
                    overridedDefaultValue: clearGBufferEnablable && defaultFrameSettings.IsEnabled(FrameSettingsField.ClearGBuffers));

                area.AmmendInfo(FrameSettingsField.RayTracing, overrideable: () => hdrpSettings.supportRayTracing);
#if !ENABLE_VIRTUALTEXTURES
                area.AmmendInfo(FrameSettingsField.VirtualTexturing, overrideable: () => false);
#endif

                area.AmmendInfo(FrameSettingsField.RayTracing, overrideable: () => hdrpSettings.supportRayTracing);
#if !ENABLE_VIRTUALTEXTURES
                area.AmmendInfo(FrameSettingsField.VirtualTexturing, overrideable: () => false);
#endif
                area.AmmendInfo(FrameSettingsField.MotionVectors, overrideable: () => hdrpSettings.supportMotionVectors);
                area.AmmendInfo(FrameSettingsField.ObjectMotionVectors, overrideable: () => hdrpSettings.supportMotionVectors);
                area.AmmendInfo(FrameSettingsField.TransparentsWriteMotionVector, overrideable: () => hdrpSettings.supportMotionVectors);
                area.AmmendInfo(FrameSettingsField.Decals, overrideable: () => hdrpSettings.supportDecals);
                area.AmmendInfo(FrameSettingsField.DecalLayers, overrideable: () => hdrpSettings.supportDecalLayers);
                area.AmmendInfo(FrameSettingsField.Distortion, overrideable: () => hdrpSettings.supportDistortion);
                area.AmmendInfo(FrameSettingsField.RoughDistortion, overrideable: () => hdrpSettings.supportDistortion);

                area.AmmendInfo(FrameSettingsField.Postprocess, overrideable: () => (frameSettingType != FrameSettingsRenderType.CustomOrBakedReflection &&
                    frameSettingType != FrameSettingsRenderType.RealtimeReflection));

                area.AmmendInfo(
                    FrameSettingsField.LODBiasMode,
                    overridedDefaultValue: LODBiasMode.FromQualitySettings,
                    customGetter: () => serialized.lodBiasMode.GetEnumValue<LODBiasMode>(),
                    customSetter: v => serialized.lodBiasMode.SetEnumValue((LODBiasMode)v),
                    hasMixedValues: serialized.lodBiasMode.hasMultipleDifferentValues
                );
                area.AmmendInfo(FrameSettingsField.LODBiasQualityLevel,
                    overridedDefaultValue: ScalableLevel3ForFrameSettingsUIOnly.Low,
                    customGetter: () => (ScalableLevel3ForFrameSettingsUIOnly)serialized.lodBiasQualityLevel.intValue,
                    customSetter: v => serialized.lodBiasQualityLevel.intValue = (int)v,
                    overrideable: () => serialized.lodBiasMode.GetEnumValue<LODBiasMode>() != LODBiasMode.OverrideQualitySettings,
                    ignoreDependencies: true,
                    hasMixedValues: serialized.lodBiasQualityLevel.hasMultipleDifferentValues);

                if (hdrpAsset != null)
                {
                    area.AmmendInfo(FrameSettingsField.LODBias,
                        overridedDefaultValue: hdrpAsset.currentPlatformRenderPipelineSettings.lodBias[serialized.lodBiasQualityLevel.intValue],
                        customGetter: () => serialized.lodBias.floatValue,
                        customSetter: v => serialized.lodBias.floatValue = (float)v,
                        overrideable: () => serialized.lodBiasMode.GetEnumValue<LODBiasMode>() != LODBiasMode.FromQualitySettings,
                        ignoreDependencies: true,
                        labelOverride: serialized.lodBiasMode.GetEnumValue<LODBiasMode>() == LODBiasMode.ScaleQualitySettings ? "Scale Factor" : "LOD Bias",
                        hasMixedValues: serialized.lodBias.hasMultipleDifferentValues);
                }

                area.AmmendInfo(
                    FrameSettingsField.MaximumLODLevelMode,
                    overridedDefaultValue: MaximumLODLevelMode.FromQualitySettings,
                    customGetter: () => serialized.maximumLODLevelMode.GetEnumValue<MaximumLODLevelMode>(),
                    customSetter: v => serialized.maximumLODLevelMode.SetEnumValue((MaximumLODLevelMode)v),
                    hasMixedValues: serialized.maximumLODLevelMode.hasMultipleDifferentValues
                );
                area.AmmendInfo(FrameSettingsField.MaximumLODLevelQualityLevel,
                    overridedDefaultValue: ScalableLevel3ForFrameSettingsUIOnly.Low,
                    customGetter: () => (ScalableLevel3ForFrameSettingsUIOnly)serialized.maximumLODLevelQualityLevel.intValue,
                    customSetter: v => serialized.maximumLODLevelQualityLevel.intValue = (int)v,
                    overrideable: () => serialized.maximumLODLevelMode.GetEnumValue<MaximumLODLevelMode>() != MaximumLODLevelMode.OverrideQualitySettings,
                    ignoreDependencies: true,
                    hasMixedValues: serialized.maximumLODLevelQualityLevel.hasMultipleDifferentValues);

                if (hdrpAsset != null)
                {
                    area.AmmendInfo(FrameSettingsField.MaximumLODLevel,
                        overridedDefaultValue: hdrpAsset.currentPlatformRenderPipelineSettings.maximumLODLevel[serialized.maximumLODLevelQualityLevel.intValue],
                        customGetter: () => serialized.maximumLODLevel.intValue,
                        customSetter: v => serialized.maximumLODLevel.intValue = (int)v,
                        overrideable: () => serialized.maximumLODLevelMode.GetEnumValue<MaximumLODLevelMode>() != MaximumLODLevelMode.FromQualitySettings,
                        ignoreDependencies: true,
                        labelOverride: serialized.maximumLODLevelMode.GetEnumValue<MaximumLODLevelMode>() == MaximumLODLevelMode.OffsetQualitySettings ? "Offset Factor" : "Maximum LOD Level",
                        hasMixedValues: serialized.maximumLODLevel.hasMultipleDifferentValues);
                }

                area.AmmendInfo(FrameSettingsField.MaterialQualityLevel,
                    overridedDefaultValue: defaultFrameSettings.materialQuality.Into(),
                    customGetter: () => ((MaterialQuality)serialized.materialQuality.intValue).Into(),
                    customSetter: v => serialized.materialQuality.intValue = (int)((MaterialQualityMode)v).Into(),
                    hasMixedValues: serialized.materialQuality.hasMultipleDifferentValues
                );

                area.Draw(withOverride);
            }
            GUI.enabled = isGUIenabled;
        }

        // Use an enum to have appropriate UI enum field in the frame setting api
        // Do not use anywhere else
        enum ScalableLevel3ForFrameSettingsUIOnly
        {
            Low,
            Medium,
            High
        }

        static internal void Drawer_SectionLightingSettings(SerializedFrameSettings serialized, Editor owner, bool withOverride)
        {
            bool isGUIenabled = GUI.enabled;

            var hdrpAsset = GetHDRPAssetFor(owner);
            if (hdrpAsset != null)
            {
                RenderPipelineSettings hdrpSettings = hdrpAsset.currentPlatformRenderPipelineSettings;
                FrameSettings defaultFrameSettings = GetDefaultFrameSettingsFor(owner);
                var area = OverridableFrameSettingsArea.GetGroupContent(1, defaultFrameSettings, serialized);
                area.AmmendInfo(FrameSettingsField.Shadowmask, overrideable: () => hdrpSettings.supportShadowMask);
                area.AmmendInfo(FrameSettingsField.SSR, overrideable: () => hdrpSettings.supportSSR);
                area.AmmendInfo(FrameSettingsField.TransparentSSR, overrideable: () => (hdrpSettings.supportSSR && hdrpSettings.supportSSRTransparent));
                area.AmmendInfo(FrameSettingsField.SSAO, overrideable: () => hdrpSettings.supportSSAO);
                area.AmmendInfo(FrameSettingsField.SSGI, overrideable: () => hdrpSettings.supportSSGI);
                area.AmmendInfo(FrameSettingsField.VolumetricClouds, overrideable: () => hdrpSettings.supportVolumetricClouds);

                // SSS
                area.AmmendInfo(
                    FrameSettingsField.SubsurfaceScattering,
                    overridedDefaultValue: hdrpSettings.supportSubsurfaceScattering,
                    overrideable: () => hdrpSettings.supportSubsurfaceScattering
                );
                area.AmmendInfo(
                    FrameSettingsField.SssQualityMode,
                    overridedDefaultValue: SssQualityMode.FromQualitySettings,
                    customGetter: () => serialized.sssQualityMode.GetEnumValue<SssQualityMode>(),
                    customSetter: v  => serialized.sssQualityMode.SetEnumValue((SssQualityMode)v),
                    overrideable: () => hdrpSettings.supportSubsurfaceScattering
                    && (serialized.IsEnabled(FrameSettingsField.SubsurfaceScattering) ?? false),
                    ignoreDependencies: true,
                    hasMixedValues: serialized.sssQualityMode.hasMultipleDifferentValues
                );
                area.AmmendInfo(FrameSettingsField.SssQualityLevel,
                    overridedDefaultValue: ScalableLevel3ForFrameSettingsUIOnly.Low,
                    customGetter:       () => (ScalableLevel3ForFrameSettingsUIOnly)serialized.sssQualityLevel.intValue,// 3 levels
                    customSetter:       v  => serialized.sssQualityLevel.intValue = Math.Max(0, Math.Min((int)v, 2)),// Levels 0-2
                    overrideable: () => hdrpSettings.supportSubsurfaceScattering
                    && (serialized.IsEnabled(FrameSettingsField.SubsurfaceScattering) ?? false)
                    && (serialized.sssQualityMode.GetEnumValue<SssQualityMode>() == SssQualityMode.FromQualitySettings),
                    ignoreDependencies: true,
                    hasMixedValues: serialized.sssQualityLevel.hasMultipleDifferentValues
                );
                area.AmmendInfo(FrameSettingsField.SssCustomSampleBudget,
                    overridedDefaultValue: (int)DefaultSssSampleBudgetForQualityLevel.Low,
                    customGetter:       () => serialized.sssCustomSampleBudget.intValue,
                    customSetter:       v  => serialized.sssCustomSampleBudget.intValue = Math.Max(1, Math.Min((int)v, (int)DefaultSssSampleBudgetForQualityLevel.Max)),
                    overrideable: () => hdrpSettings.supportSubsurfaceScattering
                    && (serialized.IsEnabled(FrameSettingsField.SubsurfaceScattering) ?? false)
                    && (serialized.sssQualityMode.GetEnumValue<SssQualityMode>() != SssQualityMode.FromQualitySettings),
                    ignoreDependencies: true,
                    hasMixedValues: serialized.sssCustomSampleBudget.hasMultipleDifferentValues
                );

                area.AmmendInfo(FrameSettingsField.Volumetrics, overrideable: () => hdrpSettings.supportVolumetrics);
                area.AmmendInfo(FrameSettingsField.ReprojectionForVolumetrics, overrideable: () => hdrpSettings.supportVolumetrics);
                area.AmmendInfo(FrameSettingsField.LightLayers, overrideable: () => hdrpSettings.supportLightLayers);
                area.AmmendInfo(FrameSettingsField.ProbeVolume, overrideable: () => hdrpSettings.supportProbeVolume);
                area.AmmendInfo(FrameSettingsField.ScreenSpaceShadows, overrideable: () => hdrpSettings.hdShadowInitParams.supportScreenSpaceShadows);
                area.Draw(withOverride);
            }
            GUI.enabled = isGUIenabled;
        }

        static internal void Drawer_SectionAsyncComputeSettings(SerializedFrameSettings serialized, Editor owner, bool withOverride)
        {
            var area = GetFrameSettingSectionContent(2, serialized, owner);
            area.Draw(withOverride);
        }

        static internal void Drawer_SectionLightLoopSettings(SerializedFrameSettings serialized, Editor owner, bool withOverride)
        {
            var area = GetFrameSettingSectionContent(3, serialized, owner);
            area.Draw(withOverride);
        }

        static OverridableFrameSettingsArea GetFrameSettingSectionContent(int group, SerializedFrameSettings serialized, Editor owner)
        {
            FrameSettings defaultFrameSettings = GetDefaultFrameSettingsFor(owner);
            var area = OverridableFrameSettingsArea.GetGroupContent(group, defaultFrameSettings, serialized);
            return area;
        }
    }
}
