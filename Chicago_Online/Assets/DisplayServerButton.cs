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
    DatabaseReference databaseReference;
    public TMP_Text buttonText;
    public string serverId;
    public string waitingRoomId;

    private void Start()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            FirebaseApp app = FirebaseApp.DefaultInstance;
            databaseReference = FirebaseDatabase.DefaultInstance.RootReference;
        });

        // Listen for changes in the server's players
        databaseReference.Child("servers").Child(serverId).Child("players").ChildChanged += HandlePlayerChanged;

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
        databaseReference.Child("servers").Child(serverId).Child("players").ChildChanged -= HandlePlayerChanged;
    }

    void HandlePlayerChanged(object sender, ChildChangedEventArgs args)
    {
        // Handle player connection or disconnection here
        StartCoroutine(CountPlayers());
    }

    public void TryToJoin()
    {
        SetServerId();
        //join if players in the server is less than 4 and if the game is not started
        if (ServerManager.instance.GetPlayerCount() < 4 && !ServerManager.instance.gameHasStarted)
        {
            // Add the player to the server
            ServerManager.instance.PlayerConnected(DataSaver.instance.userId);
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
        var playersInServer = databaseReference.Child("servers").Child(serverId).Child("players").GetValueAsync();
        yield return new WaitUntil(() => playersInServer.IsCompleted);
        DataSnapshot playersSnapshot = playersInServer.Result;

        if (playersSnapshot.Exists)
        {
            foreach (var requestSnapshot in playersSnapshot.Children)
            {
                players += 1;
            }
        }
        buttonText.text = databaseReference.Child("servers").Child(serverId).ToString() + " " + players + "/4";
    }
}
