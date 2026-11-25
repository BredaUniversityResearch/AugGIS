using UnityEngine.XR.Interaction.Toolkit.AffordanceSystem.State;

#pragma warning disable CS0618 // affordance system is marked obsolete
public class OverridableInteractableAffordanceStateProvider : XRInteractableAffordanceStateProvider
{
	private bool m_hasOverride;
	private AffordanceStateData m_overrideState;

	public void SetOverride(AffordanceStateData a_state)
	{
		m_hasOverride = true;
		m_overrideState = a_state;
		RefreshState();
	}

	public void ClearOverride()
	{
		if (!m_hasOverride)
			return;
		m_hasOverride = false;
		RefreshState(); // falls back to the real hover/select/idle state
	}

	protected override AffordanceStateData GenerateNewAffordanceState()
	{
		return m_hasOverride ? m_overrideState : base.GenerateNewAffordanceState();
	}
}
#pragma warning restore CS0618