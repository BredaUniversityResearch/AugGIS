using System.IO;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class SessionFileManager : NetworkBehaviour
{
    private NetworkVariable<FixedString64Bytes> _hostUrl = new(
        writePerm: NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[SessionManager] OnNetworkSpawn - IsServer: {IsServer}, IsClient: {IsClient}, IsHost: {IsHost}");

        if (IsServer)
        {
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            var ip = transport.ConnectionData.Address;

            if (ip == "0.0.0.0" || string.IsNullOrEmpty(ip))
                ip = "127.0.0.1";

            var port = HostFileServer.Instance.Port;
            _hostUrl.Value = new FixedString64Bytes($"{ip}:{port}");

            // Set it directly for the host, don't rely on the callback
            FileLoader.Instance.SetHostUrl(ip, port);

            HostFileServer.Instance.StartServer();
        }

        // This handles clients (and won't fire on host since host set the value, not received it)
        _hostUrl.OnValueChanged += (_, newVal) =>
        {
            Debug.Log($"[SessionManager] OnValueChanged fired: {newVal}");
            var parts = newVal.ToString().Split(':');
            FileLoader.Instance.SetHostUrl(parts[0], int.Parse(parts[1]));
        };

        if (!IsServer && !string.IsNullOrEmpty(_hostUrl.Value.ToString()))
        {
            Debug.Log($"[SessionManager] Value already present on join: {_hostUrl.Value}");
            var parts = _hostUrl.Value.ToString().Split(':');
            FileLoader.Instance.SetHostUrl(parts[0], int.Parse(parts[1]));
        }
    }
}