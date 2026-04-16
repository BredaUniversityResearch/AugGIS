using System.IO;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class SharedFileList : NetworkBehaviour
{
    public static SharedFileList Instance { get; private set; }

    private NetworkList<FixedString128Bytes> _fileNames;

    private FileSystemWatcher _watcher;

    private void Awake()
    {
        Instance = this;
        _fileNames = new NetworkList<FixedString128Bytes>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            PopulateInitialFiles();
            StartWatcher();
        }

        // All clients (including host) can subscribe to changes
        _fileNames.OnListChanged += OnFileListChanged;
    }

    // --- Host only ---

    private void PopulateInitialFiles()
    {
        var folder = HostFileServer.Instance.FolderPath;
        foreach (var file in Directory.GetFiles(folder))
        {
            var name = Path.GetFileName(file);
            if (IsSupportedFile(name))
                _fileNames.Add(new FixedString128Bytes(name));
        }
    }

    private void StartWatcher()
    {
        _watcher = new FileSystemWatcher(HostFileServer.Instance.FolderPath)
        {
            NotifyFilter = NotifyFilters.FileName,
            EnableRaisingEvents = true
        };

        _watcher.Created += (_, e) => AddFileThreadSafe(Path.GetFileName(e.Name));
        _watcher.Deleted += (_, e) => RemoveFileThreadSafe(Path.GetFileName(e.Name));
    }

    // FileSystemWatcher runs on a background thread, so dispatch to main thread
    private void AddFileThreadSafe(string fileName)
    {
        if (!IsSupportedFile(fileName)) return;
        MainThreadDispatcher.Enqueue(() =>
            _fileNames.Add(new FixedString128Bytes(fileName)));
    }

    private void RemoveFileThreadSafe(string fileName)
    {
        MainThreadDispatcher.Enqueue(() =>
        {
            for (int i = 0; i < _fileNames.Count; i++)
            {
                if (_fileNames[i].ToString() == fileName)
                {
                    _fileNames.RemoveAt(i);
                    break;
                }
            }
        });
    }

    // --- All clients ---

    private void OnFileListChanged(NetworkListEvent<FixedString128Bytes> changeEvent)
    {
        if (changeEvent.Type == NetworkListEvent<FixedString128Bytes>.EventType.Add)
        {
            var fileName = changeEvent.Value.ToString();
            Debug.Log($"[SharedFileList] New file available: {fileName}");
            // Hand off to your loader (Step 3)
            FileLoader.Instance.LoadFile(fileName);
        }
    }

    private bool IsSupportedFile(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLower();
        return ext is ".gltf" or ".glb" or ".png" or ".jpg" or ".jpeg"
                    or ".wav" or ".mp3" or ".mp4";
    }

    public override void OnNetworkDespawn()
    {
        _watcher?.Dispose();
        _fileNames.OnListChanged -= OnFileListChanged;
    }
}