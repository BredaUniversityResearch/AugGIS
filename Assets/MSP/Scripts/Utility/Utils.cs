using System;
using System.Net;
using JetBrains.Annotations;
using UnityEngine;

public static class Utils
{
	[CanBeNull]
	public static string GetEnvironmentVariable(string key)
	{
		string value = Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Machine) ??
			// retrieve from Linux Environment
			Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Process);
		return value;
	}

	public static bool TryParseIPAndPortString(string a_input, out IPAddress outIp, out ushort outPort)
	{
		IPAddress ip = null;
		ushort port = 0;

		bool ipParsed = false;
		bool portParsed = false;

		if (a_input.Contains(':'))
		{
			string[] splitInput = a_input.Split(':');

			ipParsed = IPAddress.TryParse(splitInput[0], out ip);
			outIp = ip;

			portParsed = ushort.TryParse(splitInput[1], out port);
			outPort = port;

			return ipParsed && portParsed;
		}
		else
		{
			ipParsed = IPAddress.TryParse(a_input, out ip);
			outIp = ip;

			outPort = port;
			return ipParsed;
		}
	}

	public static Matrix4x4 CalculateWorldMatrixBasedOnAnchorPosition(Vector3 a_firstAnchorPosition, Vector3 a_secondAnchorPosition)
	{
		Vector3 center = (a_firstAnchorPosition + a_secondAnchorPosition) / 2f;
		Quaternion rotation = Quaternion.LookRotation((new Vector3(a_secondAnchorPosition.x, 0f, a_secondAnchorPosition.z) - new Vector3(a_firstAnchorPosition.x, 0f, a_firstAnchorPosition.z)).normalized, Vector3.up);
		float scale = Vector3.Distance(new Vector3(a_firstAnchorPosition.x, 0f, a_firstAnchorPosition.z), new Vector3(a_secondAnchorPosition.x, 0f, a_secondAnchorPosition.z));

		return Matrix4x4.TRS(center, rotation, Vector3.one * scale);
	}
}
