using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.VisualScripting;
using UnityEngine;

public class ApplyTrackedPoseDataOnMapRing : ApplyTrackedPoseData
{
	[SerializeField]
	private DiscShapeGenerator m_mainRingMesh;

	[SerializeField]
	private List<DiscShapeGenerator> m_ringSubmeshes = new List<DiscShapeGenerator>();

	protected override void OnScaleChanged(float a_scale)
	{
		float scaleFactor = Mathf.Clamp(a_scale, m_minMaxScale.x, m_minMaxScale.y);
		Vector3 desiredScale;
		if (ScaleYAxis)
		{
			desiredScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
		}
		else
		{
			desiredScale = new Vector3(scaleFactor, transform.localScale.y, scaleFactor);
		}

		if(desiredScale == transform.localScale)
		{
			return;
		}

		transform.localScale = desiredScale;

		// Update ring thickness to compensate for scale change
		// Radius is determined and scaled accordingly using transforms only for main ring mesh
		m_mainRingMesh.GenerateShapeRuntime(m_mainRingMesh.radius, m_mainRingMesh.thickness / scaleFactor, m_mainRingMesh.segments, m_mainRingMesh.angleStart, m_mainRingMesh.angleEnd);

		foreach (DiscShapeGenerator m_ringMeshGenerator in m_ringSubmeshes)
		{
			float radiusDifference = (m_mainRingMesh.radius - m_ringMeshGenerator.radius) / scaleFactor;
			m_ringMeshGenerator.GenerateShapeRuntime(m_mainRingMesh.radius - radiusDifference, m_ringMeshGenerator.thickness / scaleFactor, m_ringMeshGenerator.segments, m_ringMeshGenerator.angleStart, m_ringMeshGenerator.angleEnd);
		}
	}
}