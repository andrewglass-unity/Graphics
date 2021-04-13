using System;
using UnityEditor;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>Decal Layers.</summary>
    public enum DecalLayerEnum
    {
        /// <summary>The light will no affect any object.</summary>
        Nothing = 0,   // Custom name for "Nothing" option
        /// <summary>Decal Layer 0.</summary>
        LightLayerDefault = 1 << 0,
        /// <summary>Decal Layer 1.</summary>
        DecalLayer1 = 1 << 1,
        /// <summary>Decal Layer 2.</summary>
        DecalLayer2 = 1 << 2,
        /// <summary>Decal Layer 3.</summary>
        DecalLayer3 = 1 << 3,
        /// <summary>Decal Layer 4.</summary>
        DecalLayer4 = 1 << 4,
        /// <summary>Decal Layer 5.</summary>
        DecalLayer5 = 1 << 5,
        /// <summary>Decal Layer 6.</summary>
        DecalLayer6 = 1 << 6,
        /// <summary>Decal Layer 7.</summary>
        DecalLayer7 = 1 << 7,
        /// <summary>Everything.</summary>
        Everything = 0xFF, // Custom name for "Everything" option
    }

    /// <summary>The scaling mode to apply to decals that use the Decal Projector.</summary>
    public enum DecalScaleMode
    {
        /// <summary>Ignores the transformation hierarchy and uses the scale values in the Decal Projector component directly.</summary>
        ScaleInvariant,
        /// <summary>Multiplies the lossy scale of the Transform with the Decal Projector's own scale then applies this to the decal.</summary>
        [InspectorName("Inherit from Hierarchy")]
        InheritFromHierarchy,
    }

    /// <summary>
    /// Decal Projector component.
    /// </summary>
    //[HelpURL(Documentation.baseURL + Documentation.version + Documentation.subURL + "Decal-Projector" + Documentation.endURL)]
    [ExecuteAlways]
#if UNITY_EDITOR
    [CanEditMultipleObjects]
#endif
    [AddComponentMenu("Rendering/URP/Decal Projector")]
    public partial class DecalProjector : MonoBehaviour
    {
        internal static readonly Quaternion k_MinusYtoZRotation = Quaternion.Euler(-90, 0, 0);
        internal static readonly Quaternion k_YtoZRotation = Quaternion.Euler(90, 0, 0);

        public delegate void DecalProjectorAction(DecalProjector decalProjector);
        public static event DecalProjectorAction onDecalAdd;
        public static event DecalProjectorAction onDecalRemove;
        public static event DecalProjectorAction onDecalPropertyChange;
        public static event DecalProjectorAction onDecalMaterialChange;

        public static bool isAnySystemUsing => onDecalAdd != null;

        internal DecalEntity decalEntity { get; set; }

        public static Material defaultMaterial { get; set; }

        [SerializeField]
        private Material m_Material = null;
        /// <summary>
        /// The material used by the decal. It should be of type HDRP/Decal if you want to have transparency.
        /// </summary>
        public Material material
        {
            get
            {
                return m_Material;
            }
            set
            {
                m_Material = value;
                OnValidate();
            }
        }

#if UNITY_EDITOR
        private int m_Layer;
#endif

        [SerializeField]
        private float m_DrawDistance = 1000.0f;
        /// <summary>
        /// Distance from camera at which the Decal is not rendered anymore.
        /// </summary>
        public float drawDistance
        {
            get
            {
                return m_DrawDistance;
            }
            set
            {
                m_DrawDistance = Mathf.Max(0f, value);
                OnValidate();
            }
        }

        [SerializeField]
        [Range(0, 1)]
        private float m_FadeScale = 0.9f;
        /// <summary>
        /// Percent of the distance from the camera at which this Decal start to fade off.
        /// </summary>
        public float fadeScale
        {
            get
            {
                return m_FadeScale;
            }
            set
            {
                m_FadeScale = Mathf.Clamp01(value);
                OnValidate();
            }
        }

        [SerializeField]
        [Range(0, 180)]
        private float m_StartAngleFade = 180.0f;
        /// <summary>
        /// Angle between decal backward orientation and vertex normal of receiving surface at which the Decal start to fade off.
        /// </summary>
        public float startAngleFade
        {
            get
            {
                return m_StartAngleFade;
            }
            set
            {
                m_StartAngleFade = Mathf.Clamp(value, 0.0f, 180.0f);
                OnValidate();
            }
        }

        [SerializeField]
        [Range(0, 180)]
        private float m_EndAngleFade = 180.0f;
        /// <summary>
        /// Angle between decal backward orientation and vertex normal of receiving surface at which the Decal end to fade off.
        /// </summary>
        public float endAngleFade
        {
            get
            {
                return m_EndAngleFade;
            }
            set
            {
                m_EndAngleFade = Mathf.Clamp(value, m_StartAngleFade, 180.0f);
                OnValidate();
            }
        }

        [SerializeField]
        private Vector2 m_UVScale = new Vector2(1, 1);
        /// <summary>
        /// Tilling of the UV of the projected texture.
        /// </summary>
        public Vector2 uvScale
        {
            get
            {
                return m_UVScale;
            }
            set
            {
                m_UVScale = value;
                OnValidate();
            }
        }

        [SerializeField]
        private Vector2 m_UVBias = new Vector2(0, 0);
        /// <summary>
        /// Offset of the UV of the projected texture.
        /// </summary>
        public Vector2 uvBias
        {
            get
            {
                return m_UVBias;
            }
            set
            {
                m_UVBias = value;
                OnValidate();
            }
        }

        [SerializeField]
        private bool m_AffectsTransparency = false;
        /// <summary>
        /// Change the transparency. It is only compatible when using HDRP/Decal shader.
        /// </summary>
        public bool affectsTransparency
        {
            get
            {
                return m_AffectsTransparency;
            }
            set
            {
                m_AffectsTransparency = value;
                OnValidate();
            }
        }

        [SerializeField]
        DecalLayerEnum m_DecalLayerMask = DecalLayerEnum.LightLayerDefault;
        /// <summary>
        /// The layer of the decal.
        /// </summary>
        public DecalLayerEnum decalLayerMask
        {
            get => m_DecalLayerMask;
            set => m_DecalLayerMask = value;
        }

        [SerializeField]
        private DecalScaleMode m_ScaleMode = DecalScaleMode.ScaleInvariant;
        /// <summary>
        /// The scaling mode to apply to decals that use this Decal Projector.
        /// </summary>
        public DecalScaleMode scaleMode
        {
            get => m_ScaleMode;
            set
            {
                m_ScaleMode = value;
                OnValidate();
            }
        }

        [SerializeField]
        internal Vector3 m_Offset = new Vector3(0, 0, 0.5f);
        /// <summary>
        /// Change the offset position.
        /// Do not expose: Could be changed by the inspector when manipulating the gizmo.
        /// </summary>
        internal Vector3 pivot
        {
            get
            {
                return m_Offset;
            }
            set
            {
                m_Offset = value;
                OnValidate();
            }
        }

        [SerializeField]
        internal Vector3 m_Size = new Vector3(1, 1, 1);
        /// <summary>
        /// The size of the projection volume.
        /// </summary>
        public Vector3 size
        {
            get
            {
                return m_Size;
            }
            set
            {
                m_Size = value;
                OnValidate();
            }
        }

        [SerializeField]
        [Range(0, 1)]
        private float m_FadeFactor = 1.0f;
        /// <summary>
        /// Controls the transparency of the decal.
        /// </summary>
        public float fadeFactor
        {
            get
            {
                return m_FadeFactor;
            }
            set
            {
                m_FadeFactor = Mathf.Clamp01(value);
                OnValidate();
            }
        }

        private Material m_OldMaterial = null;

        /// <summary>A scale that should be used for rendering and handles.</summary>
        internal Vector3 effectiveScale => m_ScaleMode == DecalScaleMode.InheritFromHierarchy ? transform.lossyScale : Vector3.one;
        /// <summary>current position in a way the DecalSystem will be able to use it</summary>
        internal Vector3 position => transform.position;
        /// <summary>current uv parameters in a way the DecalSystem will be able to use it</summary>
        internal Vector4 uvScaleBias => new Vector4(m_UVScale.x, m_UVScale.y, m_UVBias.x, m_UVBias.y);

        /// <summary>current rotation in a way the DecalSystem will be able to use it</summary>
        internal Quaternion rotation
        {
            get
            {
                // If Z-scale is negative we rotate decal differently to have correct forward direction for Angle Fade.
                return transform.rotation * (effectiveScale.z >= 0f ? k_MinusYtoZRotation : k_YtoZRotation);
            }
        }

        /// <summary>current size in a way the DecalSystem will be able to use it</summary>
        internal Vector3 decalSize
        {
            get
            {
                Vector3 scale = effectiveScale;

                // If Z-scale is negative the forward direction for rendering will be fixed by rotation,
                // so we need to flip the scale of the affected axes back.
                // The final sign of Z will depend on the other two axes, so we actually need to fix only Y here.
                if (scale.z < 0f)
                    scale.y *= -1f;

                // Flipped projector (with 1 or 3 negative components of scale) would be invisible.
                // In this case we additionally flip Z.
                bool flipped = scale.x < 0f ^ scale.y < 0f ^ scale.z < 0f;
                if (flipped)
                    scale.z *= -1f;

                return new Vector3(m_Size.x * scale.x, m_Size.z * scale.z, m_Size.y * scale.y);
            }
        }

        /// <summary>current offset in a way the DecalSystem will be able to use it</summary>
        internal Vector3 decalOffset
        {
            get
            {
                Vector3 scale = effectiveScale;

                // If Z-scale is negative the forward direction for rendering will be fixed by rotation,
                // so we need to flip the scale of the affected axes back.
                if (scale.z < 0f)
                {
                    scale.y *= -1f;
                    scale.z *= -1f;
                }

                return new Vector3(m_Offset.x * scale.x, -m_Offset.z * scale.z, m_Offset.y * scale.y);
            }
        }

        void InitMaterial()
        {
            if (m_Material == null)
            {
#if UNITY_EDITOR
                m_Material = defaultMaterial;
#endif
            }
        }

        void OnEnable()
        {
            InitMaterial();

            m_OldMaterial = m_Material;

            onDecalAdd?.Invoke(this);

#if UNITY_EDITOR
            m_Layer = gameObject.layer;
            // Handle scene visibility
            UnityEditor.SceneVisibilityManager.visibilityChanged += UpdateDecalVisibility;
#endif
        }

#if UNITY_EDITOR
        void UpdateDecalVisibility()
        {
            // Fade out the decal when it is hidden by the scene visibility
            if (UnityEditor.SceneVisibilityManager.instance.IsHidden(gameObject))
            {
                onDecalRemove?.Invoke(this);
            }
            else
            {
                onDecalAdd?.Invoke(this);
                onDecalPropertyChange?.Invoke(this); // Scene culling mask may have changed.
            }
        }

#endif

        void OnDisable()
        {
            onDecalRemove?.Invoke(this);

#if UNITY_EDITOR
            UnityEditor.SceneVisibilityManager.visibilityChanged -= UpdateDecalVisibility;
#endif
        }

        internal void OnValidate()
        {
            if (!isActiveAndEnabled)
                return;

            if (m_Material != m_OldMaterial)
            {
                onDecalMaterialChange?.Invoke(this);
                m_OldMaterial = m_Material;
            }
            else
                onDecalPropertyChange?.Invoke(this);
        }

        public bool IsValid()
        {
            if (material == null)
                return false;

            if (material.FindPass(DecalShaderPassNames.DBufferProjector) == -1)
                return false;

            return true;
        }
    }
}
