using System;
using PassthroughCameraSamples;
using POV_Unity;
using Sirenix.OdinInspector;
using UnityEngine;

public class UILayerProbe : MonoBehaviour
{
	public class UILayerProbeElementData : IUIListElementData
	{
		public string Text => m_layerName;
		public string InfoText => m_infoText;
		public LayerManager.LayerQueryData LayerQueryData => m_layerQueryData;

		private string m_layerName;
		private string m_infoText;

		private LayerManager.LayerQueryData m_layerQueryData;

		public UILayerProbeElementData(LayerManager.LayerQueryData a_layerQueryData)
		{
			m_layerQueryData = a_layerQueryData;
			m_layerName = a_layerQueryData.layer.@short;
			m_infoText = a_layerQueryData.typeData;
		}
	}

	public Action OnEnabled;
	public Action OnDisabled; 

	[SerializeField]
	[Required]
	private UIPagedList m_pagedList = null;
	public UIPagedList PagedList => m_pagedList;

    void OnEnable()
    {
		RotateUI();
		OnEnabled?.Invoke();
    }

	void OnDisable()
	{
		OnDisabled?.Invoke();
	}

    void Update()
	{
		RotateUI();
	}

    private void RotateUI()
    {
        if (PassthroughCameraUtils.HMD.isValid)
		{
			PassthroughCameraUtils.HMD.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 headPosition);
			Quaternion targetRotation = Quaternion.LookRotation((transform.position - headPosition).normalized);
			//lock rotation to only y axis.
			targetRotation.eulerAngles = new Vector3(transform.rotation.eulerAngles.x, targetRotation.eulerAngles.y, transform.eulerAngles.z);
			transform.rotation = targetRotation;
		}
    }

	public void AddLayerQueryData(LayerManager.LayerQueryData a_layerQueryData)
	{
		UILayerProbeElementData uiLayerProbeElementData = new UILayerProbeElementData(a_layerQueryData);
		m_pagedList.AddElementData(uiLayerProbeElementData);
	}

	public void ClearProbeData()
	{
		m_pagedList.ClearMenu();
	}
}
