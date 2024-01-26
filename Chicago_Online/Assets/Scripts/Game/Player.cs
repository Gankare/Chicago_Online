using UnityEngine;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEditor;
using System;

public class Player : MonoBehaviour
{
    #region Singleton
    public static Player instance;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    #endregion

    void Start()
    {
        CheckIfUserExistsInServer(DataSaver.instance.userId);
        StartCoroutine(UpdatePlayerActivity());
        StartCoroutine(CheckActivityCoroutine());
    }
    private void OnDestroy()
    {
        ServerManager.instance.PlayerDisconnected(DataSaver.instance.userId);
    } 
    private void OnDisable()
    {
        ServerManager.instance.PlayerDisconnected(DataSaver.instance.userId);
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MenuScene" || scene.name == "ServerScene")
        {
            Destroy(gameObject);
        }
    }

    void CheckIfUserExistsInServer(string userId)
    {
        var serverReference = DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").Child(userId);

        serverReference.GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError($"Error checking if user {userId} exists in the server. Error: {task.Exception}");
                return;
            }

            DataSnapshot snapshot = task.Result;

            if (!snapshot.Exists)
            {
                // Player does not exist, connect the player
                Debug.Log("add player to server");
                ServerManager.instance.PlayerConnected(userId);
            }
            else
            {
                Debug.Log($"User {userId} already exists in the server.");
                // You can add additional logic here if needed
            }
        });
    }
    private IEnumerator UpdatePlayerActivity()
    {
        while (true)
        {
            yield return new WaitForSeconds(10); // Adjust the interval as needed

            // Update player's last activity timestamp in the database
            UpdatePlayerLastActivity();
        }
    }

    private void UpdatePlayerLastActivity()
    {
        // Update the last activity timestamp in the database
        var serverReference = DataSaver.instance.dbRef
            .Child("servers").Child(ServerManager.instance.serverId)
            .Child("players").Child(DataSaver.instance.userId)
            .Child("lastActivity");

        serverReference.SetValueAsync(ServerValue.Timestamp);
    }
    private IEnumerator CheckActivityCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(30f); // Wait for 30 seconds before checking again

            // Get a reference to the players in the server
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
                foreach (var playerSnapshot in playersSnapshot.Children)
                {
                    string userId = playerSnapshot.Key;
                    var lastActivity = playerSnapshot.Child("lastActivity").Value;

                    // Check if lastActivity exists and is a valid timestamp
                    if (lastActivity != null && long.TryParse(lastActivity.ToString(), out long timestamp))
                    {
                        // Compare with the current timestamp
                        long currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        long timeDifference = currentTimestamp - timestamp;

                        // If the player has been inactive for more than 30 seconds, remove them
                        if (timeDifference > 30)
                        {
                            RemoveInactivePlayer(userId);
                        }
                    }
                }
            }
        }
    }

    private void RemoveInactivePlayer(string userId)
    {
        // Implement the logic to remove the player from the server using their userId
        var playerReference = DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").Child(userId);

        // Remove the player and handle any cleanup tasks
        playerReference.RemoveValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError($"Error removing player {userId}: {task.Exception}");
            }
            else
            {
                Debug.Log($"Player {userId} has been removed from the server.");
            }
        });
    }
}
