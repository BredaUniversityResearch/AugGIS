using System;
using System.Collections.Generic;
using System.Linq;
using POV_Unity;
using Unity.VisualScripting;
using UnityEngine;

public class UISortWorldSpace : MonoBehaviour
{
	public enum SortMode
	{
		DistanceToCamera,
		WorldSpaceYPosition
	}

	[SerializeField]
	private Canvas m_canvas;

	[SerializeField]
	private RectTransform m_rectTransform;

	[SerializeField]
	private Vector2 m_normalizedSortingPivotPosition = new Vector2(0.5f,0.5f);

	[SerializeField]
	private SortMode m_sortMode = SortMode.DistanceToCamera;
	
	private Camera m_mainCamera = null;

	private void Start()
	{
		m_mainCamera = Camera.main;
	}

	private void Update()
	{
		m_canvas.sortingOrder = 0;
		if (LayerManager.Instance == null) { return; }

		foreach (VectorLayer vectorLayer in LayerManager.Instance.SortedVectorLayers)
		{
			if (m_sortMode == SortMode.DistanceToCamera)
			{
				SortBasedOnDistanceToCamera(vectorLayer);
			}
			else if (m_sortMode == SortMode.WorldSpaceYPosition)
			{
				SortBasedOnYPosition(vectorLayer);
			}
		}
	}

	private void SortBasedOnDistanceToCamera(VectorLayer a_vectorLayer)
	{
		float layerDistanceToCamera = Vector3.Distance(m_mainCamera.transform.position, a_vectorLayer.LayerRoot.transform.position);
		float uiDistanceToCamera = Vector3.Distance(m_mainCamera.transform.position, CalculateSortingPivotWorldPosition());

		if (uiDistanceToCamera < layerDistanceToCamera)
		{
			m_canvas.sortingOrder = a_vectorLayer.CurrentRenderOrderValue + 1;
		}
	}

	private void SortBasedOnYPosition(VectorLayer a_vectorLayer)
	{
		float layerYPos = a_vectorLayer.LayerRoot.transform.position.y;
		float pivotWorldSpaceYPosition = CalculateSortingPivotWorldPosition().y;

		if (pivotWorldSpaceYPosition > layerYPos)
		{
			m_canvas.sortingOrder = a_vectorLayer.CurrentRenderOrderValue + 1;
		}
	}

	private Vector3 CalculateSortingPivotWorldPosition()
	{
		Vector3[] corners = new Vector3[4];
		m_rectTransform.GetLocalCorners(corners);

		Vector3 verticalLerp = Vector3.Lerp(corners[0], corners[2], m_normalizedSortingPivotPosition.y);
		Vector3 horizontalLerp = Vector3.Lerp(corners[0], corners[3], m_normalizedSortingPivotPosition.x);

		Vector3 localPos = new Vector3(horizontalLerp.x, verticalLerp.y, 0);
		return m_rectTransform.TransformPoint(localPos);
	}

	private void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.green;
		Gizmos.DrawSphere(CalculateSortingPivotWorldPosition(), 0.005f);
	}
}
