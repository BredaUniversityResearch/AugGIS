using System;
using POV_Unity;
using Sirenix.OdinInspector;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

public class LayerTag : NetworkBehaviour
{
	public enum LayerTagSide
	{
		West,
		East
	}

	const float HORIZONTAL_STEP_SIZE = 0.1f;

	[SerializeField]
	[Required]
	private Image m_iconImage = null;

	[SerializeField]
	[Required]
	[Tooltip("The bookmark image object that will be tinted with the category colour")]
	private Image m_categoryBookmarkImage = null;

	[SerializeField]
	[Tooltip("The sprite that will replace current bookmark sprite when the tag is being grabbed")]
	private Image m_deletionProgressImage = null;

	[SerializeField]
	[Required]
	private TextMeshProUGUI m_layerNameText = null;

	[SerializeField]
	[Required]
	MSPXRGrabInteractable m_grabInteractable = null;

	[SerializeField]
	RectTransform m_visualsRect = null;

	[SerializeField]
	[Tooltip("The maximal width of the layer tag when being pulled out for deletion")]
	private float m_maxTagWidth = 300f;

	[SerializeField]
	[Tooltip("The minimal width of the layer tag to trigger deletion progress visuals")]
	private float m_deletionProgressMinThreshold = 200f;

	private LayerManager m_layerManager => LayerManager.Instance;
	private Transform m_layerTagRootTransform = null;

	Vector2 m_defaultSizeDelta = Vector2.zero;

	private NetworkVariable<float> m_deletionProgress = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
	private NetworkVariable<int> m_networkLayerIndex = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
	private NetworkVariable<LayerTagSide> m_networkLayerTagSide = new NetworkVariable<LayerTagSide>(LayerTagSide.West, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
	private int m_defaultLayerIndex = -1;
	private LayerTagSide m_defaultLayerTagSide = LayerTagSide.West;
	private bool m_grabbed = false;
	public bool IsGrabbed => m_grabbed;

	private float m_prevLocalZPosition;

    void Awake()
    {
        m_defaultSizeDelta = m_visualsRect.sizeDelta;
    }

    public override void OnNetworkSpawn()
	{
		if (NetworkManager.Singleton.IsServer)
		{
			// Set the network variable values (on the server side only)
			m_networkLayerIndex.Value = m_defaultLayerIndex;
			m_networkLayerTagSide.Value = m_defaultLayerTagSide;
		}

		base.OnNetworkSpawn();

		m_layerManager.AddLayerTagToCanvas(this, m_networkLayerIndex.Value, m_networkLayerTagSide.Value);
		m_grabInteractable.BlockInteraction();

		m_deletionProgress.OnValueChanged += OnDeletionProgressValueChanged;
	}

	void Start()
    {
		m_grabInteractable.selectEntered.AddListener(OnGrabStart);
		m_grabInteractable.selectExited.AddListener(OnGrabEnd);
	}

	public void OnServerInit(int a_layerIndex, LayerTagSide a_side)
	{
		m_defaultLayerIndex = a_layerIndex;
		m_defaultLayerTagSide = a_side;
	}
	public void OnClientInit(ALayer a_layer, Transform a_layerTagRootTransform)
	{
		LayerInstanceData data = m_layerManager.GetLayerData(a_layer.LayerIndex);
		transform.SetParent(a_layerTagRootTransform, false);

		m_layerTagRootTransform = a_layerTagRootTransform;

		m_layerNameText.text = a_layer.@short;

		OnLayerInstanceDataChanged(data);
		transform.SetSiblingIndex(data.m_layerIndex);

		gameObject.name = "LayerTag_" + m_networkLayerIndex.Value;

		m_categoryBookmarkImage.color = a_layer.Category.colour;

		//TODO (Igli): For now this uses the category icon however in the future we want each leayer to have its own specific icon
		m_iconImage.sprite = AssetManager.GetSprite(a_layer.Category.icon);
	}

	public void OnLayerInstanceDataChanged(LayerInstanceData a_data)
	{
		bool wasActive = gameObject.activeSelf;
		gameObject.SetActive(a_data.m_visualizationMode != LayerVisualizationMode.Off);

		if (!wasActive && gameObject.activeSelf)
		{
			transform.SetAsLastSibling();
		}

		bool grabbableEnabled = a_data.m_ownerID == (long)NetworkManager.Singleton.LocalClientId || a_data.m_ownerID == -1;

		if (grabbableEnabled)
		{
			m_grabInteractable.AllowInteraction();
		}
		else
		{
			m_grabInteractable.BlockInteraction();
		}
	}

	// This function is called only from LayerTagsCanvas when the tag **is not grabbed**
	public void OnLayerTagPositionChanged(LayerTagData a_newData)
	{
		m_visualsRect.localPosition = new Vector3(m_visualsRect.localPosition.x, m_visualsRect.localPosition.y, a_newData.m_newLocalZValue);
		m_grabInteractable.transform.localPosition = m_visualsRect.localPosition;
	}

	void Update()
	{
		if (IsOwner && m_layerManager.GetLayerData(m_networkLayerIndex.Value).m_ownerID == (long)NetworkManager.Singleton.LocalClientId && m_grabbed)
		{
			// Synchronize Z value of the tag visuals to the grab interactable
			m_visualsRect.localPosition = new Vector3(m_visualsRect.localPosition.x, m_visualsRect.localPosition.y, m_grabInteractable.transform.localPosition.z);
			
			LayerTagData tagData = new LayerTagData();
			tagData.m_layerIndex = m_networkLayerIndex.Value;

			float handleLocalXPosition = m_grabInteractable.transform.localPosition.x;

			//We need to add extra half "m_defaultSizeDelta.x / 2" to compensate the pivot offset of the rect
			tagData.m_deletionProgress = Mathf.Clamp((-handleLocalXPosition - m_deletionProgressMinThreshold + m_defaultSizeDelta.x / 2) / (m_maxTagWidth - m_deletionProgressMinThreshold), 0f, 1f);

			SetDeletionProgress(tagData.m_deletionProgress);

			tagData.m_newLocalZValue = m_visualsRect.localPosition.z;
			m_layerManager.SynchronizeLayerTagsServerRPC(tagData);

			float oldWorldZValue = -m_prevLocalZPosition * m_layerTagRootTransform.lossyScale.z;
			float newWorldZValue = -m_visualsRect.localPosition.z * m_layerTagRootTransform.lossyScale.z;

			int oldVerticalStep = Math.Clamp((int)(oldWorldZValue / m_layerManager.LayerVerticalStepDisplacement), 0, m_layerManager.LayerMaxVerticalStepCount - 1);
			int newVerticalStep = Math.Clamp((int)(newWorldZValue / m_layerManager.LayerVerticalStepDisplacement), 0, m_layerManager.LayerMaxVerticalStepCount - 1);

			if (newVerticalStep != oldVerticalStep)
			{
				m_layerManager.ChangeLHStepServerRPC(m_networkLayerIndex.Value, newVerticalStep);
			}

			m_prevLocalZPosition = m_visualsRect.localPosition.z;
		}
	}

    public void SetDeletionProgress(float a_progress)
	{
		m_deletionProgress.Value = a_progress;
	}

	private void OnDeletionProgressValueChanged(float a_prevValue, float a_newValue)
	{
		float resultingWidth = Mathf.Lerp(m_defaultSizeDelta.x, m_maxTagWidth, a_newValue);
		m_visualsRect.sizeDelta = new Vector2(resultingWidth, m_visualsRect.sizeDelta.y);
		m_visualsRect.localPosition = new Vector3(-(resultingWidth - m_defaultSizeDelta.x) / 2f, m_visualsRect.localPosition.y, m_visualsRect.localPosition.z);

		if (m_deletionProgressImage != null)
		{
			m_deletionProgressImage.fillAmount = a_newValue;
		}
	}

	void OnGrabStart(SelectEnterEventArgs a_args)
	{
		m_layerManager.SetLayerOwnerIDServerRPC(m_networkLayerIndex.Value, NetworkManager.Singleton.LocalClientId);
		m_prevLocalZPosition = transform.localPosition.z;
		m_grabbed = true;
	}

	void OnGrabEnd(SelectExitEventArgs a_args)
	{
		bool toDisableLayer = m_deletionProgress.Value >= 1f;

		m_grabbed = false;
		m_layerManager.RemoveLayerOwnershipServerRPC(m_networkLayerIndex.Value);
		m_layerManager.ResetLayerTagsServerRPC(m_networkLayerIndex.Value);

		if (toDisableLayer)
		{
			m_layerManager.SetLayerVisualizationServerRPC(m_networkLayerIndex.Value, LayerVisualizationMode.Off);
		}
	}

	[Button]
	void StartGrabDebug()
	{
		OnGrabStart(null);
	}

	[Button]
	void EndGrabDebug()
	{
		OnGrabEnd(null);
	}
}
