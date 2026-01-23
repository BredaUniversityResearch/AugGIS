using System;
using System.Net;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuQRCodeState : AMainMenuState
{
	[SerializeField]
	[Required]
	private GameObject m_scanningUIFeedback; 
	[SerializeField]
	[Required]
	private GameObject m_detectedUIFeedback;

	[SerializeField]
	private NetworkConnectionQRMarker m_networkConnectionQRMarkerPrefab;
	private NetworkConnectionQRMarker m_networkConnectionQRMarker;

	[SerializeField]
	private NetworkSessionConnectionData m_networkSessionConnectionData;

	private UIMainMenu m_mainMenu;

#if USE_OPEN_CV
	private QRMarkerDetector m_qrMarkerDetector;
	private AMarkerDetector.MarkerDetectionResult? m_detectedMarker = null;

	private AMarkerDetector.MarkerDetectionResult[] m_markerDestectionResults = new AMarkerDetector.MarkerDetectionResult[1];
#endif

	public override void OnInitialise()
	{
		base.OnInitialise();
		m_mainMenu = MainMenuFSM.GetComponent<UIMainMenu>();
	}

	public override void OnEnter()
	{
		base.OnEnter();
#if USE_OPEN_CV
		m_detectedMarker = null;
		m_mainMenu.EnableTraversalButtons(OnContinue, () => { MainMenuFSM.ChangeState<MainMenuScanServersState>(); });

		m_networkConnectionQRMarker = Instantiate(m_networkConnectionQRMarkerPrefab, null);
		m_networkConnectionQRMarker.gameObject.SetActive(false);
		m_qrMarkerDetector = MarkerDetectionManager.Instance.QRMarkerDetector;
#endif
	}

	public override void OnUpdate()
	{
		base.OnUpdate();
#if USE_OPEN_CV
		int detectedMarkerCount = m_qrMarkerDetector.TryDetectMarkers(ref m_markerDestectionResults);

		if (detectedMarkerCount > 0)
		{
			AMarkerDetector.MarkerDetectionResult firstResult = m_markerDestectionResults[0];
			if (!m_detectedMarker.HasValue)
			{
				m_detectedMarker = firstResult;
				m_networkConnectionQRMarker.gameObject.SetActive(true);
			}

			OpenCVForUnity.UnityUtils.PoseData pose = m_qrMarkerDetector.EstimateMarkerPose(firstResult, null);
			m_networkConnectionQRMarker.UpdateMarker(pose.pos, pose.rot, Vector3.one * m_qrMarkerDetector.MarkerSizeInMeters, firstResult.decodedText);
		}

		m_mainMenu.SetContinueButtonInteratableState(m_detectedMarker.HasValue);
		m_scanningUIFeedback.SetActive(!m_detectedMarker.HasValue);
		m_detectedUIFeedback.SetActive(m_detectedMarker.HasValue);
#endif
	}

	public override void OnExit()
	{
		base.OnExit();
#if USE_OPEN_CV
		m_mainMenu.DisableTraversalButtons();
		Destroy(m_networkConnectionQRMarker.gameObject);
#endif
	}

	private void OnContinue()
	{
#if USE_OPEN_CV
		if (!m_detectedMarker.HasValue)
		{
			Debug.LogError("No active network qr marker found!");
			return;
		}

		m_networkSessionConnectionData.ip = IPAddress.Parse(m_networkConnectionQRMarker.DecodedNetworkData.ip);
		m_networkSessionConnectionData.Port = m_networkConnectionQRMarker.DecodedNetworkData.port;
		m_networkSessionConnectionData.isServer = false;

		MainMenuFSM.ChangeState<MainMenuUserSettingsState>();
#endif
	}
}
