using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FriendButton : MonoBehaviour
{
    public string friendId;
    public TMP_Text friendName;
    public Image friendStatusImage;
    public Button joinButton;

    private string friendServerId;

    void Start()
    {
        StartCoroutine(UpdateFriendStatus());
    }
    IEnumerator UpdateFriendStatus()
    {
        while (true)
        {
            // Get the list of server nodes
            var serversRef = DataSaver.instance.dbRef.Child("servers");
            var serversTask = serversRef.GetValueAsync();

            // Wait until the task is completed
            yield return new WaitUntil(() => serversTask.IsCompleted);

            DataSnapshot serversSnapshot = serversTask.Result;

            if (serversSnapshot.Exists)
            {
                // Iterate through each server node
                foreach (var serverNode in serversSnapshot.Children)
                {
                    // Check if the friend is in the current server
                    var serverId = serverNode.Child("players").Child(friendId).Value?.ToString();

                    if (!string.IsNullOrEmpty(serverId))
                    {
                        // Friend is in a server, update color to yellow
                        joinButton.enabled = true;
                        friendStatusImage.color = Color.yellow;
                        friendServerId = serverNode.ToString();
                        break; // Exit the loop since we found the server
                    }
                    else
                    {
                        // Friend is not in this server, update color to blue
                        joinButton.enabled = false;
                        friendStatusImage.color = Color.blue;
                        friendServerId = null;
                    }
                }
            }
            else
            {
                // No servers found, update color to default color (or handle as needed)
                friendStatusImage.color = Color.white;
                friendServerId = null;
            }

            // Wait for a specific time before checking again
            yield return new WaitForSeconds(5f); // You can adjust the interval as needed
        }
    }
    public void JoinServer()
    {
        // Check if the friend is in a server
        if (!string.IsNullOrEmpty(friendServerId))
        {
            // Fetch the gameHasStarted value
            var serverRef = DataSaver.instance.dbRef.Child("servers").Child(friendServerId);
            var gameHasStartedTask = serverRef.Child("gameHasStarted").GetValueAsync();

            // Wait until the task is completed
            gameHasStartedTask.ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError($"Error fetching gameHasStarted: {task.Exception}");
                    return;
                }

                DataSnapshot snapshot = task.Result;
                bool gameHasStarted = snapshot.Exists && snapshot.Value is bool && (bool)snapshot.Value;

                // Check the gameHasStarted value
                if (gameHasStarted)
                {
                    // Game has already started
                    Debug.Log($"Server (ID: {friendServerId}) has already started. Server is now active.");
                }
                else
                {
                    // Game has not started, you can implement the join server functionality
                    Debug.Log($"Joining server: (ID: {friendServerId})");

                }
            });
        }
        else
        {
            // Friend is not in a server
            Debug.Log("Friend is not in a server.");
        }
    }


    public void Remove()
    {
        StartCoroutine(RemoveFriend(DataSaver.instance.userId, friendId));
    }
    IEnumerator RemoveFriend(string userId, string friendId)
    {
        // Wait until both friend removals are complete
        yield return StartCoroutine(Delete(userId, friendId));

        // Load data and wait until it's completed
        yield return StartCoroutine(LoadDataAndWait());

        // Destroy the game object
        Destroy(gameObject);
    }
    IEnumerator Delete(string userId, string friendId)
    {
        // Remove friends from each other's friend list
        var removeFriendTask1 = DataSaver.instance.dbRef.Child("userFriends").Child(userId).Child(friendId).RemoveValueAsync();
        var removeFriendTask2 = DataSaver.instance.dbRef.Child("userFriends").Child(friendId).Child(userId).RemoveValueAsync();
        // Wait until both tasks are completed
        yield return new WaitUntil(() => removeFriendTask1.IsCompleted && removeFriendTask2.IsCompleted);

        DataSaver.instance.dts.friends.Remove(friendId);
        DataSaver.instance.SaveData();
    }
    IEnumerator LoadDataAndWait()
    {
        // Load data
        var loadDataEnumerator = DataSaver.instance.LoadDataEnum();

        // Iterate through the enumerator until it's done
        while (loadDataEnumerator.MoveNext())
        {
            yield return loadDataEnumerator.Current;
        }
    }
}
