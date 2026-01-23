using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "LocalPlayerSessionDataSO", menuName = "MSP/LocalPlayerSessionDataSO")]
public class LocalPlayerSessionDataSO : ScriptableObject
{
	[SerializeField]
	private string m_defaultPlayerName = "Name";

	[SerializeField]
	private Color m_defaultTeamColor = Color.red;

	[ReadOnly]
	public string playerName;

	[ReadOnly]
	public Color teamColor;

	public void Reset()
	{
		playerName = m_defaultPlayerName;
		teamColor = m_defaultTeamColor;
	}
}
