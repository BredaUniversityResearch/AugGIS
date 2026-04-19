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
                //TODO Should only do this after all data is loaded, or we might miss image data
                RefreshInfoCards(NetworkManager.Singleton.LocalClientId);
            }
        }

        [Button]
        public void SpawnInfoCard(Vector3 a_localPosition, string a_title = "test1", string a_description = "test2", string a_image = "Picture1.png", string a_time = "test3", string a_cost = "test4", string a_phone = "test5", string a_location = "test6", string a_rating = "test7", string a_website = "test7")
        {
            InfoCardData infoCardData = new InfoCardData
            {
                title = a_title,
                description = a_description,
                images = new string[] { a_image },
                time = a_time,
                cost = a_cost,
                phone = a_phone,
                location = a_location,
                rating = a_rating,
                website = a_website
            };

            SpawnInfoCardServerRPC(a_localPosition, GetInfoCardDataJson(infoCardData));
        }

        [Button]
        public void DestroyInfoCard(string a_cardID)
        {
            DestroyInfoCardServerRPC(a_cardID);
        }

        //[Button]
        //public void UpdateInfoCard(string a_cardID, Vector3 a_localPosition, InfoCardData a_infoCardData)
        //{
        //    UpdateInfoCardServerRPC(a_cardID, a_localPosition, a_infoCardData);
        //}

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SpawnInfoCardServerRPC(Vector3 a_localPosition, string a_infoCardData)
        {
            string newCardID = Guid.NewGuid().ToString();
            m_infoCardIDs.Add(newCardID);

            SpawnInfoCardClientRPC(newCardID, a_localPosition, a_infoCardData , RpcTarget.Everyone);    
        }

        [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
        private void SpawnInfoCardClientRPC(string a_cardID, Vector3 a_localPosition, string a_infoCardData, RpcParams rpcParams = default)
        {
            InfoCard infoCard = Instantiate(m_infoCardPrefab);
            infoCard.transform.parent = m_infoCardRootTransformRef.TransformRef;
            infoCard.transform.localPosition = a_localPosition;
            infoCard.Initialise(ParseInfoCardDataJson(a_infoCardData));
            m_infoCards.Add(infoCard.CardID, infoCard);
            infoCard.CloseInfoCardEvent += () => DestroyInfoCard(infoCard.CardID);
            infoCard.ChangeImageEvent += (int imageIndex) => ChangeImageServerRPC(infoCard.CardID,imageIndex);
            infoCard.ChangeTabEvent += (int tabIndex) => ChangeTabServerRPC(infoCard.CardID, tabIndex);
        }

        [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Everyone)]
        private void DestroyInfoCardServerRPC(string a_cardID)
        {
            if (m_infoCards.TryGetValue(a_cardID, out InfoCard infoCard))
            {
                m_infoCards.Remove(a_cardID);
                infoCard.CloseInfoCardEvent -= () => DestroyInfoCard(infoCard.CardID);
                infoCard.ChangeImageEvent -= (int imageIndex) => ChangeImageServerRPC(infoCard.CardID, imageIndex);
                infoCard.ChangeTabEvent -= (int tabIndex) => ChangeTabServerRPC(infoCard.CardID, tabIndex);
                infoCard.CloseCard(false);
            }

            if (IsServer)
                m_infoCardIDs.Remove(infoCard.CardID);
        }

        [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Everyone)]
        private void ChangeImageServerRPC(string a_cardID, int a_imageIndex)
        {
            if (m_infoCards.TryGetValue(a_cardID, out InfoCard infoCard))
            {
                infoCard.ChangeImage(a_imageIndex);
            }
        }

        [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Everyone)]
        private void ChangeTabServerRPC(string a_cardID, int a_tabIndex)
        {
            if (m_infoCards.TryGetValue(a_cardID, out InfoCard infoCard))
            {
                infoCard.ChangeTab(a_tabIndex);
            }
        }

        //[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Everyone)]
        //private void UpdateInfoCardServerRPC(string a_cardID, Vector3 a_localPosition, string a_infoCardData)
        //{
        //    if (m_infoCards.TryGetValue(a_cardID, out InfoCard infoCard))
        //    {
        //        infoCard.UpdateCardDetails(infoCard.GetInfoCardDataJson(a_infoCardData));
        //        infoCard.transform.localPosition = a_localPosition;
        //    }
        //}

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
                SpawnInfoCardClientRPC(infoCard.CardID, infoCard.transform.localPosition, GetInfoCardDataJson(infoCard.CardContent), RpcTarget.Single(a_clientId, RpcTargetUse.Temp));
            }
        }


        string GetInfoCardDataJson(InfoCardData a_cardData)
        {
            string jsonData = JsonUtility.ToJson(a_cardData);
            return jsonData;
        }

        InfoCardData ParseInfoCardDataJson(string a_jsonData)
        {
            InfoCardData cardData = JsonUtility.FromJson<InfoCardData>(a_jsonData);
            return cardData;
        }
    }
}