using System;
using System.IO;
using System.Net;
using System.Threading;
using UnityEngine;

public class HostFileServer : MonoBehaviour
{
    public static HostFileServer Instance { get; private set; }

    [SerializeField] private int port = 8080;
    //[SerializeField] 
    private string servedFolderPath; // Set in inspector or at runtime

    private HttpListener _listener;
    private Thread _listenerThread;

    public int Port => port;
    public string FolderPath => servedFolderPath;

    private void Awake()
    {
        Instance = this;
    }

    public void StartServer(string folderPath = null)
    {
        if (folderPath != null)
            servedFolderPath = folderPath;
        else
            servedFolderPath = Application.streamingAssetsPath + "/SharedFiles";

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://*:{port}/");
        _listener.Start();

        _listenerThread = new Thread(HandleRequests) { IsBackground = true };
        _listenerThread.Start();

        Debug.Log($"[HostFileServer] Serving '{servedFolderPath}' on port {port}");
    }

    public void StopServer()
    {
        _listener?.Stop();
        _listenerThread?.Abort();
    }

    private void HandleRequests()
    {
        while (_listener.IsListening)
        {
            try
            {
                var context = _listener.GetContext();
                ThreadPool.QueueUserWorkItem(_ => ProcessRequest(context));
            }
            catch (HttpListenerException) { break; } // Server stopped
            catch (Exception e) { Debug.LogError($"[HostFileServer] {e.Message}"); }
        }
    }

    private void ProcessRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        // Strip leading slash, block path traversal
        var fileName = Path.GetFileName(request.Url.LocalPath.TrimStart('/'));
        var fullPath = Path.Combine(servedFolderPath, fileName);

        if (!File.Exists(fullPath))
        {
            response.StatusCode = 404;
            response.Close();
            return;
        }

        try
        {
            var bytes = File.ReadAllBytes(fullPath);
            response.StatusCode = 200;
            response.ContentLength64 = bytes.Length;
            response.ContentType = GetContentType(fileName);
            response.OutputStream.Write(bytes, 0, bytes.Length);
        }
        catch (Exception e)
        {
            Debug.LogError($"[HostFileServer] Failed to serve {fileName}: {e.Message}");
            response.StatusCode = 500;
        }
        finally
        {
            response.Close();
        }
    }

    private string GetContentType(string fileName)
    {
        return Path.GetExtension(fileName).ToLower() switch
        {
            ".gltf" => "model/gltf+json",
            ".glb" => "model/gltf-binary",
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".wav" => "audio/wav",
            ".mp3" => "audio/mpeg",
            ".mp4" => "video/mp4",
            _ => "application/octet-stream" // fallback for unknown types
        };
    }

    private void OnDestroy() => StopServer();
}