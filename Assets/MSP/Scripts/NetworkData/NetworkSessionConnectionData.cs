using System.Net;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "ConnectionData", menuName = "MSP/NetworkSessionConnectionData")]
public class NetworkSessionConnectionData : ScriptableObject
{
	[ReadOnly]
	public IPAddress ip;

	[ReadOnly]
	public ushort Port;

	[ReadOnly]
	public bool isServer;

	public void Reset()
	{
		ip = null;
		Port = 0;
		isServer = false;
	}
}
