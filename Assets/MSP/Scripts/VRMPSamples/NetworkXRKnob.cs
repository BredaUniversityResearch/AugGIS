using ColourPalette;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Content.Interaction;

namespace XRMultiplayer
{
    /// <summary>
    /// Represents a networked XR knob interactable.
    /// </summary>
    [RequireComponent(typeof(XRKnob))]
    public class NetworkXRKnob : NetworkBehaviour
    {
        /// <summary>
        /// The networked knob value.
        /// </summary>
        NetworkVariable<float> m_NetworkedKnobValue = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        NetworkVariable<bool> m_NetworkedKnobHeld = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        NetworkVariable<ulong> m_NetworkKnobOwner = new NetworkVariable<ulong>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>
        /// The XR knob component.
        /// </summary>
        XRKnob m_XRKnob;

        [SerializeField]
        private CustomDiscShapeColorChanger m_CustomDiscShapeColorChanger = null;
        [SerializeField]
        private ColourAsset m_HeldColor;
        [SerializeField]
        private ColourAsset m_ReleasedColor;

        /// <inheritdoc/>
        public void Awake()
        {
            // Get associated components
            if (!TryGetComponent(out m_XRKnob))
            {
                Debug.Log("Missing Components! Disabling Now.");
                enabled = false;
                return;
            }
        }

        //TRACK LIST OF USERS HOLDING THE KNOB
        //ONLY UPDATE VALUE IF FIRST PERSON IN LIST IS ONE MOVING
        //REMOVE PERSON FROM LIST ON LAST SELECT



        /// <inheritdoc/>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            m_XRKnob.onValueChange.AddListener(KnobChanged);

            if (IsServer)
            {
                m_NetworkedKnobValue.Value = m_XRKnob.value;
            }
            else
            {
                m_XRKnob.value = m_NetworkedKnobValue.Value;
            }

            if (m_NetworkedKnobHeld.Value)
                m_CustomDiscShapeColorChanger.ChangeColor(m_HeldColor.GetColour());
            else
                m_CustomDiscShapeColorChanger.ChangeColor(m_ReleasedColor.GetColour());
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Called when the knob value is changed.
        /// </summary>
        /// <param name="newValue">The new value of the knob.</param>
        private void KnobChanged(float newValue)
        {
            KnobChangedServerRpc(newValue, NetworkManager.Singleton.LocalClientId);
        }

        /// <summary>
        /// Server RPC called when the knob value is changed.
        /// </summary>
        /// <param name="newValue">The new value of the knob.</param>
        /// <param name="clientId">The client ID of the player who changed the knob value.</param>
        [ServerRpc(RequireOwnership = false)]
        void KnobChangedServerRpc(float newValue, ulong clientId)
        {
            //CHECK IF clientID is top most in KnobHeld list
            if (clientId == m_NetworkKnobOwner.Value)
            {
                m_NetworkedKnobValue.Value = newValue;
                KnobChangedClientRpc(newValue, clientId);
            }
        }

        /// <summary>
        /// Client RPC called when the knob value is changed.
        /// </summary>
        /// <param name="newValue">The new value of the knob.</param>
        /// <param name="clientId">The client ID of the player who changed the knob value.</param>
        [ClientRpc]
        void KnobChangedClientRpc(float newValue, ulong clientId)
        {
            if (clientId != NetworkManager.Singleton.LocalClientId)
            {
                m_XRKnob.onValueChange.RemoveListener(KnobChanged);
                m_XRKnob.value = newValue;
                m_XRKnob.onValueChange.AddListener(KnobChanged);
            }
        }

        public void HoldKnob(bool holding)
        {
            KnobHeldServerRpc(holding, NetworkManager.Singleton.LocalClientId);
        }

        [ServerRpc(RequireOwnership = false)]
        void KnobHeldServerRpc(bool holdingKnob, ulong clientId)
        {
            if (m_NetworkedKnobHeld.Value && holdingKnob)
                return;

            m_NetworkedKnobHeld.Value = holdingKnob;

            if (holdingKnob)
                m_NetworkKnobOwner.Value = clientId;

            KnobHeldClientRPC(holdingKnob, clientId);
        }

        [ClientRpc]
        void KnobHeldClientRPC(bool newValue, ulong clientId)
        {
            if (clientId != NetworkManager.Singleton.LocalClientId)
            {
                if (newValue)
                    m_CustomDiscShapeColorChanger.ChangeColor(m_HeldColor.GetColour());
                else
                    m_CustomDiscShapeColorChanger.ChangeColor(m_ReleasedColor.GetColour());
            }
        }

    }
}
