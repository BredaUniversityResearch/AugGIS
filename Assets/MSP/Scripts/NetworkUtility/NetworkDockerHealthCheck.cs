using System;
using System.IO;
using System.Threading;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

public class NetworkDockerHealthCheck : MonoBehaviour
{
	[SerializeField]
	[Required]
	private NetworkManager m_networkManager;

	private int m_writeIntervalInSeconds = 10;
	private Thread m_writeThread;
	private bool m_threadIsRunning = true;
	private string m_healthcheckFile;

	void Awake()
	{
#if UNITY_SERVER
		string dockerEnv = Utils.GetEnvironmentVariable("DOCKER");
		string modeEnv = Utils.GetEnvironmentVariable("HEALTHCHECK_WRITER_MODE");

		bool dockerActive = dockerEnv == "1";
		bool modeAllowed = string.IsNullOrEmpty(modeEnv) || modeEnv == "0";

		if (dockerActive && modeAllowed)
		{
			string dir = Application.persistentDataPath;
			Debug.Log("HealthCheck writing to directory: " + dir);
			Directory.CreateDirectory(dir);
			m_healthcheckFile = Path.Combine(dir, "docker_healthcheck.txt");

			string envInterval = Utils.GetEnvironmentVariable("HEALTHCHECK_WRITER_INTERVAL");
			if (int.TryParse(envInterval, out int parsedInterval) && parsedInterval > 0)
			{
				m_writeIntervalInSeconds = parsedInterval;
			}

			m_writeThread = new Thread(WriteHealthStatusLoop);
			m_writeThread.IsBackground = true;
			m_writeThread.Start();
		}
#endif
	}

	void WriteHealthStatusLoop()
	{
		while (m_threadIsRunning)
		{
			try
			{
				if (!m_networkManager.IsServer)
				{
					continue;
				}

				string value = m_networkManager.IsListening ? "1" : "0";
				WriteHealthCheckValueToFile(value);
			}
			catch (Exception ex)
			{
				Debug.LogError("Error writing healthcheck: " + ex);
			}

			Thread.Sleep(m_writeIntervalInSeconds * 1000);
		}
	}

	private void WriteHealthCheckValueToFile(string value)
	{
		string tempFile = m_healthcheckFile + ".tmp";
		File.WriteAllText(tempFile, value);

		//if destination already exists make sure to delete file first before moving
		if (File.Exists(m_healthcheckFile))
		{
			File.Delete(m_healthcheckFile);
		}

		File.Move(tempFile, m_healthcheckFile);
	}

	void OnApplicationQuit()
	{
		m_threadIsRunning = false;
		m_writeThread?.Join();
	}
} 
