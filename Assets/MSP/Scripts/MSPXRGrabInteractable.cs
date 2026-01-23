using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class MSPXRGrabInteractable : XRGrabInteractable
{
	[SerializeField]
	private bool m_changeTransformOnGrab = true;

	[SerializeField]
	[Sirenix.OdinInspector.ReadOnly]
	private bool m_allowInteraction = true;

	protected override void OnSelectEntering(SelectEnterEventArgs args)
	{
		if (!m_allowInteraction)
		{
			return;
		}

		base.OnSelectEntering(args);
	}

	protected override void OnSelectExiting(SelectExitEventArgs args)
	{
		if (!m_allowInteraction)
		{
			return;
		}

		base.OnSelectExiting(args);
	}

	protected override void OnSelectExited(SelectExitEventArgs args)
	{
		if (!m_allowInteraction)
		{
			return;
		}

		base.OnSelectExited(args);
	}

	protected override void Grab()
	{
		Transform parentTransform = transform.parent;
		Pose worldPose = transform.GetWorldPose();
		Pose localPose = transform.GetLocalPose();

		int siblingIndex = 0;
		if (parentTransform != null)
		{
			siblingIndex = transform.GetSiblingIndex();
		}

		base.Grab();

		if (!m_changeTransformOnGrab)
		{
			transform.SetParent(parentTransform);

			transform.SetSiblingIndex(siblingIndex);
			transform.SetLocalPose(localPose);
			SetTargetPose(worldPose);
		}
	}

	public void AllowInteraction()
	{
		foreach(var collider in colliders)
        {
            collider.enabled = true;
        }
	}

	public void BlockInteraction()
	{
		foreach(var collider in colliders)
		{
			collider.enabled = false;
		}
	}
}
