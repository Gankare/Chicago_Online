using Firebase;
using Firebase.Database;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Firebase.Extensions;
using UnityEngine.SceneManagement;
using System;
using Unity.VisualScripting;

public class WaitingRoomButtons : MonoBehaviour
{
    public TMP_Text amountOfPlayersText;
    public TMP_Text countDownText;
    public List<GameObject> playerObjects;
    public List<TMP_Text> playerNames;
    public List<Image> readyCards;
    public GameObject buttons;
    public bool countDownActive = false;

    private bool updating = false;
    private string previousUserData;
    private Dictionary<string, bool> previousReadyValues = new Dictionary<string, bool>();

    private void Start()
    {
        StartCoroutine(InitializePreviousUserData());
    }

    private IEnumerator InitializePreviousUserData()
    {
        // Fetch initial data of all players
        var playersInServerTask = DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").GetValueAsync();
        yield return new WaitUntil(() => playersInServerTask.IsCompleted);

        if (playersInServerTask.IsFaulted)
        {
            Debug.LogError($"Error fetching initial player data: {playersInServerTask.Exception}");
            yield break;
        }

        DataSnapshot playersSnapshot = playersInServerTask.Result;
        foreach (var playerSnapshot in playersSnapshot.Children)
        {
            var userId = playerSnapshot.Key;
            var userDataSnapshot = playerSnapshot.Child("userData");

            // Construct the previousUserData string based on the current snapshot
            bool readyValue = userDataSnapshot.Child("ready").Exists ? (bool)userDataSnapshot.Child("ready").Value : false;
            previousUserData += $"{userId}:{readyValue};";
        }

        // Subscribe to database events after initializing previousUserData
        SubscribeToDatabaseEvents();

        // Start coroutine to update players' UI
        StartCoroutine(initLoadPlayers());
    }

    void SubscribeToDatabaseEvents()
    {
        // Subscribe to events for player changes
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").ChildAdded += HandlePlayerAdded;
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").ChildRemoved += HandlePlayerRemoved;
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").ChildChanged += HandlePlayerChanged;
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("gameHasStarted").ChildChanged += HandleGameStarted;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    void RemovePlayerChangedListener()
    {
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").ChildAdded -= HandlePlayerAdded;
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").ChildRemoved -= HandlePlayerRemoved;
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").ChildChanged -= HandlePlayerChanged;
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("gameHasStarted").ChildChanged -= HandleGameStarted;
        SceneManager.sceneLoaded -= OnSceneLoaded;
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

    IEnumerator initLoadPlayers()
    {
        yield return new WaitForSeconds(1);
        StartCoroutine(UpdatePlayers());
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MenuScene" || scene.name == "ServerScene")
        {
            Destroy(gameObject);
        }
    }

    void HandleGameStarted(object sender, ChildChangedEventArgs args)
    {
        var gameHasStartedRef = DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("gameHasStarted");

        var gameHasStartedTask = gameHasStartedRef.GetValueAsync();
        gameHasStartedTask.ContinueWith(task =>
        {
            if (task.IsCompleted && !task.IsFaulted)
            {
                DataSnapshot snapshot = task.Result;
                bool gameHasStarted = snapshot != null && snapshot.Exists && (bool)snapshot.Value;

                if (gameHasStarted)
                {
                    // If the game has started, call the function
                    if (this != null)
                        StartCoroutine(ServerManager.instance.SetGameStartedFlagCoroutine());
                }
                else
                    Debug.Log("Game not started yet");
            }
            else
            {
                Debug.LogError($"Error fetching gameHasStarted data: {task.Exception}");
            }
        });
    }

    void HandlePlayerAdded(object sender, ChildChangedEventArgs args)
    {
        StartCoroutine(UpdatePlayers());
        countDownActive = false;
    }

    void HandlePlayerRemoved(object sender, ChildChangedEventArgs args)
    {
        if (this == null)
            return;
        StartCoroutine(UpdatePlayers());
            countDownActive = false;
    }

    void HandlePlayerChanged(object sender, ChildChangedEventArgs args)
    {
        if (this == null)
            return;
        var currentUserId = args.Snapshot.Key;
        var currentUserData = args.Snapshot.Child("userData").GetRawJsonValue();
        var currentReadyNode = args.Snapshot.Child("userData").Child("ready");
        var currentReadyValue = currentReadyNode.Exists ? (bool)currentReadyNode.Value : false;

        // Check if the "ready" field has changed
        if (HasReadyStatusChanged(currentUserId, currentReadyValue))
        {
            // Trigger the update method for any changes in ready status
            if (this != null)
            {
                StartCoroutine(UpdatePlayers());
                StartCoroutine(ServerManager.instance.CheckAllPlayersReady());
            }
        }
        else if (AreJsonFieldsChanged(currentUserData, previousUserData, "lastActivity"))
        {
            // Skip the update if only lastActivity has changed
            return;
        }

        // Update the previousUserData after handling changes
        previousUserData = currentUserData;
    }

    bool HasReadyStatusChanged(string userId, bool currentReadyValue)
    {
        // Check if the ready status has changed for the specific player
        if (previousReadyValues.TryGetValue(userId, out var previousReadyValue))
        {
            if (currentReadyValue != previousReadyValue)
            {
                // Update the dictionary with the new ready value
                previousReadyValues[userId] = currentReadyValue;
                return true;
            }
        }
        else
        {
            // If the player is not in the dictionary, add them with the current ready value
            previousReadyValues.Add(userId, currentReadyValue);
        }

        return false;
    }

    bool AreJsonFieldsChanged(string json1, string json2, params string[] excludedFields)
    {
        // Check if the JSON strings are equal
        if (json1 == json2) return false;

        // Remove the fields to be excluded from the comparison
        foreach (var excludedField in excludedFields)
        {
            json1 = RemoveJsonField(json1, excludedField);
            json2 = RemoveJsonField(json2, excludedField);
        }

        // If the JSON strings are equal after removing excluded fields, return false
        if (json1 == json2) return false;

        return true;
    }

    string RemoveJsonField(string json, string fieldToRemove)
    {
        if (json == null)
        {
            // Return an empty string if JSON is null
            return string.Empty;
        }

        int fieldIndex = json.IndexOf(fieldToRemove);
        if (fieldIndex >= 0)
        {
            int colonIndex = json.IndexOf(":", fieldIndex);
            int commaIndex = json.IndexOf(",", colonIndex);
            if (commaIndex < 0)
            {
                // Last field, remove until end of string
                json = json.Remove(fieldIndex - 1);
            }
            else
            {
                // Middle field, remove until next comma
                json = json.Remove(fieldIndex - 1, commaIndex - fieldIndex + 2);
            }
        }
        return json;
    }

    [Serializable]
    public class UserData
    {
        public bool ready;
    }

    public void Ready()
    {
        StartCoroutine(ServerManager.instance.PlayerReadyStatus(DataSaver.instance.userId, true));
    }
    public void Unready()
    {
        StartCoroutine(ServerManager.instance.PlayerReadyStatus(DataSaver.instance.userId, false));
    }

    IEnumerator UpdatePlayers()
    {
        if (updating)
        {
            yield break;
        }
        updating = true;
        //Reset values
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

                // Get userId from requestSnapshot
                string userId = requestSnapshot.Key;

                // Check if the player is ready
                var isPlayerReady = requestSnapshot.Child("userData").Child("ready").Value;
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
                        readyCards[players].color = Color.white;
                    }
                }

                // Look up username in "users" node
                var usernameTask = DataSaver.instance.dbRef.Child("users").Child(userId).Child("userName").GetValueAsync();
                yield return new WaitUntil(() => usernameTask.IsCompleted);

                if (usernameTask.Exception == null)
                {
                    DataSnapshot usernameSnapshot = usernameTask.Result;
                    if (usernameSnapshot.Exists)
                    {
                        // Set the player's name in the UI
                        playerNames[players].text = usernameSnapshot.Value.ToString();
                    }
                }

                if (requestSnapshot.Key == DataSaver.instance.userId)
                {
                    buttons.transform.position = new Vector2(playerObjects[players].transform.position.x, buttons.transform.position.y);
                }

                players++;
            }
        }
        updating = false;
        amountOfPlayersText.text = $"{playersReady}/{players} players ready";
    }

    public void LeaveServer()
    {
        SceneManager.LoadScene("ServerScene");
    }
    public IEnumerator CountDownBeforeStart()
    {
        countDownActive = true;

        var countdownStartFlagRef = DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("countdownStartFlag");

        while (countDownActive)
        {
            yield return new WaitForEndOfFrame();

            var countdownStartFlagTask = countdownStartFlagRef.GetValueAsync();
            yield return new WaitUntil(() => countdownStartFlagTask.IsCompleted);

            if (countdownStartFlagTask.Exception == null)
            {
                bool countdownStartFlag = bool.Parse(countdownStartFlagTask.Result.Value.ToString());
                if (!countdownStartFlag)
                {
                    // Countdown stopped by server
                    countDownText.text = "";
                    Debug.Log("Countdown stopped.");
                    yield break;
                }
            }

            for (int i = 3; i > -1; i--)
            {
                countDownText.text = i.ToString();

                // Check if any player has become unready during the countdown
                var playersInServer = DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").GetValueAsync();
                yield return new WaitUntil(() => playersInServer.IsCompleted);

                DataSnapshot snapshot = playersInServer.Result;

                bool anyPlayerUnready = false;

                if (snapshot.Exists)
                {
                    foreach (var playerSnapshot in snapshot.Children)
                    {
                        bool isReady = bool.Parse(playerSnapshot.Child("userData").Child("ready").Value.ToString());

                        if (!isReady)
                        {
                            anyPlayerUnready = true;
                            break;
                        }
                    }
                }

                if (anyPlayerUnready)
                {
                    // If any player is unready, stop the countdown
                    Debug.Log("A player became unready. Countdown stopped.");
                    countDownText.text = "";
                    StartCoroutine(UpdatePlayers());
                    countDownActive = false;
                    break;
                }

                if (i == 0)
                    StartCoroutine(ServerManager.instance.SetGameStartedFlagCoroutine());
                yield return new WaitForSeconds(1);
            }
        }
    }
}
