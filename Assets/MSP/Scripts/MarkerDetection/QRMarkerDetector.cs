using System;

#if USE_OPEN_CV
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.ObjdetectModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.Calib3dModule;
#endif

using Sirenix.Utilities;


#if USE_OPEN_CV
[Serializable]
public class QrCodeResult
{
	public string text;
	public Mat corners;

	public OpenCVForUnity.UnityUtils.PoseData estimatedPoseData;
}
#endif

public class QRMarkerDetector : AMarkerDetector
{
	
#if USE_OPEN_CV
	private Mat m_detectedQRCorners = new Mat();
	private Mat m_straightCode = new Mat();

	QRCodeDetector m_detector = new QRCodeDetector();

	private Mat m_grayscaleMat = new Mat();

	public override int ProcessMarkers(ref Mat a_processingMat,ref MarkerDetectionResult[] a_results)
	{
		Imgproc.cvtColor(a_processingMat, m_grayscaleMat, Imgproc.COLOR_RGBA2GRAY);
		string decodedInfo = m_detector.detectAndDecode(m_grayscaleMat, m_detectedQRCorners, m_straightCode);

		if (decodedInfo.IsNullOrWhitespace() || m_detectedQRCorners.cols() < 4)
		{
			return 0;
		}

		if (m_previewImage)
		{
			m_previewMat = a_processingMat;
			for (int i = 0; i < m_detectedQRCorners.rows(); i++)
			{
				float[] points_arr = new float[8];
				m_detectedQRCorners.get(i, 0, points_arr);
				Imgproc.line(a_processingMat, new Point(points_arr[0], points_arr[1]), new Point(points_arr[2], points_arr[3]), new Scalar(255, 0, 0, 255), 2);
				Imgproc.line(a_processingMat, new Point(points_arr[2], points_arr[3]), new Point(points_arr[4], points_arr[5]), new Scalar(255, 0, 0, 255), 2);
				Imgproc.line(a_processingMat, new Point(points_arr[4], points_arr[5]), new Point(points_arr[6], points_arr[7]), new Scalar(255, 0, 0, 255), 2);
				Imgproc.line(a_processingMat, new Point(points_arr[6], points_arr[7]), new Point(points_arr[0], points_arr[1]), new Scalar(255, 0, 0, 255), 2);

				//Debug.Log("Decoded Info: " + decodedInfo);
				Imgproc.putText(a_processingMat, decodedInfo, new Point(points_arr[0], points_arr[1]), Imgproc.FONT_HERSHEY_SIMPLEX, 0.7, new Scalar(255, 255, 255, 255), 2, Imgproc.LINE_AA, false);
			}
		}

		//NOTE: For now we are only returning the first detected marker
		a_results[0].corners = m_detectedQRCorners;
		a_results[0].decodedText = decodedInfo;

		return 1;
	}

	void OnDestroy()
	{
		m_detectedQRCorners?.Dispose();
		m_straightCode?.Dispose();
		m_grayscaleMat?.Dispose();
	}
#endif
}