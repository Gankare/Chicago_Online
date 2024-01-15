using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Firebase.Database;

public class InputDataAfterLogin : MonoBehaviour
{
    #region Singleton
    public static InputDataAfterLogin instance;

    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(this);
    }
    #endregion

    public TMP_Text profileName;
    public TMP_Text profileWins;

    public GameObject friendRequestObject;
    public Transform friendRequestList;

    public GameObject friendObject;
    public Transform friendList;

    private void Start()
    {
        DataSaver.instance.LoadData();
        ShowPlayerProfile();
    }
    public void ShowPlayerProfile()
    {
        profileName.text = DataSaver.instance.dts.userName;
        profileWins.text = "Wins: " + DataSaver.instance.dts.matchesWon;
        foreach (string friendId in DataSaver.instance.dts.friendRequests)
        {
            var request = Instantiate(friendRequestObject, friendRequestList);
            StartCoroutine(GetRequestUsernameAndDisplay(friendId));
        }
        // Display current friends
        foreach (string friendId in DataSaver.instance.dts.friends)
        {
            StartCoroutine(GetFriendUsernameAndDisplay(friendId, friendList));
        }
    }
    IEnumerator GetRequestUsernameAndDisplay(string friendId)
    {
        // Fetch the user data based on the friend ID
        var userData = DataSaver.instance.dbRef.Child("users").Child(friendId).GetValueAsync();
        yield return new WaitUntil(() => userData.IsCompleted);

        DataSnapshot userSnapshot = userData.Result;

        if (userSnapshot.Exists)
        {
            string friendUsername = userSnapshot.Child("userName").Value.ToString();

            // Display the friend request with the username
            var request = Instantiate(friendRequestObject, friendRequestList);
            request.GetComponent<FriendRequestButton>().friendId = friendId;
            request.GetComponentInChildren<Text>().text = friendUsername;
        }
        else
        {
            Debug.LogWarning($"User with ID {friendId} not found.");
        }
    }
    IEnumerator GetFriendUsernameAndDisplay(string friendId, Transform parent)
    {
        // Fetch the user data based on the friend ID
        var userData = DataSaver.instance.dbRef.Child("users").Child(friendId).GetValueAsync();
        yield return new WaitUntil(() => userData.IsCompleted);

        DataSnapshot userSnapshot = userData.Result;

        if (userSnapshot.Exists)
        {
            string friendUsername = userSnapshot.Child("userName").Value.ToString();

            // Display the friend with the username
            var friend = Instantiate(friendObject, parent);
            friend.GetComponent<FriendButton>().friendId = friendId;
            friend.GetComponentInChildren<Text>().text = friendUsername;
        }
        else
        {
            Debug.LogWarning($"User with ID {friendId} not found.");
        }
    }
}

