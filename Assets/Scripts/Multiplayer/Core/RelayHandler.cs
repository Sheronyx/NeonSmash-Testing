using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Relay;
using UnityEngine;

public class RelayHandler
{
    public static async Task<string> CreateAsync(int maxConnections = 1)
    {
        var allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
        var joinCode   = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetHostRelayData(
            allocation.RelayServer.IpV4,
            (ushort)allocation.RelayServer.Port,
            allocation.AllocationIdBytes,
            allocation.Key,
            allocation.ConnectionData,
            isSecure: false
        );

        Debug.Log($"[Relay] Erstellt. JoinCode: {joinCode}");
        return joinCode;
    }

    public static async Task JoinAsync(string joinCode)
    {
        var join = await RelayService.Instance.JoinAllocationAsync(joinCode);

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetClientRelayData(
            join.RelayServer.IpV4,
            (ushort)join.RelayServer.Port,
            join.AllocationIdBytes,
            join.Key,
            join.ConnectionData,
            join.HostConnectionData,
            isSecure: false
        );

        Debug.Log($"[Relay] Beigetreten. JoinCode: {joinCode}");
    }
}
