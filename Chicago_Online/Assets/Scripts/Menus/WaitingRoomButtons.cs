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
using System;

public class WaitingRoomButtons : MonoBehaviour
{
    public TMP_Text amountOfPlayersText;
    public List<GameObject> playerObjects;
    public List<TMP_Text> playerNames;
    public List<Image> readyCards;
    public GameObject buttons;
    private bool isUpdatingPlayers = false;
    private string previousUserData;
    private HashSet<string> currentPlayers = new HashSet<string>();

    private void Start()
    {
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").ChildAdded += HandlePlayerAdded;
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").ChildRemoved += HandlePlayerRemoved;
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").ChildChanged += HandlePlayerChanged;
        //StartCoroutine(UpdatePlayers());
    }

    void HandlePlayerAdded(object sender, ChildChangedEventArgs args)
    {
        var currentUserId = args.Snapshot.Key;
        currentPlayers.Add(currentUserId);

        StartCoroutine(UpdatePlayers());
    }

    void HandlePlayerRemoved(object sender, ChildChangedEventArgs args)
    {
        var currentUserId = args.Snapshot.Key;
        currentPlayers.Remove(currentUserId);

        StartCoroutine(UpdatePlayers());
    }

    void HandlePlayerChanged(object sender, ChildChangedEventArgs args)
    {
        var currentUserData = args.Snapshot.Child("userData").GetRawJsonValue();
        var previousReadyValue = JsonUtility.FromJson<UserData>(previousUserData)?.ready ?? false;
        var currentReadyValue = args.Snapshot.Child("userData").Child("ready").Exists ? args.Snapshot.Child("userData").Child("ready").Value.ToString() : "false";

        if (previousUserData != null)
        {
            // Check if userData has changed (excluding lastActivity)
            if (AreJsonFieldsChanged(currentUserData, previousUserData, "lastActivity") || previousReadyValue.ToString() != currentReadyValue)
            {
                // Trigger the update method for any changes
                StartCoroutine(UpdatePlayers());
            }
            else
            {
                // Handle other changes if needed
                Debug.Log($"Unhandled change in player data: {args.Snapshot.GetRawJsonValue()}");
            }
        }

        // Update the previousUserData after handling changes
        previousUserData = currentUserData;
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
    }
    private void OnDestroy()
    {
        // Remove the listener when the object is destroyed
        RemovePlayerChangedListener();
    }
    void RemovePlayerChangedListener()
    {
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").ChildAdded -= HandlePlayerAdded;
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").ChildRemoved -= HandlePlayerRemoved;
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
        //Reset values
        isUpdatingPlayers = true;
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

                if(requestSnapshot.Key == DataSaver.instance.userId)
                {
                    buttons.transform.position = new Vector2(playerObjects[players].transform.position.x, buttons.transform.position.y);
                }

                players++;
            }
            isUpdatingPlayers = false;
        }

        amountOfPlayersText.text = $"{playersReady}/{players} players ready";
    }

    public void LeaveServer()
    {
        SceneManager.LoadScene("ServerScene");
    }
}
