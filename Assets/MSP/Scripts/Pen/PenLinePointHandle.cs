#if !UNITY_SERVER
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Grabbable sphere on a single PenLine point. Created and pooled by PenLinePointHandles.
/// - Grab: drag the point freely in 3D; on release it snaps back to the drawing plane (line-local y = 0).
/// - Dragged too far from the plane: turns red and the point is removed on release.
/// - Grabbing a point that is already held (by another hand, local or remote): inserts a new point after it
///   and hands the grab over to that new point.
/// </summary>
public class PenLinePointHandle : XRGrabInteractable
{
	private PenLinePointHandles m_owner;
	private ulong m_pointId;
	private bool m_isDraggedLocally;
	private bool m_isInRemoveRange;
	private float m_nextSendTime;
	private readonly List<IXRSelectInteractor> m_pendingInsertInteractors = new List<IXRSelectInteractor>();
	private readonly List<IXRSelectInteractor> m_interactorBuffer = new List<IXRSelectInteractor>();
	private IXRSelectInteractor m_dragInteractor;
	private MaterialInstanceColorAffordanceReceiver m_colorReceiver;
	private Color m_baseColor;
	private bool m_hasBaseColor;

	private static readonly int ms_baseColorId = Shader.PropertyToID("_BaseColor");

	public PenLinePointHandles Owner => m_owner;
	public ulong PointId => m_pointId;
	public bool IsDraggedLocally => m_isDraggedLocally;
	public bool IsInRemoveRange => m_isInRemoveRange;

	/// <summary>
	/// One-time XRI configuration, called right after the component is added.
	/// </summary>
	public void Configure()
	{
		movementType = MovementType.Instantaneous;
		trackRotation = false;
		trackScale = false;
		throwOnDetach = false;
		useDynamicAttach = false;
		attachEaseInTime = 0f;
		selectMode = InteractableSelectMode.Multiple;
		m_colorReceiver = GetComponentInChildren<MaterialInstanceColorAffordanceReceiver>(true);
	}

	/// <summary>
	/// Sets the category colour (normal / endpoint / remove). Interaction feedback is multiplied on top by the affordance receiver.
	/// Written to this handle's own material instance, so the handles stay SRP Batcher compatible.
	/// </summary>
	public void SetBaseColor(Color a_color)
	{
		if (m_hasBaseColor && m_baseColor == a_color)
			return;

		m_baseColor = a_color;
		m_hasBaseColor = true;
		if (m_colorReceiver != null)
		{
			m_colorReceiver.Material.SetColor(ms_baseColorId, a_color);
		}
	}

	public void Initialise(PenLinePointHandles a_owner, ulong a_pointId)
	{
		m_owner = a_owner;
		m_pointId = a_pointId;
		m_isDraggedLocally = false;
		m_dragInteractor = null;
		m_pendingInsertInteractors.Clear();
		m_isInRemoveRange = true; //force the visual reset, pooled handles may still be red
		SetRemoveState(false);
	}

	/// <summary>
	/// Marks this handle as being dragged by a_interactor without going through the hold request
	/// (used when the grab is handed over after an insert).
	/// </summary>
	public void SetDraggedByInsert(IXRSelectInteractor a_interactor)
	{
		m_isDraggedLocally = true;
		m_dragInteractor = a_interactor;
		m_nextSendTime = 0f;
	}

	protected override void OnSelectEntered(SelectEnterEventArgs a_args)
	{
		base.OnSelectEntered(a_args);
		if (m_owner == null)
			return;

		//Grab was handed over from an insert, already set up
		if (m_isDraggedLocally && a_args.interactorObject == m_dragInteractor)
			return;

		PenLine line = m_owner.Line;
		bool heldElsewhere = interactorsSelecting.Count > 1
			|| (line.TryGetPoint(m_pointId, out PenLinePoint point) && point.IsHeld && point.HeldBy != PenLinePoint.LocalClientId);

		if (heldElsewhere)
		{
			//Handled in Tick: changing selection from inside a select callback is not safe
			m_pendingInsertInteractors.Add(a_args.interactorObject);
			return;
		}

		if (line.BeginPointDrag(m_pointId))
		{
			m_isDraggedLocally = true;
			m_dragInteractor = a_args.interactorObject;
			m_nextSendTime = 0f;
		}
	}

	protected override void OnSelectExited(SelectExitEventArgs a_args)
	{
		base.OnSelectExited(a_args);
		if (m_owner == null)
			return;

		if (m_pendingInsertInteractors.Remove(a_args.interactorObject))
			return;

		if (!m_isDraggedLocally || a_args.interactorObject != m_dragInteractor)
			return;

		//Another local hand may still hold this handle (only for a frame before its insert); hand the drag over
		if (interactorsSelecting.Count > 0 && !m_pendingInsertInteractors.Contains(interactorsSelecting[0]))
		{
			m_dragInteractor = interactorsSelecting[0];
			return;
		}

		EndDrag();
	}

	private void EndDrag()
	{
		PenLine line = m_owner.Line;
		bool wasMovingLine = m_owner.IsMovePoint(m_pointId);

		m_isDraggedLocally = false;
		m_dragInteractor = null;

		Vector3 localPosition;
		bool remove;
		if (wasMovingLine && line.TryGetPoint(m_pointId, out PenLinePoint point))
		{
			//The whole line was moved: the point keeps its place on the line (no stretching)
			m_owner.EndLineMove();
			localPosition = point.Position;
			remove = false;
		}
		else
		{
			localPosition = line.transform.InverseTransformPoint(transform.position);
			remove = CheckRemoveRange(line, localPosition);
		}
		localPosition.y = 0f;

		SetRemoveState(false);
		line.EndPointDrag(m_pointId, localPosition, remove);
	}

	/// <summary>
	/// Called every frame by PenLinePointHandles while the handles are visible.
	/// </summary>
	public void Tick()
	{
		if (m_owner == null)
			return;

		PenLine line = m_owner.Line;

		if (m_pendingInsertInteractors.Count > 0)
		{
			ProcessPendingInserts(line);
		}

		if (!m_isDraggedLocally)
			return;

		//While the whole line is being moved by this point, the point itself stays where it is on the line
		if (m_owner.IsMovePoint(m_pointId))
		{
			SetRemoveState(false);
			return;
		}

		Vector3 localPosition = line.transform.InverseTransformPoint(transform.position);
		bool send = Time.unscaledTime >= m_nextSendTime;
		if (send)
		{
			m_nextSendTime = Time.unscaledTime + m_owner.SendInterval;
		}
		line.DragPoint(m_pointId, localPosition, send);

		SetRemoveState(CheckRemoveRange(line, localPosition));
	}

	private void ProcessPendingInserts(PenLine a_line)
	{
		m_interactorBuffer.Clear();
		m_interactorBuffer.AddRange(m_pendingInsertInteractors);
		m_pendingInsertInteractors.Clear();

		foreach (IXRSelectInteractor interactor in m_interactorBuffer)
		{
			if (interactor == null || !interactorsSelecting.Contains(interactor))
				continue;

			Vector3 worldPosition = interactor.GetAttachTransform(this).position;
			Vector3 localPosition = a_line.transform.InverseTransformPoint(worldPosition);

			if (!a_line.BeginInsertDrag(m_pointId, localPosition, out ulong newPointId))
			{
				interactionManager.SelectExit(interactor, this);
				continue;
			}

			PenLinePointHandle newHandle = m_owner.GetHandle(newPointId, worldPosition);
			newHandle.SetDraggedByInsert(interactor);

			//Keep this one in the pending list while exiting so OnSelectExited ignores it
			m_pendingInsertInteractors.Add(interactor);
			interactionManager.SelectExit(interactor, this);
			m_pendingInsertInteractors.Remove(interactor);

			interactionManager.SelectEnter(interactor, newHandle);
		}
	}

	/// <summary>
	/// Forces all interactors to release this handle without committing anything (its point no longer exists).
	/// </summary>
	public void ForceRelease()
	{
		m_isDraggedLocally = false;
		m_dragInteractor = null;
		m_pendingInsertInteractors.Clear();

		m_interactorBuffer.Clear();
		m_interactorBuffer.AddRange(interactorsSelecting);
		foreach (IXRSelectInteractor interactor in m_interactorBuffer)
		{
			m_pendingInsertInteractors.Add(interactor);
			interactionManager.SelectExit(interactor, this);
		}
		m_pendingInsertInteractors.Clear();
	}

	/// <summary>
	/// Line-local position the point would land on if released now (on the drawing plane, y = 0).
	/// False when not dragged locally, when the point would be removed, or when it is moving the whole line (it then stays where it is).
	/// </summary>
	public bool TryGetLandingPosition(out Vector3 a_localPosition)
	{
		a_localPosition = default;
		if (m_owner == null || !m_isDraggedLocally || m_isInRemoveRange || m_owner.IsMovePoint(m_pointId))
			return false;

		a_localPosition = m_owner.Line.transform.InverseTransformPoint(transform.position);
		a_localPosition.y = 0f;
		return true;
	}

	private bool CheckRemoveRange(PenLine a_line, Vector3 a_localPosition)
	{
		return Mathf.Abs(a_localPosition.y) > a_line.GlobalToLocalDistance(a_line.RemoveHeight);
	}

	private void SetRemoveState(bool a_remove)
	{
		if (m_isInRemoveRange == a_remove && m_owner != null)
			return;

		m_isInRemoveRange = a_remove;
		if (m_owner != null)
		{
			m_owner.RefreshHandleVisual(this);
		}
	}
}
#endif
