using GLTFast;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Networking;

public class FileLoader : MonoBehaviour
{
    public static FileLoader Instance { get; private set; }

    private string _baseUrl;
    public string BaseUrl => _baseUrl;


    private void Awake() => Instance = this;


    // Call this once you know the host IP (see Step 5)
    public void SetHostUrl(string hostIp, int port)
    {
        _baseUrl = $"http://{hostIp}:{port}";
    }

    public void LoadFile(string fileName)
    {
        var ext = System.IO.Path.GetExtension(fileName).ToLower();

        switch (ext)
        {
            case ".gltf":
            case ".glb":
                LoadGltf(fileName);  // no StartCoroutine
                break;
            case ".png":
            case ".jpg":
            case ".jpeg":
                StartCoroutine(LoadImage(fileName));
                break;
            case ".wav":
            case ".mp3":
                StartCoroutine(LoadAudio(fileName));
                break;
            case ".mp4":
                LoadVideo(fileName);
                break;
            default:
                StartCoroutine(LoadRawBytes(fileName));
                break;
        }
    }

    private async void LoadGltf(string fileName)
    {
        var url = $"{_baseUrl}/{fileName}";
        var gltf = new GltfImport();
        var success = await gltf.Load(url);
        if (success)
        {
            await gltf.InstantiateMainSceneAsync(new GameObject(fileName).transform);
        }
    }

    private IEnumerator LoadImage(string fileName)
    {
        var url = $"{_baseUrl}/{fileName}";
        using var req = UnityWebRequestTexture.GetTexture(url);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            var texture = DownloadHandlerTexture.GetContent(req);
            Debug.Log($"[FileLoader] Image loaded: {fileName} ({texture.width}x{texture.height})");
            // Use texture as needed
        }
    }

    private IEnumerator LoadAudio(string fileName)
    {
        var url = $"{_baseUrl}/{fileName}";
        var audioType = fileName.EndsWith(".mp3") ? AudioType.MPEG : AudioType.WAV;
        using var req = UnityWebRequestMultimedia.GetAudioClip(url, audioType);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            var clip = DownloadHandlerAudioClip.GetContent(req);
            Debug.Log($"[FileLoader] Audio loaded: {fileName}");
            // Use clip as needed
        }
    }

    private void LoadVideo(string fileName)
    {
        var url = $"{_baseUrl}/{fileName}";
        var videoPlayer = gameObject.AddComponent<UnityEngine.Video.VideoPlayer>();
        videoPlayer.url = url;
        videoPlayer.Play();
    }

    private IEnumerator LoadRawBytes(string fileName)
    {
        var url = $"{_baseUrl}/{fileName}";
        using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET);
        req.downloadHandler = new DownloadHandlerBuffer();
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            var bytes = req.downloadHandler.data;
            Debug.Log($"[FileLoader] Raw file loaded: {fileName} ({bytes.Length} bytes)");
            // Handle bytes as needed
        }
    }

    private Dictionary<string, Texture2D> _textureCache = new();
    private Dictionary<string, int> _textureRefCount = new();


    public async Task<Texture2D> GetImageAsync(string fileName)
    {
        // Wait for host URL to be available
        float timeout = 5f;
        while (string.IsNullOrEmpty(_baseUrl))
        {
            Debug.Log($"[FileLoader] GetImageAsync called, _baseUrl: '{_baseUrl}'");

            timeout -= 0.1f;
            if (timeout <= 0)
            {
                Debug.LogError($"[FileLoader] Timed out waiting for host URL");
                return null;
            }
            await Awaitable.WaitForSecondsAsync(0.1f);
        }

        if (_textureCache.TryGetValue(fileName, out var cached))
        {
            _textureRefCount[fileName]++;
            return cached;
        }

        var url = $"{_baseUrl}/{fileName}";
        var req = UnityWebRequestTexture.GetTexture(url);

        try
        {
            await req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[FileLoader] Image not found: {fileName}");
                return null;
            }

            var texture = DownloadHandlerTexture.GetContent(req);
            _textureCache[fileName] = texture;
            _textureRefCount[fileName] = 1;
            return texture;
        }
        finally
        {
            req.Dispose();
        }
    }

    public void ReleaseImage(string fileName)
    {
        if (!_textureCache.ContainsKey(fileName)) return;

        _textureRefCount[fileName]--;

        if (_textureRefCount[fileName] <= 0)
        {
            Destroy(_textureCache[fileName]);
            _textureCache.Remove(fileName);
            _textureRefCount.Remove(fileName);
        }
    }
}