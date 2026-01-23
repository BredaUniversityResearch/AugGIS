using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AnchorSetupMenu : MonoBehaviour
{
	public event Action AnchorPlacementConfirmed;

	[Header("Anchor Data")]
	[SerializeField]
	[Required]
	private MapAnchor m_firstAnchor = null;

	[SerializeField]
	[Required]
	private MapAnchor m_secondAnchor = null;

	[Header("Buttons")]
	[Required]
	[SerializeField]
	private CustomXRButton m_confirmAnchorPlacementButton = null;

	[SerializeField]
	[Required]
	private CustomXRButton m_resetAnchorsButton = null;

	[SerializeField]
	[Required]
	private CustomXRButton m_cancelButton = null;

	[SerializeField]
	[Required]
	private GameObject m_AnchorConfirmationUI = null;

	[Header("Tutorial")]
	[SerializeField]
	[Required]
	private TextMeshProUGUI m_tutorialTextComponent;

	[Header("Preview Visualisation")]
	[SerializeField]
	private GameObject m_mapPreviewPrefab = null;
	private GameObject m_mapPreviewGameObject;

	private bool AreAnchorsReadyToConfirm => (m_firstAnchor.CurrentAnchorState == MapAnchor.EAnchorState.Released || m_firstAnchor.CurrentAnchorState == MapAnchor.EAnchorState.PlacedOnMarker) &&
										 	 (m_secondAnchor.CurrentAnchorState == MapAnchor.EAnchorState.Released || m_secondAnchor.CurrentAnchorState == MapAnchor.EAnchorState.PlacedOnMarker);
	
	
#if USE_OPEN_CV
	private ArUcoMarkerDetector m_arucoMarkerDetector;
	private AMarkerDetector.MarkerDetectionResult[] m_markerDetectionResults = new AMarkerDetector.MarkerDetectionResult[4];
#endif
	void Start()
	{
		m_confirmAnchorPlacementButton.OnPress.AddListener(ConfirmPlacement);
		m_resetAnchorsButton.OnPress.AddListener(OnResetButtonClicked);
		m_cancelButton.OnPress.AddListener(OnCancelButtonPressed);

		m_firstAnchor.AnchorStateChanged += OnAnchorStateChanged;
		m_secondAnchor.AnchorStateChanged += OnAnchorStateChanged;

#if USE_OPEN_CV
		m_arucoMarkerDetector = MarkerDetectionManager.Instance.ArUcoMarkerDetector;
#endif
		m_mapPreviewGameObject = Instantiate(m_mapPreviewPrefab, null);
		m_mapPreviewPrefab.gameObject.SetActive(false);
	}

	void OnEnable()
	{
		InitialiseUI();
	}

	void OnDisable()
	{
		m_mapPreviewGameObject.SetActive(false);
	}

	void Update()
	{
		m_mapPreviewGameObject.SetActive(m_firstAnchor.CurrentAnchorState != MapAnchor.EAnchorState.Idle && m_secondAnchor.CurrentAnchorState != MapAnchor.EAnchorState.Idle);
		if (m_mapPreviewGameObject.activeInHierarchy)
		{
			Matrix4x4 worldMatrix = Utils.CalculateWorldMatrixBasedOnAnchorPosition(m_firstAnchor.transform.position, m_secondAnchor.transform.position);
			m_mapPreviewGameObject.transform.SetPositionAndRotation(worldMatrix.GetPosition(), worldMatrix.rotation);
			m_mapPreviewGameObject.transform.localScale = worldMatrix.lossyScale;
		}

#if USE_OPEN_CV
		ProccessArUcoMarkers();
#endif
	}

#if USE_OPEN_CV
	private void ProccessArUcoMarkers()
	{
		if (m_arucoMarkerDetector == null)
		{
			return;
		}

		if (AreAnchorsReadyToConfirm)
		{
			return;
		}

		if (m_firstAnchor.ShouldScanArUcoMarker == false && m_secondAnchor.ShouldScanArUcoMarker == false)
		{
			return;
		}

		int detectedMarkers = m_arucoMarkerDetector.TryDetectMarkers(ref m_markerDetectionResults);
		for (int i = 0; i < detectedMarkers; i++)
		{
			AMarkerDetector.MarkerDetectionResult result = m_markerDetectionResults[i];
			m_firstAnchor.TryPlaceOnArUcoMarker(result);
			m_secondAnchor.TryPlaceOnArUcoMarker(result);
		}
	}
#endif

	void OnDestroy()
	{
		m_firstAnchor.AnchorStateChanged -= OnAnchorStateChanged;
		m_secondAnchor.AnchorStateChanged -= OnAnchorStateChanged;
	}

	private void InitialiseUI()
	{
		m_tutorialTextComponent.gameObject.SetActive(true);
		m_AnchorConfirmationUI.SetActive(false);
	}

	private void OnAnchorStateChanged(MapAnchor.EAnchorState newState)
	{
		m_tutorialTextComponent.gameObject.SetActive(m_firstAnchor.CurrentAnchorState == MapAnchor.EAnchorState.Idle || m_secondAnchor.CurrentAnchorState == MapAnchor.EAnchorState.Idle);
		
		if (AreAnchorsReadyToConfirm)
		{
			m_AnchorConfirmationUI.SetActive(true);
		}
	}

	[Button("Accept Anchors", ButtonSizes.Medium),  GUIColor(0f, 1, 0f)]
	[HideInEditorMode]
	private void ConfirmPlacement()
	{
		Debug.Log("Confrim Placement");
		m_firstAnchor.SetAnchorState(MapAnchor.EAnchorState.Confirmed);
		m_secondAnchor.SetAnchorState(MapAnchor.EAnchorState.Confirmed);
		AnchorPlacementConfirmed?.Invoke();
	}

	[Button("Reset Anchors", ButtonSizes.Medium), GUIColor(0.4f, 0.8f, 1)]
	[HideInEditorMode]
	private void OnResetButtonClicked()
	{
		Debug.Log("Reset Placement");
		InitialiseUI();
		m_firstAnchor.SetAnchorState(MapAnchor.EAnchorState.Idle);
		m_secondAnchor.SetAnchorState(MapAnchor.EAnchorState.Idle);
	}

	[Button("Cancel", ButtonSizes.Medium), GUIColor(0.8f, 0.1f, 0.1f)]
	[HideInEditorMode]
	private void OnCancelButtonPressed()
	{
		m_firstAnchor.SetAnchorState(MapAnchor.EAnchorState.Idle);
		m_secondAnchor.SetAnchorState(MapAnchor.EAnchorState.Idle);
		SessionManager.Instance.SessionFSM.ChangeState<MainMenuSessionState>();
	}
}
