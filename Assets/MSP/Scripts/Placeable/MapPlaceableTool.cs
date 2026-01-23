using System;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

public class MapPlaceableTool : NetworkBehaviour
{
	public Action AttachedToMap;
	public Action DetachedFromMap;
	public Action SnappedToMap;
	public Action UnsnappedFromMap;

	[SerializeField]
	[Required]
	private ToolStationSelection m_toolStationSelection;

	[Header("Snap Settings")]
	[SerializeField]
	private GameObject m_snapIndicator;

	[SerializeField]
	private LayerMask m_placementLayerMask;

	[SerializeField]
	[Tooltip("The distance threshold within which the snap indicator will appear.")]
	private float m_snapIndicatorDistanceThreshold = 0.07f;

	[SerializeField]
	[Tooltip("The distance threshold within which the object will snap to the map.")]
	private float m_snapDistanceThreshold = 0.007f;

	[SerializeField]
	[Tooltip("The distance threshold within which the object will not be unsnapped from the map.")]
	private float m_unsnapDistanceThreshold = 0.035f;

	[SerializeField]
	[Tooltip("The distance threshold within which the object will not be unsnapped from the map if the hand is below the map.")]
	private float m_unsnapBelowMapDistanceThreshold = 0.08f;

	[SerializeField]
	[Tooltip("The Y offset applied when placing the object on the map to position it slightly above the layers.")]
	private float m_placementYOffset = 0.005f;

	bool m_isGrabbedLocally = false;
	public bool IsGrabbedLocally => m_isGrabbedLocally;

	[SerializeField]
	private bool m_defaultIsPlaced = false;
	private NetworkVariable<bool> m_isPlaced = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
	public bool IsPlaced => m_isPlaced.Value;

	[SerializeField]
	[Required]
	private Transform m_visualsRoot;
	public Transform VisualsRoot => m_visualsRoot;

	private bool m_isSnappedToMap = false;
	private Vector3 m_snappedWorldPosition = Vector3.zero;
	private Quaternion m_snappedWorldRotation = Quaternion.identity;



	public void SetDefaultIsPlaced(bool a_isPlaced)
	{
		m_defaultIsPlaced = a_isPlaced;
	}

    public override void OnNetworkSpawn()
	{
		if (NetworkManager.Singleton.IsServer)
		{
			m_isPlaced.Value = m_defaultIsPlaced;
		}

		base.OnNetworkSpawn();

		m_toolStationSelection.Grabbed += OnGrabbed;
		m_toolStationSelection.Released += OnReleased;
		m_isPlaced.OnValueChanged += OnPlacementValueChanged;
	}

	protected override void OnNetworkPostSpawn()
	{
		base.OnNetworkPostSpawn();
		OnPlacementValueChanged(false, m_isPlaced.Value);
	}

	public override void OnNetworkDespawn()
	{
		base.OnNetworkDespawn();
		m_isPlaced.OnValueChanged -= OnPlacementValueChanged;
		m_toolStationSelection.Grabbed -= OnGrabbed;
		m_toolStationSelection.Released -= OnReleased;
	}

    void Update()
	{
		if (m_isGrabbedLocally)
		{
			RaycastHit hit;

			if (m_isSnappedToMap)
			{
				bool handleTooFar;
				Vector3 snapToHandle = transform.position - m_snappedWorldPosition;

				// This condition is true only when the object has just been grabbed and has not been unsnapped yet.
				if (m_isPlaced.Value)
				{
					Vector3 adjustedSnapToHandle = new Vector3(snapToHandle.x, snapToHandle.y / 2f, snapToHandle.z);
					handleTooFar = Vector3.Magnitude(adjustedSnapToHandle) > m_unsnapDistanceThreshold;
				}
				else
				{
					bool handleBelowMap = snapToHandle.y < 0f;
					if (handleBelowMap)
					{
						handleTooFar = Vector3.Magnitude(snapToHandle) > m_unsnapBelowMapDistanceThreshold;
					}
					else
					{
						handleTooFar = Vector3.Magnitude(snapToHandle) > m_unsnapDistanceThreshold;
					}
				}

				if (handleTooFar)
				{
					UnSnap();
					ResetVisualsTransform();
					SetIsPlacedValueServerRPC(false);
				}
				else
				{
					SnapVisualsToMap();
				}
			}
			else 
			{
				if (Physics.Raycast(transform.position, Vector3.down, out hit, m_snapIndicatorDistanceThreshold, m_placementLayerMask))
				{
					m_snapIndicator.SetActive(true);
					m_snapIndicator.transform.position = hit.point + new Vector3(0,0.001f,0);
					m_snapIndicator.transform.rotation = Quaternion.FromToRotation(transform.up, hit.normal) * transform.rotation;

					if (hit.distance <= m_snapDistanceThreshold)
					{
						Snap(hit.point + Vector3.up * m_placementYOffset);
						m_snapIndicator.SetActive(false);
					}
				}
				else
				{
					m_snapIndicator.SetActive(false);
				}
			}
		}
		else
		{
			m_snapIndicator.SetActive(false);
		}
	}

	private void Snap(Vector3 a_position)
	{
		SnapWithoutNotify(a_position);
		SnappedToMap?.Invoke();
	}

	private void SnapWithoutNotify(Vector3 a_position)
	{
		m_isSnappedToMap = true;
		m_snappedWorldPosition = a_position;
		m_snappedWorldRotation = Quaternion.FromToRotation(transform.up, Vector3.up) * m_visualsRoot.rotation;
	}

	private void UnSnap()
	{
		UnSnapWithoutNotify();
		UnsnappedFromMap?.Invoke();
	}

	private void UnSnapWithoutNotify()
	{
		m_isSnappedToMap = false;
	}

	private void SnapVisualsToMap()
	{
		m_visualsRoot.position = m_snappedWorldPosition;
		m_visualsRoot.rotation = m_snappedWorldRotation;
	}

	private void ResetVisualsTransform()
	{
		m_visualsRoot.localPosition = Vector3.zero;
		m_visualsRoot.localRotation = Quaternion.identity;
	}

	private void OnPlacementValueChanged(bool previousValue, bool newValue)
	{
		if (newValue)
		{
			AttachedToMap?.Invoke();
		}
		else
		{
			DetachedFromMap?.Invoke();
		}
	}

	void OnGrabbed()
	{
		m_isGrabbedLocally = true;
		if (m_isPlaced.Value)
		{
			SnapWithoutNotify(transform.position);
			SnapVisualsToMap();
		}
	}

	void OnReleased()
	{
		if (m_snapIndicator.activeSelf)
		{
			Snap(m_snapIndicator.transform.position);
		}

		OnReleaseServerRPC(m_isSnappedToMap, GetSnappedLocalPosition(), GetSnappedLocalRotation());

		m_snapIndicator.transform.localPosition = Vector3.zero;
		m_snapIndicator.transform.localRotation = Quaternion.identity;
		m_snapIndicator.SetActive(false);

		m_isGrabbedLocally = false;
	}

	[Rpc(SendTo.Server, RequireOwnership = false)]
	private void OnReleaseServerRPC(bool a_isSnappedToMap, Vector3 a_snapLocalPosition, Quaternion a_snapLocalRotation)
	{
		if (a_isSnappedToMap)
		{
			transform.localPosition = a_snapLocalPosition;
			transform.localRotation = a_snapLocalRotation;
		}

		SetIsPlacedValueServerRPC(a_isSnappedToMap);

		UnSnapWithoutNotify();
		ResetVisualsTransform();
	}

	[Rpc(SendTo.Server, RequireOwnership = true)]
	private void SetIsPlacedValueServerRPC(bool a_isPlaced)
	{
		m_isPlaced.Value = a_isPlaced;
	}

	public void PlaceableWasDuplicated()
	{
		SetIsPlacedValueServerRPC(false);
		UnSnapWithoutNotify();
		ResetVisualsTransform();
	}

	public Vector3 GetSnappedLocalPosition()
    {
		return GetLocalMapPosition(m_snappedWorldPosition);
    }

	public Quaternion GetSnappedLocalRotation()
	{
		return GetLocalMapRotation(m_snappedWorldRotation);
	}

	private Vector3 GetLocalMapPosition(Vector3 a_worldPosition)
	{
		return m_toolStationSelection.DefaultRootTransform.InverseTransformPoint(a_worldPosition);
	}

	private Quaternion GetLocalMapRotation(Quaternion a_worldRotation)
	{
		return Quaternion.Inverse(m_toolStationSelection.DefaultRootTransform.rotation) * a_worldRotation;
	}
}