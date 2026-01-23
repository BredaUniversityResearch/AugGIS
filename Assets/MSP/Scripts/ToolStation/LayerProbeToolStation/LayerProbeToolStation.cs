public class LayerProbeToolStation : ToolStation
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
