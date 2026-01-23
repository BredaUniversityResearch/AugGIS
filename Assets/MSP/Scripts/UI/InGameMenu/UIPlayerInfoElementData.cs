using UnityEngine;

public class UIPlayerInfoElementData : IUIListElementData
{
	public string Text => m_playerName;

	private string m_playerName;
	private Color m_playerTeamColor;
	public Color PlayerTeamColor => m_playerTeamColor;

	private bool m_isGameMaster;
	public bool IsGameMaster => m_isGameMaster;

	public UIPlayerInfoElementData(string a_playerName, Color a_playerTeamColor, bool a_isPlayerGameMaster)
	{
		m_playerName = a_playerName;
		m_playerTeamColor = a_playerTeamColor;
		m_isGameMaster = a_isPlayerGameMaster;
	}

}
