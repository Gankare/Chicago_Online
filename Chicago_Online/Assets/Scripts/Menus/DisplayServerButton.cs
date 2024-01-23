using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using UnityEngine.SceneManagement;

public class DisplayServerButton : MonoBehaviour
{
    public TMP_Text buttonText;
    public string serverId;
    public string waitingRoomId;

    private void Start()
    {
        StartCoroutine(CountPlayers());
        // Listen for changes in the server's players
        DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").ChildChanged += HandlePlayerChanged;

        // Initial count
        StartCoroutine(CountPlayers());
    }

    public void SetServerId()
    {
        ServerManager.instance.serverId = serverId;
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
        DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").ChildChanged -= HandlePlayerChanged;
    }

    void HandlePlayerChanged(object sender, ChildChangedEventArgs args)
    {
        // Handle player connection or disconnection here
        StartCoroutine(CountPlayers());
    }

    public void TryToJoin()
    {
        StartCoroutine(CheckAndCreateServer(serverId));
    }

    IEnumerator CheckAndCreateServer(string serverId)
    {
        var serverReference = DataSaver.instance.dbRef.Child("servers").Child(serverId);

        var task = serverReference.GetValueAsync();
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.IsFaulted || task.IsCanceled)
        {
            Debug.LogError($"Error checking server existence for {serverId}. Error: {task.Exception}");
            yield break;
        }

        DataSnapshot snapshot = task.Result;

        if (!snapshot.Exists)
        {
            Debug.Log($"Server {serverId} does not exist. Creating...");

            // If the server does not exist, create it
            var setValueTask = serverReference.Child("players").SetValueAsync(DataSaver.instance.userId);
            yield return new WaitUntil(() => setValueTask.IsCompleted);

            Debug.Log($"Server {serverId} created.");
        }
        else
        {
            Debug.Log($"Server {serverId} already exists.");
        }

        SetServerId();

        // Join if players in the server are less than 4 and if the game is not started
        if (ServerManager.instance.GetPlayerCount() < 4 && !ServerManager.instance.gameHasStarted)
        {
            // Add the player to the server
            SceneManager.LoadScene(waitingRoomId);
        }
        else
        {
            Debug.Log("Cannot join the server.");
        }
    }

    IEnumerator CountPlayers()
    {
        int players = 0;
        var playersInServer = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").GetValueAsync();
        yield return new WaitUntil(() => playersInServer.IsCompleted);
        DataSnapshot playersSnapshot = playersInServer.Result;

        if (playersSnapshot.Exists)
        {
            foreach (var requestSnapshot in playersSnapshot.Children)
            {
                players += 1;
            }
        }
        buttonText.text = serverId + " " + players + "/4";
    }
}
