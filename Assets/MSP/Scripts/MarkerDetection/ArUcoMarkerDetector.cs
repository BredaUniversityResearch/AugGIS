using System.Collections.Generic;
using System.Text;

#if USE_OPEN_CV
using OpenCVForUnity.Calib3dModule;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.ObjdetectModule;
using static OpenCVForUnityExample.ArUcoExample;
#endif

using UnityEngine;
public class ArUcoMarkerDetector : AMarkerDetector
{
	#if USE_OPEN_CV
	[SerializeField]
	private ArUcoDictionary m_arUcoDictionaryType = ArUcoDictionary.DICT_5X5_50;
	private Dictionary m_arUcoDictionary;
	private ArucoDetector m_arucoDetector;

	private Mat m_processingRgbMat;

	private Mat m_detectedMarkerIds;
	private List<Mat> m_detectedMarkerCorners;
	private List<Mat> m_rejectedMarkerCandidates;
	private Mat m_recoveredMarkerIndices;

	public override void OnInitialise()
	{
		base.OnInitialise();

		DetectorParameters detectorParams = new DetectorParameters();
		detectorParams.set_minDistanceToBorder(3);
		detectorParams.set_useAruco3Detection(true);
		detectorParams.set_cornerRefinementMethod(Objdetect.CORNER_REFINE_SUBPIX);
		detectorParams.set_minSideLengthCanonicalImg(20);
		detectorParams.set_errorCorrectionRate(0.8);
		RefineParameters refineParameters = new RefineParameters(10f, 3f, true);

		m_arUcoDictionary = Objdetect.getPredefinedDictionary((int)m_arUcoDictionaryType);
		m_arucoDetector = new ArucoDetector(m_arUcoDictionary, detectorParams, refineParameters);

		m_processingRgbMat = new Mat(m_downsizedImgMat.rows(), m_downsizedImgMat.cols(), CvType.CV_8UC3);

		m_detectedMarkerIds = new Mat();
		m_detectedMarkerCorners = new List<Mat>();
		m_rejectedMarkerCandidates = new List<Mat>();
		m_recoveredMarkerIndices = new Mat();
	}

	public override int ProcessMarkers(ref Mat a_proccessingImage, ref MarkerDetectionResult[] a_results)
	{
		Imgproc.cvtColor(a_proccessingImage, m_processingRgbMat, Imgproc.COLOR_RGBA2RGB);
		
		m_detectedMarkerIds.create(0, 50, CvType.CV_32S);
		m_detectedMarkerCorners.Clear();
		m_rejectedMarkerCandidates.Clear();

		m_arucoDetector.detectMarkers(m_processingRgbMat, m_detectedMarkerCorners, m_detectedMarkerIds, m_rejectedMarkerCandidates);

		if (m_previewImage != null && (m_detectedMarkerCorners.Count == m_detectedMarkerIds.total() || m_detectedMarkerIds.total() == 0))
		{
			Objdetect.drawDetectedMarkers(m_processingRgbMat, m_detectedMarkerCorners, m_detectedMarkerIds, new Scalar(0, 255, 0));
		}

		int detectedMarkerCount = (int)m_detectedMarkerIds.total();
		for (int i = 0; i < detectedMarkerCount; i++)
		{
			int currentMarkerId = (int)m_detectedMarkerIds.get(i, 0)[0];
			a_results[i].decodedText = currentMarkerId.ToString();
			Mat detectedQRCorners = m_detectedMarkerCorners[i];
			a_results[i].corners = detectedQRCorners;
		}

		if (m_previewImage)
		{
			m_previewMat = m_processingRgbMat;
		}

		return detectedMarkerCount;
	}

	#endif
}
