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
                        friendStatusImage.color = Color.yellow;
                        break; // Exit the loop since we found the server
                    }
                    else
                    {
                        // Friend is not in this server, update color to blue
                        friendStatusImage.color = Color.blue;
                    }
                }
            }
            else
            {
                // No servers found, update color to default color (or handle as needed)
                friendStatusImage.color = Color.white;
            }

            // Wait for a specific time before checking again
            yield return new WaitForSeconds(10f); // You can adjust the interval as needed
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
