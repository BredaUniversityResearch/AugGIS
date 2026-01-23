using System.Collections;
using Sirenix.OdinInspector;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHeadVisual : NetworkBehaviour
{
	[SerializeField]
	private GameObject[] m_headVisualObjects;

	[SerializeField]
	[Required]
	private LocalPlayerSessionDataSO m_localPlayerSessionDataSO;

	[SerializeField]
	[Required]
	private TextMeshProUGUI m_playerNameText;

	[SerializeField]
	[Required]
	private Image m_teamColorImage;

	private Transform m_cameraTransform;

	private NetworkVariable<FixedString128Bytes> m_syncedPlayerName = new NetworkVariable<FixedString128Bytes>(" ", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
	private NetworkVariable<Color> m_syncedPlayerColor = new NetworkVariable<Color>(Color.white, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();

		m_syncedPlayerName.OnValueChanged += OnPlayerNameChanged;
		m_syncedPlayerColor.OnValueChanged += OnPlayerTeamColorChanged;

		if (IsOwner)
		{
			StartCoroutine(GetPlayerCamera());
			UpdatePlayerHeadVisualVariablesRPC(m_localPlayerSessionDataSO.playerName, m_localPlayerSessionDataSO.teamColor);
		}
		else
		{
			m_playerNameText.text = m_syncedPlayerName.Value.ToString();
			m_teamColorImage.color = m_syncedPlayerColor.Value;

			int activeVisual = Random.Range(0, m_headVisualObjects.Length);
			for (int i = 0; i < m_headVisualObjects.Length; i++)
			{
				if (i == activeVisual)
				{
					m_headVisualObjects[i].gameObject.SetActive(true);
				}
				else
				{
					m_headVisualObjects[i].gameObject.SetActive(false);
				}
			}
		}
	}

	public override void OnNetworkDespawn()
	{
		base.OnNetworkDespawn();
		m_syncedPlayerName.OnValueChanged -= OnPlayerNameChanged;
		m_syncedPlayerColor.OnValueChanged -= OnPlayerTeamColorChanged;
    }

	private void OnPlayerNameChanged(FixedString128Bytes previousValue, FixedString128Bytes newValue)
	{
		m_playerNameText.text = newValue.ToString();
	}

	private void OnPlayerTeamColorChanged(Color previousValue, Color newValue)
	{
		m_teamColorImage.color = newValue;
	}

	private void LateUpdate()
	{
		if (!IsOwner)
		{
			return;
		}

		if (m_cameraTransform != null)
		{
			transform.SetPositionAndRotation(m_cameraTransform.position, m_cameraTransform.rotation);
		}
	}

	IEnumerator GetPlayerCamera()
	{
		while (m_cameraTransform == null)
		{
			Debug.Log("finding playercamera");
			if (GameObject.FindWithTag("MainCamera") != null)
			{
				m_cameraTransform = GameObject.FindWithTag("MainCamera").transform;
			}

			yield return null;
		}
	}

	[Rpc(SendTo.Owner)]
	private void UpdatePlayerHeadVisualVariablesRPC(string a_playerName, Color a_teamColor)
	{
		m_syncedPlayerColor.Value = a_teamColor;
		m_syncedPlayerName.Value = a_playerName;
	}
}
