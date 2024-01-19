using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    void Start()
    {
        ServerManager serverManager = FindObjectOfType<ServerManager>();
        serverManager.PlayerConnected("playerUserId");
    }

    void OnDestroy()
    {
        ServerManager serverManager = FindObjectOfType<ServerManager>();
        serverManager.PlayerDisconnected("playerUserId");
    }
}