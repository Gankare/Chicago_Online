using UnityEngine;

public class Player : MonoBehaviour
{
    void Start()
    {
        ServerManager.instance.PlayerConnected(DataSaver.instance.userId);
    }

    void OnDestroy()
    {
        ServerManager.instance.PlayerDisconnected(DataSaver.instance.userId);
    }
}
