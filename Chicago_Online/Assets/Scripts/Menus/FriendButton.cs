using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class FriendButton : MonoBehaviour
{
    public string friendId;
    public string friendServerId;
    public TMP_Text friendName;
    public Image friendStatusImage;
    public Button joinButton;

    void Start()
    {
        StartCoroutine(UpdateFriendStatus());
    }
    IEnumerator UpdateFriendStatus()
    {
        while (true)
        {
            var serversRef = DataSaver.instance.dbRef.Child("servers");
            var serversTask = serversRef.GetValueAsync();
            yield return new WaitUntil(() => serversTask.IsCompleted);

            DataSnapshot serversSnapshot = serversTask.Result;

            if (serversSnapshot.Exists)
            {
                foreach (var serverNode in serversSnapshot.Children)
                {
                    // Check if the friend is in the current server
                    var serverId = serverNode.Child("players").Child(friendId).Value?.ToString();

                    if (!string.IsNullOrEmpty(serverId))
                    {
                        // Check if the game has started
                        var gameHasStarted = serverNode.Child("gameHasStarted").Exists && (bool)serverNode.Child("gameHasStarted").Value;

                        // Check if there are fewer than 4 players in the server
                        var playerCount = serverNode.Child("players").ChildrenCount;

                        if (gameHasStarted || playerCount >= 4)
                        {
                            // Game has started or there are 4 or more players, update color to blue
                            joinButton.enabled = false;
                            friendStatusImage.color = Color.blue;
                            friendServerId = null;
                        }
                        else
                        {
                            // Friend is in a server, update color to yellow
                            joinButton.enabled = true;
                            friendStatusImage.color = Color.yellow;
                            friendServerId = serverNode.Key;
                            Debug.Log(friendServerId);
                            break;
                        }
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
                // No servers found, update color to red
                joinButton.enabled = false;
                friendStatusImage.color = Color.red;
                friendServerId = null;
            }
            yield return new WaitForSeconds(5f); 
        }
    }

    public void JoinServer()
    {
        ServerManager.instance.serverId = friendServerId;
        SceneManager.LoadScene(friendServerId + "WaitingRoom");
    }

    public void Remove()
    {
        StartCoroutine(RemoveFriend(DataSaver.instance.userId, friendId));
    }
    IEnumerator RemoveFriend(string userId, string friendId)
    {
        yield return StartCoroutine(Delete(userId, friendId));
        yield return StartCoroutine(LoadDataAndWait());
        Destroy(gameObject);
    }
    IEnumerator Delete(string userId, string friendId)
    {
        var removeFriendTask1 = DataSaver.instance.dbRef.Child("userFriends").Child(userId).Child(friendId).RemoveValueAsync();
        var removeFriendTask2 = DataSaver.instance.dbRef.Child("userFriends").Child(friendId).Child(userId).RemoveValueAsync();
        yield return new WaitUntil(() => removeFriendTask1.IsCompleted && removeFriendTask2.IsCompleted);

        DataSaver.instance.dts.friends.Remove(friendId);
        DataSaver.instance.SaveData();
    }
    IEnumerator LoadDataAndWait()
    {
        var loadDataEnumerator = DataSaver.instance.LoadDataEnum();
        while (loadDataEnumerator.MoveNext())
        {
            yield return loadDataEnumerator.Current;
        }
    }
}
