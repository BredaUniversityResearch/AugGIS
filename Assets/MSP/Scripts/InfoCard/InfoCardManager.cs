using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

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

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                return;
            }
            else
            {
                //TODO Should only do this after all data is loaded, or we will miss image data
                RefreshInfoCards(NetworkManager.Singleton.LocalClientId);
            }
        }

        [Button]
        public void SpawnInfoCard(Vector3 a_localPosition, string a_title, string a_description, string a_image)
        {
            SpawnInfoCardServerRPC(a_localPosition, a_title, a_description, a_image);
        }

        [Button]
        public void DestroyInfoCard(string a_cardID)
        {
            DestroyInfoCardServerRPC(a_cardID);
        }

        [Button]
        public void UpdateInfoCard(string a_cardID, Vector3 a_localPosition, string a_title, string a_description, string a_image = "")
        {
            UpdateInfoCardServerRPC(a_cardID, a_localPosition, a_title, a_description, a_image);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SpawnInfoCardServerRPC(Vector3 a_localPosition, string a_title, string a_description, string a_image)
        {
            string newCardID = Guid.NewGuid().ToString();
            m_infoCardIDs.Add(newCardID);

            SpawnInfoCardClientRPC(newCardID, a_localPosition, a_title, a_description,a_image,RpcTarget.Everyone);    
        }

        [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
        private void SpawnInfoCardClientRPC(string a_cardID, Vector3 a_localPosition, string a_title, string a_description, string a_image, RpcParams rpcParams = default)
        {
            InfoCard infoCard = Instantiate(m_infoCardPrefab);
            infoCard.transform.parent = m_infoCardRootTransformRef.TransformRef;
            infoCard.transform.localPosition = a_localPosition;
            infoCard.Initialise(a_title, a_description, a_cardID, a_image);
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
            }

            if (IsServer)
                m_infoCardIDs.Remove(infoCard.CardID);
        }

        [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Everyone)]
        private void UpdateInfoCardServerRPC(string a_cardID, Vector3 a_localPosition, string a_title, string a_description, string a_image)
        {
            if (m_infoCards.TryGetValue(a_cardID, out InfoCard infoCard))
            {
                infoCard.SetTitle(a_title);
                infoCard.SetDescription(a_description);
                infoCard.transform.localPosition = a_localPosition;
            }
        }

        //TODO needs to wait until card is spawned or declined before moving to next
        private void RefreshInfoCards(ulong a_clientId)
        {
            foreach (var cardID in m_infoCardIDs)
            {
                if (m_infoCards.TryGetValue(cardID.ToString(), out InfoCard infoCard))
                {
                    Debug.Log("Card Exists");
                }
                else
                {
                    RequestInfoCardServerRPC(a_clientId, cardID.ToString());
                }
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestInfoCardServerRPC(ulong a_clientId, string a_cardID)
        {
            if (m_infoCards.TryGetValue(a_cardID.ToString(), out InfoCard infoCard))
            {
                SpawnInfoCardClientRPC(infoCard.CardID, infoCard.transform.localPosition, infoCard.CardTitle, infoCard.CardDescription, infoCard.CardImage, RpcTarget.Single(a_clientId, RpcTargetUse.Temp));
            }
        }
    }
}