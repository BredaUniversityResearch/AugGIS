#if !UNITY_SERVER
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Shows grabbable handles on the points of a finished PenLine while a (free) hand is near the line.
/// Added at runtime by PenLine if not present on the prefab.
/// </summary>
[RequireComponent(typeof(PenLine))]
public class PenLinePointHandles : MonoBehaviour
{
	[SerializeField]
	[Tooltip("World-space metres. Handles appear when a free hand is closer than this to the line.")]
	private float m_showDistance = 0.06f;

	[SerializeField]
	[Tooltip("World-space metres. Handles disappear when no free hand is closer than this to the line.")]
	private float m_hideDistance = 0.09f;

	[SerializeField]
	[Tooltip("World-space metres. Diameter of a handle sphere.")]
	private float m_handleSize = 0.015f;

	[SerializeField]
	[Tooltip("Seconds between network position updates while dragging.")]
	private float m_sendInterval = 0.05f;

	[SerializeField]
	private Color m_handleColor = new Color(1f, 1f, 1f, 0.85f);

	[SerializeField]
	private Color m_removeColor = new Color(1f, 0.15f, 0.15f, 0.9f);

	[SerializeField]
	[Tooltip("Optional. Material for the handles, must use vertex colours. Falls back to a copy of the line material.")]
	private Material m_handleMaterial;

	[SerializeField]
	[Tooltip("Physics layer of the handles. Must be in the hands' near caster physics mask (Default) and not in the pen's drawing mask.")]
	private int m_handleLayer = 0;

	public float SendInterval => m_sendInterval;
	public PenLine Line => m_line;

	private PenLine m_line;
	private bool m_visible;
	private bool m_pointsDirty = true;
	private readonly Dictionary<ulong, PenLinePointHandle> m_handles = new Dictionary<ulong, PenLinePointHandle>();
	private readonly Stack<PenLinePointHandle> m_pool = new Stack<PenLinePointHandle>();
	private readonly HashSet<ulong> m_seenIds = new HashSet<ulong>();
	private readonly List<ulong> m_removeBuffer = new List<ulong>();
	private readonly List<PenLinePointHandle> m_tickBuffer = new List<PenLinePointHandle>();
	private Transform m_handleRoot;

	// Shared across all lines
	private static readonly List<NearFarInteractor> ms_hands = new List<NearFarInteractor>();
	private static float ms_nextHandRefreshTime = 0f;
	private const float HAND_REFRESH_INTERVAL = 1f;
	private static Mesh ms_normalMesh;
	private static Mesh ms_removeMesh;
	private static Material ms_sharedMaterial;

	private void Awake()
	{
		m_line = GetComponent<PenLine>();
		m_line.PointsChangedEvent += OnPointsChanged;

		m_handleRoot = new GameObject("PointHandles").transform;
		m_handleRoot.SetParent(transform, false);
	}

	private void OnDestroy()
	{
		if (m_line != null)
		{
			m_line.PointsChangedEvent -= OnPointsChanged;
		}

		//Grabbed handles are unparented by XRI, so destroy them explicitly
		foreach (PenLinePointHandle handle in m_handles.Values)
		{
			if (handle != null)
				Destroy(handle.gameObject);
		}
		m_handles.Clear();
		foreach (PenLinePointHandle handle in m_pool)
		{
			if (handle != null)
				Destroy(handle.gameObject);
		}
		m_pool.Clear();
	}

	private void OnPointsChanged()
	{
		m_pointsDirty = true;
	}

	private void LateUpdate()
	{
		bool shouldBeVisible = m_line.IsFinished && (AnyHandleSelected() || IsFreeHandNear(m_visible ? m_hideDistance : m_showDistance));

		if (shouldBeVisible != m_visible)
		{
			m_visible = shouldBeVisible;
			m_pointsDirty = true;
			if (!m_visible)
			{
				ReleaseAllHandles();
			}
		}

		if (!m_visible)
			return;

		if (m_pointsDirty)
		{
			m_pointsDirty = false;
			SyncHandles();
		}

		//Ticking may create handles (inserts) or change points, so iterate a copy
		m_tickBuffer.Clear();
		m_tickBuffer.AddRange(m_handles.Values);
		foreach (PenLinePointHandle handle in m_tickBuffer)
		{
			if (handle != null)
				handle.Tick();
		}

		float scale = m_handleSize;
		foreach (PenLinePointHandle handle in m_handles.Values)
		{
			if (handle == null)
				continue;

			if (!handle.IsDraggedLocally && m_line.TryGetPoint(handle.PointId, out PenLinePoint point))
			{
				handle.transform.position = transform.TransformPoint(point.Position);
			}
			SetWorldScale(handle.transform, scale);
		}
	}

	private bool AnyHandleSelected()
	{
		foreach (PenLinePointHandle handle in m_handles.Values)
		{
			if (handle != null && (handle.isSelected || handle.IsDraggedLocally))
				return true;
		}
		return false;
	}

	private bool IsFreeHandNear(float a_distance)
	{
		RefreshHands();

		IReadOnlyList<PenLinePoint> points = m_line.Points;
		if (points.Count == 0)
			return false;

		Bounds bounds = m_line.LineRenderer.bounds;
		bounds.Expand(a_distance * 2f);
		float localDistance = m_line.GlobalToLocalDistance(a_distance);

		foreach (NearFarInteractor hand in ms_hands)
		{
			if (hand == null || !hand.isActiveAndEnabled)
				continue;

			//A hand that holds something (e.g. the pen) does not reveal handles, unless it holds one of ours
			if (hand.hasSelection && !IsSelectingOurHandle(hand))
				continue;

			Vector3 handPosition = (hand.attachTransform != null ? hand.attachTransform : hand.transform).position;
			if (!bounds.Contains(handPosition))
				continue;

			Vector3 localHand = transform.InverseTransformPoint(handPosition);
			if (DistanceToPolyline(localHand, points, m_line.IsClosed) <= localDistance)
				return true;
		}
		return false;
	}

	private bool IsSelectingOurHandle(NearFarInteractor a_hand)
	{
		foreach (var selected in a_hand.interactablesSelected)
		{
			if (selected is PenLinePointHandle handle && handle.Owner == this)
				return true;
		}
		return false;
	}

	private static float DistanceToPolyline(Vector3 a_point, IReadOnlyList<PenLinePoint> a_points, bool a_closed)
	{
		if (a_points.Count == 1)
			return Vector3.Distance(a_point, a_points[0].Position);

		float min = float.MaxValue;
		for (int i = 1; i < a_points.Count; i++)
		{
			min = Mathf.Min(min, LineSimplification.PerpendicularDistance(a_point, a_points[i - 1].Position, a_points[i].Position));
		}
		if (a_closed)
		{
			min = Mathf.Min(min, LineSimplification.PerpendicularDistance(a_point, a_points[a_points.Count - 1].Position, a_points[0].Position));
		}
		return min;
	}

	private static void RefreshHands()
	{
		if (Time.unscaledTime < ms_nextHandRefreshTime)
			return;

		ms_nextHandRefreshTime = Time.unscaledTime + HAND_REFRESH_INTERVAL;
		ms_hands.Clear();
		ms_hands.AddRange(FindObjectsByType<NearFarInteractor>());
	}

	private void SyncHandles()
	{
		IReadOnlyList<PenLinePoint> points = m_line.Points;
		m_seenIds.Clear();

		for (int i = 0; i < points.Count; i++)
		{
			ulong id = points[i].Id;
			m_seenIds.Add(id);
			if (!m_handles.ContainsKey(id))
			{
				GetHandle(id, transform.TransformPoint(points[i].Position));
			}
		}

		m_removeBuffer.Clear();
		foreach (KeyValuePair<ulong, PenLinePointHandle> pair in m_handles)
		{
			if (!m_seenIds.Contains(pair.Key))
				m_removeBuffer.Add(pair.Key);
		}
		foreach (ulong id in m_removeBuffer)
		{
			ReleaseHandle(id);
		}
	}

	/// <summary>
	/// Returns the handle for a point, creating (or reusing a pooled) one if needed.
	/// </summary>
	public PenLinePointHandle GetHandle(ulong a_pointId, Vector3 a_worldPosition)
	{
		if (m_handles.TryGetValue(a_pointId, out PenLinePointHandle existing) && existing != null)
			return existing;

		PenLinePointHandle handle = m_pool.Count > 0 ? m_pool.Pop() : CreateHandle();
		handle.transform.SetParent(m_handleRoot, false);
		handle.transform.position = a_worldPosition;
		SetWorldScale(handle.transform, m_handleSize);
		handle.Initialise(this, a_pointId);
		handle.gameObject.SetActive(true);
		m_handles[a_pointId] = handle;
		return handle;
	}

	private void ReleaseHandle(ulong a_pointId)
	{
		if (!m_handles.TryGetValue(a_pointId, out PenLinePointHandle handle))
			return;

		m_handles.Remove(a_pointId);
		if (handle == null)
			return;

		if (handle.isSelected)
		{
			//Point was removed (possibly by someone else) while held: force the hands to let go
			handle.ForceRelease();
		}
		handle.gameObject.SetActive(false);
		handle.transform.SetParent(m_handleRoot, false);
		m_pool.Push(handle);
	}

	private void ReleaseAllHandles()
	{
		m_removeBuffer.Clear();
		m_removeBuffer.AddRange(m_handles.Keys);
		foreach (ulong id in m_removeBuffer)
		{
			ReleaseHandle(id);
		}
	}

	private PenLinePointHandle CreateHandle()
	{
		EnsureSharedAssets();

		GameObject handleObject = new GameObject("PointHandle");
		handleObject.SetActive(false);
		handleObject.layer = m_handleLayer;
		handleObject.transform.SetParent(m_handleRoot, false);

		handleObject.AddComponent<MeshFilter>().sharedMesh = ms_normalMesh;
		MeshRenderer meshRenderer = handleObject.AddComponent<MeshRenderer>();
		meshRenderer.sharedMaterial = m_handleMaterial != null ? m_handleMaterial : ms_sharedMaterial;
		meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		meshRenderer.receiveShadows = false;

		SphereCollider sphereCollider = handleObject.AddComponent<SphereCollider>();
		sphereCollider.radius = 0.5f;
		//Not a trigger: the hands' near caster ignores triggers (QueryTriggerInteraction.Ignore)
		sphereCollider.isTrigger = false;

		Rigidbody body = handleObject.AddComponent<Rigidbody>();
		body.isKinematic = true;
		body.useGravity = false;

		//Added last so XRI's Awake (on activation) picks up the collider
		PenLinePointHandle handle = handleObject.AddComponent<PenLinePointHandle>();
		handle.Configure();
		return handle;
	}

	private void EnsureSharedAssets()
	{
		if (ms_normalMesh == null)
		{
			GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			Mesh sphere = primitive.GetComponent<MeshFilter>().sharedMesh;
			Destroy(primitive);

			ms_normalMesh = CreateColoredMesh(sphere, m_handleColor, "PenLineHandle");
			ms_removeMesh = CreateColoredMesh(sphere, m_removeColor, "PenLineHandleRemove");
		}

		if (ms_sharedMaterial == null)
		{
			ms_sharedMaterial = new Material(m_line.LineRenderer.sharedMaterial);
			ms_sharedMaterial.renderQueue = m_line.LineRenderer.sharedMaterial.renderQueue + 1;
		}
	}

	private static Mesh CreateColoredMesh(Mesh a_source, Color a_color, string a_name)
	{
		Mesh mesh = Instantiate(a_source);
		mesh.name = a_name;
		Color[] colors = new Color[mesh.vertexCount];
		for (int i = 0; i < colors.Length; i++)
		{
			colors[i] = a_color;
		}
		mesh.colors = colors;
		return mesh;
	}

	public void SetHandleRemoveState(PenLinePointHandle a_handle, bool a_remove)
	{
		a_handle.GetComponent<MeshFilter>().sharedMesh = a_remove ? ms_removeMesh : ms_normalMesh;
	}

	private static void SetWorldScale(Transform a_transform, float a_worldSize)
	{
		Transform parent = a_transform.parent;
		float parentScale = parent != null ? Mathf.Abs(parent.lossyScale.x) : 1f;
		a_transform.localScale = Vector3.one * (parentScale > 1e-6f ? a_worldSize / parentScale : a_worldSize);
	}
}
#endif
