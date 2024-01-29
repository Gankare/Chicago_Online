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
    private Button button;
    public TMP_Text buttonText;
    public string waitingRoomId;
    public string serverId;
    private bool isUpdatingPlayers = false;
    private bool joiningServer = false;

    private void Start()
    {
        button = GetComponent<Button>();
        button.enabled = true;

        // Listen for changes in the server's players
        DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").ChildChanged += HandlePlayerChanged;

        //Timer so that serverid loads in before function

        // Start counting players
        StartCoroutine(CountPlayers());
        Invoke(nameof(UpdatePlayers), 0.5f);
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
        if (this == null)
        {
            // The object has been destroyed, so we should stop processing
            return;
        }
        // Handle player connection or disconnection here
        if (!isUpdatingPlayers)
            UpdatePlayers();
    }
    public void SetServerId()
    {
        ServerManager.instance.serverId = serverId;
    }
    public void TryToJoin()
    {
        button.enabled = false;
        StartCoroutine(TryToJoinCoroutine());
    }
    IEnumerator TryToJoinCoroutine()
    {
        joiningServer = true;
        SetServerId();
        yield return StartCoroutine(CheckAndCreateServer(serverId));
        ServerManager.instance.GetGameStartedFlag(gameStarted =>
        {
            ServerManager.instance.GetPlayerCount(count =>
            {
                if (count < 4 && !gameStarted)
                {
                    SceneManager.LoadScene(waitingRoomId);
                }
                else
                {
                    Debug.Log("Cannot join the server.");
                }
            },serverId);
        },serverId);
        joiningServer = false;
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
            var setUser = serverReference.Child("players").SetValueAsync(DataSaver.instance.userId);
            var setUserConnected = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("connected").SetValueAsync(true);
            var setUserReady = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("ready").SetValueAsync(false);
            var setGameStarted = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameHasStarted").SetValueAsync(false);
            yield return new WaitUntil(() => setUser.IsCompleted && setUserConnected.IsCompleted && setUserReady.IsCompleted && setGameStarted.IsCompleted);

            Debug.Log($"Server {serverId} created.");
        }
        else
        {
            Debug.Log($"Server {serverId} already exists.");
            yield break;
        }
    }

    public void UpdatePlayers()
    {
        isUpdatingPlayers = true;
        if (this == null || joiningServer)  // Check if the script has been destroyed
        {
            return;
        }
        ServerManager.instance.GetGameStartedFlag(gameStarted =>
        {
            if (gameStarted)
            {
                Debug.Log("game has started");
                buttonText.text = "Game ongoing";
                button.enabled = false;
                return;
            }

            ServerManager.instance.GetPlayerCount(count =>
            {
                if (count > 3)
                {
                    Debug.Log("full lobby");
                    buttonText.text = "Server full";
                    button.enabled = false;
                    return;
                }
            }, serverId);
        }, serverId);
        StartCoroutine(CountPlayers());
        isUpdatingPlayers = false;
    }
    IEnumerator CountPlayers()
    {
        button.enabled = true;
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
        buttonText.text = $"{serverId} {players}/4";
    }
}

