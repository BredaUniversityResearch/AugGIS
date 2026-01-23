using System;
using PassthroughCameraSamples;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

public class UIInGameStationSettings : MonoBehaviour
{
	[SerializeField]
	[Required]
	private CustomXRButton m_spawnPawnToolStationButton = null;

	[SerializeField]
	[Required]
	private CustomXRButton m_spawnPenToolStationButton = null;

	[SerializeField]
	[Required]
	private CustomXRButton m_spawnEraserToolStationButton = null;

	[SerializeField]
	[Required]
	private CustomXRButton m_spawnLayerProbeToolStationButton = null;

	[SerializeField]
	[Required]
	private CustomXRButton m_spawnTrashToolStationButton = null;

	[Header("Spawn Offsets")]

	[SerializeField]
	private float m_rightSpawnOffset = 0.2f;

	void Awake()
	{
		m_spawnPawnToolStationButton.OnPress.AddListener(OnSpawnPawnToolStationButtonClicked);
		m_spawnPenToolStationButton.OnPress.AddListener(OnSpawnPenToolStationButtonClicked);
		m_spawnEraserToolStationButton.OnPress.AddListener(OnSpawnEraserToolStationButtonClicked);
		m_spawnLayerProbeToolStationButton.OnPress.AddListener(OnSpawnLayerProbeToolStationButtonClicked);
		m_spawnTrashToolStationButton.OnPress.AddListener(OnSpawnTrashToolStationButtonClicked);
	}

	private void OnSpawnPawnToolStationButtonClicked()
	{
		SessionManager.Instance.WorldManager.ToolStationSpawner.SpawnPawnToolStation(CalculateSpawnPosition());
	}

	private void OnSpawnPenToolStationButtonClicked()
	{
		SessionManager.Instance.WorldManager.ToolStationSpawner.SpawnPenToolStation(CalculateSpawnPosition());
	}

	private void OnSpawnEraserToolStationButtonClicked()
	{
		SessionManager.Instance.WorldManager.ToolStationSpawner.SpawnEraserToolStation(CalculateSpawnPosition());
	}

	private void OnSpawnLayerProbeToolStationButtonClicked()
	{
		SessionManager.Instance.WorldManager.ToolStationSpawner.SpawnLayerProbeToolStation(CalculateSpawnPosition());
	}

	private void OnSpawnTrashToolStationButtonClicked()
	{
		SessionManager.Instance.WorldManager.ToolStationSpawner.SpawnTrashToolStation(CalculateSpawnPosition());
	}

	private Vector3 CalculateSpawnPosition()
	{
		return transform.position + transform.right * m_rightSpawnOffset;
	}
}
