using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Lets a menu that follows the head be dragged to another spot and keep following from there.
/// The follow uses only the head's turn, not its tilt, so the menu keeps its height when the
/// player looks up or down. While it is held the follow is off; on release the place it was left
/// at, relative to the head, becomes the new follow offset.
/// </summary>
[RequireComponent(typeof(LazyFollow))]
[RequireComponent(typeof(XRGrabInteractable))]
public class FollowWhereReleased : MonoBehaviour
{
	private LazyFollow m_follow;
	private XRGrabInteractable m_grab;

	// Stands where the head is and turns with it, but never tilts. LazyFollow follows this.
	private Transform m_levelHead;

	private void Awake()
	{
		m_follow = GetComponent<LazyFollow>();
		m_grab = GetComponent<XRGrabInteractable>();

		m_levelHead = new GameObject(name + " level head").transform;
		m_follow.target = m_levelHead;
	}

	private void OnEnable()
	{
		m_grab.selectEntered.AddListener(OnGrabbed);
		m_grab.selectExited.AddListener(OnReleased);
	}

	private void OnDisable()
	{
		m_grab.selectEntered.RemoveListener(OnGrabbed);
		m_grab.selectExited.RemoveListener(OnReleased);
	}

	private void OnDestroy()
	{
		if (m_levelHead != null)
		{
			Destroy(m_levelHead.gameObject);
		}
	}

	private void Update()
	{
		Camera head = Camera.main;
		if (head == null)
		{
			return;
		}

		Vector3 forward = head.transform.forward;
		forward.y = 0f;
		if (forward.sqrMagnitude < 0.0001f)
		{
			// Looking straight up or down: keep the last turn rather than guess one.
			m_levelHead.position = head.transform.position;
			return;
		}

		m_levelHead.position = head.transform.position;
		m_levelHead.rotation = Quaternion.LookRotation(forward, Vector3.up);
	}

	private void OnGrabbed(SelectEnterEventArgs a_args)
	{
		m_follow.enabled = false;
	}

	private void OnReleased(SelectExitEventArgs a_args)
	{
		// LazyFollow adds its offset in the followed object's own directions, so the offset is stored that way.
		Vector3 fromHead = transform.position - m_levelHead.position;
		m_follow.targetOffset = m_levelHead.InverseTransformVector(fromHead);
		m_follow.enabled = true;
	}
}
