using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace POV_Unity
{
    public class InfoCardManager : NetworkBehaviour
    {

        // Setup card prefab
        // Determine default amount of cards to instantiate
        // Call to and from to perform action with card
        // How to have one manager for a bunch of different cards?

        [SerializeField]
        [Required]
        private TransformReference m_infoCardRootTransformRef;

        [SerializeField]
        [Required]
        private InfoCard m_infoCardPrefab = null;

        private Dictionary<string,InfoCard> m_infoCards = new Dictionary<string, InfoCard>();

        [SerializeField]
        private NetworkList<FixedString64Bytes> m_infoCardIDs = new NetworkList<FixedString64Bytes>(new List<FixedString64Bytes>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        [Button]
        public void SpawnInfoCard(Vector3 a_localPosition, string a_title, string a_description)
        {
            SpawnInfoCardServerRPC(a_localPosition, a_title, a_description);
        }

        [Button]
        public void DestroyInfoCard(string a_cardID)
        {
            DestroyInfoCardServerRPC(a_cardID);
        }

        [Button]
        public void UpdateInfoCard(Vector3 a_localPosition, string a_title, string a_description, string a_cardID)
        {
            UpdateInfoCardServerRPC(a_localPosition, a_title, a_description, a_cardID);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SpawnInfoCardServerRPC(Vector3 a_localPosition, string a_title, string a_description)
        {
            string newCardID = Guid.NewGuid().ToString();
            m_infoCardIDs.Add(newCardID);

            SpawnInfoCardClientRPC(a_localPosition, a_title, a_description, newCardID);    
        }

        [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
        private void SpawnInfoCardClientRPC(Vector3 a_localPosition, string a_title, string a_description, string a_cardID)
        {
            InfoCard infoCard = Instantiate(m_infoCardPrefab);
            infoCard.transform.parent = m_infoCardRootTransformRef.TransformRef;
            infoCard.transform.localPosition = a_localPosition;
            infoCard.Initialise(a_title, a_description, a_cardID);
            m_infoCards.Add(infoCard.CardID, infoCard);
            infoCard.CloseInfoCardEvent += () => DestroyInfoCard(infoCard.CardID);
        }

        [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Everyone)]
        private void DestroyInfoCardServerRPC(string a_cardID)
        {
            if (m_infoCards.TryGetValue(a_cardID, out InfoCard infoCard))
            {
                m_infoCards.Remove(a_cardID);
                infoCard.CloseInfoCardEvent -= () => DestroyInfoCard(infoCard.CardID);
                Destroy(infoCard.gameObject);
                
                if(IsServer)
                    m_infoCardIDs.Remove(infoCard.CardID);
            }
        }

        [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Everyone)]
        private void UpdateInfoCardServerRPC(Vector3 a_localPosition, string a_title, string a_description,string a_cardID)
        {
            if (m_infoCards.TryGetValue(a_cardID, out InfoCard infoCard))
            {
                infoCard.SetTitle(a_title);
                infoCard.SetDescription(a_description);
                infoCard.transform.localPosition = a_localPosition;
            }
        }
    }
}