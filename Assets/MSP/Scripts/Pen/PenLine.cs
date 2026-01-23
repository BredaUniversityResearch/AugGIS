using System;
using POV_Unity;
using Sirenix.OdinInspector;
using UnityEngine;

public class PenLine : MonoBehaviour
{
	public Action<Vector3> LinePointAddedEvent;
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
		Vector3 localPosition = transform.InverseTransformPoint(a_position);
		AddLocalPoint(localPosition);
	}

	public float GetLastPointDistance(Vector3 a_position)
	{
		float distance = Vector3.Distance(m_lineRenderer.GetPosition(m_lineRenderer.positionCount - 1), a_position - transform.position);
		return distance;
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
		if (m_lineRenderer.positionCount == 1) Debug.Log(m_lineRenderer.GetPosition(0), this);
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
