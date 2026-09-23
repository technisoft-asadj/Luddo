using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Ludo.Online
{
    /// <summary>
    /// Makes sure a Netcode "NetworkManager" exists before a room is created or joined. The Multiplayer Services package
    /// starts it for us (host or client, through Unity's Relay servers) - we only have to provide it. It is built in code
    /// so no extra scene object can be forgotten.
    /// </summary>
    public static class OnlineNetwork
    {
        public static NetworkManager Ensure()
        {
            if (NetworkManager.Singleton != null) return NetworkManager.Singleton;

            var go = new GameObject("NetworkManager");
            go.SetActive(false);                                   // configure first, wake up afterwards
            var transport = go.AddComponent<UnityTransport>();
            var manager = go.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                EnableSceneManagement = false,                      // we load our own scenes; nothing is spawned by the network
                ConnectionApproval = false,
                ForceSamePrefabs = false,
                TickRate = 20
            };
            Object.DontDestroyOnLoad(go);
            go.SetActive(true);
            return manager;
        }

        /// <summary>Stop the network (after leaving a room). Safe to call when nothing runs.</summary>
        public static void Stop()
        {
            var m = NetworkManager.Singleton;
            if (m != null && (m.IsListening || m.ShutdownInProgress == false && m.IsConnectedClient)) m.Shutdown();
        }
    }
}
