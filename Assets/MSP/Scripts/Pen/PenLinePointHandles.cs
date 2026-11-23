#if !UNITY_SERVER
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.AffordanceSystem.Receiver.Rendering;
using UnityEngine.XR.Interaction.Toolkit.AffordanceSystem.Theme.Primitives;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Shows grabbable handles on the points of a finished PenLine.
/// - While a (free) hand is near the line, all points get a handle.
/// - While a held pen is near the start/end of an open line, only those endpoints get a handle, so the user can see where a stroke
///   would continue the line. The endpoint the pen would continue from is drawn larger.
/// - When the same user drags the start and end point of an open line (or any two points of a closed line) with two hands,
///   the whole line is moved (translated and rotated around the vertical axis) instead of stretching it.
/// Added at runtime by PenLine if not present on the prefab.
/// </summary>
[RequireComponent(typeof(PenLine))]
public class PenLinePointHandles : MonoBehaviour
{
	private enum VisibilityMode
	{
		Hidden,
		PenEndpoints,
		All
	}

	[SerializeField] private float m_showDistance = 0.06f; //World-space metres. Handles appear when a free hand is closer than this to the line.
	[SerializeField] private float m_hideDistance = 0.09f; //World-space metres. Handles disappear when no free hand is closer than this to the line.
	[SerializeField] private float m_penShowDistance = 0.06f; //World-space metres. Endpoint handles appear when a held pen tip is closer than this to an endpoint (on the drawing plane).
	[SerializeField] private float m_penShowHeight = 0.15f; //World-space metres. Max height of a held pen tip above/below the drawing plane for endpoint handles to appear.
	[SerializeField] private float m_handleSize = 0.015f; //World-space metres. Diameter of a handle sphere.
	[SerializeField] private float m_penTargetScale = 1.4f; //Scale multiplier of the endpoint a held pen would continue from.
	[SerializeField] private float m_sendInterval = 0.05f; //Seconds between network position updates while dragging.
	// [SerializeField] private Color m_handleColor = new Color(1f, 1f, 1f, 0.85f);
	// [SerializeField] private Color m_endpointColor = new Color(0.3f, 1f, 0.45f, 0.9f); //Colour of the start and end point handles of open lines.
	// [SerializeField] private Color m_removeColor = new Color(1f, 0.15f, 0.15f, 0.9f);
	// [SerializeField] private Material m_handleMaterial; //Optional. Material for the handles, must use vertex colours. Falls back to a copy of the line material.
	[SerializeField] private ColorAffordanceThemeDatum m_normalColorAffordance; //TODO: Replace affordance change with basecolour change
	[SerializeField] private ColorAffordanceThemeDatum m_deletedColorAffordance; 
	[SerializeField] private ColorAffordanceThemeDatum m_endpointColorAffordance; 
	[SerializeField] private GameObject m_penLinePointPrefab; 

	public float SendInterval => m_sendInterval;
	public PenLine Line => m_line;

	private PenLine m_line;
	private VisibilityMode m_mode = VisibilityMode.Hidden;
	private bool m_pointsDirty = true;
	private ulong m_penTargetPointId;
	private bool m_hasPenTarget;
	private readonly Dictionary<ulong, PenLinePointHandle> m_handles = new Dictionary<ulong, PenLinePointHandle>();
	private readonly Stack<PenLinePointHandle> m_pool = new Stack<PenLinePointHandle>();
	private readonly HashSet<ulong> m_seenIds = new HashSet<ulong>();
	private readonly List<ulong> m_removeBuffer = new List<ulong>();
	private readonly List<PenLinePointHandle> m_tickBuffer = new List<PenLinePointHandle>();
	private Transform m_handleRoot;

	// Whole-line move state
	private bool m_isMoving;
	private PenLinePointHandle m_moveHandleA; //Start
	private PenLinePointHandle m_moveHandleB; //End
	private Vector3 m_moveStartLocalPosition;
	private Quaternion m_moveStartLocalRotation;
	private Vector3 m_moveStartA; //parent space, at the start pose
	private Vector3 m_moveStartB;

	// Shared across all lines
	private static readonly List<NearFarInteractor> ms_hands = new List<NearFarInteractor>();
	private static float ms_nextHandRefreshTime = 0f;
	private const float HAND_REFRESH_INTERVAL = 1f;
	private const float PEN_HIDE_FACTOR = 1.5f;
	// private static Mesh ms_normalMesh;
	// private static Mesh ms_endpointMesh;
	// private static Mesh ms_removeMesh;
	// private static Material ms_sharedMaterial;

	private void Awake()
	{
		m_line = GetComponent<PenLine>();
		m_line.PointsChangedEvent += OnPointsChanged;
		m_line.ClosedChangedEvent += OnClosedChanged;

		m_handleRoot = new GameObject("PointHandles").transform;
		m_handleRoot.SetParent(transform, false);
	}

	private void OnDestroy()
	{
		if (m_line != null)
		{
			m_line.PointsChangedEvent -= OnPointsChanged;
			m_line.ClosedChangedEvent -= OnClosedChanged;
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

	private void OnClosedChanged(bool a_closed)
	{
		//Endpoint colours only apply to open lines
		m_pointsDirty = true;
	}

	private void LateUpdate()
	{
		VisibilityMode mode = EvaluateVisibility();

		if (mode != m_mode)
		{
			m_mode = mode;
			m_pointsDirty = true;
			if (m_mode == VisibilityMode.Hidden)
			{
				ReleaseAllHandles();
			}
		}

		if (m_mode == VisibilityMode.Hidden)
		{
			EndLineMove();
			return;
		}

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

		UpdateLineMove();

		foreach (PenLinePointHandle handle in m_handles.Values)
		{
			if (handle == null)
				continue;

			if (!handle.IsDraggedLocally && m_line.TryGetPoint(handle.PointId, out PenLinePoint point))
			{
				handle.transform.position = transform.TransformPoint(point.Position);
			}

			bool isPenTarget = m_mode == VisibilityMode.PenEndpoints && m_hasPenTarget && handle.PointId == m_penTargetPointId;
			SetWorldScale(handle.transform, isPenTarget ? m_handleSize * m_penTargetScale : m_handleSize);
		}
	}

	private VisibilityMode EvaluateVisibility()
	{
		m_hasPenTarget = false;
		if (!m_line.IsFinished)
			return VisibilityMode.Hidden;

		if (AnyHandleSelected() || IsFreeHandNear(m_mode == VisibilityMode.All ? m_hideDistance : m_showDistance))
			return VisibilityMode.All;

		float penDistance = m_mode == VisibilityMode.PenEndpoints ? m_penShowDistance * PEN_HIDE_FACTOR : m_penShowDistance;
		if (IsPenNearEndpoint(penDistance))
			return VisibilityMode.PenEndpoints;

		return VisibilityMode.Hidden;
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

	/// <summary>
	/// True if a held pen tip is near the start or end of this line (only for lines that can be continued).
	/// Also determines the endpoint the pen would continue from.
	/// </summary>
	private bool IsPenNearEndpoint(float a_distance)
	{
		IReadOnlyList<Pen> pens = Pen.HeldPens;
		if (pens.Count == 0 || !m_line.CanContinue)
			return false;

		IReadOnlyList<PenLinePoint> points = m_line.Points;
		float scale = Mathf.Abs(transform.lossyScale.x);
		bool near = false;
		float bestTargetDistance = float.MaxValue;

		foreach (Pen pen in pens)
		{
			if (pen == null || !pen.isActiveAndEnabled)
				continue;

			Vector3 localTip = transform.InverseTransformPoint(pen.TipPosition);
			if (Mathf.Abs(localTip.y) * scale > m_penShowHeight)
				continue;

			for (int e = 0; e < 2; e++)
			{
				int index = e == 0 ? points.Count - 1 : 0;
				if (e == 1 && points.Count < 2)
					break;

				Vector3 endpoint = points[index].Position;
				float distance = new Vector2(localTip.x - endpoint.x, localTip.z - endpoint.z).magnitude * scale;
				if (distance > a_distance)
					continue;

				near = true;
				if (distance <= pen.ContinueDistance && distance < bestTargetDistance)
				{
					bestTargetDistance = distance;
					m_penTargetPointId = points[index].Id;
					m_hasPenTarget = true;
				}
			}
		}
		return near;
	}

	private bool IsFreeHandNear(float a_distance)
	{
		RefreshHands();

		IReadOnlyList<PenLinePoint> points = m_line.Points;
		if (points.Count == 0)
			return false;

		//The line renderer is disabled for dots, so its bounds can't be used
		Bounds bounds = m_line.GetWorldBounds();
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
		//KH: Could be simplified to just check distance to points, rather than line segments
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

		bool endpointsOnly = m_mode == VisibilityMode.PenEndpoints;
		for (int i = 0; i < points.Count; i++)
		{
			if (endpointsOnly && i != 0 && i != points.Count - 1)
				continue;

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

		//Inserts and removals change which points are endpoints
		foreach (PenLinePointHandle handle in m_handles.Values)
		{
			if (handle != null)
				RefreshHandleVisual(handle);
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

		if (handle == m_moveHandleA || handle == m_moveHandleB)
		{
			EndLineMove();
		}

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

	#region Whole-line move

	/// <summary>
	/// True if the point is one of the two points currently used to move the whole line.
	/// Such points keep their position on the line instead of being dragged.
	/// </summary>
	public bool IsMovePoint(ulong a_pointId)
	{
		return m_isMoving
			&& ((m_moveHandleA != null && m_moveHandleA.PointId == a_pointId) || (m_moveHandleB != null && m_moveHandleB.PointId == a_pointId));
	}

	private void UpdateLineMove()
	{
		if (!TryGetMoveHandles(out PenLinePointHandle handleA, out PenLinePointHandle handleB))
		{
			EndLineMove();
			return;
		}

		if (m_isMoving && (handleA != m_moveHandleA || handleB != m_moveHandleB))
		{
			EndLineMove();
		}

		if (!m_isMoving)
		{
			BeginLineMove(handleA, handleB);
			if (!m_isMoving)
				return;
		}

		if (!m_line.HasTransformAuthority)
		{
			//Keep asking until the server hands over the line (it checks that we hold both points)
			m_line.RequestTransformAuthority(handleA.PointId, handleB.PointId);
			return;
		}

		ApplyLineMove();
	}

	/// <summary>
	/// Open lines move when their start and end point are dragged locally, closed lines when any two points are.
	/// </summary>
	private bool TryGetMoveHandles(out PenLinePointHandle a_handleA, out PenLinePointHandle a_handleB)
	{
		a_handleA = null;
		a_handleB = null;

		IReadOnlyList<PenLinePoint> points = m_line.Points;
		if (points.Count < 2)
			return false;

		// if (!m_line.IsClosed)
		// {
			if (m_handles.TryGetValue(points[0].Id, out a_handleA) && m_handles.TryGetValue(points[points.Count - 1].Id, out a_handleB)
				&& a_handleA != null && a_handleB != null && a_handleA.IsDraggedLocally && a_handleB.IsDraggedLocally)
			{
				return true;
			}
			a_handleA = null;
			a_handleB = null;
			return false;
		// }

		// //Closed: keep the current pair while it is still held, otherwise take the first two dragged handles
		// if (m_isMoving && m_moveHandleA != null && m_moveHandleB != null && m_moveHandleA.IsDraggedLocally && m_moveHandleB.IsDraggedLocally)
		// {
		// 	a_handleA = m_moveHandleA;
		// 	a_handleB = m_moveHandleB;
		// 	return true;
		// }

		// foreach (PenLinePointHandle handle in m_handles.Values)
		// {
		// 	if (handle == null || !handle.IsDraggedLocally)
		// 		continue;

		// 	if (a_handleA == null)
		// 	{
		// 		a_handleA = handle;
		// 	}
		// 	else
		// 	{
		// 		a_handleB = handle;
		// 		return true;
		// 	}
		// }
		// a_handleA = null;
		// return false;
	}

	private void BeginLineMove(PenLinePointHandle a_handleA, PenLinePointHandle a_handleB)
	{
		if (!m_line.TryGetPoint(a_handleA.PointId, out PenLinePoint pointA) || !m_line.TryGetPoint(a_handleB.PointId, out PenLinePoint pointB))
			return;

		m_isMoving = true;
		m_moveHandleA = a_handleA;
		m_moveHandleB = a_handleB;
		m_moveStartLocalPosition = transform.localPosition;
		m_moveStartLocalRotation = transform.localRotation;
		m_moveStartA = LineLocalToParent(pointA.Position);
		m_moveStartB = LineLocalToParent(pointB.Position);
	}

	private void ApplyLineMove()
	{
		Vector3 handA = WorldToParent(m_moveHandleA.transform.position);
		Vector3 handB = WorldToParent(m_moveHandleB.transform.position);

		Vector3 startDirection = m_moveStartB - m_moveStartA;
		Vector3 handDirection = handB - handA;
		startDirection.y = 0f;
		handDirection.y = 0f;

		float yaw = 0f;
		if (startDirection.sqrMagnitude > 1e-8f && handDirection.sqrMagnitude > 1e-8f)
		{
			yaw = Vector3.SignedAngle(startDirection, handDirection, Vector3.up);
		}
		Quaternion rotation = Quaternion.AngleAxis(yaw, Vector3.up);

		Vector3 startMid = (m_moveStartA + m_moveStartB) * 0.5f;
		Vector3 handMid = (handA + handB) * 0.5f;

		Vector3 position = handMid + rotation * (m_moveStartLocalPosition - startMid);
		position.y = m_moveStartLocalPosition.y;

		transform.localPosition = position;
		transform.localRotation = rotation * m_moveStartLocalRotation;
	}

	/// <summary>
	/// Ends a whole-line move. Called when one of the two hands lets go.
	/// </summary>
	public void EndLineMove()
	{
		if (!m_isMoving)
			return;

		//Points, fill and collider are in line-local space, so they moved along with the transform
		m_isMoving = false;
		m_moveHandleA = null;
		m_moveHandleB = null;
	}

	private Vector3 LineLocalToParent(Vector3 a_linePosition)
	{
		Vector3 world = transform.TransformPoint(a_linePosition);
		return WorldToParent(world);
	}

	private Vector3 WorldToParent(Vector3 a_world)
	{
		Transform parent = transform.parent;
		return parent != null ? parent.InverseTransformPoint(a_world) : a_world;
	}

	#endregion

	#region Visuals

	private PenLinePointHandle CreateHandle()
	{
		// EnsureSharedAssets();

		//TODO: replace with prefab spawn
		GameObject handleObject = Instantiate(m_penLinePointPrefab,m_handleRoot);
		handleObject.SetActive(false);
		// handleObject.layer = m_handleLayer;
		// handleObject.transform.SetParent(m_handleRoot, false);

		// handleObject.AddComponent<MeshFilter>().sharedMesh = ms_normalMesh;
		// MeshRenderer meshRenderer = handleObject.AddComponent<MeshRenderer>();
		// meshRenderer.sharedMaterial = m_handleMaterial != null ? m_handleMaterial : ms_sharedMaterial;
		// meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		// meshRenderer.receiveShadows = false;

		// SphereCollider sphereCollider = handleObject.AddComponent<SphereCollider>();
		// sphereCollider.radius = 0.5f;
		// //Not a trigger: the hands' near caster ignores triggers (QueryTriggerInteraction.Ignore)
		// sphereCollider.isTrigger = false;

		// Rigidbody body = handleObject.AddComponent<Rigidbody>();
		// body.isKinematic = true;
		// body.useGravity = false;

		//Added last so XRI's Awake (on activation) picks up the collider
		// PenLinePointHandle handle = handleObject.AddComponent<PenLinePointHandle>();
		PenLinePointHandle handle = handleObject.GetComponent<PenLinePointHandle>();
		handle.Configure();
		return handle;
	}

	// private void EnsureSharedAssets()
	// {
	// 	if (ms_normalMesh == null)
	// 	{
	// 		GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
	// 		Mesh sphere = primitive.GetComponent<MeshFilter>().sharedMesh;
	// 		Destroy(primitive);

	// 		ms_normalMesh = CreateColoredMesh(sphere, m_handleColor, "PenLineHandle");
	// 		ms_endpointMesh = CreateColoredMesh(sphere, m_endpointColor, "PenLineHandleEndpoint");
	// 		ms_removeMesh = CreateColoredMesh(sphere, m_removeColor, "PenLineHandleRemove");
	// 	}

	// 	if (ms_sharedMaterial == null)
	// 	{
	// 		ms_sharedMaterial = new Material(m_line.LineRenderer.sharedMaterial);
	// 		ms_sharedMaterial.renderQueue = m_line.LineRenderer.sharedMaterial.renderQueue + 1;
	// 	}
	// }

	// private static Mesh CreateColoredMesh(Mesh a_source, Color a_color, string a_name)
	// {
	// 	Mesh mesh = Instantiate(a_source);
	// 	mesh.name = a_name;
	// 	Color[] colors = new Color[mesh.vertexCount];
	// 	for (int i = 0; i < colors.Length; i++)
	// 	{
	// 		colors[i] = a_color;
	// 	}
	// 	mesh.colors = colors;
	// 	return mesh;
	// }

	/// <summary>
	/// Picks the handle mesh: remove (red) > endpoint > normal.
	/// </summary>
	public void RefreshHandleVisual(PenLinePointHandle a_handle)
	{
		ColorMaterialPropertyAffordanceReceiver affordance = a_handle.GetComponentInChildren<ColorMaterialPropertyAffordanceReceiver>();
		if (a_handle.IsInRemoveRange)
			affordance.affordanceThemeDatum.Value = m_deletedColorAffordance.Value;
		else if (m_line.IsEndpoint(a_handle.PointId))
			affordance.affordanceThemeDatum.Value = m_endpointColorAffordance.Value;
		else
			affordance.affordanceThemeDatum.Value = m_normalColorAffordance.Value;



		// Mesh mesh;
		// if (a_handle.IsInRemoveRange)
		// 	mesh = ms_removeMesh;
		// else if (!m_line.IsClosed && m_line.IsEndpoint(a_handle.PointId))
		// 	mesh = ms_endpointMesh;
		// else
		// 	mesh = ms_normalMesh;

		// a_handle.GetComponent<MeshFilter>().sharedMesh = mesh;
	}

	private static void SetWorldScale(Transform a_transform, float a_worldSize)
	{
		Transform parent = a_transform.parent;
		float parentScale = parent != null ? Mathf.Abs(parent.lossyScale.x) : 1f;
		a_transform.localScale = Vector3.one * (parentScale > 1e-6f ? a_worldSize / parentScale : a_worldSize);
	}

	#endregion
}
#endif
