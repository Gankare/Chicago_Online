using Firebase;
using Firebase.Database;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Firebase.Extensions;
using UnityEngine.SceneManagement;

public class WaitingRoomButtons : MonoBehaviour
{
    public TMP_Text amountOfPlayersText;
    private void Start()
    {
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").ChildChanged += HandlePlayerChanged;
    }
    void HandlePlayerChanged(object sender, ChildChangedEventArgs args)
    {
        // Handle player connection or disconnection here
        StartCoroutine(CountPlayers());
    }
    public void Ready()
    {
        ServerManager.instance.PlayerReadyStatus(DataSaver.instance.userId, true);
    }
    public void Unready()
    {
        ServerManager.instance.PlayerReadyStatus(DataSaver.instance.userId, false);
    }

    IEnumerator CountPlayers()
    {
        int players = 0;
        var playersInServer = DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").GetValueAsync();
        yield return new WaitUntil(() => playersInServer.IsCompleted);
        DataSnapshot playersSnapshot = playersInServer.Result;

        if (playersSnapshot.Exists)
        {
            foreach (var requestSnapshot in playersSnapshot.Children)
            {
                players += 1;
            }
        }
        amountOfPlayersText.text = DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).ToString() + " " + players + "/4";
    }
}
