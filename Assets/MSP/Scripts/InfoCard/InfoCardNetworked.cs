using System;
using System.ComponentModel;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace POV_Unity
{
    [RequireComponent(typeof(InfoCard))]

    public class InfoCardNetworked : NetworkBehaviour
    {
        InfoCard m_infoCard;

        [SerializeField]
        private NetworkVariable<string> m_CardTitle = new NetworkVariable<string>("Title", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        [SerializeField]
        private NetworkVariable<string> m_CardDescription = new NetworkVariable<string>("Description", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private void Awake()
        {
            m_infoCard = GetComponent<InfoCard>();

            m_infoCard.TitleChangedEvent += OnTitleChanged;
            m_infoCard.DescriptionChangedEvent += OnDescriptionChanged;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                m_CardTitle.Value = m_infoCard.CardTitle;
                m_CardDescription.Value = m_infoCard.CardDescription;
            }
            else
            {
                m_infoCard.SetTitle(m_CardTitle.Value,false);
                m_infoCard.SetDescription(m_CardDescription.Value, false);
            }
        }
        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            m_CardTitle.OnValueChanged -= OnSyncedTitleValueChanged;
            m_CardDescription.OnValueChanged -= OnSyncedDescriptionValueChanged;
        }

        //TITLE NETWORKING
        private void OnSyncedTitleValueChanged(string a_previousValue, string a_newValue)
        {
            m_infoCard.SetTitle(a_newValue, false);
        }

        private void OnTitleChanged(string a_title)
        {
            ChangeTitleValueOwnerRPC(a_title);
        }

        [Rpc(SendTo.Owner, RequireOwnership = false)]
        private void ChangeTitleValueOwnerRPC(string a_newTitle)
        {
            m_CardTitle.Value = a_newTitle;
        }

        //DESCRIPTION NETWORKING
        private void OnSyncedDescriptionValueChanged(string a_previousValue, string a_newValue)
        {
            m_infoCard.SetDescription(a_newValue,false);
        }

        private void OnDescriptionChanged(string a_description)
        {
            ChangeDescriptionValueOwnerRPC(a_description);
        }

        [Rpc(SendTo.Owner, RequireOwnership = false)]
        private void ChangeDescriptionValueOwnerRPC(string a_newDescription)
        {
            m_CardTitle.Value = a_newDescription;
        }
    }
}