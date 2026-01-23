using UnityEngine;

public class EraserToolStation : ToolStation
{
	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();

		if (IsServer)
		{
			SpawnToolStationSelection();
		}
	}
}
