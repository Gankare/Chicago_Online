using Firebase;
using Firebase.Database;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Firebase.Extensions;
using UnityEngine.SceneManagement;
using System.Linq;
using System.Threading.Tasks;

public class WaitingRoomButtons : MonoBehaviour
{
    public TMP_Text amountOfPlayersText;
    public List<GameObject> playerObjects;
    public List<TMP_Text> playerNames;
    public List<Image> readyCards;
    private void Start()
    {
        UpdatePlayers();
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").ChildChanged += HandlePlayerChanged;
    }
    void HandlePlayerChanged(object sender, ChildChangedEventArgs args)
    {
        // Handle player connection or disconnection here
        StartCoroutine(UpdatePlayers());
    }
    private void OnDisable()
    {
        // Remove the listener when the script is disabled
        RemovePlayerChangedListener();
    }
    private void OnDestroy()
    {
        // Remove the listener when the object is destroyed
        RemovePlayerChangedListener();
    }
    void RemovePlayerChangedListener()
    {
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").ChildChanged -= HandlePlayerChanged;
    }
    public void Ready()
    {
        ServerManager.instance.PlayerReadyStatus(DataSaver.instance.userId, true);
    }
    public void Unready()
    {
        ServerManager.instance.PlayerReadyStatus(DataSaver.instance.userId, false);
    }

    IEnumerator UpdatePlayers()
    {
        int players = 0;
        int playersReady = 0;
        foreach (GameObject card in playerObjects) 
        {
            card.SetActive(false);
        }

        var playersInServer = DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").GetValueAsync();
        yield return new WaitUntil(() => playersInServer.IsCompleted);

        if (playersInServer.Exception != null)
        {
            Debug.LogError($"Error getting players data: {playersInServer.Exception}");
            yield break;
        }

        DataSnapshot playersSnapshot = playersInServer.Result;

        if (playersSnapshot.Exists)
        {
            foreach (var requestSnapshot in playersSnapshot.Children)
            {
                playerObjects[players].SetActive(true);
                //playerNames[players].text = username of the requestsnapshot userid
                // Use requestSnapshot directly to get the child value
                var isPlayerReady = requestSnapshot.Child("ready").Value;
                var playername = requestSnapshot.Child("username").Value;
                // Check if the player is ready
                if (isPlayerReady != null)
                {
                    bool readyValue = bool.Parse(isPlayerReady.ToString());
                    if (readyValue)
                    {
                        playersReady++;
                        readyCards[players].color = Color.green;
                    }
                    else
                    {
                        readyCards[players].color = Color.clear;
                    }
                }

                players++;
            }
        }

        amountOfPlayersText.text = $"{playersReady}/{players} players ready";
    }
}
