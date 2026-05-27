using Unity.Netcode;
using UnityEngine;

public class MultiplayerSessionSpawner : MonoBehaviour
{
    [SerializeField] GameObject sessionPrefab;

    void Start()
    {
        if (!MultiplayerManager.IsMultiplayerGame) return;
        if (!NetworkManager.Singleton.IsHost) return;

        var obj = Instantiate(sessionPrefab);
        obj.GetComponent<NetworkObject>().Spawn(true);
    }
}
