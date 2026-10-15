using System;
using UnityEngine;

namespace MSP.Scripts.GisData
{
	/// <summary>
	/// Registers the SQLitePCL provider exactly once at process start-up, before any
	/// SQLiteConnection is opened anywhere in the game. Binds to the system sqlite3
	/// library (SQLite3Provider_sqlite3) rather than the bundled native provider
	/// (SQLite3Provider_e_sqlite3), which has been found to crash. This replaces the
	/// provider-registration responsibility previously prototyped in
	/// LogSqliteCompilationOptions.
	/// </summary>
	public static class SqliteRuntimeBootstrap
	{
		private static bool s_initialised;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void InitialiseSqliteProvider()
		{
			if (s_initialised)
				return;

			Debug.Log("Initializing SQLite provider.");
#if USE_SQLITEPCL_RAW
        Debug.Log("sqlite-net: Using SQLitePCL raw provider.");
#endif
#if NO_SQLITEPCL_RAW_BATTERIES
			Debug.Log("sqlite-net: do not automatically load SQLitePCL raw batteries.");
#endif

			try
			{
				Debug.Log("Attempting to initialize system sqlite3 provider.");
				SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_sqlite3());
				Debug.Log($"sqlite3 provider initialized, version: {SQLitePCL.raw.sqlite3_libversion().utf8_to_string()}");
			}
			catch (System.Exception ex)
			{
				Debug.Log($"No system level sqlite3 library available, trying shipped provider.");
				try
				{
					SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());
					Debug.Log($"shipped sqlite3 provider initialized, version: {SQLitePCL.raw.sqlite3_libversion().utf8_to_string()}");
				}
				catch (System.Exception ex2)
				{
					Debug.LogError($"Failed to initialize shipped sqlite3 provider: {ex2.Message}");
					Debug.LogError("SQLite initialization failed completely.");
				}
			}
			s_initialised = true;
		}
	}
}
