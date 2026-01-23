using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.AffordanceSystem.Receiver.Primitives;
using UnityEngine.XR.Interaction.Toolkit.AffordanceSystem.Rendering;

/// <summary>
/// This is a modified version of ColorMaterialPropertyAffordanceReceiver that supports multiple MaterialPropertyBlockHelpers
/// to allow for pawns with multiple submeshes to be colored correctly.
/// </summary>


namespace UnityEngine.XR.Interaction.Toolkit.AffordanceSystem.Receiver.Rendering
{
    /// <summary>
    /// Apply affordance to material property block color property.
    /// </summary>
    //[AddComponentMenu("Affordance System/Receiver/Rendering/Color Material Property Affordance Receiver", 12)]
    //[HelpURL(XRHelpURLConstants.k_ColorMaterialPropertyAffordanceReceiver)]
    [Obsolete("The Affordance System namespace and all associated classes have been deprecated. The existing affordance system will be moved, replaced and updated with a new interaction feedback system in a future version of XRI.")]
    public class CustomColorMaterialPropertyAffordanceReceiver : ColorAffordanceReceiver
    {
        /// <summary>
        /// Material Property Block Helper component references used to set material properties. Automatically loaded from children on Awake.
        /// </summary>
        List<MaterialPropertyBlockHelper> m_MaterialPropertyBlockHelpers = new List<MaterialPropertyBlockHelper>();

        [SerializeField]
        [Tooltip("The GameObject containing the visuals to apply the color to.")]
        [Required]
        GameObject m_visuals;

        /// <summary>
        /// Material Property Block Helper component reference used to set material properties.
        /// </summary>
        public List<MaterialPropertyBlockHelper> materialPropertyBlockHelpers
        {
            get => m_MaterialPropertyBlockHelpers;
            set => m_MaterialPropertyBlockHelpers = value;
        }

        [SerializeField]
        [Tooltip("Shader property name to set the color of. When empty, the component will attempt to use the default for the current render pipeline.")]
        string m_ColorPropertyName;

        /// <summary>
        /// Shader property name to set the color of.
        /// </summary>
        public string colorPropertyName
        {
            get => m_ColorPropertyName;
            set
            {
                m_ColorPropertyName = value;
                UpdateColorPropertyID();
            }
        }

        protected override void Start()
        {
            base.Start();
            MaterialPropertyBlockHelpersUpdate();
        }

        /// <summary>
        /// Updates the list of MaterialPropertyBlockHelpers. Should be called whenever the visuals gameobject or components change.
        /// </summary>
        public void MaterialPropertyBlockHelpersUpdate()
        {
            bool includeInactive = true;

            m_MaterialPropertyBlockHelpers = m_visuals.GetComponentsInChildren<MaterialPropertyBlockHelper>(includeInactive).ToList();
#if UNITY_EDITOR
            if (m_MaterialPropertyBlockHelpers.IsNullOrEmpty())
            {
                Debug.LogWarning($"No {nameof(MaterialPropertyBlockHelper)} components found in children of {nameof(CustomColorMaterialPropertyAffordanceReceiver)} on {gameObject.name}.", this);
            }
#endif
            UpdateColorPropertyID();
        }

        int m_ColorProperty;


        /// <inheritdoc/>
        protected override void OnAffordanceValueUpdated(Color newValue)
        {
            foreach (var materialPropertyBlockHelper in m_MaterialPropertyBlockHelpers)
            {
                MaterialPropertyBlock materialPropertyBlock = materialPropertyBlockHelper.GetMaterialPropertyBlock();
                materialPropertyBlock?.SetColor(m_ColorProperty, newValue);
            }
            base.OnAffordanceValueUpdated(newValue);
        }

        /// <inheritdoc/>
        protected override Color GetCurrentValueForCapture()
        {
            if (m_MaterialPropertyBlockHelpers.IsNullOrEmpty())
            {
                Debug.LogWarning($"No {nameof(MaterialPropertyBlockHelper)} components found in children of {nameof(CustomColorMaterialPropertyAffordanceReceiver)} on {gameObject.name}. Returning default color.", this);
                return Color.white;
            }

            // In our case we assume all the pawns are controlled only through this script, so we simply return the first one.
            return m_MaterialPropertyBlockHelpers[0].GetSharedMaterialForTarget().GetColor(m_ColorProperty);
        }

        void UpdateColorPropertyID()
        {
            if (!string.IsNullOrEmpty(m_ColorPropertyName))
            {
                m_ColorProperty = Shader.PropertyToID(m_ColorPropertyName);
            }
            else
            {
                m_ColorProperty = GraphicsSettings.currentRenderPipeline != null ? ShaderPropertyLookup.baseColor : ShaderPropertyLookup.color;
            }
        }

        readonly struct ShaderPropertyLookup
        {
            public static readonly int baseColor = Shader.PropertyToID("_BaseColor");
            public static readonly int color = Shader.PropertyToID("_Color"); // Legacy
        }
    }
}
