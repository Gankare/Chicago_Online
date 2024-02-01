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

public class WaitingRoomButtons : MonoBehaviour
{
    public TMP_Text amountOfPlayersText;
    public List<GameObject> playerObjects;
    public List<TMP_Text> playerNames;
    public List<Image> readyCards;
    public GameObject buttons;

    private bool updating = false;
    private string previousUserData;
    private Dictionary<string, bool> previousReadyValues = new Dictionary<string, bool>();

    private void Start()
    {
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").ChildAdded += HandlePlayerAdded;
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").ChildRemoved += HandlePlayerRemoved;
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").ChildChanged += HandlePlayerChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
        StartCoroutine(initLoadPlayers());
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

    void HandlePlayerAdded(object sender, ChildChangedEventArgs args)
    {
        StartCoroutine(UpdatePlayers());
    }

    void HandlePlayerRemoved(object sender, ChildChangedEventArgs args)
    {
        if (this != null)
        StartCoroutine(UpdatePlayers());
    }

    void HandlePlayerChanged(object sender, ChildChangedEventArgs args)
    {
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
                ServerManager.instance.CheckAllPlayersReady();
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
        var dict1 = JsonUtility.FromJson<Dictionary<string, object>>(json1);
        var dict2 = JsonUtility.FromJson<Dictionary<string, object>>(json2);

        // Check for null dictionaries
        if (dict1 == null || dict2 == null)
        {
            return dict1 != dict2; // Return true if either dictionary is null
        }

        // Remove the fields to be excluded from the comparison
        foreach (var excludedField in excludedFields)
        {
            dict1?.Remove(excludedField);
            dict2?.Remove(excludedField);
        }

        return !DictionaryEquals(dict1, dict2);
    }

    bool DictionaryEquals<TKey, TValue>(IDictionary<TKey, TValue> dict1, IDictionary<TKey, TValue> dict2)
    {
        if (dict1 == dict2) return true;
        if (dict1 == null || dict2 == null) return false;
        if (dict1.Count != dict2.Count) return false;

        foreach (var kvp in dict1)
        {
            if (!dict2.TryGetValue(kvp.Key, out var value) || !EqualityComparer<TValue>.Default.Equals(kvp.Value, value))
            {
                return false;
            }
        }

        return true;
    }

    [Serializable]
    public class UserData
    {
        public bool ready;
    }

    private void OnDisable()
    {
        // Remove the listener when the script is disabled
        RemovePlayerChangedListener();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    private void OnDestroy()
    {
        // Remove the listener when the object is destroyed
        RemovePlayerChangedListener();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    void RemovePlayerChangedListener()
    {
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").ChildAdded -= HandlePlayerAdded;
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").ChildRemoved -= HandlePlayerRemoved;
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").ChildChanged -= HandlePlayerChanged;
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
}
