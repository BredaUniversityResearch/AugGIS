using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
#if USE_OPEN_CV
using OpenCVForUnity.Calib3dModule;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils;
#endif
using PassthroughCameraSamples;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

public abstract class AMarkerDetector : MonoBehaviour
{
	#if USE_OPEN_CV
	public enum MarkerDetectionMode
	{
		Single,
		Multiple
	}

	[Serializable]
	public struct MarkerDetectionResult : IEquatable<MarkerDetectionResult>
	{
		public string decodedText;
		public Mat corners;

		public bool Equals(MarkerDetectionResult a_other)
		{
			return decodedText == a_other.decodedText;
		}
	}

	[SerializeField]
	[Required]
	private Transform m_cameraAnchorTransform;

	[SerializeField]
	[Required]
	private WebCamTextureManager m_webCamTextureManager;

	[SerializeField]
	protected RawImage m_previewImage;

	[SerializeField]
	protected int m_passthroughTextureDownscaleFactor = 4;

	[SerializeField]
	private float m_markerSizeInMeters = 0.15f;
	public float MarkerSizeInMeters => m_markerSizeInMeters;

	private bool m_isInitialised = false;
	private PassthroughCameraEye m_passthroughCameraEye;
	private RenderTexture m_downsampledTexture;
	private Texture2D m_webcamTextureCache;
	Texture2D m_previewTexture;
	protected Mat m_cameraIntrinsicMatrix;
	protected MatOfDouble m_cameraDistortionCoeffcients;
	private MatOfPoint3f m_markerObjectPoints;
	Mat m_imgMat;
	protected Mat m_downsizedImgMat;
	protected Mat m_previewMat = null;

	private Dictionary<string, OpenCVForUnity.UnityUtils.PoseData> m_prevPoseDataDictionary = new Dictionary<string, OpenCVForUnity.UnityUtils.PoseData>();
	private void Awake()
	{
		m_markerObjectPoints = new MatOfPoint3f(new Point3(-m_markerSizeInMeters / 2f, m_markerSizeInMeters / 2f, 0),
											 new Point3(m_markerSizeInMeters / 2f, m_markerSizeInMeters / 2f, 0),
											 new Point3(m_markerSizeInMeters / 2f, -m_markerSizeInMeters / 2f, 0),
											 new Point3(-m_markerSizeInMeters / 2f, -m_markerSizeInMeters / 2f, 0));

		m_passthroughCameraEye = m_webCamTextureManager.Eye;
		StartCoroutine(IntiailiseCameraRoutine());
	}

	public abstract int ProcessMarkers(ref Mat m_processingMat, ref MarkerDetectionResult[] a_results);
	public virtual void OnInitialise() { }
	private IEnumerator IntiailiseCameraRoutine()
	{
		yield return new WaitUntil(() => m_webCamTextureManager.IsInitialised);

		PassthroughCameraIntrinsics intrinsics = PassthroughCameraUtils.GetCameraIntrinsics(m_passthroughCameraEye);
		InitialseCameraMatrix(intrinsics.FocalLength.x, intrinsics.FocalLength.y, intrinsics.PrincipalPoint.x, intrinsics.PrincipalPoint.y);

		int textureWidth = m_webCamTextureManager.WebCamTexture.width;
		int textureHeight = m_webCamTextureManager.WebCamTexture.height;

		m_imgMat = new Mat(textureHeight, textureWidth, CvType.CV_8UC4);
		m_downsizedImgMat = new Mat();

		m_downsizedImgMat = new Mat(textureHeight / m_passthroughTextureDownscaleFactor, textureWidth / m_passthroughTextureDownscaleFactor, CvType.CV_8UC4);
		m_previewTexture = new Texture2D(m_downsizedImgMat.cols(), m_downsizedImgMat.rows(), TextureFormat.RGBA32, false);
		m_isInitialised = true;

		OnInitialise();
		yield return null;
	}

	public void InitialseCameraMatrix(float a_fx, float a_fy, float a_cx, float a_cy)
	{
		a_fx = a_fx / m_passthroughTextureDownscaleFactor;
		a_fy = a_fy / m_passthroughTextureDownscaleFactor;
		a_cx = a_cx / m_passthroughTextureDownscaleFactor;
		a_cy = a_cy / m_passthroughTextureDownscaleFactor;

		m_cameraIntrinsicMatrix = new Mat(3, 3, CvType.CV_64FC1);
		m_cameraIntrinsicMatrix.put(0, 0, a_fx);
		m_cameraIntrinsicMatrix.put(0, 1, 0);
		m_cameraIntrinsicMatrix.put(0, 2, a_cx);
		m_cameraIntrinsicMatrix.put(1, 0, 0);
		m_cameraIntrinsicMatrix.put(1, 1, a_fy);
		m_cameraIntrinsicMatrix.put(1, 2, a_cy);
		m_cameraIntrinsicMatrix.put(2, 0, 0);
		m_cameraIntrinsicMatrix.put(2, 1, 0);
		m_cameraIntrinsicMatrix.put(2, 2, 1.0f);

		m_cameraDistortionCoeffcients = new MatOfDouble(0, 0, 0, 0);
	}

	public int TryDetectMarkers(ref MarkerDetectionResult[] results)
	{
		if (!m_isInitialised)
		{
			return 0;
		}

		if (!m_webCamTextureManager)
		{
			Debug.LogWarning("[MarkerCodeScanner] Camera helper is not assigned.");
			return 0;
		}

		if (!m_webCamTextureManager.WebCamTexture || !m_webCamTextureManager.WebCamTexture.isPlaying)
		{
			return 0;
		}

		OpenCVForUnity.UnityUtils.Utils.webCamTextureToMat(m_webCamTextureManager.WebCamTexture, m_imgMat);

		Imgproc.resize(m_imgMat, m_downsizedImgMat, m_downsizedImgMat.size());
		int markersFound = ProcessMarkers(ref m_downsizedImgMat, ref results);

		if (m_previewImage != null && m_previewMat != null)
		{
			OpenCVForUnity.UnityUtils.Utils.matToTexture2D(m_previewMat, m_previewTexture);
			m_previewImage.texture = m_previewTexture;
			AspectRatioFitter aspectRatioFitter = m_previewImage.GetComponent<AspectRatioFitter>();
			if (aspectRatioFitter)
			{
				aspectRatioFitter.aspectRatio = (float)m_previewTexture.width / m_previewTexture.height;
			}
			m_previewImage.texture = m_previewTexture;
		}

		return markersFound;

	}

	public OpenCVForUnity.UnityUtils.PoseData EstimateMarkerPose(MarkerDetectionResult a_markerDetectionResult,Mat a_previewMat)
	{
		using (Mat rotationVec = new Mat(1, 1, CvType.CV_64FC3))
		using (Mat translationVec = new Mat(1, 1, CvType.CV_64FC3))
		using (Mat corner_4x1 = a_markerDetectionResult.corners.reshape(2, 4))
		using (MatOfPoint2f imagePoints = new MatOfPoint2f(corner_4x1))
		{
			Calib3d.solvePnP(m_markerObjectPoints, imagePoints, m_cameraIntrinsicMatrix, m_cameraDistortionCoeffcients, rotationVec, translationVec);

			// Convert to Unity coordinate system
			double[] rvecArr = new double[3];
			rotationVec.get(0, 0, rvecArr);
			double[] tvecArr = new double[3];
			translationVec.get(0, 0, tvecArr);

			if (a_previewMat != null && m_previewImage != null)
			{
				Calib3d.drawFrameAxes(a_previewMat, m_cameraIntrinsicMatrix, m_cameraDistortionCoeffcients, rotationVec, translationVec, m_markerSizeInMeters * 0.5f);
			}

			OpenCVForUnity.UnityUtils.PoseData localPoseData = ARUtils.ConvertRvecTvecToPoseData(rvecArr, tvecArr);

			Pose eyeWorldPose = PassthroughCameraUtils.GetCameraPoseInWorld(m_passthroughCameraEye);

			m_cameraAnchorTransform.rotation = eyeWorldPose.rotation;
			m_cameraAnchorTransform.position = eyeWorldPose.position;

			Matrix4x4 arMatrix = ARUtils.ConvertPoseDataToMatrix(ref localPoseData, true);
			arMatrix = m_cameraAnchorTransform.localToWorldMatrix * arMatrix;

			OpenCVForUnity.UnityUtils.PoseData resultPoseData = new OpenCVForUnity.UnityUtils.PoseData();

			resultPoseData.pos = ARUtils.ExtractTranslationFromMatrix(ref arMatrix);
			resultPoseData.rot = ARUtils.ExtractRotationFromMatrix(ref arMatrix);

			return resultPoseData;
		}
	}
	private void OnDestroy()
	{
		if (m_downsampledTexture != null)
		{
			m_downsampledTexture.Release();
			Destroy(m_downsampledTexture);
		}
		if (m_webcamTextureCache != null)
		{
			Destroy(m_webcamTextureCache);
		}

		if (m_previewTexture != null)
		{
			Destroy(m_previewTexture);
		}

		m_cameraIntrinsicMatrix?.Dispose();
		m_cameraDistortionCoeffcients?.Dispose();

		m_markerObjectPoints?.Dispose();
		m_imgMat?.Dispose();
		m_downsizedImgMat?.Dispose();
	}

	#endif
}
