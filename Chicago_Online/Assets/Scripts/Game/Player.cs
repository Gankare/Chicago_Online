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
    void Start()
    {
        CheckIfUserExistsInServer(DataSaver.instance.userId);
        StartCoroutine(UpdatePlayerActivity());
        StartCoroutine(CheckActivityCoroutine());
    }
    private void OnEnable()
    {
        ServerManager.instance.PlayerConnected(DataSaver.instance.userId);
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
    public IEnumerator UpdatePlayerActivity()
    {
        while (true)
        {
            // Update player's last activity timestamp in the database
            UpdatePlayerLastActivity();
            yield return new WaitForSeconds(10); // Adjust the interval as needed

        }
    }
    private void UpdatePlayerLastActivity()
    {
        if (this == null)
        {
            return;
        }

        var userId = DataSaver.instance.userId;
        var serverId = ServerManager.instance.serverId;

        if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(serverId))
        {
            // Check if the player exists in the server
            var playerReference = DataSaver.instance.dbRef
                .Child("servers").Child(serverId)
                .Child("players").Child(userId);

            playerReference.GetValueAsync().ContinueWith(task =>
            {
                if (task.IsCompleted && task.Result.Exists)
                {
                    // Player exists, update the last activity timestamp in the database
                    var serverReference = playerReference
                        .Child("userData").Child("lastActivity");
                    Debug.Log("updating Activity");
                    serverReference.SetValueAsync(ServerValue.Timestamp);
                }
            });
        }
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
                    var lastActivity = playerSnapshot.Child("userData").Child("lastActivity").Value;

                    // Check if lastActivity exists and is a valid timestamp
                    if (lastActivity != null && long.TryParse(lastActivity.ToString(), out long timestamp))
                    {
                        // Convert the timestamp to DateTime
                        DateTime lastActivityDateTime = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime;

                        // Calculate the time difference
                        TimeSpan timeDifference = DateTime.UtcNow - lastActivityDateTime;

                        // If the player has been inactive for more than 30 seconds, remove them
                        if (timeDifference.TotalSeconds > 30)
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
