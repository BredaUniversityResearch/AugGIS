using UnityEngine;
using SQLitePCL;
using SQLite;



public class LogSqliteCompilationOptions : MonoBehaviour
{
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void InitSqlite()
    {
        //SQLitePCL.Batteries.Init();
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());
        
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public class CompileOptionRow
    {
        public string compile_option { get; set; }
    }

    void Start()
    {
        string path = System.IO.Path.Combine(Application.persistentDataPath, "gisdata.db");
        using var db = new SQLiteConnection(path);

        var rows = db.Query<CompileOptionRow>("PRAGMA compile_options;");
        Debug.Log("SQLite Compile Options:\n" +
            string.Join("\n", rows.ConvertAll(r => r.compile_option)));
    }

    // Update is called once per frame
    void Update()
    {

    }
}
