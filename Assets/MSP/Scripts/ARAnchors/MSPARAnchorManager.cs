using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class MSPARAnchorManager : MonoBehaviour
{
	private static MSPARAnchorManager m_instance = null;
	public static MSPARAnchorManager Instance => m_instance;

	[Required]
	[SerializeField]
	private ARAnchorManager m_arAnchorManager;

	public ARAnchorManager ARAnchorManager => m_arAnchorManager;

	void Awake()
	{
		if (m_instance != null)
		{
			Debug.LogError("There is already an existing instance of MSPARAnchorManager");
			Destroy(this);
			return;
		}

		m_instance = this;
	}

	public async Awaitable<ARAnchor> TryCreateAnchorAtPose(Pose a_pose)
	{
		var result = await m_arAnchorManager.TryAddAnchorAsync(a_pose);
		if (result.status.IsSuccess())
		{
			ARAnchor anchor = result.value;
			return anchor;
		}


		Debug.LogError("Could Not Create AR Anchor!");
		return null;
	}

	public bool TryRemoveAnchor(ARAnchor a_anchor)
	{
		return m_arAnchorManager.TryRemoveAnchor(a_anchor);
	}
}
