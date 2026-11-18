using System;
using System.Collections.Generic;
using POV_Unity;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

public class PenLine : MonoBehaviour
{
	// Drawing events (raised by the drawer while the stroke is being created)
	public Action<PenLinePoint> LinePointAddedEvent;
	public Action<PenLinePoint> LastPointUpdatedEvent;
	public Action LinePointsReplacedEvent;
	public Action LineErasedEvent;
	public Action LineFinishedEvent;

	public Action LineColorChangedEvent;
	public Action LineWidthChangedEvent;

	// Raised whenever the point model changes for any reason (drawing, editing, network sync)
	public Action PointsChangedEvent;
	public Action<bool> ClosedChangedEvent;

	private LineRenderer m_lineRenderer;
	public LineRenderer LineRenderer => m_lineRenderer;
	private MeshCollider m_meshCollider;

	[SerializeField]
	private GameObjectSet m_set;

	[Header("Editing")]
	[SerializeField]
	[Tooltip("World-space metres. A line is closed when its first and last point are closer than this (on the drawing plane).")]
	private float m_closeDistance = 0.03f;

	[SerializeField]
	[Tooltip("World-space metres. Points dragged further than this above/below the drawing plane are removed on release.")]
	private float m_removeHeight = 0.08f;
	public float RemoveHeight => m_removeHeight;

	[Header("Fill")]
	[SerializeField]
	[Tooltip("Optional. Material for the fill of closed lines, must use vertex colours. Falls back to a copy of the line material.")]
	private Material m_fillMaterial;

	[SerializeField]
	[Range(0f, 1f)]
	private float m_fillAlpha = 0.35f;

	[SerializeField]
	[Tooltip("World-space metres. The fill is drawn this far below the drawing plane so the line stays on top.")]
	private float m_fillDepthOffset = 0.0005f;

	[SerializeField]
	[ReadOnly]
	private Color m_lineColor;
	public Color LineColor => m_lineColor;

	[SerializeField]
	[ReadOnly]
	private float m_lineWidth;
	public float LineWidth => m_lineWidth;

	[SerializeField]
	[ReadOnly]
	private bool m_isClosed;
	public bool IsClosed => m_isClosed;

	[SerializeField]
	[ReadOnly]
	private bool m_isFinished;
	public bool IsFinished => m_isFinished;

	/// <summary>
	/// When set, point edits are forwarded to this handler (e.g. the network layer) instead of only being applied locally.
	/// </summary>
	public IPenLineEditHandler EditHandler { get; set; }

	private readonly List<PenLinePoint> m_points = new List<PenLinePoint>();
	public IReadOnlyList<PenLinePoint> Points => m_points;
	public int PointCount => m_points.Count;

	private Vector3[] m_positionBuffer = new Vector3[16];

	private GameObject m_fillObject;
	private Mesh m_fillMesh;
	private Material m_fillMaterialInstance;

	private const float LOCAL_EDIT_TIMEOUT = 2f;

	private enum LocalEditState
	{
		Holding,
		PendingCommit,
		PendingRemove
	}

	/// <summary>
	/// An edit made by this client that the authoritative (synced) point list may not reflect yet.
	/// It is overlaid on every network sync so the local user does not see their edit jitter or snap back.
	/// </summary>
	private class LocalEdit
	{
		public LocalEditState State;
		public Vector3 Position;
		public ulong AfterId;
		public bool PendingInsert;
		public float Time;
	}

	private readonly Dictionary<ulong, LocalEdit> m_localEdits = new Dictionary<ulong, LocalEdit>();
	private readonly List<ulong> m_localEditKeyBuffer = new List<ulong>();

	private void Awake()
	{
		m_lineRenderer = GetComponent<LineRenderer>();
		m_meshCollider = GetComponent<MeshCollider>();

		Debug.Assert(m_lineRenderer != null, this);
		Debug.Assert(m_meshCollider != null, this);

		//The prefab's renderer contains the line origin, adopt it into the point model
		for (int i = 0; i < m_lineRenderer.positionCount; i++)
		{
			m_points.Add(new PenLinePoint(PenLinePoint.NewId(), m_lineRenderer.GetPosition(i)));
		}

		if (ImportedConfigRoot.Instance.ImportComplete)
		{
			OnConfigImportComplete();
		}
		else
		{
			ImportedConfigRoot.Instance.m_onImportComplete += OnConfigImportComplete;
		}

		m_set.Add(gameObject);

#if !UNITY_SERVER
		if (GetComponent<PenLinePointHandles>() == null)
		{
			gameObject.AddComponent<PenLinePointHandles>();
		}
#endif
	}

	private void OnDestroy()
	{
		m_set.Remove(gameObject);

		if (ImportedConfigRoot.Instance != null)
		{
			ImportedConfigRoot.Instance.m_onImportComplete -= OnConfigImportComplete;
		}

		if (m_fillMesh != null)
			Destroy(m_fillMesh);
		if (m_fillMaterialInstance != null)
			Destroy(m_fillMaterialInstance);
		if (m_meshCollider != null && m_meshCollider.sharedMesh != null)
			Destroy(m_meshCollider.sharedMesh);
	}

	private void OnConfigImportComplete()
	{
		m_lineRenderer.material.renderQueue = VectorLayer.s_HighestBaseRenderQueueValue + 20;
		if (m_fillMaterialInstance != null)
		{
			m_fillMaterialInstance.renderQueue = m_lineRenderer.material.renderQueue - 1;
		}
	}

	#region Drawing

	public void AddLocalPoint(Vector3 a_position)
	{
		PenLinePoint point = new PenLinePoint(PenLinePoint.NewId(), a_position);
		m_points.Add(point);
		OnPointsChanged();
		LinePointAddedEvent?.Invoke(point);
	}

	public void AddGlobalPoint(Vector3 a_position)
	{
		AddLocalPoint(GlobalToLocal(a_position));
	}

	public void UpdateLastLocalPoint(Vector3 a_position)
	{
		if (m_points.Count == 0)
		{
			AddLocalPoint(a_position);
			return;
		}

		PenLinePoint point = m_points[m_points.Count - 1];
		point.Position = a_position;
		m_points[m_points.Count - 1] = point;
		OnPointsChanged();
		LastPointUpdatedEvent?.Invoke(point);
	}

	/// <summary>
	/// Replaces all points (including the origin point at index 0).
	/// Points whose position matches an existing point (in order) keep that point's id.
	/// </summary>
	public void ReplaceLocalPoints(IReadOnlyList<Vector3> a_points)
	{
		List<PenLinePoint> newPoints = new List<PenLinePoint>(a_points.Count);
		int searchIndex = 0;
		for (int i = 0; i < a_points.Count; i++)
		{
			ulong id = 0;
			bool found = false;
			for (int j = searchIndex; j < m_points.Count; j++)
			{
				if (m_points[j].Position == a_points[i])
				{
					id = m_points[j].Id;
					searchIndex = j + 1;
					found = true;
					break;
				}
			}
			newPoints.Add(new PenLinePoint(found ? id : PenLinePoint.NewId(), a_points[i]));
		}

		m_points.Clear();
		m_points.AddRange(newPoints);
		OnPointsChanged();
		LinePointsReplacedEvent?.Invoke();
	}

	public void GetLocalPoints(List<Vector3> a_result)
	{
		a_result.Clear();
		for (int i = 0; i < m_points.Count; i++)
		{
			a_result.Add(m_points[i].Position);
		}
	}

	public Vector3 GetLastLocalPoint()
	{
		return m_points.Count > 0 ? m_points[m_points.Count - 1].Position : Vector3.zero;
	}

	#endregion

	#region Point model

	public int IndexOf(ulong a_pointId)
	{
		for (int i = 0; i < m_points.Count; i++)
		{
			if (m_points[i].Id == a_pointId)
				return i;
		}
		return -1;
	}

	public bool TryGetPoint(ulong a_pointId, out PenLinePoint a_point)
	{
		int index = IndexOf(a_pointId);
		a_point = index >= 0 ? m_points[index] : default;
		return index >= 0;
	}

	public bool IsEndpoint(ulong a_pointId)
	{
		int index = IndexOf(a_pointId);
		return index >= 0 && (index == 0 || index == m_points.Count - 1);
	}

	/// <summary>
	/// True if this client has an in-progress or unconfirmed edit on the point.
	/// </summary>
	public bool IsLocallyEdited(ulong a_pointId)
	{
		return m_localEdits.ContainsKey(a_pointId);
	}

	public bool HasLocalHolds()
	{
		foreach (LocalEdit edit in m_localEdits.Values)
		{
			if (edit.State == LocalEditState.Holding)
				return true;
		}
		return false;
	}

	/// <summary>
	/// Replaces the point model with authoritative (synced) data, then re-applies this client's pending local edits on top.
	/// </summary>
	public void SetPoints(IEnumerable<PenLinePoint> a_points)
	{
		m_points.Clear();
		m_points.AddRange(a_points);
		ApplyLocalEdits();
		OnPointsChanged();
	}

	private void ApplyLocalEdits()
	{
		if (m_localEdits.Count == 0)
			return;

		ulong localClientId = PenLinePoint.LocalClientId;
		m_localEditKeyBuffer.Clear();
		m_localEditKeyBuffer.AddRange(m_localEdits.Keys);

		foreach (ulong id in m_localEditKeyBuffer)
		{
			LocalEdit edit = m_localEdits[id];
			int index = IndexOf(id);
			if (index >= 0)
			{
				edit.PendingInsert = false;
			}

			bool timedOut = edit.State != LocalEditState.Holding && Time.time - edit.Time > LOCAL_EDIT_TIMEOUT;

			switch (edit.State)
			{
				case LocalEditState.Holding:
				case LocalEditState.PendingCommit:
					if (index < 0)
					{
						int afterIndex = edit.PendingInsert ? IndexOf(edit.AfterId) : -1;
						if (afterIndex < 0 || timedOut)
						{
							//Point (or the point it was inserted after) was removed by someone else
							m_localEdits.Remove(id);
							continue;
						}
						index = afterIndex + 1;
						m_points.Insert(index, new PenLinePoint(id, edit.Position, edit.State == LocalEditState.Holding ? localClientId : PenLinePoint.NOT_HELD));
					}

					if (edit.State == LocalEditState.PendingCommit)
					{
						PenLinePoint synced = m_points[index];
						bool confirmed = !edit.PendingInsert && synced.HeldBy != localClientId && (synced.Position - edit.Position).sqrMagnitude < 1e-10f;
						if (confirmed || timedOut)
						{
							m_localEdits.Remove(id);
							continue;
						}
					}

					PenLinePoint point = m_points[index];
					point.Position = edit.Position;
					if (edit.State == LocalEditState.Holding)
						point.HeldBy = localClientId;
					m_points[index] = point;
					break;

				case LocalEditState.PendingRemove:
					if (index < 0 || timedOut)
					{
						m_localEdits.Remove(id);
						continue;
					}
					m_points.RemoveAt(index);
					break;
			}
		}
	}

	#endregion

	#region Editing

	/// <summary>
	/// Starts dragging an existing point.
	/// </summary>
	public bool BeginPointDrag(ulong a_pointId)
	{
		int index = IndexOf(a_pointId);
		if (index < 0)
			return false;

		PenLinePoint point = m_points[index];
		point.HeldBy = PenLinePoint.LocalClientId;
		m_points[index] = point;
		m_localEdits[a_pointId] = new LocalEdit { State = LocalEditState.Holding, Position = point.Position };

		EditHandler?.RequestHold(a_pointId);
		OnPointsChanged();
		return true;
	}

	/// <summary>
	/// Inserts a new point after a_afterPointId and starts dragging it.
	/// </summary>
	public bool BeginInsertDrag(ulong a_afterPointId, Vector3 a_localPosition, out ulong a_newPointId)
	{
		a_newPointId = 0;
		int afterIndex = IndexOf(a_afterPointId);
		if (afterIndex < 0)
			return false;

		a_newPointId = PenLinePoint.NewId();
		m_points.Insert(afterIndex + 1, new PenLinePoint(a_newPointId, a_localPosition, PenLinePoint.LocalClientId));
		m_localEdits[a_newPointId] = new LocalEdit
		{
			State = LocalEditState.Holding,
			Position = a_localPosition,
			AfterId = a_afterPointId,
			PendingInsert = EditHandler != null
		};

		EditHandler?.RequestInsert(a_afterPointId, a_newPointId, a_localPosition);
		OnPointsChanged();
		return true;
	}

	/// <summary>
	/// Updates the position of a point that is being dragged by this client.
	/// </summary>
	public void DragPoint(ulong a_pointId, Vector3 a_localPosition, bool a_sendUpdate)
	{
		if (!m_localEdits.TryGetValue(a_pointId, out LocalEdit edit) || edit.State != LocalEditState.Holding)
			return;

		int index = IndexOf(a_pointId);
		if (index < 0)
			return;

		edit.Position = a_localPosition;
		PenLinePoint point = m_points[index];
		point.Position = a_localPosition;
		m_points[index] = point;
		OnPointsChanged();

		if (a_sendUpdate)
		{
			EditHandler?.RequestMove(a_pointId, a_localPosition);
		}
	}

	/// <summary>
	/// Ends a drag: either commits the point at a_localPosition or removes it.
	/// Re-evaluates the closed state when an endpoint was edited.
	/// </summary>
	public void EndPointDrag(ulong a_pointId, Vector3 a_localPosition, bool a_remove)
	{
		int index = IndexOf(a_pointId);
		if (index < 0 || !m_localEdits.TryGetValue(a_pointId, out LocalEdit edit))
		{
			m_localEdits.Remove(a_pointId);
			return;
		}

		bool wasEndpoint = index == 0 || index == m_points.Count - 1;

		if (a_remove)
		{
			m_points.RemoveAt(index);
		}
		else
		{
			PenLinePoint point = m_points[index];
			point.Position = a_localPosition;
			point.HeldBy = PenLinePoint.NOT_HELD;
			m_points[index] = point;
		}

		bool closedAfter = wasEndpoint ? EvaluateClosed() : (m_isClosed && m_points.Count >= 3);

		if (EditHandler == null)
		{
			m_localEdits.Remove(a_pointId);
			if (a_remove && m_points.Count <= 1)
			{
				Destroy(gameObject);
				return;
			}
		}
		else
		{
			edit.State = a_remove ? LocalEditState.PendingRemove : LocalEditState.PendingCommit;
			edit.Position = a_localPosition;
			edit.Time = Time.time;

			if (a_remove)
				EditHandler.RequestRemove(a_pointId, closedAfter);
			else
				EditHandler.RequestCommit(a_pointId, a_localPosition, closedAfter);
		}

		//Applied locally right away, the server confirms it
		SetClosedWithoutNotify(closedAfter);
		OnPointsChanged();
		RebuildCollider();
	}

	/// <summary>
	/// True if the line has at least 3 points and its first and last point are within the close distance on the drawing plane.
	/// </summary>
	public bool EvaluateClosed()
	{
		return EvaluateClosed(m_points);
	}

	public bool EvaluateClosed(IReadOnlyList<PenLinePoint> a_points)
	{
		if (a_points.Count < 3)
			return false;

		Vector3 first = a_points[0].Position;
		Vector3 last = a_points[a_points.Count - 1].Position;
		float closeDistance = GlobalToLocalDistance(m_closeDistance);
		return new Vector2(last.x - first.x, last.z - first.z).sqrMagnitude <= closeDistance * closeDistance;
	}

	public void SetClosedWithoutNotify(bool a_closed)
	{
		if (m_isClosed == a_closed)
			return;

		m_isClosed = a_closed;
		m_lineRenderer.loop = a_closed;
		UpdateFill();
		ClosedChangedEvent?.Invoke(a_closed);
	}

	#endregion

	#region Rendering

	private void OnPointsChanged()
	{
		ApplyPointsToRenderer();
		if (m_isClosed)
		{
			UpdateFill();
		}
		PointsChangedEvent?.Invoke();
	}

	private void ApplyPointsToRenderer()
	{
		int count = m_points.Count;
		if (m_positionBuffer.Length != count)
		{
			m_positionBuffer = new Vector3[count];
		}
		for (int i = 0; i < count; i++)
		{
			m_positionBuffer[i] = m_points[i].Position;
		}
		m_lineRenderer.positionCount = count;
		m_lineRenderer.SetPositions(m_positionBuffer);
	}

	private void UpdateFill()
	{
#if !UNITY_SERVER
		if (!m_isClosed)
		{
			if (m_fillObject != null)
				m_fillObject.SetActive(false);
			return;
		}

		EnsureFillObject();

		Color fillColor = m_lineColor;
		fillColor.a *= m_fillAlpha;
		float height = -GlobalToLocalDistance(m_fillDepthOffset);
		bool valid = PenLineFillBuilder.Build(m_points, height, GlobalToLocalDistance(0.001f), fillColor, m_fillMesh);
		m_fillObject.SetActive(valid);
#endif
	}

	private void EnsureFillObject()
	{
		if (m_fillObject != null)
			return;

		m_fillObject = new GameObject("Fill");
		m_fillObject.layer = 0;
		m_fillObject.transform.SetParent(transform, false);

		m_fillMesh = new Mesh { name = "PenLineFill" };
		m_fillMesh.MarkDynamic();
		m_fillObject.AddComponent<MeshFilter>().sharedMesh = m_fillMesh;

		MeshRenderer meshRenderer = m_fillObject.AddComponent<MeshRenderer>();
		m_fillMaterialInstance = new Material(m_fillMaterial != null ? m_fillMaterial : m_lineRenderer.sharedMaterial);
		m_fillMaterialInstance.renderQueue = m_lineRenderer.sharedMaterial.renderQueue - 1;
		meshRenderer.sharedMaterial = m_fillMaterialInstance;
		meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
		meshRenderer.receiveShadows = false;
		meshRenderer.lightProbeUsage = LightProbeUsage.Off;
		meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
	}

	#endregion

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

		if (m_isClosed)
		{
			UpdateFill();
		}
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
		m_isFinished = true;
		RebuildCollider();
	}

	public void RebuildCollider()
	{
#if UNITY_SERVER
		//Line colliders are only used by the (client side) eraser
		return;
#else
		if (m_points.Count < 2)
			return;

		Mesh previousMesh = m_meshCollider.sharedMesh;
		Mesh mesh = new Mesh();
		m_lineRenderer.BakeMesh(mesh, Camera.main);

		m_meshCollider.sharedMesh = mesh;
		if (previousMesh != null)
		{
			Destroy(previousMesh);
		}
#endif
	}

	public void EraseLine()
	{
		if (LineErasedEvent == null || LineErasedEvent.GetInvocationList().Length == 0)
		{
			Destroy(gameObject);
		}
		else
		{
			LineErasedEvent.Invoke();
		}
	}
}
