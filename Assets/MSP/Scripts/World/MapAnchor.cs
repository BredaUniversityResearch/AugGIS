using System;
using Sirenix.OdinInspector;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class MapAnchor : MonoBehaviour
{
	[Serializable]
	public enum EAnchorState
	{
		None,
		Idle,
		Scanning,
		Grabbed,
		PlacedOnMarker,
		Released,
		Confirmed,
	}

	public delegate void AnchorStateChangedEvent(EAnchorState newAnchorState);
	public event AnchorStateChangedEvent AnchorStateChanged;

	[SerializeField]
	[Required]
	private XRGrabInteractable m_xrGrabInteractable = null;

	[SerializeField]
	[Required]
	private MapAnchorPlacementData m_anchorPlacementData = null;

	[SerializeField]
	[Required]
	private ArUcoMarker m_arUcoMarker;
	public ArUcoMarker ArUcoMarker => m_arUcoMarker;

	[SerializeField]
	private int m_arUcoMarkerID = 0;

	[SerializeField]
	[Required]
	private CustomXRButton m_acceptPositionXRButton;

	[SerializeField]
	[Required]
	private GameObject m_acceptButtonPivot;

	private EAnchorState m_anchorState = EAnchorState.None;
	public EAnchorState CurrentAnchorState => m_anchorState;
	private Transform m_idleParentTransform = null;
	private Vector3 m_idlePosition = Vector3.zero;

	private ARAnchor m_arAnchor = null;

	public bool ShouldScanArUcoMarker =>  m_anchorState == EAnchorState.Idle || m_anchorState == EAnchorState.Scanning;

#if USE_OPEN_CV
	private const int c_poseFrameCount = 5;
	private OpenCVForUnity.UnityUtils.PoseData[] m_arUcoMarkerFramePoses = new OpenCVForUnity.UnityUtils.PoseData[c_poseFrameCount];
	private int m_adddedPoseCount = 0;
#endif

	private void Awake()
	{
		m_idleParentTransform = transform.parent;
		m_idlePosition = transform.localPosition;

		m_xrGrabInteractable.selectEntered.AddListener(OnGrabbed);
		m_xrGrabInteractable.selectExited.AddListener(OnReleased);

		MSPARAnchorManager.Instance.ARAnchorManager.trackablesChanged.AddListener(OnARAnchorTrackableChanged);
		m_acceptPositionXRButton.OnPress.AddListener(() =>
		{
			SetAnchorState(EAnchorState.PlacedOnMarker);
		});
	}

	private void OnEnable()
	{
		SetAnchorState(EAnchorState.Idle);
	}

	private void Update()
	{
		if (m_acceptButtonPivot.activeInHierarchy)
		{
			Quaternion targetRotation = Quaternion.LookRotation((Camera.main.transform.position - m_acceptButtonPivot.transform.position).normalized);
			targetRotation.eulerAngles = new Vector3(0, targetRotation.eulerAngles.y, 0);
			m_acceptButtonPivot.transform.rotation = targetRotation;
		}
	}

	private void OnDestroy()
	{
		m_xrGrabInteractable.selectEntered.RemoveListener(OnGrabbed);
		m_xrGrabInteractable.selectExited.RemoveListener(OnReleased);
		MSPARAnchorManager.Instance.ARAnchorManager.trackablesChanged.RemoveListener(OnARAnchorTrackableChanged);
	}

#if USE_OPEN_CV
	public bool TryPlaceOnArUcoMarker(AMarkerDetector.MarkerDetectionResult a_detectionResult)
	{
		if (int.TryParse(a_detectionResult.decodedText, out int markerID))
		{
			if (m_arUcoMarkerID == markerID && ShouldScanArUcoMarker)
			{
				OpenCVForUnity.UnityUtils.PoseData poseData = MarkerDetectionManager.Instance.ArUcoMarkerDetector.EstimateMarkerPose(a_detectionResult, null);
				poseData.rot.eulerAngles = new Vector3(poseData.rot.eulerAngles.x + 90, poseData.rot.y, poseData.rot.z);

				m_adddedPoseCount++;

				if (m_adddedPoseCount > c_poseFrameCount)
				{
					m_adddedPoseCount = 0;
				}
				
				m_arUcoMarkerFramePoses[m_adddedPoseCount - 1] = poseData;

				Vector3 positionCumulative = new Vector3();
				Vector4 rotationCumulative = new Vector4();

				for (int i = 0; i < m_adddedPoseCount; i++)
				{
					positionCumulative += m_arUcoMarkerFramePoses[i].pos;
					Quaternion rotation = m_arUcoMarkerFramePoses[i].rot;
					rotationCumulative += new Vector4(rotation.x, rotation.y, rotation.z, rotation.w);
				}

				Vector3 averagedPosition = positionCumulative / m_adddedPoseCount;
				Vector4 averagedRotation = rotationCumulative / m_adddedPoseCount;

				m_arUcoMarker.UpdateMarker(averagedPosition, new Quaternion(averagedRotation.x, averagedRotation.y, averagedRotation.z, averagedRotation.w).normalized, transform.localScale, a_detectionResult.decodedText);
				SetAnchorState(EAnchorState.Scanning);
				return true;
			}
		}

		return false;
	}
#endif

	public void SetAnchorState(EAnchorState newAnchorState)
	{
		if (m_anchorState == newAnchorState)
		{
			return;
		}

		m_anchorState = newAnchorState;
		Debug.LogFormat("Set Map Anchor State: {0}", m_anchorState.ToString());

		switch (m_anchorState)
		{
			case EAnchorState.Idle:
				{
					DestroyARAnchor();

					transform.SetParent(m_idleParentTransform);
					transform.localRotation = Quaternion.identity;
					transform.localPosition = m_idlePosition;
					m_arUcoMarker.enabled = true;
					m_acceptButtonPivot.SetActive(false);
					break;
				}
			case EAnchorState.Scanning:
				{
					DestroyARAnchor();
					transform.SetParent(null);
					m_acceptButtonPivot.SetActive(true);
					break;
				}
			case EAnchorState.Grabbed:
				{
					m_arUcoMarker.enabled = false;
					transform.SetParent(null);
					DestroyARAnchor();
					m_acceptButtonPivot.SetActive(false);
					break;
				}
			case EAnchorState.PlacedOnMarker:
			case EAnchorState.Released:
				{
					m_acceptButtonPivot.SetActive(false);
					transform.SetParent(null);
					TryCreateARAnchor();
					break;
				}
			case EAnchorState.Confirmed:
				{
					transform.SetParent(m_idleParentTransform);
					m_anchorPlacementData.SetAnchorPlacementPosition(m_xrGrabInteractable.transform.position);
					break;
				}
		}

		AnchorStateChanged?.Invoke(newAnchorState);
	}

	private void OnGrabbed(SelectEnterEventArgs a_args)
	{
		SetAnchorState(EAnchorState.Grabbed);
	}

	private void OnReleased(SelectExitEventArgs a_args)
	{
		SetAnchorState(EAnchorState.Released);
	}

	private async void TryCreateARAnchor()
	{
#if (!UNITY_SERVER && !UNITY_EDITOR)
		m_arAnchor = await MSPARAnchorManager.Instance.TryCreateAnchorAtPose(new Pose(transform.position, transform.rotation));
#endif
	}

	public void DestroyARAnchor()
	{
#if (!UNITY_SERVER && !UNITY_EDITOR)
		if (m_arAnchor == null)
		{
			return;
		}

		MSPARAnchorManager.Instance.TryRemoveAnchor(m_arAnchor);
		Destroy(m_arAnchor.gameObject);
#endif
	}

	public void OnARAnchorTrackableChanged(ARTrackablesChangedEventArgs<ARAnchor> a_args)
	{
		for (int i = 0; i < a_args.updated.Count; i++)
		{
			ARAnchor anchor = a_args.updated[i];
			if (anchor == m_arAnchor)
			{
				transform.SetPositionAndRotation(anchor.pose.position, anchor.pose.rotation);
			}
		}
	}
}
