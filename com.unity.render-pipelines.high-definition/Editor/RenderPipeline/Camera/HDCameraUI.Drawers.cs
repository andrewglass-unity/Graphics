using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;

namespace UnityEditor.Rendering.HighDefinition
{
    using CED = CoreEditorDrawer<SerializedHDCamera>;

    static partial class HDCameraUI
    {
        enum Expandable
        {
            Projection = 1 << 0,
            Physical = 1 << 1,
            Output = 1 << 2,
            Orthographic = 1 << 3,
            RenderLoop = 1 << 4,
            Rendering = 1 << 5,
            Environment = 1 << 6,
        }

        enum ProjectionType
        {
            Perspective,
            Orthographic
        }

        enum ProjectionMatrixMode
        {
            Explicit,
            Implicit,
            PhysicalPropertiesBased,
        }

        static bool s_FovChanged;
        static float s_FovLastValue;

        static readonly ExpandedState<Expandable, Camera> k_ExpandedState = new ExpandedState<Expandable, Camera>(Expandable.Projection, "HDRP");

        public static readonly CED.IDrawer SectionProjectionSettings = CED.FoldoutGroup(
            Styles.projectionSettingsHeaderContent,
            Expandable.Projection,
            k_ExpandedState,
            FoldoutOption.Indent,
            CED.Group(
                Drawer_Projection
                ),
            PhysicalCamera.Drawer,
            CED.Group(
                Drawer_FieldClippingPlanes
            )
        );

        public static readonly CED.IDrawer SectionOutputSettings = CED.FoldoutGroup(
            Styles.outputSettingsHeaderContent,
            Expandable.Output,
            k_ExpandedState,
            FoldoutOption.Indent,
            CED.Group(
#if ENABLE_VR && ENABLE_XR_MANAGEMENT
                Drawer_SectionXRRendering,
#endif
#if ENABLE_MULTIPLE_DISPLAYS
                Drawer_SectionMultiDisplay,
#endif
                Drawer_FieldRenderTarget,
                Drawer_AllowDynamicResolution,
                Drawer_FieldDepth,
                Drawer_FieldNormalizedViewPort
            )
        );

        public static readonly CED.IDrawer[] Inspector = new[]
        {
            SectionProjectionSettings,
            Rendering.Drawer,
            Environment.Drawer,
            SectionOutputSettings,
        };

        static void Drawer_Projection(SerializedHDCamera p, Editor owner)
        {
            // Most of this is replicated from CameraEditor.DrawProjection as we don't want to draw
            // it the same way it's done in non-SRP cameras. Unfortunately, because a lot of the
            // code is internal, we have to copy/paste some stuff from the editor code :(

            var cam = p.baseCameraSettings;

            Rect perspectiveRect = EditorGUILayout.GetControlRect();

            ProjectionType projectionType;
            EditorGUI.BeginProperty(perspectiveRect, Styles.projectionContent, cam.orthographic);
            {
                projectionType = cam.orthographic.boolValue ? ProjectionType.Orthographic : ProjectionType.Perspective;

                EditorGUI.BeginChangeCheck();
                projectionType = (ProjectionType)EditorGUI.EnumPopup(perspectiveRect, Styles.projectionContent, projectionType);
                if (EditorGUI.EndChangeCheck())
                    cam.orthographic.boolValue = (projectionType == ProjectionType.Orthographic);
            }
            EditorGUI.EndProperty();

            if (cam.orthographic.hasMultipleDifferentValues)
                return;

            if (projectionType == ProjectionType.Orthographic)
            {
                EditorGUILayout.PropertyField(cam.orthographicSize, Styles.sizeContent);
            }
            else
            {
                float fovCurrentValue;
                bool multipleDifferentFovValues = false;
                bool isPhysicalCamera = p.projectionMatrixMode.intValue == (int)ProjectionMatrixMode.PhysicalPropertiesBased;

                var rect = EditorGUILayout.GetControlRect();

                var guiContent = EditorGUI.BeginProperty(rect, Styles.FOVAxisModeContent, cam.fovAxisMode);
                EditorGUI.showMixedValue = cam.fovAxisMode.hasMultipleDifferentValues;

                EditorGUI.BeginChangeCheck();
                var fovAxisNewVal = (int)(Camera.FieldOfViewAxis)EditorGUI.EnumPopup(rect, guiContent, (Camera.FieldOfViewAxis)cam.fovAxisMode.intValue);
                if (EditorGUI.EndChangeCheck())
                    cam.fovAxisMode.intValue = fovAxisNewVal;
                EditorGUI.EndProperty();

                bool fovAxisVertical = cam.fovAxisMode.intValue == 0;

                if (!fovAxisVertical && !cam.fovAxisMode.hasMultipleDifferentValues)
                {
                    var targets = p.serializedObject.targetObjects;
                    var camera0 = targets[0] as Camera;
                    float aspectRatio = isPhysicalCamera ? cam.sensorSize.vector2Value.x / cam.sensorSize.vector2Value.y : camera0.aspect;
                    // camera.aspect is not serialized so we have to check all targets.
                    fovCurrentValue = Camera.VerticalToHorizontalFieldOfView(camera0.fieldOfView, aspectRatio);
                    if (targets.Cast<Camera>().Any(camera => camera.fieldOfView != fovCurrentValue))
                        multipleDifferentFovValues = true;
                }
                else
                {
                    fovCurrentValue = cam.verticalFOV.floatValue;
                    multipleDifferentFovValues = cam.fovAxisMode.hasMultipleDifferentValues;
                }

                EditorGUI.showMixedValue = multipleDifferentFovValues;
                var content = EditorGUI.BeginProperty(EditorGUILayout.BeginHorizontal(), Styles.fieldOfViewContent, cam.verticalFOV);
                EditorGUI.BeginDisabledGroup(p.projectionMatrixMode.hasMultipleDifferentValues || isPhysicalCamera && (cam.sensorSize.hasMultipleDifferentValues || cam.fovAxisMode.hasMultipleDifferentValues));
                EditorGUI.BeginChangeCheck();
                s_FovLastValue = EditorGUILayout.Slider(content, fovCurrentValue, 0.00001f, 179f);
                s_FovChanged = EditorGUI.EndChangeCheck();
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
                EditorGUI.EndProperty();
                EditorGUI.showMixedValue = false;

                content = EditorGUI.BeginProperty(EditorGUILayout.BeginHorizontal(), Styles.physicalCameraContent, p.projectionMatrixMode);
                EditorGUI.showMixedValue = p.projectionMatrixMode.hasMultipleDifferentValues;

                EditorGUI.BeginChangeCheck();
                isPhysicalCamera = EditorGUILayout.Toggle(content, isPhysicalCamera);
                if (EditorGUI.EndChangeCheck())
                    p.projectionMatrixMode.intValue = isPhysicalCamera ? (int)ProjectionMatrixMode.PhysicalPropertiesBased : (int)ProjectionMatrixMode.Implicit;
                EditorGUILayout.EndHorizontal();
                EditorGUI.EndProperty();

                EditorGUI.showMixedValue = false;
                if (s_FovChanged && (!isPhysicalCamera || p.projectionMatrixMode.hasMultipleDifferentValues))
                {
                    cam.verticalFOV.floatValue = fovAxisVertical
                        ? s_FovLastValue
                        : Camera.HorizontalToVerticalFieldOfView(s_FovLastValue, (p.serializedObject.targetObjects[0] as Camera).aspect);
                }
                else if (s_FovChanged && isPhysicalCamera && !p.projectionMatrixMode.hasMultipleDifferentValues)
                {
                    cam.verticalFOV.floatValue = fovAxisVertical
                        ? s_FovLastValue
                        : Camera.HorizontalToVerticalFieldOfView(s_FovLastValue, (p.serializedObject.targetObjects[0] as Camera).aspect);
                }
            }
        }

        static void Drawer_FieldClippingPlanes(SerializedHDCamera p, Editor owner)
        {
            CoreEditorUtils.DrawMultipleFields(
                Styles.clippingPlaneMultiFieldTitle,
                new[] { p.baseCameraSettings.nearClippingPlane, p.baseCameraSettings.farClippingPlane },
                new[] { Styles.nearPlaneContent, Styles.farPlaneContent });
        }

        static void Drawer_FieldNormalizedViewPort(SerializedHDCamera p, Editor owner)
        {
            EditorGUILayout.PropertyField(p.baseCameraSettings.normalizedViewPortRect, Styles.viewportContent);
        }

        static void Drawer_FieldDepth(SerializedHDCamera p, Editor owner)
        {
            EditorGUILayout.PropertyField(p.baseCameraSettings.depth, Styles.depthContent);
        }

        static void Drawer_AllowDynamicResolution(SerializedHDCamera p, Editor owner)
        {
            EditorGUILayout.PropertyField(p.allowDynamicResolution, Styles.allowDynResContent);
            p.baseCameraSettings.allowDynamicResolution.boolValue = p.allowDynamicResolution.boolValue;
        }

        static void Drawer_FieldRenderTarget(SerializedHDCamera p, Editor owner)
        {
            EditorGUILayout.PropertyField(p.baseCameraSettings.targetTexture);

            // show warning if we have deferred but manual MSAA set
            // only do this if the m_TargetTexture has the same values across all target cameras
            if (!p.baseCameraSettings.targetTexture.hasMultipleDifferentValues)
            {
                var targetTexture = p.baseCameraSettings.targetTexture.objectReferenceValue as RenderTexture;
                if (targetTexture
                    && targetTexture.antiAliasing > 1
                    && p.frameSettings.litShaderMode == LitShaderMode.Deferred)
                {
                    EditorGUILayout.HelpBox(Styles.msaaWarningMessage, MessageType.Warning, true);
                }
            }
        }

#if ENABLE_VR && ENABLE_XR_MANAGEMENT
        static void Drawer_SectionXRRendering(SerializedHDCamera p, Editor owner)
        {
            EditorGUILayout.PropertyField(p.xrRendering, Styles.xrRenderingContent);
        }

#endif

#if ENABLE_MULTIPLE_DISPLAYS
        static void Drawer_SectionMultiDisplay(SerializedHDCamera p, Editor owner)
        {
            if (ModuleManager_ShouldShowMultiDisplayOption())
            {
                var prevDisplay = p.baseCameraSettings.targetDisplay.intValue;
                EditorGUILayout.IntPopup(p.baseCameraSettings.targetDisplay, DisplayUtility_GetDisplayNames(), DisplayUtility_GetDisplayIndices(), Styles.targetDisplayContent);
                if (prevDisplay != p.baseCameraSettings.targetDisplay.intValue)
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
        }

#endif

        static MethodInfo k_DisplayUtility_GetDisplayIndices = Type.GetType("UnityEditor.DisplayUtility,UnityEditor")
            .GetMethod("GetDisplayIndices");
        static int[] DisplayUtility_GetDisplayIndices()
        {
            return (int[])k_DisplayUtility_GetDisplayIndices.Invoke(null, null);
        }

        static MethodInfo k_DisplayUtility_GetDisplayNames = Type.GetType("UnityEditor.DisplayUtility,UnityEditor")
            .GetMethod("GetDisplayNames");
        static GUIContent[] DisplayUtility_GetDisplayNames()
        {
            return (GUIContent[])k_DisplayUtility_GetDisplayNames.Invoke(null, null);
        }

        static MethodInfo k_ModuleManager_ShouldShowMultiDisplayOption = Type.GetType("UnityEditor.Modules.ModuleManager,UnityEditor")
            .GetMethod("ShouldShowMultiDisplayOption", BindingFlags.Static | BindingFlags.NonPublic);
        static bool ModuleManager_ShouldShowMultiDisplayOption()
        {
            return (bool)k_ModuleManager_ShouldShowMultiDisplayOption.Invoke(null, null);
        }
    }
}
