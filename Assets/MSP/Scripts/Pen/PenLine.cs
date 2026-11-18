using System;
using System.Collections.Generic;
using POV_Unity;
using Sirenix.OdinInspector;
using UnityEngine;

public class PenLine : MonoBehaviour
{
	public Action<Vector3> LinePointAddedEvent;
	public Action<Vector3> LastPointUpdatedEvent;
	public Action LinePointsReplacedEvent;
	public Action LineErasedEvent;
	public Action LineFinishedEvent;

	public Action LineColorChangedEvent;
	public Action LineWidthChangedEvent;

	private LineRenderer m_lineRenderer;
	public LineRenderer LineRenderer => m_lineRenderer;
	private MeshCollider m_meshCollider;

	[SerializeField]
	private GameObjectSet m_set;

	[SerializeField]
	[ReadOnly]
	private Color m_lineColor;
	public Color LineColor => m_lineColor;

	[SerializeField]
	[ReadOnly]
	private float m_lineWidth;

	public float LineWidth => m_lineWidth;

	private void Awake()
	{
		m_lineRenderer = GetComponent<LineRenderer>();
		m_meshCollider = GetComponent<MeshCollider>();

		Debug.Assert(m_lineRenderer != null, this);
		Debug.Assert(m_meshCollider != null, this);

		if (ImportedConfigRoot.Instance.ImportComplete)
		{
			OnConfigImportComplete();
		}
		else
		{
			ImportedConfigRoot.Instance.m_onImportComplete += OnConfigImportComplete;
		}

		m_set.Add(gameObject);
	}

	private void OnDestroy()
	{
		m_set.Remove(gameObject);
	}

	private void OnConfigImportComplete()
	{
		m_lineRenderer.material.renderQueue = VectorLayer.s_HighestBaseRenderQueueValue + 20;
	}

	public void AddLocalPoint(Vector3 a_position)
	{
		AddLocalPointWithoutNotify(a_position);
		LinePointAddedEvent?.Invoke(a_position);
	}

	public void AddLocalPointWithoutNotify(Vector3 a_position)
	{
		m_lineRenderer.positionCount += 1;
		m_lineRenderer.SetPosition(m_lineRenderer.positionCount - 1, a_position);
	}

	public void AddGlobalPoint(Vector3 a_position)
	{
		AddLocalPoint(GlobalToLocal(a_position));
	}

	public void UpdateLastLocalPoint(Vector3 a_position)
	{
		if (m_lineRenderer.positionCount == 0)
		{
			AddLocalPoint(a_position);
			return;
		}

		UpdateLastLocalPointWithoutNotify(a_position);
		LastPointUpdatedEvent?.Invoke(a_position);
	}

	public void UpdateLastLocalPointWithoutNotify(Vector3 a_position)
	{
		m_lineRenderer.SetPosition(m_lineRenderer.positionCount - 1, a_position);
	}

	/// <summary>
	/// Replaces all renderer positions (including the origin point at index 0).
	/// </summary>
	public void ReplaceLocalPoints(IReadOnlyList<Vector3> a_points)
	{
		m_lineRenderer.positionCount = a_points.Count;
		for (int i = 0; i < a_points.Count; i++)
		{
			m_lineRenderer.SetPosition(i, a_points[i]);
		}
		LinePointsReplacedEvent?.Invoke();
	}

	public void GetLocalPoints(List<Vector3> a_result)
	{
		a_result.Clear();
		for (int i = 0; i < m_lineRenderer.positionCount; i++)
		{
			a_result.Add(m_lineRenderer.GetPosition(i));
		}
	}

	public Vector3 GetLastLocalPoint()
	{
		return m_lineRenderer.positionCount > 0 ? m_lineRenderer.GetPosition(m_lineRenderer.positionCount - 1) : Vector3.zero;
	}

	public Vector3 GlobalToLocal(Vector3 a_position)
	{
		return transform.InverseTransformPoint(a_position);
	}

	/// <summary>
	/// Converts a world-space distance into this line's local space (assumes uniform scale).
	/// </summary>
	public float GlobalToLocalDistance(float a_distance)
	{
		float scale = Mathf.Abs(transform.lossyScale.x);
		return scale > 1e-6f ? a_distance / scale : a_distance;
	}

	public void ChangeLineColor(Color a_newColor)
	{
		ChangeLineColorWithoutNotify(a_newColor);
		LineColorChangedEvent?.Invoke();
	}

	public void ChangeLineColorWithoutNotify(Color a_newColor)
	{
		m_lineColor = a_newColor;
		float alpha = 1.0f;

		Gradient gradient = new Gradient();
		gradient.SetKeys(
			new GradientColorKey[] { new GradientColorKey(a_newColor, 0.0f), new GradientColorKey(a_newColor, 1.0f) },
			new GradientAlphaKey[] { new GradientAlphaKey(alpha, 0.0f), new GradientAlphaKey(alpha, 1.0f) }
			);
		m_lineRenderer.colorGradient = gradient;
	}

	public void ChangeLineWidth(float a_newWidth)
	{
		ChangeLineWidthWithoutNotify(a_newWidth);
		LineWidthChangedEvent?.Invoke();
	}

	public void ChangeLineWidthWithoutNotify(float a_newWidth)
	{
		m_lineWidth = a_newWidth;
		m_lineRenderer.startWidth = a_newWidth;
		m_lineRenderer.endWidth = a_newWidth;
	}

	public void FinishLine()
	{
		FinishLineWithoutNotify();
		LineFinishedEvent?.Invoke();
	}

	public void FinishLineWithoutNotify()
	{
		CreateCollider();
	}

	void CreateCollider()
	{
		Mesh mesh = new Mesh();
		m_lineRenderer.BakeMesh(mesh, Camera.main);

		m_meshCollider.sharedMesh = mesh;
	}
	
	public void EraseLine()
	{
		if(LineErasedEvent.GetInvocationList().Length == 0)
		{
			Destroy(gameObject);
		}
		else
		{
			LineErasedEvent.Invoke();
		}
	}
}
