using UnityEngine;

public class Player : MonoBehaviour
{
    void Start()
    {
        ServerManager serverManager = FindObjectOfType<ServerManager>();
        serverManager.PlayerConnected(DataSaver.instance.userId);
    }

    void OnDestroy()
    {
        ServerManager serverManager = FindObjectOfType<ServerManager>();
        serverManager.PlayerDisconnected(DataSaver.instance.userId);
    }
}
