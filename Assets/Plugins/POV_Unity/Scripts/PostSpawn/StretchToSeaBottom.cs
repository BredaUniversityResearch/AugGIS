using System.Collections;
using UnityEngine;

namespace POV_Unity
{
	public class StretchToSeaBottom : MonoBehaviour, IPostSpawnCallback
	{
		[SerializeField] float m_heightAtScale1;

		public void Initialise()
		{
			float seaBottomDepth = -ImportedConfigRoot.Instance.m_bathymetry.GetHeightAtWorldPosition(transform.position) * ImportedConfigRoot.Instance.ConfigToWorldScale;
			transform.localScale = new Vector3(1f, seaBottomDepth / m_heightAtScale1, 1f);
		}	
	}
}