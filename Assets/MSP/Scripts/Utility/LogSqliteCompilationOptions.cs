using UnityEngine;
using SQLitePCL;
using SQLite;
using System.Collections.Generic;



public class LogSqliteCompilationOptions : MonoBehaviour
{

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void InitSqlite()
    {
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
    }


    void Start()
    {
        try
        {
            SQLitePCL.raw.sqlite3_open(":memory:", out var handle);
            Debug.Log("raw open succeeded, version " + SQLitePCL.raw.sqlite3_libversion().utf8_to_string());
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to open SQLite database: " + ex.Message);
        }

        try
        {
            string path = System.IO.Path.Combine(Application.persistentDataPath, "gisdata.db");
            using var db = new SQLiteConnection(path);
            Debug.Log("Opened SQLite database at path: " + path);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to open SQLite database at path: " + ex.Message);
        }
    }

    // Update is called once per frame
    void Update()
    {

    }
}
