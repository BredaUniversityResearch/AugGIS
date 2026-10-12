using UnityEngine;
using SQLitePCL;
using SQLite;
using System.Collections.Generic;



public class LogSqliteCompilationOptions : MonoBehaviour
{
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void InitSqlite()
    {
        Debug.Log("Initializing SQLite");
        try
        {
            // Bind to the system sqlite3 library
            //SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3()); // crashes
            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_sqlite3()); // works, uses system sqlite3

            Debug.Log($"dynamic_cdecl provider initialized, version: {SQLitePCL.raw.sqlite3_libversion().utf8_to_string()}");
        
#if NO_SQLITEPCL_RAW_BATTERIES 
            Debug.Log("NO_SQLITEPCL_RAW_BATTERIES is defined");
#endif
        
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed: {ex.Message}");
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public class CompileOptionRow
    {
        public string compile_option { get; set; }
    }

    void Start()
    {
        // try
        // {
        //     SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_sqlite3());
        //     Debug.Log("SQLite initialized with raw provider.");

        // }
        // catch (System.Exception ex)
        // {
        //     Debug.LogError("Failed to initialize SQLite: " + ex.Message);
        // }

        try
        {
            SQLitePCL.raw.sqlite3_open(":memory:", out var handle);
            Debug.Log("raw open succeeded, version " + SQLitePCL.raw.sqlite3_libversion().utf8_to_string());

            string path = System.IO.Path.Combine(Application.persistentDataPath, "gisdata.db");
            using var db = new SQLiteConnection(path);
            // Debug.Log("Opened SQLite database at path: " + path);

            // var rows = db.Query<CompileOptionRow>("PRAGMA compile_options;");
            // Debug.Log("SQLite Compile Options:\n" +
            //     string.Join("\n", rows.ConvertAll(r => r.compile_option)));

        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to query SQLite compile options: " + ex.Message);
        }

    }

    // Update is called once per frame
    void Update()
    {

    }
}
